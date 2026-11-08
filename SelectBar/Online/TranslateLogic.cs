using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SelectBar.Localization;

namespace SelectBar.Online;

public sealed record MyMemoryResult(string? Text, string? DetectedLanguage, string? Error, bool SameLanguage = false);

public static class MyMemoryParser
{
    public const string CannotProcess = "Le service de traduction ne peut pas traiter ce texte.";
    public static string QuotaMessage => L.Get("Online_TranslateQuota");

    public static MyMemoryResult Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var status = ReadInt(root, "responseStatus") ?? 200;
            string? text = null, detected = null;
            if (root.TryGetProperty("responseData", out var rd) && rd.ValueKind == JsonValueKind.Object)
            {
                if (rd.TryGetProperty("translatedText", out var t) && t.ValueKind == JsonValueKind.String) text = t.GetString();
                if (rd.TryGetProperty("detectedLanguage", out var d) && d.ValueKind == JsonValueKind.String)
                    detected = LanguageCodes.Normalize(d.GetString(), "");
            }
            if (string.IsNullOrEmpty(detected)) detected = null;
            var quota = root.TryGetProperty("quotaFinished", out var qf) && qf.ValueKind == JsonValueKind.True;
            if (quota || status == 429 || (text != null && text.StartsWith("MYMEMORY WARNING", StringComparison.OrdinalIgnoreCase)))
                return new MyMemoryResult(null, detected, QuotaMessage);
            var details = root.TryGetProperty("responseDetails", out var rdet) && rdet.ValueKind == JsonValueKind.String ? rdet.GetString() : null;
            if (status == 403 && ((text ?? "").Contains("DISTINCT LANGUAGES", StringComparison.OrdinalIgnoreCase)
                                  || (details ?? "").Contains("DISTINCT LANGUAGES", StringComparison.OrdinalIgnoreCase)))
                return new MyMemoryResult(null, detected, L.Get("Online_SameLanguage"), SameLanguage: true);
            if (status != 200)
                return new MyMemoryResult(null, detected, CannotProcess);
            if (string.IsNullOrWhiteSpace(text))
                return new MyMemoryResult(null, detected, L.Get("Online_TranslateEmpty"));
            return new MyMemoryResult(WebUtility.HtmlDecode(text), detected, null);
        }
        catch (JsonException)
        {
            return new MyMemoryResult(null, null, L.Get("Online_TranslateUnreadable"));
        }
    }

    private static int? ReadInt(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var s)) return s;
        return null;
    }
}

/// <summary>Découpe par phrases en morceaux de 500 caractères au plus (limite de MyMemory par requête).</summary>
public static class TranslateChunker
{
    public const int DefaultMax = 500;

    /// <summary>Les morceaux gardent leurs espaces et retours à la ligne finaux ; chacun fait au plus <paramref name="max"/> caractères une fois rognés.</summary>
    public static (IReadOnlyList<string> Chunks, bool Truncated) Split(string text, int max = DefaultMax, int maxChunks = 6)
    {
        var chunks = new List<string>();
        var truncated = false;
        var cur = new StringBuilder();
        foreach (var raw in Sentences((text ?? "").Replace("\r\n", "\n").Trim()))
        {
            var piece = raw;
            if (piece.TrimEnd().Length > max) { piece = CutAtWord(piece.TrimEnd(), max) + " "; truncated = true; }
            if (cur.Length > 0 && cur.Length + piece.Length > max) { chunks.Add(cur.ToString()); cur.Clear(); }
            cur.Append(piece);
        }
        if (cur.Length > 0) chunks.Add(cur.ToString());
        if (chunks.Count > maxChunks) { chunks.RemoveRange(maxChunks, chunks.Count - maxChunks); truncated = true; }
        return (chunks, truncated);
    }

    public static IEnumerable<string> Sentences(string text)
    {
        var sb = new StringBuilder();
        var n = text.Length;
        for (var i = 0; i < n; i++)
        {
            var ch = text[i];
            sb.Append(ch);
            var boundary = false;
            if (ch == '\n') boundary = true;
            else if (".!?…".IndexOf(ch) >= 0)
            {
                while (i + 1 < n && ".!?…\"'»)".IndexOf(text[i + 1]) >= 0) sb.Append(text[++i]);
                boundary = i + 1 >= n || char.IsWhiteSpace(text[i + 1]);
            }
            if (!boundary) continue;
            while (i + 1 < n && char.IsWhiteSpace(text[i + 1])) sb.Append(text[++i]);
            yield return sb.ToString();
            sb.Clear();
        }
        if (sb.Length > 0) yield return sb.ToString();
    }

    private static string CutAtWord(string s, int max)
    {
        var cut = s.LastIndexOf(' ', max - 1);
        if (cut < max / 2) cut = max;
        return s.Substring(0, cut).TrimEnd();
    }
}

public sealed record TranslateOutcome(string? Text, string? Detected, string Target, bool Truncated, string? Error);

public delegate Task<MyMemoryResult> TranslateCall(string source, string target, string text, CancellationToken ct);

public static class TranslateFlow
{
    /// <summary>
    /// Traduit morceau par morceau. Si la langue détectée est déjà la langue cible, bascule vers la langue de secours.
    /// </summary>
    public static async Task<TranslateOutcome> RunAsync(string text, string target, string fallback, TranslateCall call, CancellationToken ct,
        int maxChunk = TranslateChunker.DefaultMax, int maxChunks = 6)
    {
        target = LanguageCodes.Normalize(target, "fr");
        fallback = LanguageCodes.Normalize(fallback, "en");
        var (chunks, truncated) = TranslateChunker.Split(text, maxChunk, maxChunks);
        if (chunks.Count == 0) return new TranslateOutcome(null, null, target, false, L.Get("Online_NothingToTranslate"));

        var first = await call("autodetect", target, chunks[0].Trim(), ct).ConfigureAwait(false);
        string? detected = first.DetectedLanguage;
        // texte déjà dans la langue cible : soit la langue détectée l'indique, soit MyMemory refuse (« DISTINCT LANGUAGES », sans langue détectée)
        if (first.SameLanguage || (first.Error == null && detected != null && detected == target))
        {
            var origin = target;
            target = fallback != target ? fallback : (target == "fr" ? "en" : "fr");
            detected = detected ?? origin;
            first = await call(first.SameLanguage ? "autodetect" : detected, target, chunks[0].Trim(), ct).ConfigureAwait(false); // un seul rejeu
        }
        if (first.Error != null) return new TranslateOutcome(null, detected, target, truncated, first.Error);

        var source = first.DetectedLanguage ?? detected ?? "autodetect";
        var sb = new StringBuilder(WithSuffix(first.Text!, chunks[0]));
        for (var i = 1; i < chunks.Count; i++)
        {
            var r = await call(source, target, chunks[i].Trim(), ct).ConfigureAwait(false);
            if (r.Error != null) return new TranslateOutcome(null, detected, target, truncated, r.Error);
            sb.Append(WithSuffix(r.Text!, chunks[i]));
        }
        return new TranslateOutcome(sb.ToString().Trim(), detected, target, truncated, null);
    }

    private static string WithSuffix(string translated, string original) =>
        translated.TrimEnd() + original.Substring(original.TrimEnd().Length);
}
