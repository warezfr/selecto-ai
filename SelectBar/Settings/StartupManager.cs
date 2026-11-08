using System;
using Microsoft.Win32;

namespace SelectBar.Settings;

/// <summary>Démarrage avec Windows via HKCU\...\Run (aucun droit admin requis). La logique (entrée « Selecto AI », suppression de l'ancienne) est dans StartupEntries.</summary>
internal static class StartupManager
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private sealed class RegistryRunKey : IRunKey
    {
        public string? Get(string name)
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue(name) as string;
        }

        public void Set(string name, string value)
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
            key?.SetValue(name, value);
        }

        public void Delete(string name)
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
        }
    }

    public static void Set(bool enabled)
    {
#if EDITION_STORE
        // MSIX : la clé Run est virtualisée ; tâche de démarrage du manifeste (StoreStartup)
        try { StoreStartup.Set(enabled); }
        catch (Exception ex) { Log.Write("Démarrage auto : " + ex.Message); }
        return;
#else
        try { StartupEntries.Apply(new RegistryRunKey(), enabled, Environment.ProcessPath); }
        catch (Exception ex) { Log.Write("Démarrage auto : " + ex.Message); }
#endif
    }

    public static bool IsEnabled()
    {
#if EDITION_STORE
        try { return StoreStartup.IsEnabled(); }
        catch { return false; }
#else
        try { return StartupEntries.IsEnabled(new RegistryRunKey()); }
        catch { return false; }
#endif
    }
}
