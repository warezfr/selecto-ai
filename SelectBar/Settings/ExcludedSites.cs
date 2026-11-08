using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SelectBar.Settings;

/// <summary>
/// Domaines où la barre est désactivée. Appliqué au mieux : seulement si le navigateur actif expose
/// son URL (barre d'adresse via UI Automation) ou, à défaut, un titre de fenêtre contenant le domaine.
/// </summary>
public sealed class ExcludedSites
{
    private static readonly Regex DomainRx = new(
        @"^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?(?:\.[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?)*$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    private readonly HashSet<string> _set = new(StringComparer.OrdinalIgnoreCase);

    public ExcludedSites(IEnumerable<string>? items = null)
    {
        if (items != null) foreach (var i in items) Add(i);
    }

    public int Count => _set.Count;
    public IReadOnlyList<string> Items => _set.OrderBy(x => x, StringComparer.Ordinal).ToList();

    /// <summary>« https://www.Exemple.com:8080/x?y », « *.exemple.com » ou « exemple.com » -> « exemple.com » ; null si invalide.</summary>
    public static string? NormalizeDomain(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var s = input.Trim().ToLowerInvariant();
        var scheme = s.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0) s = s.Substring(scheme + 3);
        var cut = s.IndexOfAny(new[] { '/', '?', '#' });
        if (cut >= 0) s = s.Substring(0, cut);
        var at = s.LastIndexOf('@');
        if (at >= 0) s = s.Substring(at + 1);
        var colon = s.IndexOf(':');
        if (colon >= 0) s = s.Substring(0, colon);
        if (s.StartsWith("*.", StringComparison.Ordinal)) s = s.Substring(2);
        if (s.StartsWith("www.", StringComparison.Ordinal)) s = s.Substring(4);
        s = s.Trim('.');
        if (s.Length == 0 || s.Length > 253) return null;
        if (!DomainRx.IsMatch(s)) return null;
        return s.Contains('.') || s == "localhost" ? s : null;
    }

    public bool Add(string input)
    {
        var d = NormalizeDomain(input);
        return d != null && _set.Add(d);
    }

    public bool Remove(string input)
    {
        var d = NormalizeDomain(input);
        return d != null && _set.Remove(d);
    }

    /// <summary>Hôte d'une URL ou d'un texte de barre d'adresse (« exemple.com/page » sans schéma accepté).</summary>
    public static string? HostFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var u = url.Trim();
        if (u.Any(char.IsWhiteSpace)) return null;
        if (!u.Contains("://", StringComparison.Ordinal)) u = "https://" + u;
        return Uri.TryCreate(u, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
            ? uri.Host.ToLowerInvariant() : null;
    }

    public static bool HostMatches(string host, string domain) =>
        host == domain || host.EndsWith("." + domain, StringComparison.Ordinal);

    /// <summary>URL connue -> comparaison sur l'hôte (sous-domaines inclus). Sinon repli : le titre cite le domaine.</summary>
    public bool IsExcluded(string? url, string? windowTitle)
    {
        if (_set.Count == 0) return false;
        var host = HostFromUrl(url);
        if (host != null) return _set.Any(d => HostMatches(host, d));
        if (string.IsNullOrWhiteSpace(windowTitle)) return false;
        var title = windowTitle.ToLowerInvariant();
        return _set.Any(d => TitleMentions(title, d));
    }

    private static bool TitleMentions(string title, string domain)
    {
        var from = 0;
        while (true)
        {
            var i = title.IndexOf(domain, from, StringComparison.Ordinal);
            if (i < 0) return false;
            var before = i == 0 || !char.IsLetterOrDigit(title[i - 1]);
            var end = i + domain.Length;
            var after = end >= title.Length || !char.IsLetterOrDigit(title[end]);
            if (before && after) return true;
            from = i + 1;
        }
    }
}
