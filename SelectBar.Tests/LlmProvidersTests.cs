using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SelectBar.Localization;
using SelectBar.Online;
using SelectBar.Settings;
using Xunit;

namespace SelectBar.Tests;

public class LlmProviderCatalogTests
{
    [Theory]
    [InlineData("openai", "https://api.openai.com/v1", "https://platform.openai.com/api-keys")]
    [InlineData("anthropic", "https://api.anthropic.com/v1", "https://console.anthropic.com/settings/keys")]
    [InlineData("gemini", "https://generativelanguage.googleapis.com/v1beta/openai", "https://aistudio.google.com/apikey")]
    [InlineData("mistral", "https://api.mistral.ai/v1", "https://console.mistral.ai/api-keys")]
    [InlineData("groq", "https://api.groq.com/openai/v1", "https://console.groq.com/keys")]
    [InlineData("deepseek", "https://api.deepseek.com/v1", "https://platform.deepseek.com/api_keys")]
    [InlineData("xai", "https://api.x.ai/v1", "https://console.x.ai")]
    [InlineData("together", "https://api.together.xyz/v1", "https://api.together.xyz/settings/api-keys")]
    [InlineData("openrouter", "https://openrouter.ai/api/v1", "https://openrouter.ai/keys")]
    public void Each_provider_has_its_base_url_and_keys_page(string id, string baseUrl, string keysPage)
    {
        var p = LlmPresets.Get(id)!;
        Assert.Equal(baseUrl, p.BaseUrl);
        Assert.Equal(keysPage, p.KeysPage);
        Assert.True(p.NeedsKey);
        Assert.True(LlmUrlValidator.Validate(p.BaseUrl, true, out _, out _));
    }

    [Fact]
    public void Online_list_has_ten_choices_ending_with_the_free_url_one_and_only_anthropic_is_native()
    {
        Assert.Equal(10, LlmPresets.Online.Count);
        Assert.Equal(LlmPresets.Custom, LlmPresets.Online.Last().Id);
        Assert.Equal("Autre (compatible OpenAI)", LlmPresets.CustomPreset.Label);
        Assert.False(LlmPresets.CustomPreset.NeedsKey);
        Assert.Equal(new[] { "anthropic" }, LlmPresets.Online.Where(p => p.Transport == LlmTransportKind.Anthropic).Select(p => p.Id));
        Assert.Equal(LlmPresets.Online.Count, LlmPresets.Online.Select(p => p.Id).Distinct().Count());
        Assert.Null(LlmPresets.Get("inconnu"));
    }

    [Fact]
    public void Apply_sets_the_url_and_a_free_url_stays_empty_until_typed()
    {
        var s = new AppSettings();
        LlmPresets.Apply(s, LlmPresets.Gemini);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/openai", s.LlmBaseUrl);
        LlmPresets.Apply(s, LlmPresets.Custom);
        Assert.Equal("", s.LlmBaseUrl);
        s.LlmBaseUrl = "https://mon-serveur.test/v1";
        LlmPresets.Apply(s, LlmPresets.Custom);   // même fournisseur : l'adresse saisie est gardée
        Assert.Equal("https://mon-serveur.test/v1", s.LlmBaseUrl);
    }

    [Fact]
    public void Privacy_notice_names_the_provider()
    {
        L.Culture = UiCulture.Make("fr");
        Assert.Equal("Le texte sélectionné est envoyé à OpenAI.", LlmPresets.PrivacyNotice(LlmPresets.OpenAi));
        Assert.Equal("Le texte sélectionné est envoyé à Anthropic (Claude).", LlmPresets.PrivacyNotice(LlmPresets.Anthropic));
        Assert.Equal("Le texte sélectionné est envoyé à ce serveur.", LlmPresets.PrivacyNotice(LlmPresets.Custom));
        Assert.Contains("reste sur ton PC", LlmPresets.PrivacyNotice(LlmPresets.Ollama));
    }

