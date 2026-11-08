using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SelectBar.Settings;
using SelectBar.Localization;

namespace SelectBar.Online;

/// <summary>Choix du ou des fournisseurs selon la demande (pur).</summary>
public static class ResultRouting
{
    public static int WordCount(string text) =>
        (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>Fournisseurs essayés dans l'ordre jusqu'au premier succès.</summary>
    public static IReadOnlyList<string> Chain(ResultRequest r) => r.Kind switch
    {
        ResultKind.Define => WordCount(r.Text) == 1 ? new[] { "dictionary" } : new[] { "translate" },
        ResultKind.Translate => new[] { "translate" },
        ResultKind.Explain => WordCount(r.Text) <= 3 ? new[] { "wikipedia", "llm" } : new[] { "llm" },
        _ => new[] { "llm" }
    };
}

/// <summary>Exécute une demande : routage, cache LRU, erreurs converties en messages lisibles (jamais d'exception vers l'interface).</summary>
public sealed class ResultService
{
    private readonly Dictionary<string, IResultProvider> _providers;
    private readonly LlmProvider _llm;
    private readonly LruCache<string, ResultPayload> _cache = new(200);

    public ResultService(Func<AppSettings> settings, HttpClient http, Func<string?> apiKey, Func<CancellationToken, Task<EngineEnsure>>? ensureEngine = null)
    {
        _llm = new LlmProvider(http, settings, apiKey, ensureEngine);
        _providers = new IResultProvider[]
        {
            new DictionaryProvider(http, settings), new TranslateProvider(http, settings),
            new WikipediaProvider(http), _llm, new CopilotWebProvider()
        }.ToDictionary(p => p.Id);
    }

    public async Task<ResultPayload> RunAsync(ResultRequest request, CancellationToken ct)
    {
        request = request with
        {
            Text = (request.Text ?? "").Trim(),
            TargetLanguage = LanguageCodes.Normalize(request.TargetLanguage, "fr")
        };
        ResultPayload? last = null;
        foreach (var id in ResultRouting.Chain(request))
        {
            var p = await RunOneAsync(id, request, useCache: true, ct).ConfigureAwait(false);
            if (p.Error == null) return p;
            last = p;
        }
        return Fallbacks.Attach(last ?? ResultPayload.Failure(L.Get("Online_NothingToShow")), request);
    }

    /// <summary>Ping minimal du modèle configuré (bouton « Tester la connexion »), sans cache.</summary>
    public async Task<ResultPayload> TestLlmAsync(CancellationToken ct) =>
        await RunOneAsync("llm", new ResultRequest { Text = "ping", Kind = ResultKind.Rephrase }, useCache: false, ct).ConfigureAwait(false);

    private async Task<ResultPayload> RunOneAsync(string id, ResultRequest req, bool useCache, CancellationToken ct)
    {
        if (!_providers.TryGetValue(id, out var provider)) return ResultPayload.Failure("Service inconnu.");
        var key = id + "|" + (id == "llm" ? _llm.CacheScope() : "") + "|" + req.Kind + "|" + req.TargetLanguage + "|" + req.IsEditable + "|" + req.Text;
        if (useCache && _cache.TryGet(key, out var hit)) return hit;
        try
        {
            var p = await provider.RunAsync(req, ct).ConfigureAwait(false);
            if (p.Error == null && useCache) _cache.Set(key, p);
            return p;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (TimeoutException) { return ResultPayload.Failure(L.Get("Online_ErrServiceSlow")); }
        catch (HttpRequestException ex)
        {
            OnlineLog.Write($"Service {id} injoignable : {ex.Message}");
            return ResultPayload.Failure(L.Get("Online_ErrUnreachable"));
        }
        catch (Exception ex)
        {
            OnlineLog.Write($"Service {id} : {ex}");
            return ResultPayload.Failure(L.Get("Online_ErrGeneric"));
        }
    }
}
