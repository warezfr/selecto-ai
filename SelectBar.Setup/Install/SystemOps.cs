using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using SelectBar.Settings;
using SelectBar.Setup.Logic;

namespace SelectBar.Setup.Install;

/// <summary>Registre HKCU\...\Run (même clé que l'application).</summary>
internal sealed class RegistryRunKey : IRunKey
{
    public string? Get(string name)
    {
        using var k = Registry.CurrentUser.OpenSubKey(InstallPaths.RunKeyPath);
        return k?.GetValue(name) as string;
    }

    public void Set(string name, string value)
    {
        using var k = Registry.CurrentUser.CreateSubKey(InstallPaths.RunKeyPath);
        k.SetValue(name, value, RegistryValueKind.String);
    }

    public void Delete(string name)
    {
        using var k = Registry.CurrentUser.OpenSubKey(InstallPaths.RunKeyPath, true);
        k?.DeleteValue(name, false);
    }
}

internal sealed record InstalledInfo(string? Version, string? Location);

/// <summary>Opérations Windows (registre, processus, raccourcis, fichiers). Tout est enveloppé : un échec est journalisé, jamais fatal sauf mention.</summary>
internal static class SystemOps
{
    public static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    public static string StartMenuPrograms => Environment.GetFolderPath(Environment.SpecialFolder.Programs);
    public static string Desktop => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    // ---------- installation existante ----------

    public static InstalledInfo ReadInstalled()
    {
        var versions = new List<string?>();
        string? location = null;
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(UninstallEntry.KeyPath);
            if (k != null)
            {
                versions.Add(k.GetValue("DisplayVersion") as string);
                location = k.GetValue("InstallLocation") as string;
            }
        }
        catch (Exception ex) { SetupLog.Write("Lecture de l'entrée du Setup : " + ex.Message); }

