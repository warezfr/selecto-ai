using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SelectBar.Localization;

namespace SelectBar.Online;

public enum OllamaStatus { Absent, NoModels, Ready }
public sealed record OllamaState(OllamaStatus Status, IReadOnlyList<OllamaModel> Models);

/// <summary>Détection d'Ollama (GET /api/tags, 1,5 s) et téléchargement d'un modèle (POST /api/pull, sur clic explicite seulement).</summary>
public sealed class OllamaClient
{
    private readonly HttpClient _http;
    private readonly string _host;
    public OllamaClient(HttpClient http, string host = LlmPresets.OllamaHost) { _http = http; _host = host.TrimEnd('/'); }

    public async Task<OllamaState> DetectAsync(CancellationToken ct)
    {
        try
        {
            var (status, body) = await OnlineHttp.GetAsync(_http, _host + "/api/tags", TimeSpan.FromMilliseconds(1500), ct).ConfigureAwait(false);
            if ((int)status >= 400) return new OllamaState(OllamaStatus.Absent, Array.Empty<OllamaModel>());
            var models = OllamaParsers.ParseTags(body);
            return new OllamaState(models.Count == 0 ? OllamaStatus.NoModels : OllamaStatus.Ready, models);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return new OllamaState(OllamaStatus.Absent, Array.Empty<OllamaModel>()); }
    }

    /// <summary>Supprime un modèle (DELETE /api/delete) ; renvoie null si c'est fait, sinon un message d'erreur en français.</summary>
    public async Task<string?> DeleteModelAsync(string model, CancellationToken ct)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new { model, name = model });
            using var req = new HttpRequestMessage(HttpMethod.Delete, _host + "/api/delete") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            var (status, _) = await OnlineHttp.SendAsync(_http, req, OnlineHttp.ShortTimeout, ct).ConfigureAwait(false);
            return (int)status < 400 ? null : L.Get("Online_ModelDeleteFailed");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            OnlineLog.Write("Suppression du modèle : " + ex.Message);
            return L.Get("Online_EngineNotResponding");
        }
    }

    /// <summary>Télécharge le modèle en suivant la progression ; renvoie null si terminé, sinon un message d'erreur en français.</summary>
    public async Task<string?> PullAsync(string model, Action<PullProgress> report, CancellationToken ct)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new { model, name = model, stream = true });
            using var req = new HttpRequestMessage(HttpMethod.Post, _host + "/api/pull") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return OllamaParsers.PullError(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var finished = false;
            string? line;
            while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) != null)
            {
                var p = OllamaParsers.ParsePullLine(line);
                if (p == null) continue;
                if (p.Error != null) return OllamaParsers.PullError(p.Error);
                report(p);
                if (p.Done) finished = true;
            }
            return finished ? null : L.Get("Online_DownloadInterrupted");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            OnlineLog.Write("Téléchargement du modèle : " + ex.Message);
            return L.Get("Online_OllamaNotRunning");
        }
    }
}

public sealed class OpenRouterClient
{
    private readonly HttpClient _http;
    public OpenRouterClient(HttpClient http) { _http = http; }

    /// <summary>Modèles gratuits ; liste de secours (Fallback=true) si le réseau échoue.</summary>
    public async Task<(IReadOnlyList<OpenRouterModel> Models, bool Fallback)> FreeModelsAsync(CancellationToken ct)
    {
        try
        {
            var (status, body) = await OnlineHttp.GetAsync(_http, LlmPresets.OpenRouterUrl + "/models", OnlineHttp.ShortTimeout, ct).ConfigureAwait(false);
            if ((int)status < 400)
            {
                var list = OpenRouterParsers.ParseFreeModels(body);
                if (list.Count > 0) return (list, false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { OnlineLog.Write("Liste des modèles OpenRouter : " + ex.Message); }
        return (OpenRouterParsers.Fallback, true);
    }
}
