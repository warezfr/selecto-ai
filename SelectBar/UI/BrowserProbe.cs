using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Automation;
using SelectBar.Input;

namespace SelectBar.UI;

/// <summary>
/// Meilleur effort : URL (barre d'adresse via UI Automation) et titre de la fenêtre de navigateur active.
/// À appeler hors thread UI ; peut échouer ou être lent, l'appelant impose un délai.
/// </summary>
internal static class BrowserProbe
{
    private static readonly HashSet<string> Browsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome.exe", "msedge.exe", "firefox.exe", "brave.exe", "opera.exe", "vivaldi.exe", "arc.exe", "chromium.exe"
    };

    private static readonly string[] AddressHints = { "adresse", "address", "url", "rechercher ou saisir", "search or enter", "search with" };

    public static bool IsBrowser(string? exe) => exe != null && Browsers.Contains(exe);

    public static (string? Url, string Title) Read()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return (null, "");
        var title = NativeMethods.GetWindowTitle(hwnd);
        try
        {
            var root = AutomationElement.FromHandle(hwnd);
            var edits = root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            for (var i = 0; i < edits.Count && i < 12; i++)
            {
                var el = edits[i];
                var name = (el.Current.Name ?? "").ToLowerInvariant();
                if (!AddressHints.Any(h => name.Contains(h))) continue;
                if (el.TryGetCurrentPattern(ValuePattern.Pattern, out var p) && p is ValuePattern vp)
                {
                    var v = vp.Current.Value;
                    if (!string.IsNullOrWhiteSpace(v)) return (v, title);
                }
            }
        }
        catch { }
        return (null, title);
    }
}
