using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SelectBar.Settings;

namespace SelectBar.UI;

/// <summary>Couleurs d'une pilule de la barre : sombre (texte blanc) ou claire (texte sombre).</summary>
internal sealed class BarPalette
{
    public required Brush Back { get; init; }
    public required Brush Border { get; init; }
    public required Brush Text { get; init; }
    public required Brush Hover { get; init; }
    public required Brush Pressed { get; init; }
    public required Brush Separator { get; init; }
    public required Brush PrimaryBack { get; init; }
    public required Brush PrimaryHover { get; init; }
    public required Brush PrimaryText { get; init; }

    private static BarPalette? _dark, _light;

    /// <summary>
    /// Style choisi : « auto » suit le thème des applications de Windows (AppsUseLightTheme), « light » / « dark » forcent la pilule.
    /// Clair : acrylique claire à texte sombre (lisible sur fond clair) ; sombre : acrylique sombre à texte blanc.
    /// </summary>
    public static BarPalette For(string? barStyle) =>
        Appearance.IsDark(barStyle, FluentTheme.AppsUseLightTheme())
            ? _dark ??= new BarPalette
            {
                Back = Theme.Solid(ThemePalette.BarBack, ThemePalette.BarAlpha), Border = Theme.Solid("#FFFFFF", 0.14),
                Text = Theme.Solid(ThemePalette.BarTextDark), Hover = Theme.Solid("#FFFFFF", 0.12), Pressed = Theme.Solid("#FFFFFF", 0.08),
                Separator = Theme.Solid("#FFFFFF", 0.22), PrimaryBack = Theme.Solid(ThemePalette.AccentDark), PrimaryHover = Theme.Solid(ThemePalette.AccentHoverDark),
                PrimaryText = Theme.Solid("#000000")
            }
            : _light ??= new BarPalette
            {
                Back = Theme.Solid(ThemePalette.BarBackLight, ThemePalette.BarAlpha), Border = Theme.Solid("#000000", 0.12),
                Text = Theme.Solid(ThemePalette.BarTextLight), Hover = Theme.Solid("#000000", 0.06), Pressed = Theme.Solid("#000000", 0.10),
                Separator = Theme.Solid("#000000", 0.18), PrimaryBack = Theme.Solid(ThemePalette.Accent), PrimaryHover = Theme.Solid(ThemePalette.AccentHover),
                PrimaryText = Theme.Solid("#FFFFFF")
            };
}

/// <summary>Éléments visuels de la pilule (barre flottante et aperçu des réglages) : acrylique Fluent, boutons de 32 px à rayon 4.</summary>
internal static class BarVisuals
{
    /// <summary>Palette en cours (fixée par la barre ou l'aperçu avant de construire ses boutons).</summary>
    public static BarPalette Current { get; set; } = BarPalette.For(Appearance.Auto);

    /// <summary>Pilule : fond acrylique 92 %, bordure fine, rayon 8, ombre légère. Le contenu est dans le verre intérieur.</summary>
    public static Border CreatePill(Panel inner, double scale)
    {
        var glass = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(4), Child = inner };
        var pill = new Border
        {
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Margin = new Thickness(8),
            Effect = Theme.Shadow(16, 0.2),
            LayoutTransform = new ScaleTransform(scale, scale),
            RenderTransform = new TranslateTransform(),
            Child = glass
        };
        Restyle(pill);
        return pill;
    }

    /// <summary>Applique la palette courante à une pilule existante (changement de style entre deux affichages).</summary>
    public static void Restyle(Border pill)
    {
        pill.Background = Current.Back;
        pill.BorderBrush = Current.Border;
    }

    public static UIElement Separator() => new Border
    {
        Width = 1, Height = 16, Margin = new Thickness(4, 0, 4, 0),
        Background = Current.Separator, VerticalAlignment = VerticalAlignment.Center
    };

    /// <summary>Bouton de la barre (32 px, rayon 4) : glyphe Segoe Fluent seul ou libellé ; le bouton principal est en accent.</summary>
    public static Border Button(string? glyph, string label, string tooltip, bool primary, Action? onClick)
    {
        var pal = Current;
        var normal = primary ? pal.PrimaryBack : Brushes.Transparent;
        var hover = primary ? pal.PrimaryHover : pal.Hover;
        var text = new TextBlock
        {
            Text = glyph ?? label,
            Foreground = primary ? pal.PrimaryText : pal.Text,
            FontSize = glyph != null ? 16 : 14,
            FontFamily = glyph != null ? Theme.IconFont : Theme.Font,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var b = new Border
        {
            Background = normal, CornerRadius = new CornerRadius(4), Height = 32, Margin = new Thickness(1, 0, 1, 0),
            MinWidth = 32, Padding = new Thickness(glyph != null ? 8 : 12, 0, glyph != null ? 8 : 12, 0),
            ToolTip = tooltip, Child = text
        };
        System.Windows.Automation.AutomationProperties.SetName(b, tooltip);
        b.MouseEnter += (_, _) => b.Background = hover;
        b.MouseLeave += (_, _) => b.Background = normal;
        b.MouseLeftButtonDown += (_, _) => b.Background = primary ? pal.PrimaryHover : pal.Pressed;
        if (onClick != null)
            b.MouseLeftButtonUp += (_, e) => { e.Handled = true; b.Background = hover; onClick(); };
        return b;
    }

    /// <summary>Animations de fenêtre actives dans Windows (équivalent de prefers-reduced-motion : désactivées si l'utilisateur les coupe).</summary>
    public static bool MotionAllowed => SystemParameters.ClientAreaAnimation;

    /// <summary>
    /// Apparition de 140 ms : opacité 0 → 1 et glissement de 4 px vers la position finale. Sans animation (réglage Windows), l'élément
    /// apparaît tout de suite.
    /// </summary>
    public static void AnimateIn(FrameworkElement el, bool fromBelow)
    {
        if (!MotionAllowed) { el.Opacity = 1; return; }
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(140);
        var fade = new DoubleAnimation(0, 1, duration) { EasingFunction = ease };
        // l'animation ne garde pas la valeur finale : l'opacité redevient une valeur ordinaire (sinon la prochaine apparition clignoterait)
        fade.Completed += (_, _) => { el.BeginAnimation(UIElement.OpacityProperty, null); el.Opacity = 1; };
        el.BeginAnimation(UIElement.OpacityProperty, fade);
        if (el.RenderTransform is not TranslateTransform t)
        {
            t = new TranslateTransform();
            el.RenderTransform = t;
        }
        var slide = new DoubleAnimation(fromBelow ? 4 : -4, 0, duration) { EasingFunction = ease };
        slide.Completed += (_, _) => { t.BeginAnimation(TranslateTransform.YProperty, null); t.Y = 0; };
        t.BeginAnimation(TranslateTransform.YProperty, slide);
    }
}
