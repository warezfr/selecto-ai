using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SelectBar.Actions;
using SelectBar.Input;
using SelectBar.Online;
using SelectBar.SelfTest;
using SelectBar.Settings;
using Xunit;

namespace SelectBar.Tests;

public class LaunchPolicyTests
{
    private sealed class Svc : IActionServices
    {
        public void CopyToClipboard(string text) { }
        public Task SendCutAsync() => Task.CompletedTask;
        public Task SendPasteAsync() => Task.CompletedTask;
        public void OpenUrl(string url) { }
        public void RevealInExplorer(string path) { }
    }

    [Fact]
    public void Only_four_builtin_actions_may_launch_something()
    {
        var all = BuiltInActions.Create(new Svc(), () => new AppSettings(), _ => Array.Empty<string>(), _ => Task.CompletedTask);
        var allowed = all.Where(a => LaunchPolicy.MayLaunch(a.Id)).Select(a => a.Id).OrderBy(x => x).ToList();
        var expected = new[] { BuiltInActions.Mail, BuiltInActions.OpenLink, BuiltInActions.Reveal, BuiltInActions.Search }.OrderBy(x => x).ToList();
        Assert.Equal(expected, allowed);
    }

    [Theory]
    [InlineData(BuiltInActions.Copy)]
    [InlineData(BuiltInActions.Cut)]
    [InlineData(BuiltInActions.Paste)]
    [InlineData(BuiltInActions.Define)]
    [InlineData(BuiltInActions.Translate)]
    [InlineData(BuiltInActions.Ai)]
    [InlineData(BuiltInActions.AiSummarize)]
    [InlineData(BuiltInActions.AiExplain)]
    [InlineData(BuiltInActions.AiRephrase)]
    [InlineData(BuiltInActions.AiCorrect)]
    [InlineData(BuiltInActions.Spell)]
    [InlineData("")]
    public void Other_actions_never_launch(string id) => Assert.False(LaunchPolicy.MayLaunch(id));

    [Fact]
    public void Popup_fallback_extensions_and_settings_buttons_are_allowed_origins()
    {
        Assert.True(LaunchPolicy.MayLaunch(LaunchPolicy.PopupFallback));
        Assert.True(LaunchPolicy.MayLaunch("extension:com.exemple.upper"));
        Assert.True(LaunchPolicy.MayLaunch("ui:settings.keys"));
        Assert.False(LaunchPolicy.MayLaunch(null));
    }

    [Fact]
    public void Origin_of_an_action_is_its_id_or_extension_prefixed()
    {
        var builtIn = new ActionDefinition { Id = BuiltInActions.Search, Title = "x", BuiltIn = true, Execute = _ => Task.CompletedTask };
        var ext = new ActionDefinition { Id = "com.exemple.a", Title = "x", Execute = _ => Task.CompletedTask };
        Assert.Equal("builtin.search", LaunchPolicy.OriginOf(builtIn));
        Assert.Equal("extension:com.exemple.a", LaunchPolicy.OriginOf(ext));
    }
}

public class ExternalLauncherTests
{
    private sealed class Backend : ILaunchBackend
    {
        public List<(LaunchKind Kind, string Target)> Started { get; } = new();
        public bool Fail { get; set; }
        public void Start(LaunchKind kind, string target)
        {
            if (Fail) throw new InvalidOperationException("boom");
            Started.Add((kind, target));
        }
    }

    private static (ExternalLauncher L, Backend B, List<string> Log) Make()
    {
        var b = new Backend();
        var log = new List<string>();
        return (new ExternalLauncher(b, log.Add, () => (321, 654), () => new DateTime(2026, 10, 18, 12, 0, 0)), b, log);
    }

    [Fact]
    public void Without_a_user_click_nothing_launches_and_the_refusal_is_logged()
    {
        var (l, b, log) = Make();
        Assert.False(l.InUserAction);
        Assert.False(l.Open(LaunchKind.Browser, "https://exemple.com"));
        Assert.Empty(b.Started);
        Assert.Contains(log, x => x.Contains("REFUSÉ") && x.Contains("https://exemple.com"));
        Assert.False(l.Records.Single().Accepted);
    }