    private sealed class Reverse : ISecretProtector
    {
        public byte[] Protect(byte[] d) => d.Reverse().ToArray();
        public byte[] Unprotect(byte[] d) => d.Reverse().ToArray();
    }

    [Fact]
    public void Keys_are_stored_per_provider_and_survive_switching()
    {
        var vault = new ApiKeyVault(new Reverse());
        var s = new AppSettings();
        LlmPresets.Apply(s, LlmPresets.OpenAi);
        ProviderKeys.Set(s, LlmPresets.OpenAi, vault.Protect("sk-openai-secret"));
        Assert.Equal("sk-openai-secret", vault.Unprotect(s.LlmApiKeyProtected));

        LlmPresets.Apply(s, LlmPresets.Anthropic);
        Assert.Null(s.LlmApiKeyProtected);                       // pas de clé Anthropic : celle d'OpenAI ne fuit pas
        ProviderKeys.Set(s, LlmPresets.Anthropic, vault.Protect("sk-ant-secret"));
        Assert.Equal("sk-ant-secret", vault.Unprotect(s.LlmApiKeyProtected));
        Assert.Equal("sk-openai-secret", vault.Unprotect(ProviderKeys.Get(s, LlmPresets.OpenAi)));

        LlmPresets.Apply(s, LlmPresets.OpenAi);
        Assert.Equal("sk-openai-secret", vault.Unprotect(s.LlmApiKeyProtected));
        Assert.Equal("sk-ant-secret", vault.Unprotect(ProviderKeys.Get(s, LlmPresets.Anthropic)));

        LlmPresets.Apply(s, LlmPresets.Ollama);
        Assert.Null(s.LlmApiKeyProtected);
        Assert.Equal(2, s.LlmApiKeys.Count);

        var json = JsonSerializer.Serialize(s);
        Assert.DoesNotContain("sk-openai-secret", json);
        Assert.DoesNotContain("sk-ant-secret", json);
        var back = JsonSerializer.Deserialize<AppSettings>(json)!;
        Assert.Equal(2, back.LlmApiKeys.Count);
        Assert.Equal("sk-ant-secret", vault.Unprotect(ProviderKeys.Get(back, LlmPresets.Anthropic)));
    }

    [Fact]
    public void Forgetting_a_key_clears_it_everywhere()
    {
        var s = new AppSettings();
        LlmPresets.Apply(s, LlmPresets.Groq);
        ProviderKeys.Set(s, LlmPresets.Groq, "abc");
        Assert.Equal("abc", s.LlmApiKeyProtected);
        ProviderKeys.Set(s, LlmPresets.Groq, null);
        Assert.Null(s.LlmApiKeyProtected);
        Assert.Empty(s.LlmApiKeys);
        ProviderKeys.Set(s, LlmPresets.Ollama, "x");     // Ollama n'a pas de clé
        Assert.Empty(s.LlmApiKeys);
    }

    [Fact]
    public void A_provider_is_configured_only_with_its_own_key_and_a_model()
    {
        var s = new AppSettings();
        LlmPresets.Apply(s, LlmPresets.Mistral);
        s.LlmModel = "mistral-small-latest";
        Assert.False(LlmPresets.IsConfigured(s));
        ProviderKeys.Set(s, LlmPresets.Mistral, "k");
        Assert.True(LlmPresets.IsConfigured(s));
        LlmPresets.Apply(s, LlmPresets.Groq);
        s.LlmModel = "llama-3.1-8b-instant";
        Assert.False(LlmPresets.IsConfigured(s));        // la clé de Mistral ne sert pas à Groq
    }
}

public class LlmTransportTests
{
    private static readonly IReadOnlyDictionary<string, string> NoExtra = new Dictionary<string, string>();