        var inno = InstallPaths.UninstallKeyRoot + "\\" + InstallPaths.InnoUninstallKeyName;
        foreach (var (hive, view) in new[] { (RegistryHive.CurrentUser, RegistryView.Default), (RegistryHive.LocalMachine, RegistryView.Registry64), (RegistryHive.LocalMachine, RegistryView.Registry32) })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var k = baseKey.OpenSubKey(inno);
                if (k == null) continue;
                versions.Add(k.GetValue("DisplayVersion") as string);
                // Une installation « pour tous » (HKLM) ne sert que pour la version : son dossier est inaccessible en écriture sans droits administrateur.
                if (hive == RegistryHive.CurrentUser) location ??= (k.GetValue("InstallLocation") as string)?.TrimEnd('\\');
            }
            catch (Exception ex) { SetupLog.Write("Lecture de l'entrée Inno : " + ex.Message); }
        }
        return new InstalledInfo(VersionLogic.Newest(versions), location);
    }

    public static void DeleteInnoEntryForCurrentUser()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(InstallPaths.UninstallKeyRoot, true);
            k?.DeleteSubKeyTree(InstallPaths.InnoUninstallKeyName, false);
        }
        catch (Exception ex) { SetupLog.Write("Entrée Inno non supprimée : " + ex.Message); }
    }

    public static void WriteUninstallEntry(IEnumerable<RegValue> values)
    {
        using var k = Registry.CurrentUser.CreateSubKey(UninstallEntry.KeyPath);
        foreach (var v in values)
        {
            if (v.DWord.HasValue) k.SetValue(v.Name, v.DWord.Value, RegistryValueKind.DWord);
            else if (v.Text != null) k.SetValue(v.Name, v.Text, RegistryValueKind.String);
        }
    }

    public static void DeleteUninstallEntry()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(InstallPaths.UninstallKeyRoot, true);
            k?.DeleteSubKeyTree(InstallPaths.UninstallKeyName, false);
        }
        catch (Exception ex) { SetupLog.Write("Entrée de désinstallation non supprimée : " + ex.Message); }
    }

    public static void DeleteDataRegistryKey()
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(InstallPaths.DataRegistryKey, false); }
        catch (Exception ex) { SetupLog.Write("Clé HKCU\\Software\\SelectoAI non supprimée : " + ex.Message); }
    }

    // ---------- application en cours ----------

    public static bool IsAppRunning()
    {
        try
        {
            if (Mutex.TryOpenExisting(InstallPaths.MutexName, out var m)) { m.Dispose(); return true; }
        }
        catch { }
        return false;
    }

    /// <summary>Ferme proprement SelectoAI.exe (fenêtre principale) puis le termine si besoin. Renvoie (elle tournait, chemin de l'exécutable).</summary>
    public static (bool WasRunning, string? Exe) CloseApp(CancellationToken ct)
    {
        Process[] procs;
        try { procs = Process.GetProcessesByName("SelectoAI"); }
        catch { procs = Array.Empty<Process>(); }
        var running = procs.Length > 0 || IsAppRunning();
        string? exe = null;
        foreach (var p in procs)
        {
            try { exe ??= p.MainModule?.FileName; } catch { }
        }
        if (!running) return (false, null);
        SetupLog.Write("Selecto AI est en cours d'exécution : fermeture.");
        foreach (var p in procs) { try { p.CloseMainWindow(); } catch { } }
        WaitExit(procs, 3000, ct);
        foreach (var p in procs)
        {
            try { if (!p.HasExited) { SetupLog.Write("Arrêt forcé de SelectoAI.exe (pid " + p.Id + ")."); p.Kill(); } } catch { }
        }
        WaitExit(procs, 5000, ct);
        for (var i = 0; i < 25 && IsAppRunning(); i++) Thread.Sleep(200);
        foreach (var p in procs) p.Dispose();
        return (true, exe);
    }

    private static void WaitExit(Process[] procs, int ms, CancellationToken ct)
    {
        var end = Environment.TickCount64 + ms;
        while (Environment.TickCount64 < end)
        {
            ct.ThrowIfCancellationRequested();
            var alive = false;
            foreach (var p in procs) { try { if (!p.HasExited) alive = true; } catch { } }
            if (!alive) return;
            Thread.Sleep(150);
        }
    }

    public static void Launch(string exePath)
    {
        try
        {
            Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exePath) ?? "" });
        }
        catch (Exception ex) { SetupLog.Write("Lancement de Selecto AI impossible : " + ex.Message); }
    }

    /// <summary>Termine uniquement les processus lancés depuis <paramref name="dir"/> (le moteur Ollama privé), jamais un Ollama installé ailleurs.</summary>
    public static void KillProcessesUnder(string dir)
    {
        Process[] all;
        try { all = Process.GetProcesses(); } catch { return; }
        var self = Environment.ProcessId;
        foreach (var p in all)
        {
            try
            {
                if (p.Id == self) continue;
                var path = p.MainModule?.FileName;
                if (InstallPaths.IsUnderDir(path, dir)) { SetupLog.Write("Arrêt de " + path); p.Kill(); p.WaitForExit(3000); }
            }
            catch { }
            finally { p.Dispose(); }
        }
        Thread.Sleep(500);
    }

    // ---------- raccourcis ----------

    public static void CreateShortcut(Shortcut s)
    {
        var dir = Path.GetDirectoryName(s.LinkPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell est indisponible.");
        dynamic shell = Activator.CreateInstance(type)!;
        try
        {
            dynamic link = shell.CreateShortcut(s.LinkPath);
            link.TargetPath = s.Target;
            if (!string.IsNullOrEmpty(s.Arguments)) link.Arguments = s.Arguments;
            link.WorkingDirectory = Path.GetDirectoryName(s.Target) ?? "";
            link.IconLocation = s.IconPath;
            link.Description = s.Description;
            link.Save();
        }
        finally
        {
            try { System.Runtime.InteropServices.Marshal.FinalReleaseComObject((object)shell); } catch { }
        }
    }

    // ---------- fichiers ----------

    public static long DirSize(string dir)
    {
        long total = 0;
        try
        {
            if (!Directory.Exists(dir)) return 0;
            foreach (var entry in new DirectoryInfo(dir).EnumerateFileSystemInfos())
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                if (entry is DirectoryInfo d) total += DirSize(d.FullName);
                else if (entry is FileInfo f) total += f.Length;
            }
        }
        catch { }
        return total;
    }

    public static void DeleteFileRobust(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (!File.Exists(path)) return;
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                return;
            }
            catch when (attempt < 3) { Thread.Sleep(400); }
        }
    }

    /// <summary>Supprime un dossier sans suivre les points de réparation (jonctions, liens) ; renvoie les fichiers non supprimés.</summary>
    public static List<string> DeleteTree(string dir, Action<string>? onFile = null)
    {
        var failed = new List<string>();
        if (!Directory.Exists(dir)) return failed;
        DeleteTreeInner(new DirectoryInfo(dir), failed, onFile);
        return failed;
    }

    private static void DeleteTreeInner(DirectoryInfo di, List<string> failed, Action<string>? onFile)
    {
        try
        {
            if ((di.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                di.Attributes = FileAttributes.Normal;
                di.Delete(false); // supprime le lien, pas sa cible
                return;
            }
            foreach (var entry in di.EnumerateFileSystemInfos().ToList())
            {
                if (entry is DirectoryInfo sub) DeleteTreeInner(sub, failed, onFile);
                else
                {
                    try { DeleteFileRobust(entry.FullName); onFile?.Invoke(entry.FullName); }
                    catch (Exception ex) { failed.Add(entry.FullName); SetupLog.Write("Non supprimé : " + entry.FullName + " (" + ex.Message + ")"); }
                }
            }
            try { di.Attributes = FileAttributes.Normal; di.Delete(false); } catch { }
        }
        catch (Exception ex) { failed.Add(di.FullName); SetupLog.Write("Dossier non supprimé : " + di.FullName + " (" + ex.Message + ")"); }
    }

    /// <summary>
    /// Nettoyage final d'une désinstallation sur place (sans copie temporaire ni script cmd). Si le dossier est vide, il est supprimé.
    /// Sinon, les fichiers verrouillés (Uninstall.exe lui-même, en cours d'exécution) sont supprimés au prochain redémarrage,
    /// puis le dossier. Renvoie les chemins qui n'ont pas pu être programmés (à consigner dans le journal).
    /// </summary>
    public static List<string> FinishInstallDir(string dir, IEnumerable<string> locked)
    {
        var left = new List<string>();
        var files = locked.ToList();
        if (files.Count == 0)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, false); }
            catch (Exception ex) { SetupLog.Write("Dossier non supprimé : " + dir + " (" + ex.Message + ")"); left.Add(dir); }
            return left;
        }
        // Les fichiers sont programmés avant le dossier : au redémarrage, le dossier est donc vide quand on le supprime.
        foreach (var f in files)
        {
            if (!ScheduleDelete(f)) left.Add(f);
        }
        if (left.Count == 0 && !ScheduleDelete(dir)) left.Add(dir);
        if (left.Count == 0) SetupLog.Write(files.Count + " fichier(s) verrouillé(s) seront supprimés au prochain redémarrage.");
        return left;
    }

    private const int MoveFileDelayUntilReboot = 0x4;

    private static bool ScheduleDelete(string path)
    {
        if (MoveFileEx(path, null, MoveFileDelayUntilReboot)) return true;
        SetupLog.Write("Suppression au redémarrage impossible : " + path + " (erreur " + Marshal.GetLastWin32Error() + ")");
        return false;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool MoveFileEx(string lpExistingFileName, string? lpNewFileName, int dwFlags);
}
