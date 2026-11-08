using System;
using System.Windows;
using System.Windows.Markup;
using SelectBar.Localization;

namespace SelectBar.UI;

/// <summary>
/// Fabrique de fenêtres : pose le sens de lecture (FlowDirection) et la langue (xml:lang) de chaque fenêtre d'après
/// <see cref="L.Culture"/>, et les réapplique quand la langue change. Un seul endroit pour la droite-à-gauche.
/// </summary>
public static class WindowFactory
{
    public static void Apply(Window w)
    {
        ApplyCulture(w);
        // L'abonnement est retiré à la fermeture : la fenêtre n'est plus retenue par L.CultureChanged.
        Action handler = () =>
        {
            if (w.Dispatcher.CheckAccess()) ApplyCulture(w);
            else w.Dispatcher.BeginInvoke(new Action(() => ApplyCulture(w)));
        };
        L.CultureChanged += handler;
        w.Closed += (_, _) => L.CultureChanged -= handler;
    }

    private static void ApplyCulture(Window w)
    {
        w.FlowDirection = RtlLayout.IsRtl(L.Culture) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        w.Language = XmlLanguage.GetLanguage(L.Culture.IetfLanguageTag);
    }
}
