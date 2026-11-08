using System;
using System.Collections.Generic;
using SelectBar.Settings;
using SelectBar.Localization;

namespace SelectBar.Online;

/// <summary>Protocole d'un fournisseur : « compatible OpenAI » (/chat/completions) ou API native d'Anthropic (/messages).</summary>
public enum LlmTransportKind { OpenAiCompatible, Anthropic }

public sealed record LlmPreset(string Id, string Label, string BaseUrl, bool NeedsKey, IReadOnlyDictionary<string, string> Headers,
    LlmTransportKind Transport = LlmTransportKind.OpenAiCompatible, string? KeysPage = null)
{
    /// <summary>Nom court du service pour les messages (« Le texte sélectionné est envoyé à OpenAI. »).</summary>
    public string ServiceName => Label;
}

/// <summary>Préréglages de l'assistant « IA » : tout est prérempli, l'utilisateur ne tape rien dans les cas courants.</summary>
public static class LlmPresets
{
    public const string None = "none", Ollama = "ollama", Custom = "custom";
    public const string OpenAi = "openai", Anthropic = "anthropic", Gemini = "gemini", Mistral = "mistral", Groq = "groq",
        DeepSeek = "deepseek", Xai = "xai", Together = "together", OpenRouter = "openrouter";
    public const string OllamaUrl = "http://localhost:11434/v1";
    public const string OllamaHost = "http://localhost:11434";
    public const string OpenRouterUrl = "https://openrouter.ai/api/v1";
    public const string OpenRouterKeysPage = "https://openrouter.ai/keys";

    private static readonly IReadOnlyDictionary<string, string> NoHeaders = new Dictionary<string, string>();
    private static readonly IReadOnlyDictionary<string, string> OpenRouterHeaders = new Dictionary<string, string> { ["X-Title"] = "Selecto AI" };

    public static readonly LlmPreset OllamaPreset = new(Ollama, "Sur mon PC (Ollama)", OllamaUrl, false, NoHeaders);
    public static readonly LlmPreset OpenRouterPreset = new(OpenRouter, "OpenRouter", OpenRouterUrl, true, OpenRouterHeaders, LlmTransportKind.OpenAiCompatible, OpenRouterKeysPage);
    public static readonly LlmPreset CustomPreset = new(Custom, "Autre (compatible OpenAI)", "", false, NoHeaders);

    /// <summary>Fournisseurs en ligne proposés, dans l'ordre d'affichage (URL de base vérifiées : GET {base}/models répond 401/403 sans clé).</summary>
    public static readonly IReadOnlyList<LlmPreset> Online = new List<LlmPreset>
    {
        new(OpenAi, "OpenAI", "https://api.openai.com/v1", true, NoHeaders, LlmTransportKind.OpenAiCompatible, "https://platform.openai.com/api-keys"),
        new(Anthropic, "Anthropic (Claude)", "https://api.anthropic.com/v1", true, NoHeaders, LlmTransportKind.Anthropic, "https://console.anthropic.com/settings/keys"),
        new(Gemini, "Google Gemini", "https://generativelanguage.googleapis.com/v1beta/openai", true, NoHeaders, LlmTransportKind.OpenAiCompatible, "https://aistudio.google.com/apikey"),
        new(Mistral, "Mistral", "https://api.mistral.ai/v1", true, NoHeaders, LlmTransportKind.OpenAiCompatible, "https://console.mistral.ai/api-keys"),
        new(Groq, "Groq", "https://api.groq.com/openai/v1", true, NoHeaders, LlmTransportKind.OpenAiCompatible, "https://console.groq.com/keys"),
        new(DeepSeek, "DeepSeek", "https://api.deepseek.com/v1", true, NoHeaders, LlmTransportKind.OpenAiCompatible, "https://platform.deepseek.com/api_keys"),
        new(Xai, "xAI Grok", "https://api.x.ai/v1", true, NoHeaders, LlmTransportKind.OpenAiCompatible, "https://console.x.ai"),
        new(Together, "Together AI", "https://api.together.xyz/v1", true, NoHeaders, LlmTransportKind.OpenAiCompatible, "https://api.together.xyz/settings/api-keys"),
        OpenRouterPreset,
        CustomPreset
    };

    public static LlmPreset? Get(string? id)
    {
        var key = (id ?? "").ToLowerInvariant();
        if (key == Ollama) return OllamaPreset;
        foreach (var p in Online)
            if (p.Id == key) return p;
        return null;
    }

    /// <summary>En-têtes supplémentaires du fournisseur choisi (OpenRouter : X-Title).</summary>
    public static IReadOnlyDictionary<string, string> HeadersFor(string? providerId) => Get(providerId)?.Headers ?? NoHeaders;

    /// <summary>« Le texte sélectionné est envoyé à OpenAI. » (note de confidentialité affichée à l'activation).</summary>
    public static string PrivacyNotice(string? providerId)
    {
        var p = Get(providerId);
        if (p == null || p.Id == Ollama) return L.Get("Online_Privacy_Local");
        return L.Format("Online_Privacy_Sent", p.Id == Custom ? L.Get("Online_ThisServer") : p.ServiceName);
    }

