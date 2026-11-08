using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SelectBar.Actions;
using SelectBar.Online;
using SelectBar.Settings;

namespace SelectBar.SelfTest;

/// <summary>Une sélection type que l'auto-test joue contre chaque action intégrée.</summary>
public sealed record SelfTestSample(string Name, string Text);

public enum SelfTestStatus { Ok, Ko, Skipped }

public sealed record SelfTestOutcome(string ActionId, string ActionTitle, string Sample, bool Editable, SelfTestStatus Status,
    string Detail, IReadOnlyList<string> Launches, long Milliseconds, string? Result);

/// <summary>Faux services : ENREGISTRENT les lancements, le presse-papiers, les Ctrl+X / Ctrl+V et les popups au lieu de les exécuter.</summary>
public sealed class RecordingServices : IActionServices
{
    private readonly Func<ResultRequest, CancellationToken, Task<ResultPayload>> _online;

    public RecordingServices(Func<ResultRequest, CancellationToken, Task<ResultPayload>> online) { _online = online; }

    public List<string> Launches { get; } = new();
    public List<string> Copied { get; } = new();
    public List<string> Pasted { get; } = new();
    public List<ResultRequest> Popups { get; } = new();
    public ResultPayload? LastPayload { get; private set; }
    public int Cuts { get; private set; }
    public int Pastes { get; private set; }
    public CancellationToken Token { get; set; }

    public void Reset()
    {
        Launches.Clear(); Copied.Clear(); Pasted.Clear(); Popups.Clear();
        LastPayload = null; Cuts = 0; Pastes = 0;
    }

    public void CopyToClipboard(string text) => Copied.Add(text);
    public Task SendCutAsync() { Cuts++; return Task.CompletedTask; }
    public Task SendPasteAsync() { Pastes++; return Task.CompletedTask; }

    public void OpenUrl(string url)
    {
        switch (LinkSafety.Check(url, out var target))
        {
            case LinkKind.Web:
                Launches.Add((target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? "messagerie : " : "navigateur : ") + target);
                break;
            case LinkKind.LocalPath:
                Launches.Add("Explorateur : " + target);
                break;
            default:
                Launches.Add("refusé par LinkSafety : " + url);
                break;
        }
    }

    public void RevealInExplorer(string path) => Launches.Add("Explorateur : " + path);

    public async Task ShowResultAsync(ResultRequest request)
    {
        Popups.Add(request);
        LastPayload = await _online(request, Token).ConfigureAwait(false);
    }
}

public sealed class SelfTestReport
{
    public List<SelfTestOutcome> Outcomes { get; } = new();
    /// <summary>Lancements d'une action qui n'a pas le droit d'en faire (Copier, Couper, Coller, Dictionnaire, Traduire, IA…).</summary>
    public List<string> UnexpectedLaunches { get; } = new();
    public int NotOffered { get; set; }

    public int Ok => Outcomes.Count(o => o.Status == SelfTestStatus.Ok);
    public int Ko => Outcomes.Count(o => o.Status == SelfTestStatus.Ko);
    public int Skipped => Outcomes.Count(o => o.Status == SelfTestStatus.Skipped);
    public bool HasFailure => Aborted || Ko > 0 || UnexpectedLaunches.Count > 0;
    public int ExitCode => HasFailure ? 1 : 0;

    public bool Aborted { get; set; }

    /// <summary>Une ligne de rapport pour un cas (écrite au fur et à mesure pendant l'auto-test).</summary>
    public static string FormatLine(SelfTestOutcome o)
    {
        var tag = o.Status switch { SelfTestStatus.Ok => "OK     ", SelfTestStatus.Ko => "KO     ", _ => "IGNORÉ " };
        var sb = new StringBuilder();
        sb.Append($"[{tag}] {o.Sample} / {o.ActionId} ({o.ActionTitle}) · {(o.Editable ? "champ éditable" : "lecture seule")} · {o.Milliseconds} ms");
        sb.Append(o.Launches.Count == 0 ? " · aucun lancement" : " · lancé : " + string.Join(" ; ", o.Launches));
        if (!string.IsNullOrEmpty(o.Detail)) sb.Append(" · " + o.Detail);
        if (!string.IsNullOrEmpty(o.Result)) sb.Append(" · résultat : " + o.Result);
        return sb.ToString();
    }

