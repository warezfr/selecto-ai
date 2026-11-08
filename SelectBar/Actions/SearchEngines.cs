using System;
using System.Collections.Generic;
using System.Linq;

namespace SelectBar.Actions;

public sealed record SearchEngine(string Id, string Name, string UrlTemplate);

public static class SearchEngines
{
    public const string Placeholder = "***";
    public const string DefaultId = "google";
    public const string CustomId = "custom";
    /// <summary>Longueur maximale de la requête : une page entière sélectionnée donnerait une adresse que les navigateurs et LinkSafety refusent.</summary>
    public const int MaxQueryChars = 400;

    public static readonly IReadOnlyList<SearchEngine> All = new List<SearchEngine>
    {
        new("google", "Google", "https://www.google.com/search?q=***"),
        new("bing", "Bing", "https://www.bing.com/search?q=***"),
        new("duckduckgo", "DuckDuckGo", "https://duckduckgo.com/?q=***"),
        new("brave", "Brave", "https://search.brave.com/search?q=***"),
        new("ecosia", "Ecosia", "https://www.ecosia.org/search?q=***"),
        new("kagi", "Kagi", "https://kagi.com/search?q=***"),
        new("startpage", "Startpage", "https://www.startpage.com/do/search?q=***"),
        new("yahoo", "Yahoo", "https://search.yahoo.com/search?p=***"),
    };

    public static SearchEngine Find(string? id) =>
        All.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)) ?? All[0];

    /// <summary>Remplace chaque *** par le texte encodé. Moteur "custom" : modèle fourni, sinon repli sur Google.</summary>
    public static string BuildUrl(string? engineId, string text, string? customTemplate = null)
    {
        var template = string.Equals(engineId, CustomId, StringComparison.OrdinalIgnoreCase)
                       && customTemplate != null && customTemplate.Contains(Placeholder)
            ? customTemplate
            : Find(engineId).UrlTemplate;
        return template.Replace(Placeholder, Uri.EscapeDataString(Shorten(text.Trim())));
    }

    /// <summary>Texte raccourci à MaxQueryChars, coupé à la fin d'un mot si possible.</summary>
    public static string Shorten(string text)
    {
        if (text.Length <= MaxQueryChars) return text;
        var cut = text.Substring(0, MaxQueryChars);
        var space = cut.LastIndexOf(' ');
        return (space > MaxQueryChars / 2 ? cut.Substring(0, space) : cut).TrimEnd();
    }
}
