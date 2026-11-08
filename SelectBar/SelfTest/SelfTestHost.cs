using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SelectBar.Online;
using SelectBar.Settings;

namespace SelectBar.SelfTest;

/// <summary>
/// SelectoAI.exe --selftest : sans interface ni hook. Joue chaque action intégrée sur des sélections types avec de faux services
/// (lancements, presse-papiers et popups enregistrés, jamais exécutés) et de vrais fournisseurs pour les actions en ligne.
/// Écrit selftest-report.txt dans %APPDATA%\SelectoAI ; code de sortie 1 s'il y a un KO.
/// </summary>
public static class SelfTestHost
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    public static readonly TimeSpan GlobalTimeout = TimeSpan.FromMinutes(3);

    /// <summary>Lance l'auto-test et TERMINE le processus (Environment.Exit) : il ne reste jamais en arrière-plan.</summary>
    public static int Run()
    {
        var code = 2;
        try { code = RunCore(); }
        catch (Exception ex) { Log.Write("Auto-test : échec " + ex); }
        Log.Write("Auto-test : fin, code de sortie " + code);
        Environment.Exit(code);
        return code;
    }

    private static int RunCore()
    {
        TextWriter? console = null;
        try
        {
            AttachConsole(-1);                                   // affiche le résumé dans le terminal qui a lancé l'application
            console = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
        }
        catch { }
        void Say(string line) { try { console?.WriteLine(line); } catch { } }

        Log.Write("Auto-test : démarrage");
        var store = new SettingsStore();
        var path = Path.Combine(store.Directory, "selftest-report.txt");
        var version = typeof(SelfTestHost).Assembly.GetName().Version?.ToString(3) ?? "2.0.0";
        var report = new SelfTestReport();
        var gate = new object();
        void Append(string line)                                // rapport écrit au fur et à mesure, une ligne par cas
        {
            lock (gate)
            {
                try { Directory.CreateDirectory(store.Directory); File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(true)); }
                catch { }
            }
        }
        // une exception non gérée ne doit jamais laisser le processus en vie
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log.Write("Auto-test : exception non gérée " + e.ExceptionObject);
            Append("ERREUR NON GÉRÉE : " + e.ExceptionObject);
            Environment.Exit(2);
        };

        try { File.WriteAllText(path, $"Selecto AI {version} — auto-test des actions — {DateTime.Now:yyyy-MM-dd HH:mm:ss} (rapport écrit cas par cas)" + Environment.NewLine, new UTF8Encoding(true)); }
        catch (Exception ex) { Log.Write("Auto-test : rapport non créé : " + ex.Message); }

        var settings = store.Load();
        settings.OnlineActionsEnabled = true;                    // en mémoire seulement : rien n'est enregistré
        Log.Write("Auto-test : réglages lus");
        var vault = new ApiKeyVault(new SecretStore());
        OnlineLog.Sink = Log.Write;
        LocalAiManager? localAi = null;
        // Aucun Dispatcher, aucune boucle de messages WPF : tout passe par des tâches du pool et des faux services.
        if (settings.LlmProvider == LlmPresets.Ollama)
        {
            // le moteur local est démarré une seule fois, avec une limite de 25 s (un cas ne doit jamais l'attendre)
            localAi = new LocalAiManager(OnlineHttp.Client, new OllamaClient(OnlineHttp.Client));
            try
            {
                var start = Task.Run(() => localAi.EnsureRunningAsync(CancellationToken.None));
                Log.Write("Auto-test : moteur IA " + (start.Wait(TimeSpan.FromSeconds(25)) ? "prêt" : "pas prêt après 25 s"));
            }
            catch (Exception ex) { Log.Write("Auto-test : moteur IA : " + ex.Message); }
        }
        var results = new ResultService(() => settings, OnlineHttp.Client, () => vault.Unprotect(settings.LlmApiKeyProtected));

        // même garde que l'application : un partage réseau injoignable ne doit pas bloquer l'auto-test
        Func<string, bool> exists = p =>
        {
            var t = Task.Run(() => File.Exists(p) || Directory.Exists(p));
            return t.Wait(800) && t.Result;
        };
        var samples = SelfTestPlan.Samples(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        var llm = LlmPresets.IsConfigured(settings)
            ? $"Modèle IA : {settings.LlmProvider} / {settings.LlmModel}"
            : "Modèle IA : non configuré (les actions IA sont ignorées)";
        Append(llm);
        Append("Fournisseurs en ligne : dictionnaire, traduction et Wikipédia réels. Délais : 10 s par cas local, 20 s par cas réseau/IA, 3 min au total.");

        using var cts = new CancellationTokenSource();
        var run = Task.Run(() => SelfTestRunner.RunAsync(samples, exists, results.RunAsync, cts.Token, line =>
        {
            Log.Write(line);
            Say("  " + line);
        }, null, null, report, o => Append(SelfTestReport.FormatLine(o))));

        var finished = false;
        try { finished = run.Wait(GlobalTimeout); }
        catch (Exception ex) { Log.Write("Auto-test : échec " + ex.GetBaseException()); Append("ERREUR : " + ex.GetBaseException().Message); }
        if (!finished && !run.IsCompleted)
        {
            report.Aborted = true;
            cts.Cancel();
            Log.Write("Auto-test : délai global de 3 minutes dépassé, sortie forcée avec le rapport partiel");
        }

        try { localAi?.StopOwned(); } catch { }
        string full;
        lock (gate) full = report.Format(DateTime.Now, version, new[] { llm });
        Append("");
        Append(full.Substring(full.IndexOf("RÉSUMÉ", StringComparison.Ordinal) is var i && i >= 0 ? i : 0));
        // copie complète (ordre par texte) à côté du rapport au fil de l'eau
        try { File.WriteAllText(Path.Combine(store.Directory, "selftest-report-complet.txt"), full, new UTF8Encoding(true)); } catch { }

        var summary = $"Auto-test : {report.Outcomes.Count} cas, {report.Ok} OK, {report.Ko} KO, {report.Skipped} ignoré(s), {report.UnexpectedLaunches.Count} lancement(s) inattendu(s)";
        Log.Write(summary);
        Say(summary);
        Say("Rapport : " + path);
        return report.ExitCode;
    }
}
