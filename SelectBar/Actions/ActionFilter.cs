using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SelectBar.Settings;

namespace SelectBar.Actions;

/// <summary>Filtrage des actions par contexte (éditable ou non), type de texte, regex et application.</summary>
public static class ActionFilter
{
    public static bool Matches(ActionDefinition a, ActionContext ctx)
    {
        if (a.Context == ContextRequirement.EditableOnly && !ctx.IsEditable) return false;
        if (a.Context == ContextRequirement.ReadOnlyOnly && ctx.IsEditable) return false;
        if (a.RequiresUrl && !ctx.IsUrl) return false;
        if (a.RequiresPath && !ctx.IsPath) return false;
        if (a.RequiresClipboardText && !ctx.ClipboardHasText) return false;

        var exe = ctx.AppExe == null ? null : ExcludedApps.Normalize(ctx.AppExe);
        if (a.IncludeApps.Count > 0 && (exe == null || !a.IncludeApps.Any(x => ExcludedApps.Normalize(x) == exe))) return false;
        if (exe != null && a.ExcludeApps.Any(x => ExcludedApps.Normalize(x) == exe)) return false;

        if (a.TextPattern != null)
        {
            try { if (!a.TextPattern.IsMatch(ctx.ProcessText)) return false; }
            catch (RegexMatchTimeoutException) { return false; }
        }
        if (a.Predicate != null && !a.Predicate(ctx)) return false;
        return true;
    }

    /// <summary>Actions visibles, désactivées retirées, ordonnées selon les réglages.</summary>
    public static List<ActionDefinition> Select(IEnumerable<ActionDefinition> all, ActionContext ctx, AppSettings settings)
    {
        var disabled = new HashSet<string>(settings.DisabledActions, StringComparer.OrdinalIgnoreCase);
        var list = Order(all, settings).Where(a => !disabled.Contains(a.Id) && Matches(a, ctx)).ToList();
        // une sous-action n'existe que si son parent est affiché
        var ids = new HashSet<string>(list.Where(a => a.ParentId == null).Select(a => a.Id), StringComparer.OrdinalIgnoreCase);
        return list.Where(a => a.ParentId == null || ids.Contains(a.ParentId)).ToList();
    }

    public static List<ActionDefinition> Order(IEnumerable<ActionDefinition> all, AppSettings settings)
    {
        var order = settings.ActionOrder;
        return all.Select((a, i) => (a, i))
                  .OrderBy(t => { var k = IndexOf(order, t.a.Id); return k < 0 ? int.MaxValue : k; })
                  .ThenBy(t => t.i)
                  .Select(t => t.a)
                  .ToList();
    }

    private static int IndexOf(List<string> order, string id)
    {
        for (var i = 0; i < order.Count; i++)
            if (string.Equals(order[i], id, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }
}
