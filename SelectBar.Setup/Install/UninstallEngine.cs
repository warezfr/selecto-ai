using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SelectBar.Settings;
using SelectBar.Setup.Logic;

namespace SelectBar.Setup.Install;

/// <summary>Désinstallation exécutée sur place par Uninstall.exe (aucune copie dans %TEMP%).</summary>
internal static class UninstallEngine
{
    public static OpResult Run(UninstallRequest req, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var lang = req.Lang;
        var lastReport = 0L;
        var lastPercent = -1.0;
        void Report(InstallStep step, double fraction, string detail, bool force = false)
        {
            var percent = ProgressSteps.Percent(step, fraction);
            var now = Environment.TickCount64;
            if (!force && now - lastReport < 60 && Math.Abs(percent - lastPercent) < 0.5) return;
            lastReport = now; lastPercent = percent;
            progress?.Report(new ProgressInfo(step, percent, detail));
        }

        try
        {
            Report(InstallStep.Prepare, 0, "", true);
            var dir = InstallPaths.NormalizeDir(req.Dir);
            if (!InstallPaths.IsSafeInstallDir(dir)) return OpResult.Fail(Strings.Format("err.baddir", lang, dir));
            var knownFolder = File.Exists(InstallPaths.UninstallerPath(dir)) || File.Exists(InstallPaths.ExePath(dir));
            if (!knownFolder && Directory.Exists(dir)) return OpResult.Fail(Strings.Format("err.baddir", lang, dir));
            SetupLog.Write($"Désinstallation de {dir} (purge des données : {req.PurgeData})");

            SystemOps.CloseApp(ct);
            Report(InstallStep.Prepare, 1, "", true);

            // ---- Fichiers
            var failed = new List<string>();
            if (Directory.Exists(dir))
            {
                var total = Math.Max(1, CountFiles(dir));
                var done = 0;
                failed = SystemOps.DeleteTree(dir, f =>
                {
                    done++;
                    Report(InstallStep.Copy, (double)done / total, Path.GetFileName(f));
                });
            }
            Report(InstallStep.Copy, 1, "", true);

            // ---- Raccourcis, démarrage, registre
            Report(InstallStep.Shortcuts, 0, "", true);
            foreach (var lnk in InstallPaths.AllShortcutPaths(SystemOps.StartMenuPrograms, SystemOps.Desktop,
                         Strings.Format("un.shortcut", "fr", "Selecto AI"), Strings.Format("un.shortcut", "en", "Selecto AI")))
            {
                try { SystemOps.DeleteFileRobust(lnk); } catch (Exception ex) { SetupLog.Write("Raccourci non supprimé : " + lnk + " (" + ex.Message + ")"); }
            }
            foreach (var lnk in Directory.Exists(InstallPaths.StartMenuGroup(SystemOps.StartMenuPrograms))
                         ? Directory.GetFiles(InstallPaths.StartMenuGroup(SystemOps.StartMenuPrograms), "*.lnk")
                         : Array.Empty<string>())
            {
                try { SystemOps.DeleteFileRobust(lnk); } catch { }
            }
            try { Directory.Delete(InstallPaths.StartMenuGroup(SystemOps.StartMenuPrograms), false); } catch { }
            try { StartupEntries.Apply(new RegistryRunKey(), false, null); } catch (Exception ex) { SetupLog.Write("Entrée de démarrage non supprimée : " + ex.Message); }
            SystemOps.DeleteUninstallEntry();
            Report(InstallStep.Shortcuts, 1, "", true);

            // ---- Données (facultatif) puis nettoyage différé
            Report(InstallStep.Finalize, 0, "", true);
            if (req.PurgeData)
            {
                var roaming = InstallPaths.RoamingDataDir(SystemOps.AppData);
                var local = InstallPaths.LocalDataDir(SystemOps.LocalAppData);
                SystemOps.KillProcessesUnder(local); // moteur Ollama privé uniquement
                foreach (var d in new[] { roaming, local })
                {
                    var left = SystemOps.DeleteTree(d);
                    if (left.Count > 0) SetupLog.Write(left.Count + " fichier(s) de données non supprimé(s) dans " + d);
                }
                SystemOps.DeleteDataRegistryKey();
            }
            // Uninstall.exe est lui-même dans le dossier (verrouillé) : lui-même et le dossier sont supprimés au redémarrage si besoin.
            if (InstallPaths.IsSafeInstallDir(dir)) SystemOps.FinishInstallDir(dir, failed);
            Report(InstallStep.Finalize, 1, "", true);
            if (failed.Count > 0) SetupLog.Write(failed.Count + " fichier(s) du programme n'ont pas pu être supprimés.");
            SetupLog.Write("Désinstallation terminée.");
            return OpResult.Ok();
        }
        catch (Exception ex)
        {
            SetupLog.Write("Échec de la désinstallation : " + ex);
            return OpResult.Fail(ex.Message, ex.ToString());
        }
    }

    private static int CountFiles(string dir)
    {
        try { return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Count(); }
        catch { return 1; }
    }
}