    public string Format(DateTime when, string version, IEnumerable<string>? context = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Selecto AI {version} — auto-test des actions — {when:yyyy-MM-dd HH:mm:ss}");
        if (context != null) foreach (var c in context) sb.AppendLine(c);
        sb.AppendLine("Lecture : OK = l'action a fait ce qu'on attend d'elle ; KO = erreur, lancement manquant ou lancement inattendu ; IGNORÉ = service non configuré.");
        sb.AppendLine("Les lancements sont ENREGISTRÉS, jamais exécutés : aucune page ne s'ouvre pendant l'auto-test.");
        sb.AppendLine(new string('-', 100));
        string? lastSample = null;
        foreach (var o in Outcomes)
        {
            if (o.Sample != lastSample)
            {
                sb.AppendLine();
                sb.AppendLine("== " + o.Sample + " ==");
                lastSample = o.Sample;
            }
            sb.AppendLine(FormatLine(o));
        }
        sb.AppendLine();
        sb.AppendLine(new string('-', 100));
        sb.AppendLine($"Lancements externes inattendus : {(UnexpectedLaunches.Count == 0 ? "aucun" : UnexpectedLaunches.Count.ToString())}");
        foreach (var u in UnexpectedLaunches) sb.AppendLine("  - " + u);
        if (Aborted) sb.AppendLine("AUTO-TEST INTERROMPU (délai global dépassé) : rapport partiel.");
        sb.AppendLine($"RÉSUMÉ : {Outcomes.Count} cas, {Ok} OK, {Ko} KO, {UnexpectedLaunches.Count} lancement(s) inattendu(s).");
        sb.AppendLine($"Total : {Ok} OK, {Ko} KO, {Skipped} ignoré(s) ; {NotOffered} couple(s) action/texte non proposé(s) par la barre.");
        sb.AppendLine(HasFailure ? "RÉSULTAT : ÉCHEC (code de sortie 1)" : "RÉSULTAT : SUCCÈS (code de sortie 0)");
        return sb.ToString();
    }
}

public static class SelfTestPlan
{
    /// <summary>Jeu de sélections types. <paramref name="windowsDir"/> sert au « chemin Windows existant ».</summary>
    public static IReadOnlyList<SelfTestSample> Samples(string windowsDir)
    {
        var filler = string.Join(" ", Enumerable.Repeat("Ceci est une phrase de remplissage assez longue pour dépasser les limites habituelles.", 160));
        return new List<SelfTestSample>
        {
            new("mot anglais", "serendipity"),
            new("mot français", "maison"),
            new("mot français mal orthographié", "bonjuor"),
            new("phrase", "Il fait beau et on va se promener au bord de la rivière."),
            new("phrase mal transcrite", "bonjour sa va tu as bien dormi"),
            new("URL", "https://fr.wikipedia.org/wiki/Paris"),
            new("www.exemple.com", "www.exemple.com"),
            new("e-mail", "contact@exemple.com"),
            new("chemin Windows existant", windowsDir.TrimEnd('\\') + "\\notepad.exe"),
            new("chemin Windows inexistant", @"C:\Dossier\Inexistant\fichier-absent.txt"),
            new("chemin UNC", @"\\serveur\partage\documents\rapport.docx"),
            new("texte vide", ""),
            new("texte très long (≈ 11 000 caractères)", filler),
        };
    }

    public static bool IsOnlineAction(string id) =>
        id == BuiltInActions.Define || id == BuiltInActions.Translate || id == BuiltInActions.Ai || id.StartsWith("builtin.ai.", StringComparison.Ordinal);

