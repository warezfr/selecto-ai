using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Automation;

namespace SelectBar.Selection;

public sealed record UiaResult(UiaOutcome Outcome, string? Text, bool IsEditable);

/// <summary>Lecture de la sélection via UI Automation (TextPattern). À appeler hors thread UI.</summary>
public static class UiaSelectionReader
{
    public const int MaxDepth = 5;
    public const int MaxElements = 100;

    public static UiaResult Read(int x, int y)
    {
        try
        {
            AutomationElement? focused = null, atPoint = null;
            try { focused = AutomationElement.FocusedElement; } catch { }
            try { atPoint = AutomationElement.FromPoint(new System.Windows.Point(x, y)); } catch { }

            var editable = IsEditable(focused);
            var sawPattern = false;
            foreach (var root in new[] { focused, atPoint }.Where(e => e != null).Cast<AutomationElement>())
            {
                var budget = MaxElements;
                var r = Search(root, MaxDepth, ref budget, ref sawPattern);
                if (!string.IsNullOrWhiteSpace(r)) return new UiaResult(UiaOutcome.Text, r, editable);
            }
            return new UiaResult(sawPattern ? UiaOutcome.EmptySelection : UiaOutcome.NoTextPattern, null, editable);
        }
        catch
        {
            return new UiaResult(UiaOutcome.Error, null, false);
        }
    }

    // Parcours en largeur limité : profondeur 5, 100 éléments max.
    private static string? Search(AutomationElement root, int maxDepth, ref int budget, ref bool sawPattern)
    {
        var level = new List<AutomationElement> { root };
        for (var depth = 0; depth <= maxDepth && level.Count > 0 && budget > 0; depth++)
        {
            var next = new List<AutomationElement>();
            foreach (var el in level)
            {
                if (budget-- <= 0) break;
                var text = TryGetSelection(el, ref sawPattern);
                if (!string.IsNullOrWhiteSpace(text)) return text;
                if (depth == maxDepth) continue;
                try
                {
                    var child = TreeWalker.ControlViewWalker.GetFirstChild(el);
                    while (child != null && next.Count < MaxElements)
                    {
                        next.Add(child);
                        child = TreeWalker.ControlViewWalker.GetNextSibling(child);
                    }
                }
                catch { }
            }
            level = next;
        }
        return null;
    }

    private static string? TryGetSelection(AutomationElement el, ref bool sawPattern)
    {
        try
        {
            if (!el.TryGetCurrentPattern(TextPattern.Pattern, out var p)) return null;
            sawPattern = true;
            var ranges = ((TextPattern)p).GetSelection();
            if (ranges == null || ranges.Length == 0) return null;
            return string.Concat(ranges.Select(r => r.GetText(TextLimits.MaxTextBytes + 1)));
        }
        catch { return null; }
    }

    private static bool IsEditable(AutomationElement? el)
    {
        if (el == null) return false;
        try
        {
            var type = el.Current.ControlType;
            if (type != ControlType.Edit && type != ControlType.Document && type != ControlType.ComboBox) return false;
            if (el.TryGetCurrentPattern(ValuePattern.Pattern, out var vp))
                return !((ValuePattern)vp).Current.IsReadOnly;
            // Document sans ValuePattern (navigateur, Word...) : on suppose lecture seule.
            return type == ControlType.Edit;
        }
        catch { return false; }
    }
}
