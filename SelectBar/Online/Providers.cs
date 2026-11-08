using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SelectBar.Actions;
using SelectBar.Settings;
using SelectBar.Localization;

namespace SelectBar.Online;

public sealed class DictionaryProvider : IResultProvider
{
    private static readonly Regex Accents = new(@"[àâäçéèêëîïôöùûüÿœæ]", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
    private readonly HttpClient _http;
    private readonly Func<AppSettings> _settings;
    public DictionaryProvider(HttpClient http, Func<AppSettings> settings) { _http = http; _settings = settings; }
    public string Id => "dictionary";

    /// <summary>Langues à essayer : réglage explicite, sinon accents -> français seul, sinon français puis anglais.</summary>
    public static IReadOnlyList<string> LanguageOrder(string setting, string word)
    {
        var s = (setting ?? "auto").ToLowerInvariant();
        if (s == "fr") return new[] { "fr" };
        if (s == "en") return new[] { "en" };
        return Accents.IsMatch(word) ? new[] { "fr" } : new[] { "fr", "en" };
    }

    public async Task<ResultPayload> RunAsync(ResultRequest request, CancellationToken ct)
    {
        var word = request.Text.Trim();
        foreach (var lang in LanguageOrder(_settings().DictionaryLanguage, word))
        {
            DictionaryEntry? entry = lang == "fr" ? await LookupFrAsync(word, ct).ConfigureAwait(false) : await LookupEnAsync(word, ct).ConfigureAwait(false);
            if (entry == null) continue;
            return new ResultPayload
            {
                Title = entry.Word, Body = DictionaryFormat.Body(entry),
                Note = lang == "fr" ? "Source : Wiktionnaire" : "Source : dictionaryapi.dev (anglais)"
            };
        }
        return ResultPayload.Failure(L.Format("Online_NoDefinition", DictionaryFormat.Shorten(word, 40)), word);
    }

    private async Task<DictionaryEntry?> LookupFrAsync(string word, CancellationToken ct)
    {
        var candidates = new List<string> { word };
        var lower = word.ToLowerInvariant();
        if (lower != word) candidates.Add(lower);
        foreach (var w in candidates)
        {
            var url = "https://fr.wiktionary.org/w/api.php?action=query&prop=extracts&explaintext=1&exsectionformat=plain&exlimit=1&titles="
                + Uri.EscapeDataString(w) + "&format=json&redirects=1";
            var (status, body) = await OnlineHttp.GetAsync(_http, url, OnlineHttp.ShortTimeout, ct).ConfigureAwait(false);
            if ((int)status >= 400) continue;
            var entry = WiktionaryParser.Parse(body);
            if (entry != null) return entry;
        }
        return null;
    }

    private async Task<DictionaryEntry?> LookupEnAsync(string word, CancellationToken ct)
    {
        var url = "https://api.dictionaryapi.dev/api/v2/entries/en/" + Uri.EscapeDataString(word.ToLowerInvariant());
        var (status, body) = await OnlineHttp.GetAsync(_http, url, OnlineHttp.ShortTimeout, ct).ConfigureAwait(false);
        return (int)status >= 400 ? null : DictionaryApiParser.Parse(body);
    }
}

public sealed class TranslateProvider : IResultProvider
{
    private readonly HttpClient _http;
    private readonly Func<AppSettings> _settings;
    public TranslateProvider(HttpClient http, Func<AppSettings> settings) { _http = http; _settings = settings; }
    public string Id => "translate";

    public async Task<ResultPayload> RunAsync(ResultRequest request, CancellationToken ct)
    {
        var s = _settings();
        var email = s.MyMemoryEmail?.Trim();
        var outcome = await TranslateFlow.RunAsync(request.Text, request.TargetLanguage, s.FallbackLanguage,
            async (src, tgt, text, token) =>
            {
                var url = "https://api.mymemory.translated.net/get?q=" + Uri.EscapeDataString(text)
                    + "&langpair=" + Uri.EscapeDataString(src + "|" + tgt);
                if (!string.IsNullOrEmpty(email) && email.Contains('@')) url += "&de=" + Uri.EscapeDataString(email);
                var (status, body) = await OnlineHttp.GetAsync(_http, url, OnlineHttp.ShortTimeout, token).ConfigureAwait(false);
                return status == HttpStatusCode.TooManyRequests
                    ? new MyMemoryResult(null, null, MyMemoryParser.QuotaMessage)
                    : MyMemoryParser.Parse(body);
            }, ct).ConfigureAwait(false);
        if (outcome.Error != null) return ResultPayload.Failure(outcome.Error, "Traduction");
        var from = outcome.Detected != null ? outcome.Detected + " → " : "";
        return new ResultPayload
        {
            Title = "Traduction · " + from + outcome.Target,
            Body = outcome.Text!,
            CanReplace = request.IsEditable && request.Kind == ResultKind.Translate,
            Language = outcome.Target,
            Note = outcome.Truncated ? L.Get("Online_TextShortenedFree") : L.Get("Online_TranslatedBy")
        };
    }
}

public sealed class WikipediaProvider : IResultProvider
{
    private readonly HttpClient _http;
    public WikipediaProvider(HttpClient http) { _http = http; }
    public string Id => "wikipedia";

