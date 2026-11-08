using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SelectBar.Extensions;

/// <summary>
/// Métadonnées d'une extension. <see cref="Names"/> : titre par code de langue (fr, en, es, ar) ;
/// <see cref="Parent"/> : identifiant de l'extension qui la contient (sous-menu) ;
/// <see cref="EnabledByDefault"/> : vrai sauf « @enabled-by-default false » ; <see cref="Inline"/> : résultat affiché dans la barre ;
/// <see cref="Description"/> et <see cref="Descriptions"/> : descriptif par défaut et par code de langue (@description, @description:xx).
/// </summary>
public sealed record ExtensionMetadata(
    string Name, string Identifier, string? Icon, string? Regex,
    IReadOnlyList<string> Requirements, IReadOnlyList<string> Apps,
    IReadOnlyDictionary<string, string>? Names = null, string? Parent = null,
    bool EnabledByDefault = true, bool Inline = false,
    string? Description = null, IReadOnlyDictionary<string, string>? Descriptions = null)
{
    /// <summary>Titre dans la langue demandée, sinon le français, sinon le titre par défaut.</summary>
    public string TitleFor(string lang) => Pick(Names, lang) ?? Name;

    /// <summary>Descriptif dans la langue demandée, sinon le français, sinon le descriptif par défaut (null si absent).</summary>
    public string? DescriptionFor(string lang) => Pick(Descriptions, lang) ?? Description;

    private static string? Pick(IReadOnlyDictionary<string, string>? values, string lang)
    {
        if (values == null) return null;
        if (values.TryGetValue(lang.ToLowerInvariant(), out var v) && v.Length > 0) return v;
        return values.TryGetValue("fr", out var fr) && fr.Length > 0 ? fr : null;
    }
}

/// <summary>
/// En-tête attendu :
/// // ==SelectoExtension==  (l'ancien marqueur ==SelectBarExtension== reste accepté)
/// // @name Mon action
/// // @name:en My action            (titre par langue : fr, en, es, ar)
/// // @description Ce que fait l'action, en une phrase.     (facultatif, 200 caractères au plus)
/// // @description:en What the action does, in one sentence. (descriptif par langue, même repli que @name)
/// // @identifier com.exemple.action
/// // @parent ai.selecto.builtin.text
/// // @icon Aa
/// // @regex ^.{1,500}$
/// // @requirements text, paste
/// // @apps chrome.exe
/// // @enabled-by-default true
/// // @inline true
/// // ==/SelectoExtension==
/// </summary>
public static class ExtensionHeaderParser
{
    /// <summary>Longueur maximale d'un descriptif ; au-delà, il est tronqué avec « … ».</summary>
    public const int MaxDescriptionLength = 200;

    private static readonly Regex Start = new(@"^\s*//\s*==(?:Selecto|SelectBar)Extension==\s*$", RegexOptions.Compiled);
    private static readonly Regex End = new(@"^\s*//\s*==/(?:Selecto|SelectBar)Extension==\s*$", RegexOptions.Compiled);
    private static readonly Regex Field = new(@"^\s*//\s*@(?<k>[A-Za-z][A-Za-z-]*(?::[A-Za-z]{2})?)\s+(?<v>.*?)\s*$", RegexOptions.Compiled);

    public static bool TryParse(string source, out ExtensionMetadata? metadata, out string? error)
    {
        metadata = null; error = null;
        var lines = source.Replace("\r\n", "\n").Split('\n');
        var begin = Array.FindIndex(lines, l => Start.IsMatch(l));
        if (begin < 0) { error = "En-tête ==SelectoExtension== introuvable."; return false; }
        var finish = Array.FindIndex(lines, begin + 1, l => End.IsMatch(l));
        if (finish < 0) { error = "En-tête non terminé (==/SelectoExtension== manquant)."; return false; }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = begin + 1; i < finish; i++)
        {
            var m = Field.Match(lines[i]);
            if (m.Success) map[m.Groups["k"].Value] = m.Groups["v"].Value;
        }
        var names = LocalizedValues(map, "name");

        // @name facultatif (spec §5.3) : sans @name nu, le titre par défaut est @name:fr, sinon le premier @name:xx.
        var name = map.GetValueOrDefault("name");
        if (string.IsNullOrEmpty(name))
            name = names.TryGetValue("fr", out var fr) ? fr : names.Values.FirstOrDefault();
        if (string.IsNullOrEmpty(name)) { error = "Champ @name manquant."; return false; }
        if (!map.TryGetValue("identifier", out var id) || id.Length == 0) { error = "Champ @identifier manquant."; return false; }

        // @description facultatif : même repli que @name (nu, puis fr, puis le premier @description:xx).
        var descriptions = LocalizedValues(map, "description");
        var description = map.GetValueOrDefault("description");
        if (string.IsNullOrEmpty(description))
            description = descriptions.TryGetValue("fr", out var dfr) ? dfr : descriptions.Values.FirstOrDefault();
        description = string.IsNullOrEmpty(description) ? null : Truncate(description);
        foreach (var key in descriptions.Keys.ToList()) descriptions[key] = Truncate(descriptions[key]);

        map.TryGetValue("regex", out var rx);
        if (rx is { Length: > 2 } && rx[0] == '/' && rx[^1] == '/') rx = rx[1..^1];
        map.TryGetValue("icon", out var icon);

        map.TryGetValue("parent", out var parent);
        var enabledByDefault = !(map.TryGetValue("enabled-by-default", out var ebd) && ebd.Equals("false", StringComparison.OrdinalIgnoreCase));
        var inline = map.TryGetValue("inline", out var inl) && inl.Equals("true", StringComparison.OrdinalIgnoreCase);

        metadata = new ExtensionMetadata(name, id, string.IsNullOrEmpty(icon) ? null : icon,
            string.IsNullOrEmpty(rx) ? null : rx,
            SplitList(map.GetValueOrDefault("requirements")), SplitList(map.GetValueOrDefault("apps")),
            names, string.IsNullOrWhiteSpace(parent) ? null : parent, enabledByDefault, inline,
            description, descriptions);
        return true;
    }

    /// <summary>Valeurs « @champ:xx » (xx = code de langue) indexées par ce code, en minuscules.</summary>
    private static Dictionary<string, string> LocalizedValues(Dictionary<string, string> map, string field)
    {
        var prefix = field + ":";
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in map)
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && value.Length > 0)
                result[key[prefix.Length..].ToLowerInvariant()] = value;
        return result;
    }

    /// <summary>Tronque un texte à <see cref="MaxDescriptionLength"/> caractères (ellipse incluse).</summary>
    public static string Truncate(string text)
    {
        var t = text.Trim();
        return t.Length <= MaxDescriptionLength ? t : t[..(MaxDescriptionLength - 1)].TrimEnd() + "…";
    }

    /// <summary>Vrai si le source contient du code (sinon c'est un sous-menu : en-tête seul).</summary>
    public static bool HasCode(string source) =>
        source.Split('\n').Any(line =>
        {
            var t = line.Trim();
            return t.Length > 0 && !t.StartsWith("//", StringComparison.Ordinal);
        });

    private static IReadOnlyList<string> SplitList(string? v) =>
        string.IsNullOrWhiteSpace(v)
            ? Array.Empty<string>()
            : v.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(s => s.ToLowerInvariant()).ToList();
}