    [Fact]
    public void Inside_a_click_scope_an_allowed_action_launches_and_is_logged_with_everything()
    {
        var (l, b, log) = Make();
        using (l.Scope(BuiltInActions.Search, LaunchSource.Button))
        {
            Assert.True(l.InUserAction);
            Assert.True(l.Open(LaunchKind.Browser, "https://www.google.com/search?q=chat"));
        }
        Assert.False(l.InUserAction);
        Assert.Equal((LaunchKind.Browser, "https://www.google.com/search?q=chat"), b.Started.Single());
        var line = log.Single();
        Assert.Contains("accepté", line);
        Assert.Contains("origine=builtin.search", line);
        Assert.Contains("source=Button", line);
        Assert.Contains("cible=https://www.google.com/search?q=chat", line);
        Assert.Contains("pointeur=(321,654)", line);
        Assert.Equal(new DateTime(2026, 10, 18, 12, 0, 0), l.Records.Single().Time);
    }

    [Theory]
    [InlineData(BuiltInActions.Copy)]
    [InlineData(BuiltInActions.Define)]
    [InlineData(BuiltInActions.Translate)]
    [InlineData(BuiltInActions.Ai)]
    public void A_non_launching_action_is_refused_even_inside_a_click(string id)
    {
        var (l, b, _) = Make();
        using (l.Scope(id, LaunchSource.Button))
            Assert.False(l.Open(LaunchKind.Browser, "https://exemple.com"));
        Assert.Empty(b.Started);
    }

    [Fact]
    public void Scopes_nest_and_restore()
    {
        var (l, b, _) = Make();
        using (l.Scope(BuiltInActions.OpenLink, LaunchSource.Button))
        {
            using (l.Scope(BuiltInActions.Copy, LaunchSource.Button))
                Assert.False(l.Open(LaunchKind.Browser, "https://a.test"));
            Assert.True(l.Open(LaunchKind.Browser, "https://b.test"));
        }
        Assert.Equal("https://b.test", b.Started.Single().Target);
    }

    [Fact]
    public void Extensions_launch_only_in_a_scope_and_settings_buttons_with_an_explicit_origin()
    {
        var (l, b, _) = Make();
        Assert.False(l.Open(LaunchKind.Browser, "https://ext.test"));
        using (l.Scope("extension:com.exemple.a", LaunchSource.Extension))
            Assert.True(l.Open(LaunchKind.Browser, "https://ext.test"));
        Assert.True(l.OpenAs("ui:settings.keys", LaunchSource.Settings, LaunchKind.Browser, "https://platform.openai.com/api-keys"));
        Assert.False(l.OpenAs(null, LaunchSource.Settings, LaunchKind.Browser, "https://x.test"));
        Assert.False(l.OpenAs("builtin.cut", LaunchSource.Button, LaunchKind.Browser, "https://x.test"));
        Assert.Equal(2, b.Started.Count);
    }

    [Fact]
    public void Empty_target_is_refused_and_backend_errors_do_not_escape()
    {
        var (l, b, log) = Make();
        Assert.False(l.OpenAs("ui:tray.extensions", LaunchSource.Tray, LaunchKind.Explorer, "  "));
        b.Fail = true;
        Assert.False(l.OpenAs("ui:tray.extensions", LaunchSource.Tray, LaunchKind.Explorer, "\"C:\\x\""));
        Assert.Contains(log, x => x.Contains("échoué"));
    }
}

public class ClickGuardTests
{
    [Theory]
    [InlineData(1000, 1000, true)]
    [InlineData(1000, 1399, true)]
    [InlineData(1000, 1400, false)]
    [InlineData(1000, 5000, false)]
    [InlineData(1000, 900, false)]   // horloge qui recule : on ne bloque pas
    public void Clicks_in_the_first_400_ms_are_ignored(long shown, long now, bool ignored) =>
        Assert.Equal(ignored, ClickGuard.Ignore(shown, now));

    [Fact]
    public void Guard_is_only_active_between_arm_and_reset()
    {
        var g = new ClickGuard();
        Assert.False(g.IsAccidental(0));
        g.Arm(10_000);
        Assert.True(g.IsAccidental(10_100));
        Assert.False(g.IsAccidental(10_400));
        g.Arm(20_000);
        Assert.True(g.IsAccidental(20_399));
        g.Reset();
        Assert.False(g.IsAccidental(20_100));
        Assert.Equal(400, ClickGuard.DelayMs);
    }
}

public class BarPlacementTests
{
    [Fact]
    public void Bar_never_covers_the_pointer_with_at_least_12_px_of_margin()
    {
        foreach (var position in new[] { "above", "below" })
            foreach (var (w, h) in new[] { (200, 34), (620, 50) })
                for (var cx = 0; cx <= 1920; cx += 37)
                    for (var cy = 0; cy <= 1080; cy += 29)
                    {
                        var (x, y) = Appearance.PlaceBar(cx, cy, w, h, 10, 24, 0, 0, 1920, 1080, position, 12);
                        Assert.True(Appearance.Clearance(x, y, w, h, cx, cy) >= 12, $"{position} {w}x{h} curseur ({cx},{cy}) -> ({x},{y})");
                        Assert.InRange(x, 0, 1920 - w);
                        Assert.InRange(y, 0, 1080 - h);
                    }
    }