    public async Task<ResultPayload> RunAsync(ResultRequest request, CancellationToken ct)
    {
        var lang = LanguageCodes.Normalize(request.TargetLanguage, "fr");
        var title = request.Text.Trim().Replace(' ', '_');
        var url = $"https://{lang}.wikipedia.org/api/rest_v1/page/summary/" + Uri.EscapeDataString(title);
        var (status, body) = await OnlineHttp.GetAsync(_http, url, OnlineHttp.ShortTimeout, ct).ConfigureAwait(false);
        var summary = (int)status >= 400 ? null : WikipediaParser.Parse(body);
        if (summary == null) return ResultPayload.Failure(L.Get("Online_NoWikipedia"), request.Text.Trim());
        if (summary.IsDisambiguation) return ResultPayload.Failure(L.Get("Online_Ambiguous"), summary.Title);
        return new ResultPayload
        {
            Title = summary.Title + (summary.Description != null ? " · " + summary.Description : ""),
            Body = summary.Extract, Note = L.Format("Online_SourceWiki", L.Ltr(lang))
        };
    }
}

public sealed class LlmProvider : IResultProvider
{
    private readonly HttpClient _http;
    private readonly Func<AppSettings> _settings;
    private readonly Func<string?> _apiKey;
    private readonly Func<CancellationToken, Task<EngineEnsure>>? _ensureEngine;
    /// <param name="ensureEngine">Démarre à la demande le moteur Ollama installé par SelectBar (null = rien à démarrer).</param>
    public LlmProvider(HttpClient http, Func<AppSettings> settings, Func<string?> apiKey, Func<CancellationToken, Task<EngineEnsure>>? ensureEngine = null)
    { _http = http; _settings = settings; _apiKey = apiKey; _ensureEngine = ensureEngine; }
    public string Id => "llm";

    /// <summary>Distingue deux configurations dans le cache (changer de modèle ne ressert pas l'ancienne réponse).</summary>
    public string CacheScope() { var s = _settings(); return s.LlmProvider + "|" + s.LlmBaseUrl + "|" + s.LlmModel; }