    /// <summary>Jugement d'une action jouée : (statut, détail). Le contrôle des lancements inattendus est fait à part.</summary>
    public static (SelfTestStatus Status, string Detail) Evaluate(string id, ActionContext ctx, RecordingServices rec)
    {
        string? One(string prefix)
        {
            if (rec.Launches.Count != 1) return $"{rec.Launches.Count} lancement(s) au lieu d'un seul";
            return rec.Launches[0].StartsWith(prefix, StringComparison.Ordinal) ? null : "lancement inattendu : " + rec.Launches[0];
        }
        (SelfTestStatus, string) Check(string? problem) => problem == null ? (SelfTestStatus.Ok, "") : (SelfTestStatus.Ko, problem);

        switch (id)
        {
            case BuiltInActions.Search: return Check(One("navigateur : https://"));
            case BuiltInActions.OpenLink: return Check(One("navigateur : http"));
            case BuiltInActions.Reveal: return Check(One("Explorateur : "));
            case BuiltInActions.Mail: return Check(One("messagerie : mailto:"));
            case BuiltInActions.Cut: return Check(rec.Cuts == 1 ? null : "Ctrl+X non envoyé");
            case BuiltInActions.Paste: return Check(rec.Pastes == 1 ? null : "Ctrl+V non envoyé");
            case BuiltInActions.Copy: return Check(rec.Copied.Count == 1 && rec.Copied[0] == ctx.Text ? null : "le texte n'a pas été copié");
            case BuiltInActions.Spell: return Check(rec.Pasted.Count == 1 ? null : "aucune correction collée");
        }
        if (!IsOnlineAction(id)) return (SelfTestStatus.Ok, "");
        if (rec.Popups.Count != 1) return (SelfTestStatus.Ko, "le popup de résultat n'a pas été demandé");
        var p = rec.LastPayload;
        if (p == null) return (SelfTestStatus.Ko, "aucune réponse");
        if (p.NeedsAiSetup) return (SelfTestStatus.Skipped, "modèle IA non configuré");
        if (p.Error != null) return (SelfTestStatus.Ko, "erreur affichée à l'utilisateur : " + p.Error);
        return (SelfTestStatus.Ok, "");
    }

    public static string? ResultText(RecordingServices rec)
    {
        var p = rec.LastPayload;
        if (p == null) return null;
        var text = p.Error ?? ((string.IsNullOrWhiteSpace(p.Title) ? "" : p.Title + " — ") + p.Body);
        text = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= 160 ? text : text.Substring(0, 159) + "…";
    }
}

