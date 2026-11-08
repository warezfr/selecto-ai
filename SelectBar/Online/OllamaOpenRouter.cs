using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using SelectBar.Localization;

namespace SelectBar.Online;

public sealed record OllamaModel(string Name, long Size);
public sealed record PullProgress(string Status, long Completed, long Total, string? Error, bool Done)
{
    /// <summary>Avancement entre 0 et 1 ; null si le total est inconnu (étapes sans téléchargement).</summary>
    public double? Fraction => Total > 0 ? Math.Min(1.0, (double)Completed / Total) : null;
}

public static class OllamaParsers
{
    /// <summary>Réponse de GET /api/tags : { "models": [ { "name": "...", "size": 123 } ] }.</summary>
    public static IReadOnlyList<OllamaModel> ParseTags(string json)
    {
        var list = new List<OllamaModel>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("models", out var ms) && ms.ValueKind == JsonValueKind.Array)
                foreach (var m in ms.EnumerateArray())
                {
                    var name = m.ValueKind == JsonValueKind.Object && m.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    long size = m.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number && s.TryGetInt64(out var sz) ? sz : 0;
                    list.Add(new OllamaModel(name!, size));
                }
        }
        catch (JsonException) { }
        return list;
    }

    /// <summary>Une ligne ndjson de POST /api/pull : {"status":"pulling ...","total":N,"completed":M} ... {"status":"success"} ou {"error":"..."}.</summary>
    public static PullProgress? ParsePullLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        try
        {
            using var doc = JsonDocument.Parse(line);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return null;
            if (r.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                return new PullProgress("", 0, 0, e.GetString(), false);
            var status = r.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String ? st.GetString() ?? "" : "";
            long total = r.TryGetProperty("total", out var t) && t.ValueKind == JsonValueKind.Number && t.TryGetInt64(out var tv) ? tv : 0;
            long done = r.TryGetProperty("completed", out var c) && c.ValueKind == JsonValueKind.Number && c.TryGetInt64(out var cv) ? cv : 0;
            return new PullProgress(status, done, total, null, status == "success");
        }
        catch (JsonException) { return null; }
    }

    public static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "";
        var gb = bytes / 1_000_000_000.0;
        return gb >= 1 ? gb.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + " Go"
            : Math.Max(1, (long)Math.Round(bytes / 1_000_000.0)) + " Mo";
    }

    /// <summary>Message français pour une erreur de téléchargement d'Ollama.</summary>
    public static string PullError(string raw) =>
        raw.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0 || raw.IndexOf("file does not exist", StringComparison.OrdinalIgnoreCase) >= 0
            ? L.Get("Online_ModelNotInCatalog")
            : raw.IndexOf("disk", StringComparison.OrdinalIgnoreCase) >= 0 || raw.IndexOf("space", StringComparison.OrdinalIgnoreCase) >= 0
                ? L.Get("Online_NoDiskForModel")
                : L.Get("Online_PullFailed");
}

public sealed record OpenRouterModel(string Id, string Name);

public static class OpenRouterParsers
{
    /// <summary>Liste courte de secours si le réseau échoue.</summary>
    public static readonly IReadOnlyList<OpenRouterModel> Fallback = new[]
    {
        new OpenRouterModel("meta-llama/llama-3.2-3b-instruct:free", "Llama 3.2 3B"),
        new OpenRouterModel("google/gemma-2-9b-it:free", "Gemma 2 9B"),
        new OpenRouterModel("mistralai/mistral-7b-instruct:free", "Mistral 7B"),
        new OpenRouterModel("qwen/qwen-2.5-7b-instruct:free", "Qwen 2.5 7B")
    };

    /// <summary>Modèles gratuits de GET /api/v1/models : identifiant en « :free » et tarifs prompt/completion nuls.</summary>
    public static IReadOnlyList<OpenRouterModel> ParseFreeModels(string json, int max = 30)
    {
        var list = new List<OpenRouterModel>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return list;
            foreach (var m in data.EnumerateArray())
            {
                var id = Str(m, "id");
                if (id == null || !id.EndsWith(":free", StringComparison.OrdinalIgnoreCase)) continue;
                if (m.TryGetProperty("pricing", out var p) && p.ValueKind == JsonValueKind.Object && !(IsZero(p, "prompt") && IsZero(p, "completion"))) continue;
                list.Add(new OpenRouterModel(id, Str(m, "name") ?? id));
            }
        }
        catch (JsonException) { }
        return list.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).Take(max).ToList();
    }

    private static bool IsZero(JsonElement p, string name)
    {
        if (!p.TryGetProperty(name, out var v)) return true;
        var s = v.ValueKind == JsonValueKind.String ? v.GetString() : v.ValueKind == JsonValueKind.Number ? v.GetRawText() : null;
        return s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d == 0;
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
