using System;

namespace SelectBar.Support;

/// <summary>Adresse du serveur de signalements (« Signaler un problème ») : constante unique, surchargeable par SELECTO_API_URL pour les essais en local.</summary>
public static class FeedbackServer
{
    public const string BaseUrl = "https://selecto-api.span.observer";
    public const string EnvironmentVariable = "SELECTO_API_URL";

    /// <summary>Ticket de support « Signaler un problème ».</summary>
    public const string FeedbackPath = "/v1/feedback";

    /// <summary>
    /// URL de base sans « / » final. La variable d'environnement n'est prise en compte que si c'est une adresse https, ou http vers
    /// la machine locale (localhost, 127.0.0.1, ::1) : une adresse http distante est ignorée.
    /// </summary>
    public static string ResolveBaseUrl(Func<string, string?>? getEnvironment = null)
    {
        var raw = getEnvironment?.Invoke(EnvironmentVariable)?.Trim();
        if (!string.IsNullOrEmpty(raw) && Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
            && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
            return raw.TrimEnd('/');
        return BaseUrl;
    }
}
