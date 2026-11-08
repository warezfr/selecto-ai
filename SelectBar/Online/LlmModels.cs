using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SelectBar.Localization;

namespace SelectBar.Online;

public sealed record LlmModelInfo(string Id, string Name);

/// <summary>Lecture, filtrage et tri des listes de modèles des fournisseurs (purs, testés avec des réponses d'exemple).</summary>
public static class LlmModelLists
{
    /// <summary>Mots d'un identifiant qui désignent un modèle qui ne converse pas (voix, image, vecteurs, modération…).</summary>
    private static readonly string[] NotChat =
    {
        "embed", "whisper", "tts", "dall-e", "dalle", "moderation", "realtime", "audio", "transcribe", "image", "imagen", "veo", "sora",
        "rerank", "guard", "speech", "davinci", "babbage", "instruct-0914", "gpt-3.5-turbo-instruct", "codex", "aqa", "computer-use"
    };

    /// <summary>Mots (entiers) d'un identifiant qui signalent un modèle rapide et économique : proposés en premier.</summary>
    private static readonly string[] CheapPreferred = { "mini", "flash", "haiku", "small", "instant", "8b", "7b", "3b", "1b" };
    private static readonly string[] CheapOther = { "nano", "lite", "turbo", "air" };
    private static readonly string[] Slow = { "o1", "o3", "o4", "r1", "reasoner", "reasoning", "thinking" };