    public async Task<ResultPayload> RunAsync(ResultRequest request, CancellationToken ct)
    {
        var s = _settings();
        if (!LlmPresets.IsConfigured(s))
            return new ResultPayload { Title = "IA", Error = L.Get("Online_NoModel"), NeedsAiSetup = true };
        if (s.LlmProvider == LlmPresets.Ollama && _ensureEngine != null)
        {
            var engine = await _ensureEngine(ct).ConfigureAwait(false);
            if (engine == EngineEnsure.NotInstalled)
                return new ResultPayload { Title = "IA", Error = L.Get("Online_AiNotInstalled"), NeedsAiSetup = true };
            if (engine == EngineEnsure.Failed)   // le moteur n'a pas répondu dans le délai : ce n'est pas « Ollama non lancé »
                return ResultPayload.Failure(L.Get("Online_LocalNotStarted"), "IA");
        }
        var key = _apiKey();
        if (!LlmUrlValidator.Validate(s.LlmBaseUrl, !string.IsNullOrEmpty(key), out var baseUrl, out var urlError))
            return ResultPayload.Failure(urlError ?? "Adresse du serveur invalide.", "IA");

        var user = LlmPrompts.UserMessage(request.Text, out var truncated);
        var transport = LlmTransports.ForProvider(s.LlmProvider);
        using var msg = transport.BuildChat(baseUrl, s.LlmModel, LlmPrompts.SystemPrompt(request.Kind, request.TargetLanguage), user, key,
            LlmPresets.HeadersFor(s.LlmProvider)).ToMessage();

        // IA locale : délai adapté à l'état du modèle (premier appel = chargement en mémoire, jusqu'à 180 s)
        var local = s.LlmProvider == LlmPresets.Ollama;
        var warm = local && LocalAiTiming.Warmth.State(s.LlmModel, DateTime.UtcNow) == ModelWarmState.Warm;
        if (local && !warm) LocalAiTiming.Warmth.BeginWarming(s.LlmModel);
        var timeout = local ? LocalAiTiming.ChatTimeout(warm) : OnlineHttp.LlmTimeout;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        System.Net.HttpStatusCode status; string body;
        try { (status, body) = await OnlineHttp.SendAsync(_http, msg, timeout, ct).ConfigureAwait(false); }
        catch (HttpRequestException)
        {
            if (local) LocalAiTiming.Warmth.Failed(s.LlmModel);
            return new ResultPayload
            {
                Title = "IA",
                Error = local
                    ? L.Get("Online_OllamaNotRunningRetry")
                    : L.Get("Online_ModelServerUnreachable")
            };
        }
        catch (TimeoutException)
        {
            if (local) LocalAiTiming.Warmth.Failed(s.LlmModel);
            if (local) OnlineLog.Write("IA locale : pas de réponse en " + LocalAiTiming.FormatDuration(sw.Elapsed) + " (modèle " + s.LlmModel + ", " + (warm ? "chaud" : "froid") + ")");
            return ResultPayload.Failure(L.Get("Online_ModelSlow"), "IA");
        }

        if ((int)status == 404 && local)   // modèle disparu : on propose l'assistant
        {
            LocalAiTiming.Warmth.Failed(s.LlmModel);
            return new ResultPayload { Title = "IA", Error = L.Format("Online_ModelNoLongerInstalled", s.LlmModel), NeedsAiSetup = true };
        }
        if ((int)status >= 400)
        {
            if (local) LocalAiTiming.Warmth.Failed(s.LlmModel);
            return ResultPayload.Failure(LlmResponseParser.ErrorForStatus((int)status, s.LlmProvider, body), "IA");
        }
        var parsed = transport.ParseChat(body);
        if (parsed.Error != null)
        {
            if (local) LocalAiTiming.Warmth.Failed(s.LlmModel);
            return ResultPayload.Failure(parsed.Error, "IA");
        }
        if (local)
        {
            LocalAiTiming.Warmth.MarkWarm(s.LlmModel, DateTime.UtcNow);
            if (!warm) OnlineLog.Write("première réponse en " + LocalAiTiming.FormatDuration(sw.Elapsed) + " (modèle " + s.LlmModel + ")");
        }
        var title = request.Kind switch
        {
            ResultKind.Summarize => L.Get("Online_Title_Summary"), ResultKind.Explain => L.Get("Online_Title_Explain"),
            ResultKind.Rephrase => L.Get("Online_Title_Rephrase"), ResultKind.Correct => L.Get("Online_Title_Correct"), _ => "IA"
        };
        return new ResultPayload
        {
            Title = title, Body = parsed.Content!,
            CanReplace = request.IsEditable && request.Kind is ResultKind.Summarize or ResultKind.Rephrase or ResultKind.Correct,
            Note = (truncated ? L.Format("Online_TruncatedAt", LlmPrompts.MaxChars) + " " : "") + L.Format("Online_GeneratedBy", s.LlmModel)
        };
    }
}

/// <summary>Repli : Copilot n'a pas d'API, on ne peut que l'ouvrir dans le navigateur (bouton du popup d'erreur).</summary>
public sealed class CopilotWebProvider : IResultProvider
{
    public string Id => "copilot-web";

    public Task<ResultPayload> RunAsync(ResultRequest request, CancellationToken ct) =>
        Task.FromResult(Fallbacks.Attach(ResultPayload.Failure(L.Get("Online_CopilotNoAccess"), "Copilot"), request));
}

public static class Fallbacks
{
    public static CopilotKind CopilotKindFor(ResultKind kind) => kind switch
    {
        ResultKind.Summarize => CopilotKind.Summarize,
        ResultKind.Explain => CopilotKind.Explain,
        ResultKind.Rephrase => CopilotKind.Rephrase,
        ResultKind.Correct => CopilotKind.Correct,
        ResultKind.Translate => CopilotKind.Translate,
        _ => CopilotKind.Ask
    };

    /// <summary>Ajoute à une erreur le bouton « Ouvrir dans le navigateur » : Wiktionnaire pour un mot, sinon Copilot web.</summary>
    public static ResultPayload Attach(ResultPayload p, ResultRequest req)
    {
        if (p.Error == null || p.FallbackUrl != null || p.NeedsAiSetup) return p;
        var text = req.Text.Trim();
        if (req.Kind == ResultKind.Define && ResultRouting.WordCount(text) == 1)
            return p with { FallbackUrl = "https://fr.wiktionary.org/wiki/" + Uri.EscapeDataString(text) };
        var plan = CopilotPrompts.Plan(CopilotKindFor(req.Kind), text);
        return p with { FallbackUrl = plan.Url, FallbackClipboard = plan.NeedsClipboard ? plan.Prompt : null };
    }
}
