namespace SelectBar.Settings;

/// <summary>
/// Jetons de couleur Windows 11 Fluent (hex), clair et sombre, partagés par Theme, la barre, le popup et les tests de contraste.
/// L'accent #0067C0 (clair) / #60CDFF (sombre) est la valeur de repli : à l'exécution, la couleur d'accent du système la remplace
/// (voir FluentTheme) quand elle reste lisible.
/// </summary>
public static class ThemePalette
{
    // ---- thème clair (les noms historiques sont conservés : tests et Theme y font référence)
    public const string Accent = "#0067C0";        // sections, interrupteurs, bouton principal
    public const string AccentHover = "#005AA3";   // survol du bouton principal (texte blanc dessus)
    public const string Window = "#F3F3F3";        // fond de repli de la fenêtre (Mica indisponible)
    public const string Card = "#FFFFFF";          // carte de réglage
    public const string Text = "#1A1A1A";          // libellés
    public const string SubText = "#5F5F5F";       // descriptions, valeurs
    public const string PivotOff = "#6B6B6B";      // éléments de navigation inactifs
    public const string Separator = "#E5E5E5";     // filet (décor, hors texte)
    public const string ButtonBorder = "#D1D1D1";  // bordure de contrôle (décor)
    public const string StatusOnHex = "#0F7B0F";   // « Actif »
    public const string WarnHex = "#9D5D00";       // « En pause »
    public const string ErrorHex = "#C42B1C";      // message d'erreur (clé refusée…)

    // ---- thème sombre
    public const string AccentDark = "#60CDFF";
    public const string AccentHoverDark = "#7ED7FF";
    public const string WindowDark = "#202020";
    public const string CardDark = "#2B2B2B";
    public const string TextDark = "#FFFFFF";
    public const string SubTextDark = "#C5C5C5";
    public const string PivotOffDark = "#C5C5C5";
    public const string SeparatorDark = "#3A3A3A";
    public const string ButtonBorderDark = "#505050";
    public const string StatusOnHexDark = "#6CCB5F";
    public const string WarnHexDark = "#FFB454";
    public const string ErrorHexDark = "#FF99A4";

    // ---- barre flottante, popup, toast : acrylique sombre ou claire selon le fond (voir BarVisuals)
    public const string BarBack = "#2B2B2B";        // pilule sombre (texte blanc)
    public const string BarBackLight = "#F9F9F9";   // pilule claire (texte sombre)
    public const string BarTextDark = "#FFFFFF";    // texte sur la pilule sombre
    public const string BarTextLight = "#1A1A1A";   // texte sur la pilule claire
    public const double BarAlpha = 0.92;
}
