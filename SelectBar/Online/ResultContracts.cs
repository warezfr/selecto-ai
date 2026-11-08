using System.Threading;
using System.Threading.Tasks;

namespace SelectBar.Online;

public enum ResultKind { Define, Translate, Explain, Summarize, Rephrase, Correct }

/// <summary>Ce que l'utilisateur demande : le texte est une donnée, jamais une consigne.</summary>
public sealed record ResultRequest
{
    public string Text { get; init; } = "";
    public string TargetLanguage { get; init; } = "fr";
    public bool IsEditable { get; init; }
    public ResultKind Kind { get; init; }
}

/// <summary>Contenu du popup : résultat, ou erreur (Error) avec ses solutions de repli.</summary>
public sealed record ResultPayload
{
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public bool CanReplace { get; init; }
    public string? Note { get; init; }
    public string? Error { get; init; }
    /// <summary>Repli : page web à ouvrir (bouton « Ouvrir dans le navigateur »).</summary>
    public string? FallbackUrl { get; init; }
    /// <summary>Repli : texte à copier avant d'ouvrir la page (prompt trop long pour l'URL).</summary>
    public string? FallbackClipboard { get; init; }
    /// <summary>Aucun modèle IA configuré : le popup propose d'ouvrir l'assistant (Ollama / OpenRouter).</summary>
    public bool NeedsAiSetup { get; init; }
    /// <summary>Code de la langue cible affichée (traduction) ; null = pas de sélecteur de langue.</summary>
    public string? Language { get; init; }

    public static ResultPayload Failure(string error, string title = "") => new() { Title = title, Error = error };
}

public interface IResultProvider
{
    string Id { get; }
    Task<ResultPayload> RunAsync(ResultRequest request, CancellationToken ct);
}
