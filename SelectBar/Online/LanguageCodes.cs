using System;
using System.Collections.Generic;
using System.Linq;
using SelectBar.Localization;

namespace SelectBar.Online;

public static class LanguageCodes
{
    public static readonly IReadOnlyList<(string Code, string Name)> All = new[]
    {
        ("fr", "Français"), ("en", "Anglais"), ("es", "Espagnol"), ("de", "Allemand"),
        ("it", "Italien"), ("pt", "Portugais"), ("nl", "Néerlandais"), ("ar", "Arabe")
    };

    /// <summary>« fr-FR » -> « fr » ; valeur inconnue ou vide -> repli.</summary>
    public static string Normalize(string? code, string fallback = "fr")
    {
        if (string.IsNullOrWhiteSpace(code)) return fallback;
        var c = code.Trim().ToLowerInvariant();
        var cut = c.IndexOfAny(new[] { '-', '_' });
        if (cut > 0) c = c.Substring(0, cut);
        return c.Length is 2 or 3 && c.All(ch => ch is >= 'a' and <= 'z') ? c : fallback;
    }

    /// <summary>Nom affiché dans les menus, dans la langue de l'interface. NameOf reste en français : il entre dans le prompt.</summary>
    public static string DisplayName(string? code)
    {
        var c = Normalize(code, "");
        foreach (var (k, _) in All) if (k == c) return L.Get("Online_Lang_" + c);
        return c.Length == 0 ? "?" : c;
    }

    public static string NameOf(string? code)
    {
        var c = Normalize(code, "");
        foreach (var (k, n) in All) if (k == c) return n;
        return c.Length == 0 ? "?" : c;
    }
}
