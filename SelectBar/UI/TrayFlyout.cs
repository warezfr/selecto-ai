using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SelectBar.Packaging;
using SelectBar.Localization;
using SelectBar.Update;
using WpfUi = Wpf.Ui.Controls;

namespace SelectBar.UI;

/// <summary>Ce que le flyout de la zone de notification pilote (branché par AppController).</summary>
public sealed class TrayFlyoutActions
{
    public required AppEdition Edition { get; init; }
    public required Func<bool> IsEnabled { get; init; }
    public required Action Toggle { get; init; }
    public required Action OpenSettings { get; init; }
    public required Action OpenExtensions { get; init; }
    public required Action Quit { get; init; }

    /// <summary>« Signaler un problème » (V3-09). Null : pas d'entrée dans le flyout.</summary>
    public Action? ReportProblem { get; init; }

    /// <summary>Mise à jour disponible, ou null : badge de l'icône et bande bleue du flyout.</summary>
    public required Func<UpdateInfo?> AvailableUpdate { get; init; }

    /// <summary>« Installer » de la bande bleue : même chemin que le bouton du toast de mise à jour.</summary>
    public required Action InstallUpdate { get; init; }

    /// <summary>Entrée « À propos ».</summary>
    public required Action OpenAbout { get; init; }

    /// <summary>Nombre d'actions affichées dans la barre (« 12 actions »).</summary>
    public required Func<int> ActionCount { get; init; }
}

/// <summary>
/// Flyout du clic gauche sur l'icône de la zone de notification : petite fenêtre Fluent (320 px, coins arrondis, acrylique) ancrée au
/// bord de la barre des tâches. En-tête (logo, « Selecto AI »), carte d'état (barre active ou en pause, interrupteur),
/// bande bleue si une mise à jour est disponible, liste d'entrées à icônes, « Quitter » séparé. Se ferme au clic extérieur ou à Échap.
/// Le contenu vient de <see cref="TrayFlyoutModel"/> (logique pure testée). Le placement vient de FlyoutPlacement.
/// </summary>
public sealed class TrayFlyout : WpfUi.FluentWindow
{
    private const double FlyoutWidth = 280; // resserré (trop large auparavant)
    private readonly TrayFlyoutActions _actions;
    private bool _closing;
    private TextBlock _statusTitle = null!;
    private TextBlock _statusSub = null!;
    private WpfUi.ToggleSwitch _switch = null!;

    /// <summary>Mode capture (--open-flyout) : ne se ferme pas à la perte d'activation, pour rester visible pendant la capture.</summary>
    public bool KeepOpenOnDeactivate { get; init; }

    /// <summary>Levé quand le flyout se ferme (pour ignorer le clic sur l'icône qui l'a fermé).</summary>
    public event Action? Dismissed;

    public TrayFlyout(TrayFlyoutActions actions)
    {
        WindowFactory.Apply(this);
        _actions = actions;
        MinWidth = 0; MinHeight = 0; // FluentWindow impose une largeur minimale (la largeur 280 était ignorée)
        Width = FlyoutWidth;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        ExtendsContentIntoTitleBar = true;
        WindowBackdropType = WpfUi.WindowBackdropType.Acrylic;         // repli : couleur unie si l'acrylique est indisponible
        WindowCornerPreference = WpfUi.WindowCornerPreference.Round;   // coins de 8 px
        FontFamily = Theme.Font;
        System.Windows.Automation.AutomationProperties.SetName(this, "Selecto AI");
        Content = Build();
        Deactivated += (_, _) => { if (!KeepOpenOnDeactivate) CloseFlyout(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; CloseFlyout(); } };
        Closed += (_, _) => Dismissed?.Invoke();
    }

