using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using SelectBar.Localization;
using SelectBar.Settings;
using WpfUi = Wpf.Ui.Controls;

namespace SelectBar.UI;

/// <summary>
/// Jetons Windows 11 Fluent : polices, pinceaux (clairs ou sombres, mis à jour en place quand le thème Windows change) et petits
/// constructeurs de lignes de réglages (cartes à rayon 8, grille de 4 px) bâtis sur les contrôles WPF UI. Les couleurs viennent de
/// ThemePalette ; l'accent suit la couleur d'accent du système quand elle reste lisible.
/// </summary>
internal static class Theme
{
    public static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily DisplayFont = new("Segoe UI Variable Display, Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    public static SolidColorBrush Solid(string hex, double opacity = 1)
    {
        var b = new SolidColorBrush(C(hex)) { Opacity = opacity };
        b.Freeze();
        return b;
    }

    // Pinceaux « vivants » : un seul exemplaire chacun, dont la couleur change avec le thème (tout ce qui s'en sert suit).
    public static readonly SolidColorBrush Accent = new();
    public static readonly SolidColorBrush AccentHover = new();
    public static readonly SolidColorBrush WindowBg = new();
    public static readonly SolidColorBrush CardBg = new();
    public static readonly SolidColorBrush Text = new();
    public static readonly SolidColorBrush SubText = new();
    public static readonly SolidColorBrush PivotOff = new();
    public static readonly SolidColorBrush RowSeparator = new();
    public static readonly SolidColorBrush ButtonBorder = new();
    public static readonly SolidColorBrush StatusOn = new();
    public static readonly SolidColorBrush Warn = new();
    public static readonly SolidColorBrush Error = new();
    /// <summary>Texte posé sur l'accent (blanc en clair, noir en sombre).</summary>
    public static readonly SolidColorBrush OnAccent = new();

    static Theme() => Refresh();

    /// <summary>Recalcule les pinceaux depuis la palette Fluent et l'accent du système (thread d'interface).</summary>
    public static void Refresh()
    {
        var dark = FluentTheme.IsDark;
        var card = dark ? ThemePalette.CardDark : ThemePalette.Card;
        var accent = ColorContrast.PickAccent(FluentTheme.SystemAccentHex(), card, dark ? ThemePalette.AccentDark : ThemePalette.Accent);
        var fallbackAccent = dark ? ThemePalette.AccentDark : ThemePalette.Accent;
        Set(Accent, accent);
        Set(AccentHover, accent == fallbackAccent ? (dark ? ThemePalette.AccentHoverDark : ThemePalette.AccentHover) : accent);
        Set(WindowBg, dark ? ThemePalette.WindowDark : ThemePalette.Window);
        Set(CardBg, card);
        Set(Text, dark ? ThemePalette.TextDark : ThemePalette.Text);
        Set(SubText, dark ? ThemePalette.SubTextDark : ThemePalette.SubText);
        Set(PivotOff, dark ? ThemePalette.PivotOffDark : ThemePalette.PivotOff);
        Set(RowSeparator, dark ? ThemePalette.SeparatorDark : ThemePalette.Separator);
        Set(ButtonBorder, dark ? ThemePalette.ButtonBorderDark : ThemePalette.ButtonBorder);
        Set(StatusOn, dark ? ThemePalette.StatusOnHexDark : ThemePalette.StatusOnHex);
        Set(Warn, dark ? ThemePalette.WarnHexDark : ThemePalette.WarnHex);
        Set(Error, dark ? ThemePalette.ErrorHexDark : ThemePalette.ErrorHex);
        Set(OnAccent, dark ? "#000000" : "#FFFFFF");
    }

    private static void Set(SolidColorBrush brush, string hex) => brush.Color = C(hex);

    /// <summary>Ombre légère (pas d'ombre lourde) : flou 16, profondeur 2.</summary>
    public static DropShadowEffect Shadow(double blur = 16, double opacity = 0.2) =>
        new() { BlurRadius = blur, ShadowDepth = 2, Opacity = opacity, Color = Colors.Black };

    // ---------------------------------------------------------------- texte (échelle 12 / 14 / 18 / 20 / 28 / 40)

    public static TextBlock Label(string text, double size = 14, Brush? fg = null, FontWeight? weight = null) => new()
    {
        Text = text, FontSize = size, Foreground = fg ?? Text,
        FontFamily = size >= 18 ? DisplayFont : Font,
        FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Left
    };

    /// <summary>Sens de lecture des Réglages : droite-à-gauche en arabe (posé sur la page, donc sur toute sa colonne).</summary>
    public static FlowDirection Flow => RtlLayout.IsRtl(L.Culture) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public static TextBlock Title(string text) => Label(text, 14, Text, FontWeights.SemiBold);
    public static TextBlock Sub(string text) => Label(text, 12, SubText);

    // ---------------------------------------------------------------- sections et cartes

    /// <summary>Titre de section : 14 px semi-gras, au-dessus de ses cartes.</summary>
    public static TextBlock SectionHeader(string text, bool first = false) => new()
    {
        Text = text, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Text, FontFamily = Font,
        TextAlignment = TextAlignment.Left, Margin = new Thickness(0, first ? 4 : 24, 0, 8)
    };

    /// <summary>Empile des cartes (4 px entre deux).</summary>
    public static StackPanel Group(params UIElement[] rows)
    {
        var stack = new StackPanel();
        foreach (var r in rows) stack.Children.Add(r);
        return stack;
    }

    /// <summary>Enveloppe un élément libre dans une carte Fluent (rayon 8, bordure fine, pas d'ombre).</summary>
    public static Border Line(UIElement child) => new()
    {
        Background = CardBg, BorderBrush = RowSeparator, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
        Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(0, 0, 0, 4), Child = child
    };

    /// <summary>Carte d'un réglage : libellé 14 px (+ aide 12 px grise) à gauche, contrôle à droite, icône facultative.</summary>
    public static Border Row(string label, string? description, UIElement? control, string? glyph = null, bool glyphIsIcon = true, double minHeight = 40, string? detail = null)
    {
        var g = new Grid { MinHeight = minHeight };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (glyph != null)
        {
            g.Children.Add(new TextBlock
            {
                Text = glyph, FontFamily = glyphIsIcon ? IconFont : Font, FontSize = glyphIsIcon ? 16 : 14, Foreground = Text, Width = 32,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 4, 16, 4) };
        texts.Children.Add(new TextBlock
        {
            Text = label, FontSize = 14, Foreground = Text, FontFamily = Font, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Left
        });
        if (!string.IsNullOrEmpty(description))
            texts.Children.Add(new TextBlock
            {
                Text = description, FontSize = 12, Foreground = SubText, FontFamily = Font, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Left
            });
        if (!string.IsNullOrEmpty(detail))
            texts.Children.Add(new TextBlock
            {
                Text = detail, FontSize = 12, Foreground = SubText, FontFamily = Font, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Left
            });
        Grid.SetColumn(texts, 1);
        g.Children.Add(texts);
        if (control != null)
        {
            if (control is FrameworkElement fe) fe.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(control, 2);
            g.Children.Add(control);
        }
        return Line(g);
    }

    // ---------------------------------------------------------------- défilement

    /// <summary>Marque une zone défilante qui garde sa barre : la règle « aucun ascenseur » de FluentTheme ne la touche pas.</summary>
    public static readonly DependencyProperty KeepScrollBarProperty =
        DependencyProperty.RegisterAttached("KeepScrollBar", typeof(bool), typeof(Theme), new PropertyMetadata(false));

    /// <summary>
    /// Zone défilante des pages de Réglages : molette, tactile et clavier, avec la barre fine Fluent de WPF UI (visible, elle s'élargit
    /// au survol). Les pages À propos et Actions dépassent la fenêtre : sans barre, on ne devine pas qu'il y a une suite.
    /// </summary>
    public static ScrollViewer ThinScroll(UIElement content)
    {
        var scroll = new ScrollViewer
        {
            Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            PanningMode = PanningMode.VerticalOnly
        };
        scroll.SetValue(KeepScrollBarProperty, true);
        return scroll;
    }

    // ---------------------------------------------------------------- contrôles WPF UI

    /// <summary>Interrupteur Fluent ; <paramref name="onChange"/> renvoie false pour refuser le changement.</summary>
    public static WpfUi.ToggleSwitch Switch(bool on, Func<bool, bool> onChange, string? automationName = null)
    {
        var sw = new WpfUi.ToggleSwitch { IsChecked = on, OnContent = "", OffContent = "", Margin = new Thickness(0) };
        if (automationName != null) System.Windows.Automation.AutomationProperties.SetName(sw, automationName);
        sw.Click += (_, _) =>
        {
            var wanted = sw.IsChecked == true;
            if (!onChange(wanted)) sw.IsChecked = !wanted;
        };
        return sw;
    }

    /// <summary>Bouton Fluent (rayon 4) : secondaire, ou principal (<paramref name="primary"/>) en couleur d'accent.</summary>
    public static WpfUi.Button Btn(string text, Action onClick, bool primary = false)
    {
        var b = new WpfUi.Button
        {
            Content = text, FontFamily = Font, FontSize = 14, MinWidth = 84, MinHeight = 32,
            Margin = new Thickness(0, 0, 8, 0),
            Appearance = primary ? WpfUi.ControlAppearance.Primary : WpfUi.ControlAppearance.Secondary
        };
        System.Windows.Automation.AutomationProperties.SetName(b, text);
        b.Click += (_, _) => onClick();
        return b;
    }

    /// <summary>Petit bouton sans fond (▲ ▼ ⚙ ou lien). Avec <paramref name="icon"/>, le texte est un glyphe Segoe Fluent.</summary>
    public static WpfUi.Button LinkButton(string text, Action onClick, bool icon = false, string? automationName = null)
    {
        var b = new WpfUi.Button
        {
            Content = text, FontFamily = icon ? IconFont : Font, FontSize = icon ? 14 : 12, MinHeight = 32, MinWidth = icon ? 32 : 0,
            Padding = new Thickness(icon ? 4 : 8, 2, icon ? 4 : 8, 2), Appearance = WpfUi.ControlAppearance.Transparent
        };
        System.Windows.Automation.AutomationProperties.SetName(b, automationName ?? text);
        b.Click += (_, _) => onClick();
        return b;
    }

    /// <summary>Choix à deux ou trois segments (ex. position de la barre) : segment actif en accent.</summary>
    public static Border Segmented(string[] labels, int selected, Action<int> onSelect)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var cells = new ToggleButton[labels.Length];
        void Paint(int sel)
        {
            for (var i = 0; i < cells.Length; i++) cells[i].IsChecked = i == sel;
        }
        for (var i = 0; i < labels.Length; i++)
        {
            var index = i;
            cells[i] = new ToggleButton
            {
                Content = labels[i], FontFamily = Font, FontSize = 14, MinHeight = 32, Padding = new Thickness(12, 0, 12, 0),
                Margin = new Thickness(i == 0 ? 0 : 2, 0, 0, 0)
            };
            System.Windows.Automation.AutomationProperties.SetName(cells[i], labels[i]);
            cells[i].Click += (_, _) => { Paint(index); onSelect(index); };
            row.Children.Add(cells[i]);
        }
        Paint(selected);
        return new Border { Child = row };
    }

    public static WpfUi.TextBox Input(double width = 220) => new()
    {
        Width = width, FontFamily = Font, FontSize = 14, VerticalContentAlignment = VerticalAlignment.Center, ClearButtonEnabled = false
    };

    public static CheckBox Check(string text, bool isChecked) => new()
    {
        Content = text, IsChecked = isChecked, FontFamily = Font, FontSize = 14, Margin = new Thickness(0, 4, 0, 4)
    };

    public static RadioButton Radio(string text, string group, bool isChecked) => new()
    {
        Content = text, GroupName = group, IsChecked = isChecked, FontFamily = Font, FontSize = 14, Margin = new Thickness(0, 4, 0, 4)
    };

    /// <summary>Pastille d'état (badge) : texte 12 px sur fond plein, coins arrondis.</summary>
    public static Border Badge(string text, Brush background, Brush foreground) => new()
    {
        Background = background, CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 2, 8, 2), VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = foreground, FontFamily = Font }
    };
}
