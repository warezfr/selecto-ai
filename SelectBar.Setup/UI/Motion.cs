using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace SelectBar.Setup.UI;

/// <summary>Animations : opacité, translation, tracé (dash offset). Désactivées si Windows coupe les animations de la fenêtre.</summary>
internal static class Motion
{
    public static bool Enabled => SystemParameters.ClientAreaAnimation;

    private static readonly IEasingFunction OutCubic = MakeCubic();

    private static IEasingFunction MakeCubic()
    {
        var e = new CubicEase { EasingMode = EasingMode.EaseOut };
        e.Freeze();
        return e;
    }

    public static DoubleAnimation Anim(double from, double to, int ms, int delayMs = 0, IEasingFunction? ease = null)
        => new(from, to, TimeSpan.FromMilliseconds(ms)) { BeginTime = TimeSpan.FromMilliseconds(delayMs), EasingFunction = ease ?? OutCubic };

    /// <summary>Apparition : opacité 0 vers 1 et glissement vertical de <paramref name="dy"/> px vers 0.</summary>
    public static void FadeSlideIn(FrameworkElement e, int ms = 280, double dy = 8, int delayMs = 0)
    {
        if (!Enabled) { e.Opacity = 1; return; }
        var t = new TranslateTransform(0, dy);
        e.RenderTransform = t;
        e.Opacity = 0;
        e.BeginAnimation(UIElement.OpacityProperty, Anim(0, 1, ms, delayMs));
        t.BeginAnimation(TranslateTransform.YProperty, Anim(dy, 0, ms, delayMs));
    }

    public static void FadeOut(UIElement e, int ms, Action? done)
    {
        if (!Enabled) { done?.Invoke(); return; }
        var a = Anim(1, 0, ms);
        a.Completed += (_, _) => done?.Invoke();
        e.BeginAnimation(UIElement.OpacityProperty, a);
    }

    public static void FadeTo(UIElement e, double to, int ms)
    {
        if (!Enabled) { e.BeginAnimation(UIElement.OpacityProperty, null); e.Opacity = to; return; }
        e.BeginAnimation(UIElement.OpacityProperty, Anim(e.Opacity, to, ms));
    }

    /// <summary>
    /// Tracé progressif d'un chemin. <paramref name="lengthUnits"/> = longueur du chemin divisée par l'épaisseur du trait
    /// (les tirets se mesurent en épaisseurs), arrondie un peu au-dessus.
    /// </summary>
    public static void DrawOn(Shape s, double lengthUnits, int ms, int delayMs = 0)
    {
        if (!Enabled) { s.StrokeDashArray = null; return; }
        s.StrokeDashArray = new DoubleCollection { lengthUnits, 1000 };
        s.StrokeDashCap = PenLineCap.Round;
        s.StrokeDashOffset = lengthUnits + 1;
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseInOut };
        var anim = Anim(lengthUnits + 1, 0, ms, delayMs, ease);
        // Filet de sécurité : à la fin, le trait est affiché en entier même si la longueur estimée était trop courte.
        anim.Completed += (_, _) => { s.BeginAnimation(Shape.StrokeDashOffsetProperty, null); s.StrokeDashArray = null; };
        s.BeginAnimation(Shape.StrokeDashOffsetProperty, anim);
    }

    /// <summary>Chute avec rebond (ease-out) d'un élément translaté verticalement.</summary>
    public static void Drop(TranslateTransform t, double fromY, int ms, int delayMs)
    {
        if (!Enabled) { t.Y = 0; return; }
        t.Y = fromY;
        var bounce = new BounceEase { EasingMode = EasingMode.EaseOut, Bounces = 2, Bounciness = 3 };
        t.BeginAnimation(TranslateTransform.YProperty, Anim(fromY, 0, ms, delayMs, bounce));
    }
}
