using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SelectBar.Settings;

/// <summary>Lecture/écriture de settings.json et excluded-apps.json et excluded-sites.json dans %APPDATA%\SelectoAI.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    /// <summary>Mode capture : rien n'est écrit sur le disque (les réglages enregistrés restent intacts).</summary>
    public bool ReadOnly { get; init; }

    public string Directory { get; }
    public string SettingsPath => Path.Combine(Directory, "settings.json");
    public string ExcludedPath => Path.Combine(Directory, "excluded-apps.json");
    public string ExcludedSitesPath => Path.Combine(Directory, "excluded-sites.json");
    public string ExtensionsDirectory => Path.Combine(Directory, "Extensions");

    public SettingsStore(string? directory = null)
    {
        Directory = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppPaths.FolderName);
    }

    public AppSettings Load()
    {
        var existed = File.Exists(SettingsPath);
        AppSettings loaded;
        try
        {
            loaded = existed
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), Json) ?? Defaults()
                : new AppSettings();
        }
        catch { loaded = Defaults(); /* fichier corrompu : réglages par défaut, sans migration */ }
        var (settings, justEnabled) = SettingsMigration.Upgrade(loaded, existed);
        settings.OnlineActionsJustEnabled = justEnabled;
        return settings;
    }

    // réglages par défaut au schéma courant : un fichier illisible n'est pas « migré »
    private static AppSettings Defaults() => new() { SettingsSchema = AppSettings.CurrentSchema, HotkeyEnabled = true };

    /// <summary>Verrou de sortie : vrai après une sortie confirmée. Plus rien n'écrit settings.json, pour qu'une copie périmée
    /// (fenêtre Réglages ouverte, vérification de mise à jour, essai) ne réécrive pas l'ancien réglage après le choix de l'utilisateur.</summary>
    public bool Locked { get; private set; }

    public void Lock() => Locked = true;

    public void Save(AppSettings s)
    {
        if (ReadOnly || Locked) return;
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s, Json));
    }

    public ExcludedApps LoadExcluded()
    {
        try
        {
            if (File.Exists(ExcludedPath))
                return new ExcludedApps(JsonSerializer.Deserialize<List<string>>(File.ReadAllText(ExcludedPath), Json));
        }
        catch { }
        return new ExcludedApps(ExcludedApps.Defaults);
    }

    public void SaveExcluded(ExcludedApps apps)
    {
        if (ReadOnly) return;
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(ExcludedPath, JsonSerializer.Serialize(apps.Items, Json));
    }

    public ExcludedSites LoadExcludedSites()
    {
        try
        {
            if (File.Exists(ExcludedSitesPath))
                return new ExcludedSites(JsonSerializer.Deserialize<List<string>>(File.ReadAllText(ExcludedSitesPath), Json));
        }
        catch { }
        return new ExcludedSites();
    }

    public void SaveExcludedSites(ExcludedSites sites)
    {
        if (ReadOnly) return;
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(ExcludedSitesPath, JsonSerializer.Serialize(sites.Items, Json));
    }
}
