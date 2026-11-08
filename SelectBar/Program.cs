using System;
using System.Linq;
using System.Threading;
using System.Windows;
using SelectBar.Packaging;
using SelectBar.UI;

namespace SelectBar;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Migration silencieuse « SelectBar » -> « Selecto AI » : avant le journal, les réglages et tout le reste (qui créent le nouveau dossier).
        CaptureOptions.Current = CaptureOptions.Parse(args, Environment.GetEnvironmentVariable);
        var migrationNotes = new System.Collections.Generic.List<string>();
        try { SelectBar.Settings.AppDataMigration.RunDefault(migrationNotes.Add); }
        catch (Exception ex) { migrationNotes.Add("Migration ignorée : " + ex.Message); }

        // --selftest : sans interface ni hook ni instance unique ; rapport dans %APPDATA%\SelectoAI\selftest-report.txt
        if (args.Any(a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase)))
            return SelfTest.SelfTestHost.Run();

        using var mutex = new Mutex(true, @"Local\SelectoAI.SingleInstance", out var isFirst);
        using var showSettings = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\SelectoAI.ShowSettings");
        if (!isFirst) { showSettings.Set(); return 0; } // un second lancement rouvre les réglages de l'instance active

        // Filets globaux : une exception hors du Dispatcher (timer, thread du pool, async void) tuait le processus sans trace.
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write("Erreur fatale : " + e.ExceptionObject);
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Write("Tâche sans observateur : " + e.Exception);
            e.SetObserved();
        };

        Log.Write("Main: démarrage");
        if (CaptureOptions.Current.CaptureMode) Log.Write("Mode capture activé");
        foreach (var note in migrationNotes) Log.Write(note);
        // Langue de l'interface : réglée avant toute fenêtre. Les réglages lus ici sont transmis à AppController (pas de seconde lecture).
        var uiSettings = new SelectBar.Settings.SettingsStore().Load();
        SelectBar.Localization.L.Culture = SelectBar.Localization.UiCulture.Make(
            SelectBar.Localization.UiLanguageResolver.Resolve(uiSettings.UiLanguage, System.Globalization.CultureInfo.CurrentUICulture.Name));
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        FluentTheme.Apply(app); // WPF UI (Windows 11 Fluent, thème et accent du système) avant toute fenêtre
        AppIcon.Register();    // icône de l'application pour toutes les fenêtres
        app.DispatcherUnhandledException += (_, e) => { Log.Write("Erreur : " + e.Exception); e.Handled = true; };

        AppController? controller = null;
        app.SessionEnding += (_, _) => controller?.QuitWithoutAsking(); // arrêt de Windows : pas de confirmation
        try
        {
            controller = new AppController(uiSettings);
            Log.Write("Contrôleur créé");
            controller.Start();
            controller.ApplyCaptureOptions();
            Log.Write("Start OK, hook installé");
        }
        catch (Exception ex)
        {
            Log.Write("Démarrage impossible : " + ex);
            MessageBox.Show("Selecto AI n'a pas pu démarrer :\n" + ex.Message, EditionInfo.WindowTitle());
            return 1;
        }
        var listener = new Thread(() =>
        {
            try
            {
                while (showSettings.WaitOne())
                    app.Dispatcher.BeginInvoke(new Action(() => controller?.OpenSettings()));
            }
            catch { }
        }) { IsBackground = true };
        listener.Start();
        app.Run();
        controller.Dispose();
        return 0;
    }
}
