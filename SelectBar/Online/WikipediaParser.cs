using System;
using System.Text.Json;

namespace SelectBar.Online;

public sealed record WikipediaSummary(string Title, string Extract, string? Description, bool IsDisambiguation);

public static class WikipediaParser
{
    /// <summary>Résumé REST d'une page ; null si la page n'existe pas (réponse 404 sans extrait).</summary>
    public static WikipediaSummary? Parse(string json, int maxChars = 600)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return null;
            var title = Str(r, "title");
            var extract = Str(r, "extract");
            if (title == null || extract == null) return null;
            var disambiguation = Str(r, "type") == "disambiguation";
            return new WikipediaSummary(title, Shorten(extract, maxChars), Str(r, "description"), disambiguation);
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Coupe à la dernière fin de phrase avant la limite, sinon au dernier mot.</summary>
    public static string Shorten(string s, int max)
    {
        s = s.Trim();
        if (s.Length <= max) return s;
        var window = s.Substring(0, max);
        var end = Math.Max(window.LastIndexOf(". ", StringComparison.Ordinal), window.LastIndexOf(".\n", StringComparison.Ordinal));
        if (end > max / 3) return window.Substring(0, end + 1);
        var sp = window.LastIndexOf(' ');
        return (sp > 0 ? window.Substring(0, sp) : window).TrimEnd() + "…";
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString() : null;
}
