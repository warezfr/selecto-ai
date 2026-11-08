using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using SelectBar.Settings;
using SelectBar.Setup.Logic;

namespace SelectBar.Setup.Install;

/// <summary>
/// Installation : ferme l'application, extrait la charge utile, crée l'uninstaller, les raccourcis, l'entrée « Applications installées ».
/// Toute modification est annulable (pile d'actions inverses) : annulation de l'utilisateur ou échec = retour à l'état précédent.
/// </summary>
internal static class InstallEngine
{
    public static OpResult Run(InstallRequest req, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var undo = new Stack<Action>();
        string? backupRoot = null;
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
            if (InstallPaths.ValidateDir(dir) != DirCheck.Ok) return OpResult.Fail(Strings.Format("err.baddir", lang, dir));
            if (!BuildInfo.HasPayload) return OpResult.Fail(Strings.Get("err.nopayload", lang));

            var installed = SystemOps.ReadInstalled();
            if (VersionLogic.IsDowngrade(BuildInfo.Version, installed.Version, req.Force))
                return OpResult.Fail(Strings.Format("err.downgrade", lang, installed.Version ?? "", BuildInfo.Version));

            SetupLog.Write($"Installation {BuildInfo.DisplayName} {BuildInfo.Version} ({BuildInfo.Arch}) dans {dir} (précédente : {installed.Version ?? "aucune"})");
            Report(InstallStep.Prepare, 0.4, "", true);
            var (wasRunning, runningExe) = SystemOps.CloseApp(ct);
            ct.ThrowIfCancellationRequested();

            using var payload = BuildInfo.OpenPayload() ?? throw new InvalidOperationException("Charge utile illisible.");
            using var zip = new ZipArchive(payload, ZipArchiveMode.Read);
            var entries = zip.Entries
                .Select(e => (Entry: e, Rel: InstallPaths.SafeRelativePath(e.FullName)))
                .Where(x => x.Rel != null)
                .ToList();
            if (entries.Count == 0) return OpResult.Fail(Strings.Get("err.nopayload", lang));
            var totalBytes = Math.Max(1, SizeFormat.Total(entries.Select(x => x.Entry.Length)));

            EnsureDir(dir, undo);
            backupRoot = Path.Combine(Path.GetTempPath(), "SelectoAI-Setup-" + Guid.NewGuid().ToString("N"));
            var backupIndex = 0;
            long done = 0;
            Report(InstallStep.Prepare, 1, "", true);

            // ---- Copie des fichiers
            foreach (var (entry, rel) in entries)
            {
                ct.ThrowIfCancellationRequested();
                var target = Path.GetFullPath(Path.Combine(dir, rel!));
                if (!InstallPaths.IsUnderDir(target, dir)) throw new InvalidOperationException("Chemin refusé dans la charge utile : " + entry.FullName);
                EnsureDir(Path.GetDirectoryName(target)!, undo);
                BackupExisting(target, backupRoot, ref backupIndex, undo);

                using (var src = entry.Open())
                using (var dst = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 81920))
                {
                    undo.Push(() => SystemOps.DeleteFileRobust(target));
                    var buffer = new byte[81920];
                    int n;
                    while ((n = src.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        dst.Write(buffer, 0, n);
                        done += n;
                        Report(InstallStep.Copy, (double)done / totalBytes, rel!);
                    }
                }
                try { File.SetLastWriteTimeUtc(target, entry.LastWriteTime.UtcDateTime); } catch { }
            }
            Report(InstallStep.Copy, 1, "", true);

            if (!File.Exists(InstallPaths.ExePath(dir))) throw new FileNotFoundException("SelectoAI.exe absent de la charge utile.");
            if (!File.Exists(InstallPaths.IconPath(dir))) SetupLog.Write("Avertissement : SelectoAI.ico absent de la charge utile.");

            // ---- Désinstallateur : la partie « stub » du Setup (sans la charge utile) devient Uninstall.exe
            var self = Environment.ProcessPath;
            var uninstaller = InstallPaths.UninstallerPath(dir);
            long uninstallerSize = 0;
            if (!string.IsNullOrEmpty(self) && File.Exists(self))
            {
                BackupExisting(uninstaller, backupRoot, ref backupIndex, undo);
                if (BuildInfo.Overlay is { } overlay) OverlayTrailer.CopyPrefix(self, uninstaller, overlay.StubLength);
                else File.Copy(self, uninstaller, true);   // pas de charge utile jointe (ne devrait pas arriver ici) : copie intégrale
                undo.Push(() => SystemOps.DeleteFileRobust(uninstaller));
                uninstallerSize = new FileInfo(uninstaller).Length;
            }
            else SetupLog.Write("Avertissement : chemin du Setup inconnu, Uninstall.exe non créé.");

            // ---- Raccourcis et démarrage avec Windows
            ct.ThrowIfCancellationRequested();
            Report(InstallStep.Shortcuts, 0, "", true);
            var uninstallLabel = Strings.Format("un.shortcut", lang, BuildInfo.DisplayName);
            var shortcuts = InstallPaths.PlanShortcuts(dir, SystemOps.StartMenuPrograms, SystemOps.Desktop, BuildInfo.DisplayName, req.DesktopShortcut, uninstallLabel);
            var count = 0;
            foreach (var s in shortcuts)
            {
                ct.ThrowIfCancellationRequested();
                var existed = File.Exists(s.LinkPath);
                var groupDir = Path.GetDirectoryName(s.LinkPath);
                var groupExisted = groupDir != null && Directory.Exists(groupDir);
                SystemOps.CreateShortcut(s);
                if (!existed) undo.Push(() => SystemOps.DeleteFileRobust(s.LinkPath));
                if (!groupExisted && groupDir != null) undo.Push(() => { try { Directory.Delete(groupDir, false); } catch { } });
                count++;
                Report(InstallStep.Shortcuts, (double)count / (shortcuts.Count + 1), s.Description);
            }
            if (req.Startup)
            {
                var key = new RegistryRunKey();
                var previous = key.Get(StartupEntries.Name);
                StartupEntries.Apply(key, true, InstallPaths.ExePath(dir));
                undo.Push(() => { if (previous != null) key.Set(StartupEntries.Name, previous); else key.Delete(StartupEntries.Name); });
            }

            // ---- Finalisation : point de non-retour
            ct.ThrowIfCancellationRequested();
            Report(InstallStep.Finalize, 0, "", true);
            var installedBytes = totalBytes + uninstallerSize;
            SystemOps.WriteUninstallEntry(UninstallEntry.Build(BuildInfo.DisplayName, BuildInfo.Version, dir, installedBytes));
            SystemOps.DeleteInnoEntryForCurrentUser();
            try { if (backupRoot != null && Directory.Exists(backupRoot)) Directory.Delete(backupRoot, true); } catch { }
            undo.Clear();
            Report(InstallStep.Finalize, 1, "", true);
            SetupLog.Write("Installation terminée.");
            return OpResult.Ok(wasRunning, runningExe);
        }
        catch (OperationCanceledException)
        {
            SetupLog.Write("Installation annulée : retour en arrière.");
            Rollback(undo, backupRoot);
            return OpResult.Cancel(Strings.Get("err.cancelled", lang));
        }
        catch (Exception ex)
        {
            SetupLog.Write("Échec de l'installation : " + ex);
            Rollback(undo, backupRoot);
            return OpResult.Fail(ex.Message, ex.ToString());
        }
    }

    private static void Rollback(Stack<Action> undo, string? backupRoot)
    {
        while (undo.Count > 0)
        {
            var a = undo.Pop();
            try { a(); } catch (Exception ex) { SetupLog.Write("Retour en arrière partiel : " + ex.Message); }
        }
        try { if (backupRoot != null && Directory.Exists(backupRoot)) Directory.Delete(backupRoot, true); } catch { }
    }

    /// <summary>Crée le dossier (et ses parents) ; l'action inverse retire ceux qui ont été créés s'ils sont vides.</summary>
    private static void EnsureDir(string path, Stack<Action> undo)
    {
        var missing = new Stack<string>();
        var p = path;
        while (!string.IsNullOrEmpty(p) && !Directory.Exists(p))
        {
            missing.Push(p);
            p = Path.GetDirectoryName(p);
        }
        while (missing.Count > 0)
        {
            var d = missing.Pop();
            Directory.CreateDirectory(d);
            undo.Push(() => { try { Directory.Delete(d, false); } catch { } });
        }
    }

    /// <summary>Déplace un fichier existant dans le dossier de sauvegarde (nouvel essai si verrouillé) ; l'action inverse le remet.</summary>
    private static void BackupExisting(string target, string backupRoot, ref int index, Stack<Action> undo)
    {
        if (!File.Exists(target)) return;
        Directory.CreateDirectory(backupRoot);
        var backup = Path.Combine(backupRoot, (index++).ToString() + ".bak");
        for (var attempt = 0; ; attempt++)
        {
            try { File.Move(target, backup); break; }
            catch (IOException) when (attempt < 6) { Thread.Sleep(500); }
            catch (UnauthorizedAccessException) when (attempt < 6) { Thread.Sleep(500); }
        }
        undo.Push(() => { SystemOps.DeleteFileRobust(target); if (File.Exists(backup)) File.Move(backup, target); });
    }
}
