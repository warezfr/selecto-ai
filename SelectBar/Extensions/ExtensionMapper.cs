using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SelectBar.Actions;
using SelectBar.Localization;

namespace SelectBar.Extensions;

/// <summary>Convertit les métadonnées d'une extension en ActionDefinition (filtres et titre selon la langue inclus).</summary>
public static class ExtensionMapper
{
    public static bool TryMap(ExtensionMetadata meta, Func<ActionContext, Task> execute, out ActionDefinition? action, out string? error,
        Func<ActionContext, string?>? inlineText = null, bool opensMenu = false)
    {
        action = null; error = null;
        var context = ContextRequirement.Any;
        bool url = false, path = false;
        foreach (var r in meta.Requirements)
        {
            switch (r)
            {
                case "text": break;
                case "paste": context = ContextRequirement.EditableOnly; break;
                case "nopaste": context = ContextRequirement.ReadOnlyOnly; break;
                case "url": url = true; break;
                case "path": path = true; break;
                default: error = $"Exigence inconnue : {r}"; return false;
            }
        }
        Regex? rx = null;
        if (meta.Regex != null)
        {
            try { rx = new Regex(meta.Regex, RegexOptions.None, TimeSpan.FromMilliseconds(200)); }
            catch (ArgumentException ex) { error = "Regex invalide : " + ex.Message; return false; }
        }
        action = new ActionDefinition
        {
            Id = meta.Identifier, Title = TitleFor(meta), Icon = meta.Icon, Context = context,
            ParentId = meta.Parent,
            RequiresUrl = url, RequiresPath = path, TextPattern = rx, IncludeApps = meta.Apps, Execute = execute,
            InlineText = inlineText, OpensMenu = opensMenu
        };
        return true;
    }

    /// <summary>Titre : langue courante, sinon français, sinon @name nu, sinon le premier @name:xx disponible.</summary>
    public static string TitleFor(ExtensionMetadata meta)
    {
        if (meta.Names is { } names)
        {
            if (names.TryGetValue(L.Culture.TwoLetterISOLanguageName, out var title)) return title;
            if (names.TryGetValue("fr", out title)) return title;
        }
        if (!string.IsNullOrEmpty(meta.Name)) return meta.Name;
        return meta.Names?.Values.FirstOrDefault() ?? meta.Name;
    }
}