    /// <summary>Applique un préréglage aux réglages (URL prérempli ; modèle remis à zéro et clé du fournisseur rechargée si le fournisseur change).</summary>
    public static void Apply(AppSettings s, string providerId)
    {
        var p = Get(providerId);
        if (p == null) { s.LlmProvider = None; return; }
        var changed = !string.Equals(s.LlmProvider, p.Id, StringComparison.OrdinalIgnoreCase);
        if (changed)
        {
            s.LlmModel = "";
            ProviderKeys.Switch(s, p.Id);
        }
        s.LlmProvider = p.Id;
        if (p.Id == Custom) { if (changed || string.IsNullOrWhiteSpace(s.LlmBaseUrl)) s.LlmBaseUrl = ""; }
        else s.LlmBaseUrl = p.BaseUrl;
    }

    /// <summary>Un modèle est-il prêt à l'emploi (fournisseur, URL valide, modèle, et clé si le service en demande une) ?</summary>
    public static bool IsConfigured(AppSettings s)
    {
        var p = Get(s.LlmProvider);
        if (p == null || string.IsNullOrWhiteSpace(s.LlmModel)) return false;
        if (!LlmUrlValidator.Validate(s.LlmBaseUrl, !string.IsNullOrEmpty(s.LlmApiKeyProtected), out _, out _)) return false;
        return !p.NeedsKey || !string.IsNullOrEmpty(s.LlmApiKeyProtected);
    }
}

/// <summary>Une clé API PAR fournisseur, chiffrée (DPAPI) : changer de fournisseur ne perd pas la clé des autres.</summary>
public static class ProviderKeys
{
    private static bool Keyed(string? id) => !string.IsNullOrEmpty(id) && id != LlmPresets.None && id != LlmPresets.Ollama;

    /// <summary>Clé chiffrée enregistrée pour ce fournisseur (la clé active si c'est le fournisseur actif), ou null.</summary>
    public static string? Get(AppSettings s, string providerId)
    {
        if (string.Equals(s.LlmProvider, providerId, StringComparison.OrdinalIgnoreCase)) return string.IsNullOrEmpty(s.LlmApiKeyProtected) ? null : s.LlmApiKeyProtected;
        return s.LlmApiKeys.TryGetValue(providerId, out var k) && !string.IsNullOrEmpty(k) ? k : null;
    }

    /// <summary>Enregistre la clé chiffrée d'un fournisseur (null = l'oublier) ; si c'est le fournisseur actif, c'est aussi la clé active.</summary>
    public static void Set(AppSettings s, string providerId, string? protectedKey)
    {
        if (!Keyed(providerId)) return;
        if (string.IsNullOrEmpty(protectedKey)) s.LlmApiKeys.Remove(providerId); else s.LlmApiKeys[providerId] = protectedKey;
        if (string.Equals(s.LlmProvider, providerId, StringComparison.OrdinalIgnoreCase))
            s.LlmApiKeyProtected = string.IsNullOrEmpty(protectedKey) ? null : protectedKey;
    }

    /// <summary>Changement de fournisseur actif : met la clé de l'ancien de côté et charge celle du nouveau.</summary>
    public static void Switch(AppSettings s, string newProviderId)
    {
        if (Keyed(s.LlmProvider) && !string.IsNullOrEmpty(s.LlmApiKeyProtected)) s.LlmApiKeys[s.LlmProvider] = s.LlmApiKeyProtected!;
        s.LlmApiKeyProtected = Keyed(newProviderId) && s.LlmApiKeys.TryGetValue(newProviderId, out var k) && !string.IsNullOrEmpty(k) ? k : null;
    }
}

public static class LlmUrlValidator
{
    /// <summary>URL de base http(s) sans identifiants, requête ni fragment ; une clé ne part jamais en clair vers un serveur distant.</summary>
    public static bool Validate(string? url, bool hasKey, out string normalized, out string? error)
    {
        normalized = "";
        error = null;
        var u = (url ?? "").Trim();
        if (u.Length == 0) { error = L.Get("Online_Cfg_AddressMissing"); return false; }
        if (!Uri.TryCreate(u, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        { error = L.Get("Online_Cfg_AddressScheme"); return false; }
        if (uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
        { error = L.Get("Online_Cfg_AddressCreds"); return false; }
        if (hasKey && uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback)
        { error = L.Get("Online_Cfg_HttpsRequired"); return false; }
        normalized = u.TrimEnd('/');
        return true;
    }
}

/// <summary>Chiffrement de la clé API (DPAPI côté Windows, remplaçable dans les tests).</summary>
public interface ISecretProtector
{
    byte[] Protect(byte[] data);
    byte[] Unprotect(byte[] data);
}

public sealed class ApiKeyVault
{
    private readonly ISecretProtector _protector;
    public ApiKeyVault(ISecretProtector protector) { _protector = protector; }

    /// <summary>Texte chiffré (base64) à stocker, ou null si la clé est vide.</summary>
    public string? Protect(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return Convert.ToBase64String(_protector.Protect(System.Text.Encoding.UTF8.GetBytes(key.Trim())));
    }

    /// <summary>Clé en clair, ou null si absente ou illisible (autre compte Windows, fichier modifié).</summary>
    public string? Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return null;
        try { return System.Text.Encoding.UTF8.GetString(_protector.Unprotect(Convert.FromBase64String(stored))); }
        catch (Exception) { return null; }
    }
}
