using System;

namespace SelectBar.Settings;

/// <summary>Contraste WCAG (logique pure) pour vérifier la lisibilité de la barre.</summary>
public static class ColorContrast
{
    private static double Lin(double c8)
    {
        var c = c8 / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    public static double Luminance(double r, double g, double b) => 0.2126 * Lin(r) + 0.7152 * Lin(g) + 0.0722 * Lin(b);

    public static double Ratio(double l1, double l2) => (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);

    /// <summary>Couleur obtenue en posant (r,g,b) à l'opacité alpha sur un fond uni.</summary>
    public static (double R, double G, double B) Over(double r, double g, double b, double alpha, double bgR, double bgG, double bgB) =>
        (r * alpha + bgR * (1 - alpha), g * alpha + bgG * (1 - alpha), b * alpha + bgB * (1 - alpha));

    /// <summary>Analyse « #RRGGBB » (ou « #AARRGGBB », l'alpha est ignoré) ; false si le texte n'est pas une couleur.</summary>
    public static bool TryParseHex(string? hex, out (double R, double G, double B) rgb)
    {
        rgb = default;
        if (string.IsNullOrEmpty(hex) || hex[0] != '#' || (hex.Length != 7 && hex.Length != 9)) return false;
        var o = hex.Length - 6;
        if (!int.TryParse(hex.AsSpan(o, 2), System.Globalization.NumberStyles.HexNumber, null, out var r)
            || !int.TryParse(hex.AsSpan(o + 2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)
            || !int.TryParse(hex.AsSpan(o + 4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b)) return false;
        rgb = (r, g, b);
        return true;
    }

    /// <summary>Contraste WCAG entre deux couleurs hexadécimales ; 0 si l'une est illisible.</summary>
    public static double RatioHex(string fg, string bg) =>
        TryParseHex(fg, out var f) && TryParseHex(bg, out var b)
            ? Ratio(Luminance(f.R, f.G, f.B), Luminance(b.R, b.G, b.B))
            : 0;

    /// <summary>
    /// Couleur d'accent à employer : celle du système si elle reste lisible (contraste minimal) sur le fond, sinon la valeur de repli
    /// de la palette Fluent. Suit ainsi la couleur d'accent de Windows sans jamais produire un texte illisible.
    /// </summary>
    public static string PickAccent(string? systemHex, string backgroundHex, string fallbackHex, double minRatio = 4.5) =>
        TryParseHex(systemHex, out _) && RatioHex(systemHex!, backgroundHex) >= minRatio
            ? "#" + systemHex!.Substring(systemHex.Length - 6).ToUpperInvariant()
            : fallbackHex;
}
