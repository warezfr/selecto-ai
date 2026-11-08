using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using SelectBar.Localization;

namespace SelectBar.Online;

/// <summary>Une requête HTTP prête à partir (méthode, adresse, en-têtes, corps JSON), construite sans réseau pour pouvoir être testée.</summary>
public sealed record LlmHttpRequest(string Method, string Url, IReadOnlyDictionary<string, string> Headers, string? Body)
{
    public HttpRequestMessage ToMessage()
    {
        var msg = new HttpRequestMessage(new HttpMethod(Method), Url);
        if (Body != null) msg.Content = new StringContent(Body, Encoding.UTF8, "application/json");
        foreach (var h in Headers) msg.Headers.TryAddWithoutValidation(h.Key, h.Value);
        return msg;
    }
}

/// <summary>Façon de parler à un fournisseur : construction de la requête de conversation et de la liste des modèles, lecture des réponses.</summary>
public interface ILlmTransport
{
    LlmHttpRequest BuildChat(string baseUrl, string model, string system, string user, string? apiKey, IReadOnlyDictionary<string, string> extraHeaders);
    LlmHttpRequest BuildModels(string baseUrl, string? apiKey, IReadOnlyDictionary<string, string> extraHeaders);
    LlmResult ParseChat(string json);
    IReadOnlyList<LlmModelInfo> ParseModels(string json);
}

public static class LlmTransports
{
    public static readonly ILlmTransport OpenAiCompatible = new OpenAiCompatibleTransport();
    public static readonly ILlmTransport Anthropic = new AnthropicTransport();

    public static ILlmTransport For(LlmTransportKind kind) => kind == LlmTransportKind.Anthropic ? Anthropic : OpenAiCompatible;

    public static ILlmTransport ForProvider(string? providerId) =>
        For(LlmPresets.Get(providerId)?.Transport ?? LlmTransportKind.OpenAiCompatible);
}

/// <summary>OpenAI, Gemini (endpoint compatible), Mistral, Groq, DeepSeek, xAI, Together, OpenRouter, Ollama, serveur libre : POST {base}/chat/completions.</summary>
public sealed class OpenAiCompatibleTransport : ILlmTransport
{
    public LlmHttpRequest BuildChat(string baseUrl, string model, string system, string user, string? apiKey, IReadOnlyDictionary<string, string> extraHeaders) =>
        new("POST", baseUrl.TrimEnd('/') + "/chat/completions", Headers(apiKey, extraHeaders), LlmPrompts.BuildRequestJson(model, system, user));

    public LlmHttpRequest BuildModels(string baseUrl, string? apiKey, IReadOnlyDictionary<string, string> extraHeaders) =>
        new("GET", baseUrl.TrimEnd('/') + "/models", Headers(apiKey, extraHeaders), null);

    public LlmResult ParseChat(string json) => LlmResponseParser.Parse(json);

    public IReadOnlyList<LlmModelInfo> ParseModels(string json) => LlmModelLists.ParseOpenAiLike(json);

    private static IReadOnlyDictionary<string, string> Headers(string? apiKey, IReadOnlyDictionary<string, string> extra)
    {
        var h = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(apiKey)) h["Authorization"] = "Bearer " + apiKey;
        foreach (var kv in extra) h[kv.Key] = kv.Value;
        return h;
    }
}

/// <summary>Anthropic (Claude), API native : POST {base}/messages avec x-api-key et anthropic-version ; réponse content[0].text.</summary>
public sealed class AnthropicTransport : ILlmTransport
{
    public const string Version = "2023-06-01";
    public const int MaxTokens = 1024;

    public LlmHttpRequest BuildChat(string baseUrl, string model, string system, string user, string? apiKey, IReadOnlyDictionary<string, string> extraHeaders)
    {
        var body = JsonSerializer.Serialize(new
        {
            model,
            max_tokens = MaxTokens,
            system,
            messages = new object[] { new { role = "user", content = user } },
            temperature = 0.2
        });
        return new("POST", baseUrl.TrimEnd('/') + "/messages", Headers(apiKey, extraHeaders), body);
    }

    public LlmHttpRequest BuildModels(string baseUrl, string? apiKey, IReadOnlyDictionary<string, string> extraHeaders) =>
        new("GET", baseUrl.TrimEnd('/') + "/models?limit=100", Headers(apiKey, extraHeaders), null);

    /// <summary>content[0].text (tous les blocs « text » mis bout à bout) ; sinon error.message.</summary>
    public LlmResult ParseChat(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind == JsonValueKind.Object)
            {
                if (r.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
                {
                    var sb = new StringBuilder();
                    foreach (var block in content.EnumerateArray())
                    {
                        if (block.ValueKind != JsonValueKind.Object) continue;
                        var type = block.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : "text";
                        if (type == "text" && block.TryGetProperty("text", out var tx) && tx.ValueKind == JsonValueKind.String) sb.Append(tx.GetString());
                    }
                    var text = LlmPrompts.StripThinking(sb.ToString());
                    return text.Length == 0 ? new LlmResult(null, L.Get("Online_Llm_Empty")) : new LlmResult(text, null);
                }
                if (r.TryGetProperty("error", out var err))
                {
                    var m = err.ValueKind == JsonValueKind.Object && err.TryGetProperty("message", out var em) && em.ValueKind == JsonValueKind.String
                        ? em.GetString() : err.ValueKind == JsonValueKind.String ? err.GetString() : null;
                    return new LlmResult(null, m == null ? L.Get("Online_Llm_ErrorGeneric") : L.Format("Online_Llm_ErrorSaid", DictionaryFormat.Shorten(m, 140)));
                }
            }
        }
        catch (JsonException) { }
        return new LlmResult(null, L.Get("Online_Llm_Unexpected"));
    }

    public IReadOnlyList<LlmModelInfo> ParseModels(string json) => LlmModelLists.ParseAnthropic(json);

    private static IReadOnlyDictionary<string, string> Headers(string? apiKey, IReadOnlyDictionary<string, string> extra)
    {
        var h = new Dictionary<string, string> { ["anthropic-version"] = Version };
        if (!string.IsNullOrEmpty(apiKey)) h["x-api-key"] = apiKey;
        foreach (var kv in extra) h[kv.Key] = kv.Value;
        return h;
    }
}
