namespace SelectBar.Packaging;

/// <summary>Emballage de l'application : « Site » (installateur Inno Setup, ZIP portable, mise à jour par latest.json) ou « Store » (MSIX, mise à jour par le Microsoft Store). Aucune différence de fonctions.</summary>
public enum AppEdition { Site, Store }

/// <summary>Édition compilée (propriété MSBuild Edition -> constante EDITION_STORE pour le Store) et constantes du produit.</summary>
public static class EditionInfo
{
    public const string ProductName = "Selecto AI";

    /// <summary>Dépôt public du code (licence MIT).</summary>
    public const string GitHubUrl = "https://github.com/warezfr/selecto-ai";

    /// <summary>Identifiant du produit dans le Microsoft Store (page de l'application).</summary>
    public const string StoreProductId = "9P6W748ZHCWR";

#if EDITION_STORE
    public const AppEdition Current = AppEdition.Store;
#else
    public const AppEdition Current = AppEdition.Site;
#endif

    /// <summary>Titre des fenêtres : le nom du produit, sans mention d'édition.</summary>
    public static string WindowTitle() => ProductName;

    /// <summary>Emballage affiché dans À propos.</summary>
    public static string PackagingLabel(AppEdition edition) => edition == AppEdition.Store ? "Microsoft Store" : "";
}
