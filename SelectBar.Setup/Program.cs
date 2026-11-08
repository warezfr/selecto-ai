using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using SelectBar.Setup.Install;
using SelectBar.Setup.Logic;
using SelectBar.Setup.UI;

namespace SelectBar.Setup;

/// <summary>
/// Point d'entrée unique : installation (défaut), désinstallation (--uninstall), silencieux (/S ou --silent), démonstration (--demo).
/// Codes de sortie : 0 = succès, 1 = échec.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] argv)
    {
        var o = SetupArgs.Parse(argv);
        var lang = Strings.ResolveLang(o.Lang, CultureInfo.CurrentUICulture.Name);
        SetupLog.Write($"--- Selecto AI Setup {BuildInfo.Version} {BuildInfo.Arch} : {string.Join(' ', argv)}");
        foreach (var u in o.Unknown) SetupLog.Write("Argument ignoré : " + u);

        try
        {
            if (o.Demo) return RunUi(o, lang);

            using var mutex = new Mutex(true, InstallPaths.SetupMutexName, out var first);
            if (!first)
            {
                if (!o.Silent) MessageBox.Show(Strings.Get("running.other", lang), BuildInfo.DisplayName, MessageBoxButton.OK, MessageBoxImage.Information);
                return 1;
            }
            return o.Silent ? RunSilent(o, lang) : RunUi(o, lang);
        }
        catch (Exception ex)
        {
            SetupLog.Write("Erreur fatale : " + ex);
            if (!o.Silent)
                MessageBox.Show(ex.Message + Environment.NewLine + Environment.NewLine + SetupLog.FilePath, BuildInfo.DisplayName, MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }

    private static int RunUi(SetupOptions o, string lang)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.DispatcherUnhandledException += (_, e) =>
        {
            SetupLog.Write("Exception d'interface : " + e.Exception);
            e.Handled = true;
        };
        SetupTheme.Apply(app, o.Theme);
        var window = new SetupWindow(o, lang);
        app.Run(window);
        return window.ExitCode;
    }

    private static int RunSilent(SetupOptions o, string lang)
    {
        if (o.Mode == SetupMode.Uninstall)
        {
            var dir = InstallPaths.NormalizeDir(o.RelocatedDir ?? SystemOps.ReadInstalled().Location);
            var r = UninstallEngine.Run(new UninstallRequest(dir, o.PurgeData, lang), null, CancellationToken.None);
            SetupLog.Write(r.Success ? "Désinstallation silencieuse : OK" : "Désinstallation silencieuse : échec : " + r.Message);
            return r.Success ? 0 : 1;
        }

        var installed = SystemOps.ReadInstalled();
        var target = InstallPaths.ChooseInstallDir(o.Dir, installed.Location, SystemOps.LocalAppData);
        var result = InstallEngine.Run(new InstallRequest(target, o.DesktopShortcut, o.Startup, o.Force, lang), null, CancellationToken.None);
        if (!result.Success)
        {
            SetupLog.Write("Installation silencieuse : échec : " + result.Message);
            return 1;
        }
        // Une application qui tournait avant la mise à jour est relancée (aucun lancement sinon, comme avec skipifsilent).
        if (result.WasRunning) SystemOps.Launch(InstallPaths.ExePath(target));
        SetupLog.Write("Installation silencieuse : OK");
        return 0;
    }
}