    private UIElement Build()
    {
        var on = _actions.IsEnabled();
        var model = TrayFlyoutModel.Build(_actions.Edition, on, _actions.ActionCount(), _actions.AvailableUpdate());
        var root = new StackPanel { Margin = new Thickness(10, 10, 10, 6) };

        // en-tête : logo, nom
        var head = new Grid { Margin = new Thickness(4, 0, 4, 8) };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (AppIcon.Image() is { } icon)
        {
            var img = new Image { Source = icon, Width = 32, Height = 32, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            System.Windows.Automation.AutomationProperties.SetName(img, L.Get("Flyout_Logo"));
            head.Children.Add(img);
        }
        var title = Theme.Label("Selecto AI", 14, Theme.Text, FontWeights.SemiBold);
        title.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(title, 1);
        head.Children.Add(title);
        root.Children.Add(head);

        // carte d'état : « Barre active » et le nombre d'actions (ou « En pause ») à gauche, interrupteur à droite
        root.Children.Add(StatusCard());

        // bande bleue pleine largeur quand une mise à jour est disponible
        if (model.UpdateLine is { } updateLine) root.Children.Add(UpdateBand(updateLine));

        // entrées à icônes, Quitter en dernier, séparé par un trait
        foreach (var entry in model.Entries.Where(e => e.Kind != TrayEntryKind.Quit))
        {
            if (ActionFor(entry.Kind) is { } action) root.Children.Add(Item(entry.Glyph, entry.Label, action));
        }
        root.Children.Add(new Border { Height = 1, Background = Theme.RowSeparator, Margin = new Thickness(4, 4, 4, 4) });
        var quit = model.Entries.Single(e => e.Kind == TrayEntryKind.Quit);
        root.Children.Add(Item(quit.Glyph, quit.Label, _actions.Quit, subdued: true));
        return root;
    }

    private Action? ActionFor(TrayEntryKind kind)
    {
        switch (kind)
        {
            case TrayEntryKind.Settings: return _actions.OpenSettings;
            case TrayEntryKind.Extensions: return _actions.OpenExtensions;
            case TrayEntryKind.About: return _actions.OpenAbout;
            case TrayEntryKind.Report: return _actions.ReportProblem;
            default: return null;
        }
    }

    private UIElement StatusCard()
    {
        _statusTitle = Theme.Label("", 14, Theme.Text, FontWeights.SemiBold);
        _statusSub = Theme.Sub("");
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(_statusTitle);
        texts.Children.Add(_statusSub);

        // interrupteur « Barre active » : met en pause / reprend
        _switch = Theme.Switch(false, _ =>
        {
            _actions.Toggle();
            RefreshStatus();
            return true;
        }, L.Get("Tray_BarOn"));

        var card = new Grid { MinHeight = 44 };
        card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        card.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        card.Children.Add(texts);
        Grid.SetColumn(_switch, 1);
        card.Children.Add(_switch);
        RefreshStatus();
        return new Border
        {
            Background = Theme.CardBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 8), Child = card
        };
    }

    /// <summary>Recalcule les textes de la carte d'état et l'interrupteur depuis l'état courant.</summary>
    private void RefreshStatus()
    {
        var on = _actions.IsEnabled();
        _statusTitle.Text = on ? L.Get("Tray_BarOn") : L.Get("Tray_StatusPaused");
        _statusSub.Text = on ? L.Rtl(L.Plural(_actions.ActionCount(), "Tray_ActionsOne", "Tray_ActionsOther")) : "";
        _statusSub.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        _switch.IsChecked = on;
    }

    private UIElement UpdateBand(string text)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = Theme.Label(text, 13, Theme.Text);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        grid.Children.Add(label);
        var install = Theme.LinkButton(L.Get("Settings_Install"), () => { CloseFlyout(); _actions.InstallUpdate(); });
        install.Foreground = Theme.Accent;
        Grid.SetColumn(install, 1);
        grid.Children.Add(install);
        return new Border
        {
            Background = new SolidColorBrush(Theme.Accent.Color) { Opacity = 0.15 },
            CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 4, 4, 4), Margin = new Thickness(0, 0, 0, 8),
            Child = grid
        };
    }

    private WpfUi.Button Item(string glyph, string text, Action action, bool subdued = false)
    {
        var fg = subdued ? Theme.SubText : Theme.Text;
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock { Text = glyph, FontFamily = Theme.IconFont, FontSize = 16, Width = 28, VerticalAlignment = VerticalAlignment.Center, Foreground = fg });
        content.Children.Add(new TextBlock { Text = text, FontSize = 13, FontFamily = Theme.Font, VerticalAlignment = VerticalAlignment.Center, Foreground = fg });
        var b = new WpfUi.Button
        {
            Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
            Appearance = WpfUi.ControlAppearance.Transparent, Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0),
            MinHeight = 32, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0)
        };
        System.Windows.Automation.AutomationProperties.SetName(b, text);
        b.Click += (_, _) => { CloseFlyout(); action(); };
        return b;
    }

    private void CloseFlyout()
    {
        if (_closing) return;
        _closing = true;
        try { Close(); } catch (InvalidOperationException) { }
    }

    /// <summary>Affiche le flyout au bord de la barre des tâches, près de <paramref name="anchorX"/>, <paramref name="anchorY"/> (pixels écran).</summary>
    public void ShowAt(int anchorX, int anchorY)
    {
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(anchorX, anchorY));
        var b = screen.Bounds;
        var wa = screen.WorkingArea;
        var edge = FlyoutPlacement.DetectEdge(b.Left, b.Top, b.Right, b.Bottom, wa.Left, wa.Top, wa.Right, wa.Bottom);

        Show();
        Activate();
        // deux passes : la taille réelle en pixels dépend du DPI de l'écran où la fenêtre atterrit
        for (var pass = 0; pass < 2; pass++)
        {
            UpdateLayout();
            var dpi = VisualTreeHelper.GetDpi(this);
            var w = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX);
            var h = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);
            var (x, y) = FlyoutPlacement.Place(edge, anchorX, anchorY, w, h, wa.Left, wa.Top, wa.Right, wa.Bottom, (int)Math.Ceiling(12 * dpi.DpiScaleX));
            WindowStyles.MoveTo(this, x, y);
        }
        if (Content is FrameworkElement el)
        {
            el.RenderTransform = new TranslateTransform();
            BarVisuals.AnimateIn(el, fromBelow: edge != TaskbarEdge.Top);
        }
    }
}
