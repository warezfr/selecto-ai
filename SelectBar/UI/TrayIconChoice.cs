namespace SelectBar.UI;

/// <summary>Choix PUR de l'icône de la zone de notification (aucune dépendance Windows : testé sur Mac/Linux).</summary>
public static class TrayIconChoice
{
    public const string AppIconFile = "SelectoAI.ico";
    public const string LightFile = "tray-light.ico";
    public const string DarkFile = "tray-dark.ico";
    public const string LightPausedFile = "tray-light-paused.ico";
    public const string DarkPausedFile = "tray-dark-paused.ico";

    /// <summary>Les cinq fichiers .ico fournis dans Assets\ (déclarés dans SelectBar.csproj).</summary>
    public static readonly string[] AllFiles = { AppIconFile, LightFile, DarkFile, LightPausedFile, DarkPausedFile };

    /// <summary>
    /// Nom du fichier d'icône. systemUsesLightTheme = valeur de HKCU\...\Themes\Personalize\SystemUsesLightTheme
    /// (1 = barre claire → glyphe noir ; 0 ou autre = sombre → glyphe blanc ; absente = sombre).
    /// paused = barre désactivée ou licence qui n'autorise plus la barre (glyphe à 45 %).
    /// </summary>
    public static string FileName(int? systemUsesLightTheme, bool paused)
    {
        var light = systemUsesLightTheme == 1;
        return (light, paused) switch
        {
            (true, false) => LightFile,
            (true, true) => LightPausedFile,
            (false, false) => DarkFile,
            (false, true) => DarkPausedFile
        };
    }

    /// <summary>URI « pack » de la ressource WPF (Assets\ est embarqué comme Resource).</summary>
    public static string PackUri(string fileName) => "pack://application:,,,/Assets/" + fileName;
}
