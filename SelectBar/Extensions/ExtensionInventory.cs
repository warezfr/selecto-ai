using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SelectBar.Extensions;

/// <summary>
/// Extension de premier niveau installée dans le dossier utilisateur. <see cref="Title"/> et <see cref="Description"/>
/// sont déjà dans la langue demandée ; <see cref="ActionCount"/> compte les sous-actions ; <see cref="IsMenu"/> : en-tête seul (sous-menu).
/// </summary>
public sealed record InstalledExtension(
    string Id, string Title, string? Description, bool EnabledByDefault, int ActionCount, bool IsMenu);

/// <summary>
/// Inventaire pur des extensions utilisateur : lit les seuls en-têtes d'un dossier, sans charger ni exécuter de code.
/// Suit les mêmes règles que <see cref="ExtensionLoader"/> (un fichier de même identifiant remplace le précédent ;
/// un sous-menu sans sous-action n'apparaît pas) ; les sous-actions ne figurent pas, elles sont comptées.
/// </summary>
public static class ExtensionInventory
{
    public static IReadOnlyList<InstalledExtension> List(string dir, string lang)
    {
        var result = new List<InstalledExtension>();
        if (!Directory.Exists(dir)) return result;

        var chosen = new Dictionary<string, (ExtensionMetadata Meta, bool HasCode)>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var file in Directory.GetFiles(dir, "*.js").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var source = File.ReadAllText(file);
                if (!ExtensionHeaderParser.TryParse(source, out var meta, out _)) continue;
                if (!chosen.ContainsKey(meta!.Identifier)) order.Add(meta.Identifier);
                chosen[meta.Identifier] = (meta, ExtensionHeaderParser.HasCode(source));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // fichier illisible : ignoré, comme le chargeur
            }
        }

        var children = chosen.Values
            .Where(c => c.Meta.Parent != null)
            .GroupBy(c => c.Meta.Parent!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        foreach (var id in order)
        {
            var (meta, hasCode) = chosen[id];
            if (meta.Parent != null) continue;
            var count = children.GetValueOrDefault(id);
            var isMenu = !hasCode;
            if (isMenu && count == 0) continue;
            result.Add(new InstalledExtension(id, meta.TitleFor(lang), meta.DescriptionFor(lang), meta.EnabledByDefault, count, isMenu));
        }
        return result;
    }
}