    [Fact]
    public void Openai_compatible_request_has_bearer_and_chat_completions_body()
    {
        var req = LlmTransports.OpenAiCompatible.BuildChat("https://api.groq.com/openai/v1/", "llama-3.1-8b-instant", "SYS", "USR", "gsk_key", NoExtra);
        Assert.Equal("POST", req.Method);
        Assert.Equal("https://api.groq.com/openai/v1/chat/completions", req.Url);
        Assert.Equal("Bearer gsk_key", req.Headers["Authorization"]);
        Assert.False(req.Headers.ContainsKey("x-api-key"));
        using var doc = JsonDocument.Parse(req.Body!);
        Assert.Equal("llama-3.1-8b-instant", doc.RootElement.GetProperty("model").GetString());
        var messages = doc.RootElement.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("SYS", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("USR", messages[1].GetProperty("content").GetString());
        Assert.False(doc.RootElement.GetProperty("stream").GetBoolean());
    }

    [Fact]
    public void Openai_compatible_without_key_has_no_authorization_and_keeps_extra_headers()
    {
        var req = LlmTransports.OpenAiCompatible.BuildChat("http://localhost:11434/v1", "m", "s", "u", null, LlmPresets.HeadersFor(LlmPresets.OpenRouter));
        Assert.False(req.Headers.ContainsKey("Authorization"));
        Assert.Equal("Selecto AI", req.Headers["X-Title"]);
    }

    [Fact]
    public void Anthropic_request_uses_native_messages_api_with_x_api_key_and_version()
    {
        var req = LlmTransports.Anthropic.BuildChat("https://api.anthropic.com/v1", "claude-3-5-haiku-20241022", "SYS", "USR", "sk-ant-key", NoExtra);
        Assert.Equal("POST", req.Method);
        Assert.Equal("https://api.anthropic.com/v1/messages", req.Url);
        Assert.Equal("sk-ant-key", req.Headers["x-api-key"]);
        Assert.Equal("2023-06-01", req.Headers["anthropic-version"]);
        Assert.False(req.Headers.ContainsKey("Authorization"));
        using var doc = JsonDocument.Parse(req.Body!);
        var root = doc.RootElement;
        Assert.Equal("claude-3-5-haiku-20241022", root.GetProperty("model").GetString());
        Assert.True(root.GetProperty("max_tokens").GetInt32() > 0);
        Assert.Equal("SYS", root.GetProperty("system").GetString());
        var m = root.GetProperty("messages");
        Assert.Equal(1, m.GetArrayLength());
        Assert.Equal("user", m[0].GetProperty("role").GetString());
        Assert.Equal("USR", m[0].GetProperty("content").GetString());
        Assert.DoesNotContain("sk-ant-key", req.Body);
    }

    [Fact]
    public void Models_requests()
    {
        var o = LlmTransports.OpenAiCompatible.BuildModels("https://api.openai.com/v1", "sk-1", NoExtra);
        Assert.Equal("GET", o.Method);
        Assert.Equal("https://api.openai.com/v1/models", o.Url);
        Assert.Equal("Bearer sk-1", o.Headers["Authorization"]);
        Assert.Null(o.Body);
        var a = LlmTransports.Anthropic.BuildModels("https://api.anthropic.com/v1", "sk-ant", NoExtra);
        Assert.Equal("https://api.anthropic.com/v1/models?limit=100", a.Url);
        Assert.Equal("sk-ant", a.Headers["x-api-key"]);
        Assert.Equal("2023-06-01", a.Headers["anthropic-version"]);
    }

    [Fact]
    public void ToMessage_carries_headers_and_json_body()
    {
        var req = LlmTransports.Anthropic.BuildChat("https://api.anthropic.com/v1", "m", "s", "u", "k", NoExtra);
        using var msg = req.ToMessage();
        Assert.Equal(HttpMethod.Post, msg.Method);
        Assert.Equal("k", msg.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", msg.Headers.GetValues("anthropic-version").Single());
        Assert.Equal("application/json", msg.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public void Provider_picks_the_right_transport()
    {
        Assert.Same(LlmTransports.Anthropic, LlmTransports.ForProvider(LlmPresets.Anthropic));
        Assert.Same(LlmTransports.OpenAiCompatible, LlmTransports.ForProvider(LlmPresets.OpenAi));
        Assert.Same(LlmTransports.OpenAiCompatible, LlmTransports.ForProvider(LlmPresets.Ollama));
        Assert.Same(LlmTransports.OpenAiCompatible, LlmTransports.ForProvider("n'importe quoi"));
    }

    [Fact]
    public void Anthropic_response_is_read_from_content_blocks()
    {
        var r = LlmTransports.Anthropic.ParseChat(Fx.Read("anthropic-response.json"));
        Assert.Null(r.Error);
        Assert.Equal("Bonjour, ça va ? Tu as bien dormi ?", r.Content);
    }

    [Fact]
    public void Anthropic_error_and_garbage_are_readable()
    {
        var e = LlmTransports.Anthropic.ParseChat(Fx.Read("anthropic-error.json"));
        Assert.Null(e.Content);
        Assert.Contains("invalid x-api-key", e.Error);
        Assert.NotNull(LlmTransports.Anthropic.ParseChat("pas du json").Error);
        Assert.NotNull(LlmTransports.Anthropic.ParseChat("{\"content\":[]}").Error);
        Assert.Equal("ok", LlmTransports.Anthropic.ParseChat("{\"content\":[{\"type\":\"thinking\",\"thinking\":\"x\"},{\"type\":\"text\",\"text\":\"ok\"}]}").Content);
    }

    [Fact]
    public void Openai_response_still_parses_through_the_transport()
    {
        Assert.Equal("Bonjour le monde.", LlmTransports.OpenAiCompatible.ParseChat(Fx.Read("llm-response.json")).Content);
    }

    [Theory]
    [InlineData(401, "", "clé")]
    [InlineData(400, "{\"error\":{\"message\":\"API key not valid. Please pass a valid API key.\",\"status\":\"INVALID_ARGUMENT\"}}", "clé")]
    [InlineData(401, "{\"type\":\"error\",\"error\":{\"type\":\"authentication_error\"}}", "clé")]
    [InlineData(404, "{\"type\":\"error\",\"error\":{\"type\":\"not_found_error\",\"message\":\"model: x\"}}", "Modèle introuvable")]
    [InlineData(404, "", "Modèle introuvable")]
    [InlineData(429, "", "quota")]
    [InlineData(429, "{\"error\":{\"code\":\"insufficient_quota\"}}", "quota")]
    [InlineData(500, "", "Réessaie")]
    public void Error_messages_are_readable_for_every_provider(int status, string body, string expectedPart) =>
        Assert.Contains(expectedPart, LlmResponseParser.ErrorForStatus(status, LlmPresets.Gemini, body));
}

public class LlmModelListTests
{
    private static IReadOnlyList<LlmModelInfo> Picker(string fixture, string provider)
    {
        var t = LlmTransports.ForProvider(provider);
        return LlmModelLists.ForPicker(t.ParseModels(Fx.Read(fixture)), provider);
    }

    [Fact]
    public void Openai_list_keeps_chat_models_and_puts_cheap_stable_ones_first()
    {
        var ids = Picker("openai-models.json", LlmPresets.OpenAi).Select(m => m.Id).ToList();
        Assert.Equal(new[] { "gpt-4.1-mini", "gpt-4o-mini", "gpt-4o-mini-2024-07-18", "o3-mini", "gpt-4o" }, ids);
        Assert.Equal("gpt-4.1-mini", LlmModelLists.DefaultModel(Picker("openai-models.json", LlmPresets.OpenAi)));
    }

    [Fact]
    public void Anthropic_list_defaults_to_the_newest_haiku_in_the_providers_order()
    {
        var models = Picker("anthropic-models.json", LlmPresets.Anthropic);
        Assert.Equal("claude-3-5-haiku-20241022", LlmModelLists.DefaultModel(models));
        Assert.Equal(new[] { "claude-3-5-haiku-20241022", "claude-3-haiku-20240307", "claude-opus-4-1-20250805", "claude-sonnet-4-20250514" },
            models.Select(m => m.Id).ToList());
        Assert.Equal("Claude Haiku 3.5", models[0].Name);
    }

    [Fact]
    public void Gemini_ids_lose_the_models_prefix_and_non_chat_models_are_dropped()
    {
        var ids = Picker("gemini-models.json", LlmPresets.Gemini).Select(m => m.Id).ToList();
        Assert.Equal(new[] { "gemini-2.5-flash", "gemini-2.0-flash-lite", "gemini-2.5-pro" }, ids);
    }

    [Fact]
    public void Together_bare_array_is_read_and_images_and_embeddings_are_dropped()
    {
        var ids = Picker("together-models.json", LlmPresets.Together).Select(m => m.Id).ToList();
        Assert.Equal(new[] { "meta-llama/Llama-3.2-3B-Instruct-Turbo", "meta-llama/Llama-3.3-70B-Instruct-Turbo" }, ids);
    }

    [Fact]
    public void Mistral_capabilities_hide_the_embedding_model_and_small_comes_first()
    {
        var ids = Picker("mistral-models.json", LlmPresets.Mistral).Select(m => m.Id).ToList();
        Assert.Equal(new[] { "mistral-small-latest", "mistral-large-latest" }, ids);
    }

    [Fact]
    public void Openrouter_free_models_come_first_and_garbage_gives_an_empty_list()
    {
        var all = LlmModelLists.ParseOpenAiLike(Fx.Read("openrouter-models.json"));
        var sorted = LlmModelLists.ForPicker(all, LlmPresets.OpenRouter);
        Assert.True(sorted.Count > 0);
        Assert.EndsWith(":free", sorted[0].Id);
        Assert.Empty(LlmModelLists.ParseOpenAiLike("pas du json"));
        Assert.Empty(LlmModelLists.ParseAnthropic("{}"));
        Assert.Null(LlmModelLists.DefaultModel(Array.Empty<LlmModelInfo>()));
    }

    [Theory]
    [InlineData("gpt-4o-mini", 100)]
    [InlineData("gpt-4o-mini-2024-07-18", 80)]
    [InlineData("gpt-4o", 0)]
    [InlineData("gemini-2.5-pro", 0)]
    [InlineData("claude-3-5-haiku-20241022", 80)]
    [InlineData("mistral-small-latest", 100)]
    [InlineData("llama-3.1-8b-instant", 100)]
    [InlineData("o1-mini", 70)]
    [InlineData("gemini-2.5-flash-preview-05-20", 80)]
    public void Score_heuristic(string id, int expected) => Assert.Equal(expected, LlmModelLists.Score(id, LlmPresets.OpenAi));

    [Theory]
    [InlineData("gpt-4o", true)]
    [InlineData("text-embedding-3-large", false)]
    [InlineData("whisper-1", false)]
    [InlineData("tts-1-hd", false)]
    [InlineData("dall-e-2", false)]
    [InlineData("llama-guard-3-8b", false)]
    [InlineData("omni-moderation-latest", false)]
    [InlineData("gpt-image-1", false)]
    [InlineData("deepseek-chat", true)]
    [InlineData("grok-3-mini", true)]
    public void Chat_model_filter(string id, bool chat) => Assert.Equal(chat, LlmModelLists.IsChatModel(id));

    [Fact]
    public void Version_comparer_orders_versions_naturally()
    {
        Assert.True(VersionComparer.Instance.Compare("gemini-2.5-flash", "gemini-2.0-flash") > 0);
        Assert.True(VersionComparer.Instance.Compare("gpt-4.1-mini", "gpt-4o-mini") > 0);
        Assert.True(VersionComparer.Instance.Compare("model-10", "model-9") > 0);
        Assert.Equal(0, VersionComparer.Instance.Compare("A-1", "a-1"));
    }
}

public class LlmProviderEndToEndTests
{
    private sealed class Capture : HttpMessageHandler
    {
        public HttpRequestMessage? Seen { get; private set; }
        public string? Body { get; private set; }
        public Func<HttpRequestMessage, (HttpStatusCode, string)> Reply { get; set; } = _ => (HttpStatusCode.OK, "{}");
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Seen = request;
            Body = request.Content?.ReadAsStringAsync().Result;
            var (code, body) = Reply(request);
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private static AppSettings Anthropic()
    {
        var s = new AppSettings();
        LlmPresets.Apply(s, LlmPresets.Anthropic);
        s.LlmModel = "claude-3-5-haiku-20241022";
        s.LlmApiKeyProtected = "chiffré";
        return s;
    }

    [Fact]
    public async Task Anthropic_summary_goes_to_messages_with_native_headers_and_reads_content_text()
    {
        var h = new Capture { Reply = _ => (HttpStatusCode.OK, Fx.Read("anthropic-response.json")) };
        var s = Anthropic();
        var svc = new ResultService(() => s, new HttpClient(h), () => "sk-ant-1");
        var p = await svc.RunAsync(new ResultRequest { Kind = ResultKind.Rephrase, Text = "bonjour sa va tu as bien dormi", IsEditable = true }, CancellationToken.None);
        Assert.Null(p.Error);
        Assert.Equal("Bonjour, ça va ? Tu as bien dormi ?", p.Body);
        Assert.True(p.CanReplace);
        Assert.Equal("https://api.anthropic.com/v1/messages", h.Seen!.RequestUri!.ToString());
        Assert.Equal("sk-ant-1", h.Seen.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", h.Seen.Headers.GetValues("anthropic-version").Single());
        Assert.False(h.Seen.Headers.Contains("Authorization"));
        Assert.Contains("texte_selectionne", h.Body);
        Assert.DoesNotContain("sk-ant-1", h.Body);
    }

    [Fact]
    public async Task Anthropic_refused_key_gives_a_readable_message()
    {
        var h = new Capture { Reply = _ => (HttpStatusCode.Unauthorized, Fx.Read("anthropic-error.json")) };
        var s = Anthropic();
        var p = await new ResultService(() => s, new HttpClient(h), () => "mauvaise").RunAsync(
            new ResultRequest { Kind = ResultKind.Summarize, Text = "Texte" }, CancellationToken.None);
        Assert.Contains("clé", p.Error);
        Assert.DoesNotContain("401", p.Error);
    }

    [Fact]
    public async Task Model_client_lists_sorts_and_reports_errors_in_french()
    {
        var h = new Capture { Reply = _ => (HttpStatusCode.OK, Fx.Read("openai-models.json")) };
        var client = new LlmModelClient(new HttpClient(h));
        var preset = LlmPresets.Get(LlmPresets.OpenAi)!;
        var ok = await client.ListAsync(preset, preset.BaseUrl, "sk-1", CancellationToken.None);
        Assert.Null(ok.Error);
        Assert.Equal("gpt-4.1-mini", ok.Models[0].Id);
        Assert.Equal("https://api.openai.com/v1/models", h.Seen!.RequestUri!.ToString());
        Assert.Equal("Bearer sk-1", h.Seen.Headers.GetValues("Authorization").Single());

        h.Reply = _ => (HttpStatusCode.Unauthorized, "{}");
        var refused = await client.ListAsync(preset, preset.BaseUrl, "mauvaise", CancellationToken.None);
        Assert.Contains("clé", refused.Error);
        Assert.Empty(refused.Models);

        h.Reply = _ => (HttpStatusCode.OK, "{\"data\":[{\"id\":\"whisper-1\"}]}");
        var none = await client.ListAsync(preset, preset.BaseUrl, "sk-1", CancellationToken.None);
        Assert.Contains("aucun modèle", none.Error);

        var badUrl = await client.ListAsync(LlmPresets.CustomPreset, "http://exemple.com/v1", "clé", CancellationToken.None);
        Assert.Contains("https", badUrl.Error);
    }
}
