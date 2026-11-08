using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SelectBar.Localization;

namespace SelectBar.Online;

public sealed record DictionarySense(string Label, IReadOnlyList<string> Definitions);
public sealed record DictionaryEntry(string Word, string? Phonetic, IReadOnlyList<DictionarySense> Senses);

public static class DictionaryFormat
{
    public static string Body(DictionaryEntry e)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(e.Phonetic)) sb.Append(e.Phonetic).Append('\n');
        foreach (var s in e.Senses)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(s.Label).Append('\n');
            for (var i = 0; i < s.Definitions.Count; i++)
                sb.Append(i + 1).Append(". ").Append(s.Definitions[i]).Append('\n');
        }
        return sb.ToString().TrimEnd();
    }

    public static string Shorten(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max - 1).TrimEnd() + "…";
}

/// <summary>
/// Extrait du Wiktionnaire (page entière en texte brut, titres sans marque) : on garde le premier bloc de la langue demandée,
/// sa première nature grammaticale, la prononciation et au plus 5 définitions (les exemples cités sont écartés).
/// Les blocs sont séparés par deux lignes vides ; la première ligne d'un bloc est son titre.
/// </summary>
public static class WiktionaryParser
{
    private static readonly Regex Nature = new(
        @"^(Nom( commun| propre)?|Verbe|Adjectif|Adverbe|Pronom|Préposition|Conjonction|Interjection|Article|Déterminant|Locution|Forme|Particule|Onomatopée|Préfixe|Suffixe|Symbole|Lettre|Numéral|Proverbe|Abréviation|Sigle|Acronyme|Postposition|Infixe|Interfixe|Classificateur|Contraction)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));
    private static readonly Regex Pron = new(@"\\([^\\\n]{1,80})\\", RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));
    private static readonly Regex TrailingNumber = new(@"\s+\d+$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));
    private static readonly Regex Paren = new(@"\s*\([^)]*\)", RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));
    private static readonly Regex Blocks = new(@"\n{3,}", RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));

    /// <summary>Renvoie null si la page n'existe pas ou n'a pas de section dans cette langue.</summary>
    public static DictionaryEntry? Parse(string json, string languageHeading = "Français", int maxDefinitions = 5)
    {
        string? title = null, extract = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("query", out var q) || !q.TryGetProperty("pages", out var pages)) return null;
            foreach (var p in pages.EnumerateObject())
            {
                if (p.Value.TryGetProperty("missing", out _)) return null;
                if (p.Value.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String) title = t.GetString();
                if (p.Value.TryGetProperty("extract", out var x) && x.ValueKind == JsonValueKind.String) extract = x.GetString();
                break;
            }
        }
        catch (JsonException) { return null; }
        if (string.IsNullOrWhiteSpace(extract) || string.IsNullOrWhiteSpace(title)) return null;

        var blocks = Blocks.Split(extract.Replace("\r\n", "\n").Trim('\n'));
        var start = -1;
        for (var i = 0; i < blocks.Length; i++)
            if (string.Equals(blocks[i].Trim(), languageHeading, StringComparison.OrdinalIgnoreCase)) { start = i; break; }
        if (start < 0) return null;

        for (var j = start + 1; j < blocks.Length; j++)
        {
            var lines = blocks[j].Split('\n');
            var heading = lines[0].Trim();
            var body = lines.Skip(1).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            if (!Nature.IsMatch(heading))
            {
                if (body.Count == 0) break; // titre d'une autre langue : on s'arrête
                continue;
            }
            return Build(title!, heading, body, maxDefinitions);
        }
        return null;
    }

    private static DictionaryEntry? Build(string word, string heading, List<string> body, int max)
    {
        string? phonetic = null, gender = null;
        var skip = 0;
        if (body.Count > 0 && (body[0].Contains('\\') || body[0].StartsWith(word, StringComparison.OrdinalIgnoreCase)))
        {
            var head = body[0];
            skip = 1;
            var m = Pron.Match(head);
            if (m.Success) phonetic = "/" + m.Groups[1].Value.Trim() + "/";
            var cut = head.LastIndexOf('\\');
            if (cut >= 0)
            {
                var after = Paren.Replace(head.Substring(cut + 1), "").Trim();
                if (after.Length is > 0 and <= 40) gender = after;
            }
        }
        var defs = new List<string>();
        foreach (var line in body.Skip(skip))
        {
            if (line.Contains(" — (") || line.StartsWith("Note", StringComparison.OrdinalIgnoreCase)
                || line.Contains("exemple manque") || line.EndsWith("(Ajouter)")) continue;
            defs.Add(DictionaryFormat.Shorten(line, 260));
            if (defs.Count >= max) break;
        }
        if (defs.Count == 0) return null;
        var label = TrailingNumber.Replace(heading, "") + (gender != null ? " · " + gender : "");
        return new DictionaryEntry(word, phonetic, new[] { new DictionarySense(label, defs) });
    }
}

/// <summary>Réponse de dictionaryapi.dev : phonétique et 3 définitions par nature (3 natures au plus).</summary>
public static class DictionaryApiParser
{
    private static readonly Dictionary<string, string> Pos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["noun"] = "Online_Pos_noun", ["verb"] = "Online_Pos_verb", ["adjective"] = "Online_Pos_adjective", ["adverb"] = "Online_Pos_adverb",
        ["pronoun"] = "Online_Pos_pronoun", ["preposition"] = "Online_Pos_preposition", ["conjunction"] = "Online_Pos_conjunction", ["interjection"] = "Online_Pos_interjection"
    };

    public static DictionaryEntry? Parse(string json, int perSense = 3, int maxSenses = 3)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0) return null;
            string? word = null, phonetic = null;
            var senses = new List<DictionarySense>();
            foreach (var entry in root.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object) continue;
                word ??= Str(entry, "word");
                phonetic ??= Str(entry, "phonetic");
                if (phonetic == null && entry.TryGetProperty("phonetics", out var ph) && ph.ValueKind == JsonValueKind.Array)
                    foreach (var p in ph.EnumerateArray()) { phonetic = Str(p, "text"); if (phonetic != null) break; }
                if (!entry.TryGetProperty("meanings", out var ms) || ms.ValueKind != JsonValueKind.Array) continue;
                foreach (var m in ms.EnumerateArray())
                {
                    if (senses.Count >= maxSenses) break;
                    var defs = new List<string>();
                    if (m.TryGetProperty("definitions", out var ds) && ds.ValueKind == JsonValueKind.Array)
                        foreach (var d in ds.EnumerateArray())
                        {
                            var t = Str(d, "definition");
                            if (t != null) defs.Add(DictionaryFormat.Shorten(t, 260));
                            if (defs.Count >= perSense) break;
                        }
                    if (defs.Count == 0) continue;
                    var pos = Str(m, "partOfSpeech") ?? "";
                    senses.Add(new DictionarySense(Pos.TryGetValue(pos, out var key) ? L.Get(key) : pos, defs));
                }
            }
            return word == null || senses.Count == 0 ? null : new DictionaryEntry(word, phonetic, senses);
        }
        catch (JsonException) { return null; }
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString() : null;
}