    [Fact]
    public void Without_a_margin_request_the_old_placement_is_unchanged()
    {
        Assert.Equal((400, 456), Appearance.PlaceBar(500, 500, 200, 34, 10, 24, 0, 0, 1920, 1080, "above"));
        Assert.Equal((400, 454), Appearance.PlaceBar(500, 500, 200, 34, 10, 24, 0, 0, 1920, 1080, "above", 12));
    }

    [Fact]
    public void Clearance_is_zero_inside_and_grows_outside()
    {
        Assert.Equal(0, Appearance.Clearance(100, 100, 200, 34, 150, 120));
        Assert.Equal(12, Appearance.Clearance(100, 100, 200, 34, 150, 88));
        Assert.Equal(20, Appearance.Clearance(100, 100, 200, 34, 320, 120));
    }

    [Fact]
    public void Very_long_search_text_is_shortened_so_the_url_stays_openable()
    {
        var text = string.Join(" ", Enumerable.Repeat("mot", 4000));
        var url = SearchEngines.BuildUrl("google", text);
        Assert.True(url.Length < 2000);
        Assert.Equal("https://www.google.com", new Uri(url).GetLeftPart(UriPartial.Authority));
        Assert.Equal("café", SearchEngines.Shorten("café"));
    }
}

public class SelfTestTests
{
    private static Task<ResultPayload> Fine(ResultRequest r, CancellationToken ct) =>
        Task.FromResult(new ResultPayload { Title = "Titre", Body = "Réponse pour " + r.Text.Substring(0, Math.Min(10, r.Text.Length)) });

    private static bool Exists(string p) => p.EndsWith("notepad.exe", StringComparison.OrdinalIgnoreCase) || p == @"C:\Windows";

    private static Task<SelfTestReport> Run(Func<ResultRequest, CancellationToken, Task<ResultPayload>> online) =>
        SelfTestRunner.RunAsync(SelfTestPlan.Samples(@"C:\Windows"), Exists, online, CancellationToken.None);

    [Fact]
    public async Task Matrix_no_action_outside_the_four_launches_anything_and_nothing_fails()
    {
        var report = await Run(Fine);
        Assert.Empty(report.UnexpectedLaunches);
        Assert.Empty(report.Outcomes.Where(o => o.Status == SelfTestStatus.Ko).Select(o => $"{o.ActionId}/{o.Sample}: {o.Detail}"));
        Assert.Equal(0, report.ExitCode);
        foreach (var o in report.Outcomes.Where(o => o.Launches.Count > 0))
            Assert.True(LaunchPolicy.MayLaunch(o.ActionId), o.ActionId);
    }

    [Fact]
    public async Task Matrix_launches_come_from_the_expected_actions_for_each_kind_of_text()
    {
        var report = await Run(Fine);
        List<string> Launchers(string sample) => report.Outcomes.Where(o => o.Sample == sample && o.Launches.Count > 0)
            .Select(o => o.ActionId).Distinct().OrderBy(x => x).ToList();

        Assert.Equal(new[] { BuiltInActions.Search }, Launchers("mot anglais"));
        Assert.Equal(new[] { BuiltInActions.Search }, Launchers("phrase mal transcrite"));
        Assert.Equal(new[] { BuiltInActions.OpenLink, BuiltInActions.Search }, Launchers("URL"));
        Assert.Equal(new[] { BuiltInActions.OpenLink, BuiltInActions.Search }, Launchers("www.exemple.com"));
        Assert.Equal(new[] { BuiltInActions.Mail, BuiltInActions.Search }, Launchers("e-mail"));
        Assert.Equal(new[] { BuiltInActions.Reveal, BuiltInActions.Search }, Launchers("chemin Windows existant"));
        Assert.DoesNotContain(BuiltInActions.Reveal, Launchers("chemin Windows inexistant"));
    }

    [Fact]
    public async Task Online_actions_call_the_popup_and_never_launch()
    {
        var calls = new List<ResultKind>();
        var report = await Run((r, ct) => { calls.Add(r.Kind); return Fine(r, ct); });
        Assert.Contains(ResultKind.Define, calls);
        Assert.Contains(ResultKind.Translate, calls);
        Assert.Contains(ResultKind.Summarize, calls);
        Assert.Contains(ResultKind.Correct, calls);
        Assert.All(report.Outcomes.Where(o => SelfTestPlan.IsOnlineAction(o.ActionId)), o => Assert.Empty(o.Launches));
    }