public static class SelfTestRunner
{
    public static readonly TimeSpan LocalTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan OnlineTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Joue chaque action intégrée proposée par la barre sur chaque sélection type (lecture seule, puis champ éditable).
    /// Chaque cas a son propre délai (10 s en local, 20 s pour réseau/IA) : un cas qui ne répond pas est KO « délai dépassé » et on continue.
    /// </summary>
    /// <param name="online">Exécute la demande des actions en ligne (vrais fournisseurs côté application, faux dans les tests).</param>
    /// <param name="into">Rapport rempli au fur et à mesure (permet un rapport partiel si l'auto-test est interrompu).</param>
    /// <param name="onCase">Appelé après chaque cas (écriture du rapport ligne par ligne).</param>
    /// <param name="trace">Appelé au début de chaque cas (« début »), pour voir où ça se fige.</param>
    public static async Task<SelfTestReport> RunAsync(IReadOnlyList<SelfTestSample> samples, Func<string, bool> pathExists,
        Func<ResultRequest, CancellationToken, Task<ResultPayload>> online, CancellationToken ct, Action<string>? progress = null,
        TimeSpan? localTimeout = null, TimeSpan? onlineTimeout = null, SelfTestReport? into = null, Action<SelfTestOutcome>? onCase = null)
    {
        var report = into ?? new SelfTestReport();
        var localLimit = localTimeout ?? LocalTimeout;
        var onlineLimit = onlineTimeout ?? OnlineTimeout;
        var settings = new AppSettings { OnlineActionsEnabled = true };
        Func<string, IReadOnlyList<string>> spell = w => w == "bonjuor" ? new[] { "bonjour" } : Array.Empty<string>();

        List<ActionDefinition> Fresh(RecordingServices r) =>
            BuiltInActions.Create(r, () => settings, spell, t => { r.Pasted.Add(t); return Task.CompletedTask; }, null);
        var templates = Fresh(new RecordingServices(online));

        void Add(SelfTestOutcome o) { report.Outcomes.Add(o); onCase?.Invoke(o); }

        foreach (var sample in samples)
        {
            if (string.IsNullOrWhiteSpace(sample.Text))
            {
                // la lecture de la sélection renvoie null : aucune barre n'apparaît, donc aucune action n'est jouée
                Add(new SelfTestOutcome("(aucune)", "aucune barre", sample.Name, false, SelfTestStatus.Ok,
                    "sélection vide : la barre n'apparaît pas", Array.Empty<string>(), 0, null));
                continue;
            }
            foreach (var editable in new[] { false, true })
            {
                var ctx = TextClassifier.BuildContext(sample.Text, editable, "notepad.exe", pathExists, clipboardHasText: true);
                foreach (var template in templates)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!ActionFilter.Matches(template, ctx)) { report.NotOffered++; continue; }
                    var online1 = SelfTestPlan.IsOnlineAction(template.Id);
                    if (editable && online1 && template.Id != BuiltInActions.Ai) continue;   // même appel qu'en lecture seule

                    // services et actions neufs pour chaque cas : un cas abandonné ne peut pas écrire dans le suivant
                    var rec = new RecordingServices(online);
                    var action = Fresh(rec).First(a => a.Id == template.Id);
                    var limit = online1 ? onlineLimit : localLimit;
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(limit);
                    rec.Token = cts.Token;
                    progress?.Invoke($"Auto-test : {action.Id} / {sample.Name} : début");
                    var sw = Stopwatch.StartNew();
                    SelfTestStatus status; string detail;
                    try
                    {
                        var work = Task.Run(() => action.Execute(ctx));   // hors du thread appelant : un blocage synchrone est aussi borné
                        var finished = await Task.WhenAny(work, Task.Delay(limit, ct)).ConfigureAwait(false);
                        ct.ThrowIfCancellationRequested();
                        if (finished != work)
                        {
                            cts.Cancel();
                            status = SelfTestStatus.Ko; detail = $"délai dépassé ({(int)limit.TotalSeconds} s)";
                        }
                        else
                        {
                            await work.ConfigureAwait(false);
                            (status, detail) = SelfTestPlan.Evaluate(action.Id, ctx, rec);
                        }
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        status = SelfTestStatus.Ko; detail = $"délai dépassé ({(int)limit.TotalSeconds} s)";
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        status = SelfTestStatus.Ko; detail = "exception : " + ex.GetType().Name + " " + ex.Message;
                    }
                    sw.Stop();

                    var launches = rec.Launches.ToList();
                    if (launches.Count > 0 && !LaunchPolicy.MayLaunch(action.Id))
                    {
                        status = SelfTestStatus.Ko;
                        detail = (detail.Length > 0 ? detail + " ; " : "") + "LANCEMENT INTERDIT pour cette action";
                        foreach (var l in launches) report.UnexpectedLaunches.Add($"{action.Id} sur « {sample.Name} » : {l}");
                    }
                    var outcome = new SelfTestOutcome(action.Id, action.Title, sample.Name, editable, status, detail, launches,
                        sw.ElapsedMilliseconds, online1 ? SelfTestPlan.ResultText(rec) : null);
                    Add(outcome);
                    progress?.Invoke($"Auto-test : {action.Id} / {sample.Name} : {status.ToString().ToUpperInvariant()} en {sw.ElapsedMilliseconds} ms");
                }
            }
        }
        return report;
    }
}