    private static readonly Regex DatedSnapshot = new(@"(\d{4}-\d{2}-\d{2}|\d{8}|-\d{4}$)", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    /// <summary>Réponse « compatible OpenAI » : { "data": [ { "id": "..." } ] }, ou (Together) un tableau nu. Le préfixe « models/ » de Gemini est retiré.</summary>
    public static IReadOnlyList<LlmModelInfo> ParseOpenAiLike(string json)
    {
        var list = new List<LlmModelInfo>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            JsonElement data;
            if (root.ValueKind == JsonValueKind.Array) data = root;
            else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Array) data = d;
            else return list;
            foreach (var m in data.EnumerateArray())
            {
                var id = Str(m, "id");
                if (string.IsNullOrWhiteSpace(id)) continue;
                var type = Str(m, "type");
                if (type != null && (type is "embedding" or "image" or "audio" or "rerank" or "moderation" or "transcribe" or "video")) continue;
                if (m.TryGetProperty("capabilities", out var caps) && caps.ValueKind == JsonValueKind.Object
                    && caps.TryGetProperty("completion_chat", out var cc) && cc.ValueKind == JsonValueKind.False) continue;
                var clean = id!.StartsWith("models/", StringComparison.OrdinalIgnoreCase) ? id.Substring(7) : id;
                list.Add(new LlmModelInfo(clean, Str(m, "display_name") ?? Str(m, "name") ?? clean));
            }
        }
        catch (JsonException) { }
        return list;
    }

    /// <summary>Réponse d'Anthropic : { "data": [ { "id": "claude-...", "display_name": "..." } ] } (les plus récents d'abord).</summary>
    public static IReadOnlyList<LlmModelInfo> ParseAnthropic(string json)
    {
        var list = new List<LlmModelInfo>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return list;
            foreach (var m in data.EnumerateArray())
            {
                var id = Str(m, "id");
                if (string.IsNullOrWhiteSpace(id)) continue;
                list.Add(new LlmModelInfo(id!, Str(m, "display_name") ?? id!));
            }
        }
        catch (JsonException) { }
        return list;
    }

    public static bool IsChatModel(string id)
    {
        var l = (id ?? "").ToLowerInvariant();
        if (l.Length == 0) return false;
        foreach (var bad in NotChat)
            if (l.Contains(bad)) return false;
        return true;
    }

    /// <summary>Points de tri : plus c'est haut, plus le modèle est rapide, économique et stable (à proposer en premier).</summary>
    public static int Score(string id, string providerId)
    {
        var l = (id ?? "").ToLowerInvariant();
        var tokens = Regex.Split(l, "[^a-z0-9]+").Where(t => t.Length > 0).ToArray();
        var score = 0;
        if (tokens.Any(t => CheapPreferred.Contains(t))) score += 100;
        else if (tokens.Any(t => CheapOther.Contains(t))) score += 90;
        if (providerId == LlmPresets.OpenRouter && l.EndsWith(":free")) score += 50;
        if (DatedSnapshot.IsMatch(l) || l.Contains("preview") || l.Contains("-exp") || l.Contains("beta")) score -= 20;
        if (tokens.Any(t => Slow.Contains(t))) score -= 30;
        return score;
    }

    /// <summary>
    /// Modèles de conversation seulement, les rapides et économiques d'abord, au plus <paramref name="max"/>.
    /// À score égal : l'ordre d'Anthropic (le plus récent d'abord), sinon la version la plus haute (4.1 avant 4o, 2.5 avant 2.0).
    /// </summary>
    public static IReadOnlyList<LlmModelInfo> ForPicker(IEnumerable<LlmModelInfo> models, string providerId, int max = 120)
    {
        var chat = models.Where(m => IsChatModel(m.Id))
                         .GroupBy(m => m.Id, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                         .Select((m, i) => (Model: m, Index: i))
                         .ToList();
        IOrderedEnumerable<(LlmModelInfo Model, int Index)> ordered = chat.OrderByDescending(t => Score(t.Model.Id, providerId));
        ordered = providerId == LlmPresets.Anthropic
            ? ordered.ThenBy(t => t.Index)
            : ordered.ThenByDescending(t => t.Model.Id, VersionComparer.Instance);
        return ordered.Select(t => t.Model).Take(max).ToList();
    }

    /// <summary>Modèle proposé par défaut : le premier de la liste triée (null si la liste est vide).</summary>
    public static string? DefaultModel(IReadOnlyList<LlmModelInfo> sorted) => sorted.Count == 0 ? null : sorted[0].Id;

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

/// <summary>Compare deux identifiants de modèles par versions : « 2.5 » avant « 2.0 », « 4.1 » avant « 4o », « 3-5 » avant « 3 ».</summary>
public sealed class VersionComparer : IComparer<string>
{
    public static readonly VersionComparer Instance = new();
    private static readonly Regex Parts = new(@"[0-9]+|[a-z]+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    public int Compare(string? x, string? y)
    {
        var a = Parts.Matches((x ?? "").ToLowerInvariant()).Select(m => m.Value).ToArray();
        var b = Parts.Matches((y ?? "").ToLowerInvariant()).Select(m => m.Value).ToArray();
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var na = char.IsDigit(a[i][0]);
            var nb = char.IsDigit(b[i][0]);
            int c;
            if (na && nb)
            {
                var ta = a[i].TrimStart('0'); var tb = b[i].TrimStart('0');
                c = ta.Length != tb.Length ? ta.Length.CompareTo(tb.Length) : string.CompareOrdinal(ta, tb);
            }
            else if (na != nb) c = na ? 1 : -1;      // un nombre l'emporte sur un mot
            else c = string.CompareOrdinal(a[i], b[i]);
            if (c != 0) return c;
        }
        return a.Length.CompareTo(b.Length);
    }
}

public sealed record ModelListResult(IReadOnlyList<LlmModelInfo> Models, string? Error);

/// <summary>Récupère la liste réelle des modèles d'un fournisseur avec la clé de l'utilisateur (GET {base}/models).</summary>
public sealed class LlmModelClient
{
    private readonly HttpClient _http;
    public LlmModelClient(HttpClient http) { _http = http; }

    public async Task<ModelListResult> ListAsync(LlmPreset preset, string baseUrl, string? apiKey, CancellationToken ct)
    {
        if (!LlmUrlValidator.Validate(baseUrl, !string.IsNullOrEmpty(apiKey), out var url, out var urlError))
            return new ModelListResult(Array.Empty<LlmModelInfo>(), urlError);
        var transport = LlmTransports.For(preset.Transport);
        try
        {
            using var msg = transport.BuildModels(url, apiKey, preset.Headers).ToMessage();
            var (status, body) = await OnlineHttp.SendAsync(_http, msg, OnlineHttp.ShortTimeout, ct).ConfigureAwait(false);
            if ((int)status >= 400)
                return new ModelListResult(Array.Empty<LlmModelInfo>(), LlmResponseParser.ErrorForStatus((int)status, preset.Id, body));
            var models = LlmModelLists.ForPicker(transport.ParseModels(body), preset.Id);
            return models.Count == 0
                ? new ModelListResult(models, L.Get("Online_Models_NoChat"))
                : new ModelListResult(models, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (TimeoutException) { return new ModelListResult(Array.Empty<LlmModelInfo>(), L.Get("Online_ErrServiceSlow")); }
        catch (HttpRequestException ex)
        {
            OnlineLog.Write("Liste des modèles de " + preset.Id + " : " + ex.Message);
            return new ModelListResult(Array.Empty<LlmModelInfo>(), L.Get("Online_ErrUnreachableAddr"));
        }
    }
}
