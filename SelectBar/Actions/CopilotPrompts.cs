using System;
using SelectBar.Localization;

namespace SelectBar.Actions;

public enum CopilotKind { Ask, Summarize, Explain, Translate, Rephrase, Correct }

/// <summary>Plan d'envoi : Url à ouvrir ; si NeedsClipboard, le Prompt complet doit être copié (texte trop long pour l'URL).</summary>
public sealed record CopilotPlan(string Prompt, string Url, bool NeedsClipboard);

/// <summary>Effets de bord propres à Copilot (implémentés côté Windows, simulés dans les tests).</summary>
public interface ICopilotServices
{
    /// <summary>Le protocole ms-copilot: est-il enregistré (HKCR\ms-copilot) ?</summary>
    bool IsAppInstalled();
    void LaunchApp();
    void Notify(string text);
}

/// <summary>
/// Construction des prompts et de l'URL Copilot. La sélection est une donnée non fiable :
/// elle n'est jamais interprétée, seulement encodée comme paramètre de requête.
/// </summary>
public static class CopilotPrompts
{
    public const string BaseUrl = "https://copilot.microsoft.com/";
    public const int MaxUrlTextChars = 1500;
    public static string ClipboardNotice => L.Get("Copilot_ClipboardNotice");

    public static string Prefix(CopilotKind kind) => kind switch
    {
        CopilotKind.Summarize => "Résume ce texte : ",
        CopilotKind.Explain => "Explique ce texte : ",
        CopilotKind.Translate => "Traduis ce texte en français : ",
        CopilotKind.Rephrase => "Reformule ce texte : ",
        CopilotKind.Correct => "Corrige ce texte : ",
        _ => ""
    };

    public static string BuildPrompt(CopilotKind kind, string text) => Prefix(kind) + (text ?? "").Trim();

    public static CopilotPlan Plan(CopilotKind kind, string text)
    {
        var t = (text ?? "").Trim();
        var prompt = BuildPrompt(kind, t);
        if (t.Length <= MaxUrlTextChars)
            return new CopilotPlan(prompt, BaseUrl + "?q=" + Uri.EscapeDataString(prompt), false);
        return new CopilotPlan(prompt, BaseUrl, true);
    }
}
