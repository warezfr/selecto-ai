using System;
using System.Collections.Generic;

namespace SelectBar.Setup.Logic;

/// <summary>Comparaison de versions identique à celle de l'installateur Inno (4 champs numériques, le reste vaut 0).</summary>
public static class VersionLogic
{
    public static int Compare(string? a, string? b)
    {
        var x = Parts(a);
        var y = Parts(b);
        for (var i = 0; i < 4; i++)
        {
            if (x[i] < y[i]) return -1;
            if (x[i] > y[i]) return 1;
        }
        return 0;
    }

    private static int[] Parts(string? v)
    {
        var result = new int[4];
        if (string.IsNullOrWhiteSpace(v)) return result;
        var core = v.Trim();
        var cut = core.IndexOfAny(new[] { '-', '+', ' ' });
        if (cut >= 0) core = core.Substring(0, cut);
        var pieces = core.Split('.');
        for (var i = 0; i < 4 && i < pieces.Length; i++)
            result[i] = int.TryParse(pieces[i], out var n) && n >= 0 ? n : 0;
        return result;
    }

    /// <summary>Refus de rétrograder : la version déjà installée est plus récente, sauf /FORCE.</summary>
    public static bool IsDowngrade(string? newVersion, string? installedVersion, bool force)
        => !force && !string.IsNullOrWhiteSpace(installedVersion) && Compare(newVersion, installedVersion) < 0;

    /// <summary>La plus récente des versions trouvées (entrée du Setup, entrée Inno...), ou null.</summary>
    public static string? Newest(IEnumerable<string?> versions)
    {
        string? best = null;
        foreach (var v in versions)
        {
            if (string.IsNullOrWhiteSpace(v)) continue;
            if (best == null || Compare(v, best) > 0) best = v;
        }
        return best;
    }
}
