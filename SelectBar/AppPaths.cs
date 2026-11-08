namespace SelectBar;

/// <summary>Noms des dossiers de données. « SelectBar » est l'ancien nom du produit : il n'est plus lu que pour la migration.</summary>
public static class AppPaths
{
    /// <summary>Dossier sous %APPDATA% et %LOCALAPPDATA%.</summary>
    public const string FolderName = "SelectoAI";

    /// <summary>Ancien dossier (produit nommé « SelectBar »), conservé intact après migration.</summary>
    public const string LegacyFolderName = "SelectBar";
}
