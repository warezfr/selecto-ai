using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace SelectBar.UI;

/// <summary>Bulle de texte non activante (selecto.showText, rappel d'essai) : carte Fluent à rayon 8, apparition de 140 ms.</summary>
public sealed class ToastWindow : Window
{
    private readonly Border _card;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(4) };

    private ToastWindow(string text, params (string Label, Action Run, bool Primary)[] actions)
    {
        WindowFactory.Apply(this);
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true; Focusable = false;
        SizeToContent = SizeToContent.WidthAndHeight; ResizeMode = ResizeMode.NoResize;
        var body = new TextBlock { Text = text, Foreground = Theme.Text, TextWrapping = TextWrapping.Wrap, FontSize = 14, FontFamily = Theme.Font };
        UIElement content = body;
        if (actions.Length > 0)
        {
            // Actions facultatives (ex. « Installer » / « Plus tard ») : chaque clic ferme la bulle puis exécute l'action.
            var stack = new StackPanel();
            stack.Children.Add(body);
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            foreach (var (label, run, primary) in actions)
            {
                var button = Theme.Btn(label, () => { Close(); run(); }, primary);
                button.Margin = new Thickness(8, 0, 0, 0);
                row.Children.Add(button);
            }
            stack.Children.Add(row);
            content = stack;
        }
        _card = new Border
        {
            Background = Theme.CardBg, BorderBrush = Theme.ButtonBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(12), MaxWidth = 420, Effect = Theme.Shadow(16, 0.2),
            RenderTransform = new TranslateTransform(),
            Child = content
        };
        Content = _card;
        Loaded += (_, _) => BarVisuals.AnimateIn(_card, fromBelow: true);
        WindowStyles.MakeNoActivate(this);
        _timer.Tick += (_, _) => { _timer.Stop(); Close(); };
    }

    /// <summary>Coin bas-droite de la zone de travail (toast discret sans rapport avec le pointeur).</summary>
    public static void ShowCorner(string text)
    {
        var t = new ToastWindow(text);
        t._timer.Interval = TimeSpan.FromSeconds(8);
        t.Show();
        t.UpdateLayout();
        var work = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1280, 720);
        var dpi = VisualTreeHelper.GetDpi(t);
        var w = (int)(t.ActualWidth * dpi.DpiScaleX);
        var h = (int)(t.ActualHeight * dpi.DpiScaleY);
        WindowStyles.MoveTo(t, work.Right - w - 12, work.Bottom - h - 12);
        t._timer.Start();
    }

    /// <summary>Coin bas-droite avec deux actions (ex. « Installer » / « Plus tard ») ; reste affichée 30 s si rien n'est choisi.</summary>
    public static void ShowCornerWithActions(string text, (string Label, Action Run) primary, (string Label, Action Run) secondary)
    {
        var t = new ToastWindow(text, (primary.Label, primary.Run, true), (secondary.Label, secondary.Run, false));
        t._timer.Interval = TimeSpan.FromSeconds(30);
        t.Show();
        t.UpdateLayout();
        var work = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1280, 720);
        var dpi = VisualTreeHelper.GetDpi(t);
        var w = (int)(t.ActualWidth * dpi.DpiScaleX);
        var h = (int)(t.ActualHeight * dpi.DpiScaleY);
        WindowStyles.MoveTo(t, work.Right - w - 12, work.Bottom - h - 12);
        t._timer.Start();
    }

    public static void ShowNear(string text, int x, int y)
    {
        var t = new ToastWindow(text);
        t.Show();
        WindowStyles.MoveTo(t, x, y + 24);
        t._timer.Start();
    }
}
