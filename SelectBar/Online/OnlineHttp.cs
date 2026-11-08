using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SelectBar.Online;

/// <summary>Journal des services en ligne (branché sur Log.Write par l'application).</summary>
public static class OnlineLog
{
    public static Action<string> Sink { get; set; } = _ => { };
    public static void Write(string message) { try { Sink(message); } catch { } }
}

public static class OnlineHttp
{
    public static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan LlmTimeout = TimeSpan.FromSeconds(60);

    /// <summary>Un seul HttpClient pour toute l'application ; Wikimedia refuse les requêtes sans User-Agent.</summary>
    public static readonly HttpClient Client = Create();

    private static HttpClient Create()
    {
        var c = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgentFor(AppVersion()));
        return c;
    }

    /// <summary>« SelectoAI/3.0.0 » : produit sans espace + version à trois chiffres.</summary>
    public static string UserAgentFor(Version? version) => "SelectoAI/" + Three(version);

    private static string Three(Version? v)
    {
        v ??= AssemblyVersion();
        return $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}";
    }

    /// <summary>Version de l'application (assembly exécutable), telle que lue à l'exécution.</summary>
    public static string VersionText() => Three(AppVersion());

    private static Version? AppVersion() => System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;

    /// <summary>Version de l'assembly de ce code (repli si l'assembly d'entrée est inconnu).</summary>
    private static Version AssemblyVersion() => typeof(OnlineHttp).Assembly.GetName().Version ?? new Version(0, 0, 0);

    /// <summary>Envoie la requête avec un délai propre ; lève TimeoutException si le délai est dépassé (pas si l'appelant annule).</summary>
    public static async Task<(HttpStatusCode Status, string Body)> SendAsync(HttpClient http, HttpRequestMessage request, TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            using var resp = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            return (resp.StatusCode, body);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("Délai dépassé");
        }
    }

    public static Task<(HttpStatusCode Status, string Body)> GetAsync(HttpClient http, string url, TimeSpan timeout, CancellationToken ct) =>
        SendAsync(http, new HttpRequestMessage(HttpMethod.Get, url), timeout, ct);
}
