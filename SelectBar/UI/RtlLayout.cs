using System.Globalization;

namespace SelectBar.UI;

/// <summary>Sens de lecture de l'interface (PUR, sans WPF) : positions en miroir et glyphes directionnels.</summary>
public static class RtlLayout
{
    /// <summary>Segoe Fluent « Retour » (flèche gauche). Inversé en « Avancer » (E72A) en droite-à-gauche.</summary>
    public const string Back = "";
    /// <summary>Segoe Fluent « Avancer » (flèche droite).</summary>
    public const string Forward = "";
    /// <summary>Segoe Fluent chevron gauche. Inversé en chevron droit (E76C).</summary>
    public const string ChevronLeft = "";
    /// <summary>Segoe Fluent chevron droit.</summary>
    public const string ChevronRight = "";
    /// <summary>Segoe Fluent flèche gauche (variante « Précédent »). Inversée en E974.</summary>
    public const string PreviousArrow = "";
    /// <summary>Segoe Fluent flèche droite (variante « Suivant »).</summary>
    public const string NextArrow = "";
    // Les chevrons haut/bas (E70D ChevronDown, E70E ChevronUp) ne sont pas directionnels au sens de la lecture : ils ne sont pas inversés.

    /// <summary>Paires de glyphes directionnels inversés en droite-à-gauche (sens dans les deux sens).</summary>
    private static readonly (string Ltr, string Rtl)[] Mirrored =
    {
        (Back, Forward),
        (ChevronLeft, ChevronRight),
        (PreviousArrow, NextArrow),
    };

    /// <summary>Vrai si la culture s'écrit de droite à gauche (arabe, hébreu, persan…).</summary>
    public static bool IsRtl(CultureInfo c) => c.TextInfo.IsRightToLeft;

    /// <summary>
    /// Abscisse d'un élément de largeur <paramref name="width"/> posé à <paramref name="x"/> (distance depuis le bord gauche
    /// du conteneur de largeur <paramref name="containerWidth"/>) : miroir par rapport au bord droit en droite-à-gauche.
    /// </summary>
    public static double MirrorX(double x, double width, double containerWidth, bool rtl) =>
        rtl ? containerWidth - x - width : x;

    /// <summary>Glyphe à afficher : son inverse horizontal en droite-à-gauche si c'est une flèche/chevron listé, sinon inchangé.</summary>
    public static string Glyph(string ltrGlyph, bool rtl)
    {
        if (!rtl) return ltrGlyph;
        foreach (var (ltr, mirror) in Mirrored)
        {
            if (ltrGlyph == ltr) return mirror;
            if (ltrGlyph == mirror) return ltr;
        }
        return ltrGlyph;
    }
}
