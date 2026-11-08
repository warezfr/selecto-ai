using System;

namespace SelectBar.Settings;

/// <summary>Taille, style et position de la barre : valeurs permises et calculs purs.</summary>
public static class Appearance
{
    public const int MinScale = 80, MaxScale = 140, DefaultScale = 100;
    public const string Auto = "auto", Light = "light", Dark = "dark";
    public const string Above = "above", Below = "below";

    public static int ClampScale(int percent) => Math.Max(MinScale, Math.Min(MaxScale, percent));

    public static string NormalizeStyle(string? s) =>
        string.Equals(s, Light, StringComparison.OrdinalIgnoreCase) ? Light :
        string.Equals(s, Dark, StringComparison.OrdinalIgnoreCase) ? Dark : Auto;

    public static string NormalizePosition(string? s) =>
        string.Equals(s, Below, StringComparison.OrdinalIgnoreCase) ? Below : Above;

    /// <summary>Automatique = suit le thème des applications de Windows.</summary>
    public static bool IsDark(string? style, bool systemAppsUseLightTheme) => NormalizeStyle(style) switch
    {
        Dark => true,
        Light => false,
        _ => !systemAppsUseLightTheme
    };

    /// <summary>
    /// Position (pixels écran) du coin haut-gauche de la barre : centrée sur le curseur, au-dessus ou en dessous,
    /// retournée de l'autre côté s'il n'y a pas la place, puis ramenée dans la zone de travail.
    /// </summary>
    public static (int X, int Y) PlaceBar(int cursorX, int cursorY, int width, int height, int gapAbove, int gapBelow,
        int left, int top, int right, int bottom, string? position, int minClearance = 0)
    {
        var below = NormalizePosition(position) == Below;
        var gA = Math.Max(gapAbove, minClearance);
        var gB = Math.Max(gapBelow, minClearance);
        var x = cursorX - width / 2;
        var y = below ? cursorY + gB : cursorY - height - gA;
        if (below && y + height > bottom) y = cursorY - height - gA;
        else if (!below && y < top) y = cursorY + gB;
        x = Math.Max(left, Math.Min(x, right - width));
        y = Math.Max(top, Math.Min(y, bottom - height));
        if (minClearance <= 0 || Clearance(x, y, width, height, cursorX, cursorY) >= minClearance) return (x, y);

        // Le pointeur est trop près (bord de l'écran) : on essaie l'autre côté, puis à droite, puis à gauche du pointeur.
        int ClampX(int v) => Math.Max(left, Math.Min(v, right - width));
        int ClampY(int v) => Math.Max(top, Math.Min(v, bottom - height));
        var candidates = new (int X, int Y)[]
        {
            (ClampX(cursorX - width / 2), ClampY(cursorY - height - gA)),
            (ClampX(cursorX - width / 2), ClampY(cursorY + gB)),
            (ClampX(cursorX + minClearance), ClampY(cursorY - height / 2)),
            (ClampX(cursorX - minClearance - width), ClampY(cursorY - height / 2)),
        };
        var best = (X: x, Y: y);
        var bestClear = Clearance(x, y, width, height, cursorX, cursorY);
        foreach (var c in candidates)
        {
            var clear = Clearance(c.X, c.Y, width, height, cursorX, cursorY);
            if (clear >= minClearance) return c;
            if (clear > bestClear) { best = c; bestClear = clear; }
        }
        return best;
    }

    /// <summary>
    /// Distance (pixels) entre le pointeur et le rectangle de la fenêtre, comptée sur l'axe le plus éloigné ; 0 si le pointeur est dedans.
    /// Une valeur ≥ 12 garantit que la barre ne recouvre jamais le pointeur avec 12 px de marge.
    /// </summary>
    public static int Clearance(int x, int y, int width, int height, int pointX, int pointY)
    {
        var dx = Math.Max(Math.Max(x - pointX, pointX - (x + width)), 0);
        var dy = Math.Max(Math.Max(y - pointY, pointY - (y + height)), 0);
        return Math.Max(dx, dy);
    }
}
