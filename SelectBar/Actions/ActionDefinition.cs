using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SelectBar.Actions;

public enum ContextRequirement { Any, EditableOnly, ReadOnlyOnly }

public sealed class ActionDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    /// <summary>Texte court affiché dans la barre (sinon le titre).</summary>
    public string? Icon { get; init; }
    public bool BuiltIn { get; init; }
    /// <summary>Sous-action d'un menu : affichée seulement dans le menu déroulant de l'action parente.</summary>
    public string? ParentId { get; init; }
    public ContextRequirement Context { get; init; } = ContextRequirement.Any;
    public bool RequiresUrl { get; init; }
    public bool RequiresPath { get; init; }
    public bool RequiresClipboardText { get; init; }
    public Regex? TextPattern { get; init; }
    /// <summary>Noms d'exe (ex. "chrome.exe"). Vide = toutes les apps.</summary>
    public IReadOnlyList<string> IncludeApps { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ExcludeApps { get; init; } = Array.Empty<string>();
    public Func<ActionContext, bool>? Predicate { get; init; }
    /// <summary>Action « inline » : texte calculé pour la sélection et affiché dans la barre (null ou vide : action masquée).</summary>
    public Func<ActionContext, string?>? InlineText { get; init; }
    /// <summary>Sous-menu (en-tête seul) : le clic ouvre ses sous-actions au lieu d'exécuter quelque chose.</summary>
    public bool OpensMenu { get; init; }
    public required Func<ActionContext, Task> Execute { get; init; }
}