    [Fact]
    public async Task Empty_selection_shows_no_bar_and_long_text_is_covered()
    {
        var report = await Run(Fine);
        Assert.Contains(report.Outcomes, o => o.Sample == "texte vide" && o.Status == SelfTestStatus.Ok && o.ActionId == "(aucune)");
        Assert.Contains(report.Outcomes, o => o.Sample.StartsWith("texte très long") && o.ActionId == BuiltInActions.Search && o.Status == SelfTestStatus.Ok);
    }

    [Fact]
    public async Task Unconfigured_ai_is_skipped_but_an_online_error_is_a_failure_with_exit_code_1()
    {
        var skipped = await Run((r, ct) => Task.FromResult(new ResultPayload { Error = "Aucun modèle configuré.", NeedsAiSetup = true }));
        Assert.True(skipped.Skipped > 0);
        Assert.Equal(0, skipped.Ko);
        Assert.Equal(0, skipped.ExitCode);

        var failed = await Run((r, ct) => Task.FromResult(new ResultPayload { Error = "Impossible de joindre le service." }));
        Assert.True(failed.Ko > 0);
        Assert.Equal(1, failed.ExitCode);
        Assert.Contains("RÉSULTAT : ÉCHEC", failed.Format(new DateTime(2026, 10, 18), "2.0.0"));
    }

    [Fact]
    public async Task Report_lists_every_pair_with_the_launches_and_the_unexpected_ones()
    {
        var report = await Run(Fine);
        var text = report.Format(new DateTime(2026, 10, 18, 9, 0, 0), "2.0.0", new[] { "Modèle IA : non configuré" });
        Assert.Contains("== URL ==", text);
        Assert.Contains("builtin.openlink", text);
        Assert.Contains("lancé : navigateur : https://fr.wikipedia.org/wiki/Paris", text);
        Assert.Contains("Lancements externes inattendus : aucun", text);
        Assert.Contains("RÉSULTAT : SUCCÈS", text);
    }

    [Fact]
    public async Task A_provider_that_never_answers_ends_as_a_timeout_ko_and_the_rest_of_the_matrix_completes()
    {
        var never = new TaskCompletionSource<ResultPayload>().Task;   // ne répond jamais et ignore l'annulation
        var started = new List<string>();
        var done = new List<SelfTestOutcome>();
        var report = await SelfTestRunner.RunAsync(SelfTestPlan.Samples(@"C:\Windows"), Exists,
            (r, ct) => r.Kind == ResultKind.Translate ? never : Fine(r, ct), CancellationToken.None, started.Add,
            TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200), null, done.Add);

        var hung = report.Outcomes.Where(o => o.ActionId == BuiltInActions.Translate).ToList();
        Assert.NotEmpty(hung);
        Assert.All(hung, o => { Assert.Equal(SelfTestStatus.Ko, o.Status); Assert.Contains("délai dépassé", o.Detail); });
        Assert.All(report.Outcomes.Where(o => o.ActionId != BuiltInActions.Translate), o => Assert.NotEqual(SelfTestStatus.Ko, o.Status));
        Assert.Contains(report.Outcomes, o => o.ActionId == BuiltInActions.Copy && o.Status == SelfTestStatus.Ok);
        Assert.Contains(report.Outcomes, o => o.Sample.StartsWith("texte très long") && o.ActionId == BuiltInActions.Ai);   // la suite a bien été jouée
        Assert.Equal(1, report.ExitCode);
        Assert.Equal(report.Outcomes.Count, done.Count);
        Assert.Contains(started, l => l.Contains("début"));
        Assert.Contains(started, l => l.Contains("KO en"));
        var text = report.Format(new DateTime(2026, 10, 18), "2.0.0");
        Assert.Contains("RÉSUMÉ : " + report.Outcomes.Count + " cas", text);
    }

    [Fact]
    public void Recording_services_record_instead_of_executing()
    {
        var rec = new RecordingServices((r, ct) => Fine(r, ct));
        rec.OpenUrl("https://exemple.com");
        rec.OpenUrl("javascript:alert(1)");
        rec.RevealInExplorer(@"C:\x");
        rec.CopyToClipboard("a");
        Assert.Equal(new[] { "navigateur : https://exemple.com/", "refusé par LinkSafety : javascript:alert(1)", @"Explorateur : C:\x" }, rec.Launches);
        Assert.Equal(new[] { "a" }, rec.Copied);
        rec.Reset();
        Assert.Empty(rec.Launches);
    }
}
