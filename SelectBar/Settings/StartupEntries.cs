namespace SelectBar.Settings;

/// <summary>Accès à la clé Run de l'utilisateur (HKCU\...\Run) ; remplaçable dans les tests.</summary>
public interface IRunKey
{
    string? Get(string name);
    void Set(string name, string value);
    void Delete(string name);
}

/// <summary>Entrée de démarrage Windows : « Selecto AI » ; l'ancienne entrée « SelectBar » est toujours supprimée.</summary>
public static class StartupEntries
{
    public const string Name = "Selecto AI";
    public const string LegacyName = "SelectBar";

    public static void Apply(IRunKey key, bool enabled, string? exePath)
    {
        if (key.Get(LegacyName) != null) key.Delete(LegacyName);
        if (enabled && !string.IsNullOrEmpty(exePath)) key.Set(Name, "\"" + exePath + "\"");
        else if (key.Get(Name) != null) key.Delete(Name);
    }

    public static bool IsEnabled(IRunKey key) => key.Get(Name) != null;
}
