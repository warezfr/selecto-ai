using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SelectBar.Localization;
using SelectBar.Input;
using SelectBar.Online;
using SelectBar.Settings;

namespace SelectBar.UI;

/// <summary>Ce dont le popup a besoin pour travailler (branché par AppController).</summary>
public sealed class ResultPopupActions
{
    public required Func<ResultRequest, CancellationToken, Task<ResultPayload>> Run { get; init; }
    /// <summary>Colle le texte à la place de la sélection (la cible garde le focus).</summary>
    public required Func<string, Task> Replace { get; init; }
    public required Action<string> Copy { get; init; }
    public required Action<ResultPayload> OpenFallback { get; init; }
    /// <summary>Ouvre l'assistant « Modèle IA » des réglages (préréglage : "ollama" ou "openrouter").</summary>
    public required Action<string> OpenAiSetup { get; init; }
}

/// <summary>
/// Petite carte Fluent (rayon 8, clair ou sombre selon Windows) près de la sélection : chargement, résultat (Remplacer / Copier) ou erreur avec solution.
/// Fenêtre WPF simple, jamais activée (WS_EX_NOACTIVATE) : la cible garde le focus, donc « Remplacer » colle au bon endroit.
/// Échap, clic ailleurs et molette hors popup sont gérés par AppController (le hook voit les événements avant WPF).
/// </summary>
public sealed class ResultPopup : Window
{
    private const double CardWidth = 360, ShadowMargin = 12;

    private readonly TextBlock _title = new()
    {
        FontFamily = Theme.Font, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Theme.Text,
        TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center
    };
    private readonly TextBlock _body = new()
    {
        FontFamily = Theme.Font, FontSize = 14, Foreground = Theme.Text, TextWrapping = TextWrapping.Wrap
    };
    private readonly TextBlock _note = new()
    {
        FontFamily = Theme.Font, FontSize = 12, Foreground = Theme.SubText, TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 8, 0, 0)
    };
    private readonly ScrollViewer _scroll;
    private readonly Border _card;
    private readonly StackPanel _loading;
    private readonly WrapPanel _chips = new() { Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed };
    private readonly DockPanel _footer = new() { Margin = new Thickness(0, 12, 0, 0), LastChildFill = false };

    private ResultPopupActions? _actions;
    private ResultRequest _request = new();
    private ResultPayload? _payload;
    private (int X, int Y) _anchor;
    private string _position = Appearance.Above;
    private CancellationTokenSource? _cts;
    private System.Drawing.Rectangle _pixelBounds;
    private readonly ClickGuard _guard = new();

    public ResultPopup()
    {
        WindowFactory.Apply(this);
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Height;
        Width = CardWidth + 2 * ShadowMargin;

        var header = new DockPanel();
        header.Children.Add(_title);

        _loading = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 2) };
        _loading.Children.Add(new ProgressBar { IsIndeterminate = true, Width = 70, Height = 3, VerticalAlignment = VerticalAlignment.Center });
        _loading.Children.Add(new TextBlock
        {
            Text = L.Get("Popup_Searching"), FontFamily = Theme.Font, FontSize = 14, Foreground = Theme.SubText,
            Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center
        });

        _scroll = new ScrollViewer
        {
            MaxHeight = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _body, Margin = new Thickness(0, 8, 0, 0)
        };

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(_loading);
        stack.Children.Add(_scroll);
        stack.Children.Add(_note);
        stack.Children.Add(_chips);
        stack.Children.Add(_footer);

        _card = new Border
        {
            Width = CardWidth, Margin = new Thickness(ShadowMargin), Padding = new Thickness(16, 12, 16, 16),
            Background = Theme.CardBg, BorderBrush = Theme.ButtonBorder, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Effect = Theme.Shadow(16, 0.2), Child = stack, RenderTransform = new TranslateTransform()
        };
        Content = _card;
        WindowStyles.MakeNoActivate(this);
    }

    public bool ContainsPoint(int x, int y) => IsVisible && _pixelBounds.Contains(x, y);

    /// <summary>Ouvre (ou remplace) le popup près de <paramref name="anchor"/> et lance la demande.</summary>
    public void Present(ResultRequest request, (int X, int Y) anchor, string? barPosition, ResultPopupActions actions)
    {
        _actions = actions;
        _anchor = anchor;
        _position = Appearance.NormalizePosition(barPosition);
        _guard.Arm(Environment.TickCount64);   // les 400 ms qui suivent l'affichage ignorent tout clic sur les boutons
        Load(request);
    }

    public void HidePopup()
    {
        var cts = _cts;
        _cts = null;
        try { cts?.Cancel(); } catch (ObjectDisposedException) { }
        if (IsVisible) Hide();
        _pixelBounds = System.Drawing.Rectangle.Empty;
        _guard.Reset();
    }

    private async void Load(ResultRequest request)
    {
        var actions = _actions;
        if (actions == null) return;
        try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
        var cts = _cts = new CancellationTokenSource();
        _request = request;
        _payload = null;
        ShowLoading(request);
        ResultPayload payload;
        try { payload = await Task.Run(() => actions.Run(request, cts.Token), cts.Token); }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            Log.Write("Popup de résultat : " + ex);
            payload = ResultPayload.Failure(L.Get("Popup_ErrorGeneric"));
        }
        if (cts.IsCancellationRequested || !ReferenceEquals(_cts, cts)) return;
        _payload = payload;
        ShowPayload(request, payload);
    }

    private static string KindLabel(ResultKind k) => k switch
    {
        ResultKind.Define => L.Get("Popup_KindDefine"), ResultKind.Translate => L.Get("Popup_KindTranslate"), ResultKind.Explain => L.Get("Popup_KindExplain"),
        ResultKind.Summarize => L.Get("Popup_KindSummarize"), ResultKind.Rephrase => L.Get("Popup_KindRephrase"), _ => L.Get("Popup_KindCorrect")
    };

    private void ShowLoading(ResultRequest request)
    {
        _title.Text = KindLabel(request.Kind);
        _loading.Visibility = Visibility.Visible;
        _scroll.Visibility = Visibility.Collapsed;
        _note.Visibility = Visibility.Collapsed;
        _chips.Visibility = Visibility.Collapsed;
        _footer.Children.Clear();
        Reposition();
    }

    private void ShowPayload(ResultRequest request, ResultPayload p)
    {
        _loading.Visibility = Visibility.Collapsed;
        _title.Text = string.IsNullOrWhiteSpace(p.Title) ? KindLabel(request.Kind) : p.Title;
        var error = p.Error != null;
        _body.Text = error ? p.Error! : p.Body;
        _body.Foreground = error ? Theme.Warn : Theme.Text;
        _scroll.Visibility = Visibility.Visible;
        _scroll.ScrollToTop();
        _note.Text = p.Note ?? "";
        _note.Visibility = string.IsNullOrEmpty(p.Note) || error ? Visibility.Collapsed : Visibility.Visible;
        _chips.Visibility = Visibility.Collapsed;
        BuildFooter(request, p);
        Reposition();
    }

    private void BuildFooter(ResultRequest request, ResultPayload p)
    {
        _footer.Children.Clear();
        var actions = _actions!;
        if (p.Error != null)
        {
            if (p.NeedsAiSetup)
            {
                Add(MakeButton(L.Get("Popup_InstallAi"), true, Guarded("installAi", () => actions.OpenAiSetup(LlmPresets.Ollama))), Dock.Top);
                Add(MakeButton(L.Get("Popup_OnlineService"), false, Guarded("onlineService", () => actions.OpenAiSetup("online"))), Dock.Top);
            }
            else if (p.FallbackUrl != null)
                Add(MakeButton(L.Get("Popup_OpenInBrowser"), false, Guarded("openInBrowser", () => actions.OpenFallback(p))), Dock.Left);
            Add(MakeButton(L.Get("Popup_Close"), false, Guarded("close", HidePopup)), Dock.Right);
            return;
        }

        if (p.Language != null)
        {
            var lang = MakeButton("→ " + p.Language + " ▾", false, Guarded("language", ToggleChips));
            Add(lang, Dock.Left);
        }
        Add(MakeButton(L.Get("Popup_Close"), false, Guarded("close", HidePopup)), Dock.Right);
        Border? copy = null;
        copy = MakeButton(L.Get("Popup_Copy"), !p.CanReplace, Guarded("copy", () => { actions.Copy(p.Body); ((TextBlock)copy!.Child).Text = L.Get("Popup_Copied"); }));
        Add(copy, Dock.Right);
        if (p.CanReplace)
            Add(MakeButton(L.Get("Popup_Replace"), true, Guarded("replace", () => _ = ReplaceAsync(p.Body))), Dock.Right);
    }

    private async Task ReplaceAsync(string text)
    {
        var actions = _actions;
        HidePopup(); // d'abord masquer : la cible reçoit le collage, pas le popup
        if (actions == null) return;
        try { await actions.Replace(text); }
        catch (Exception ex) { Log.Write("Remplacer : " + ex.Message); }
    }

    private void ToggleChips()
    {
        if (_chips.Visibility == Visibility.Visible) { _chips.Visibility = Visibility.Collapsed; Reposition(); return; }
        _chips.Children.Clear();
        foreach (var (code, _) in LanguageCodes.All)
        {
            var c = code;
            var chip = MakeButton(LanguageCodes.DisplayName(code), c == _payload?.Language, Guarded("language " + c, () =>
            {
                _chips.Visibility = Visibility.Collapsed;
                Load(_request with { TargetLanguage = c });
            }));
            chip.Margin = new Thickness(0, 0, 6, 6);
            _chips.Children.Add(chip);
        }
        _chips.Visibility = Visibility.Visible;
        Reposition();
    }

    /// <summary>Garde anti-clic accidentel : un clic dans les 400 ms après l'affichage du popup est ignoré et journalisé.</summary>
    private Action Guarded(string what, Action action) => () =>
    {
        if (_guard.IsAccidental(Environment.TickCount64))
        {
            var p = System.Windows.Forms.Cursor.Position;
            Log.Write($"Clic ignoré sur le popup (affiché depuis moins de {ClickGuard.DelayMs} ms) : {what} en ({p.X},{p.Y})");
            return;
        }
        action();
    };

    private void Add(UIElement el, Dock dock)
    {
        if (dock == Dock.Top) { DockPanel.SetDock(el, Dock.Top); ((FrameworkElement)el).Margin = new Thickness(0, 0, 0, 6); }
        else { DockPanel.SetDock(el, dock); ((FrameworkElement)el).Margin = new Thickness(dock == Dock.Right ? 8 : 0, 0, dock == Dock.Left ? 8 : 0, 0); }
        _footer.Children.Add(el);
    }

    /// <summary>Bouton Fluent (32 px, rayon 4) cliquable sans prendre le focus (Border + clic, comme les boutons de la barre).</summary>
    private static Border MakeButton(string text, bool primary, Action onClick)
    {
        Brush normal = primary ? Theme.Accent : Brushes.Transparent;
        Brush hover = primary ? Theme.AccentHover : Theme.RowSeparator;
        var b = new Border
        {
            Background = normal, BorderBrush = primary ? Theme.Accent : Theme.ButtonBorder, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4), Padding = new Thickness(16, 0, 16, 0), MinHeight = 32,
            Child = new TextBlock
            {
                Text = text, FontFamily = Theme.Font, FontSize = 14, Foreground = primary ? Theme.OnAccent : Theme.Text,
                VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis
            }
        };
        System.Windows.Automation.AutomationProperties.SetName(b, text);
        b.MouseEnter += (_, _) => b.Background = hover;
        b.MouseLeave += (_, _) => b.Background = normal;
        b.MouseLeftButtonUp += (_, e) => { e.Handled = true; onClick(); };
        return b;
    }

    /// <summary>Affiche si besoin puis place la fenêtre (au-dessus de l'ancre, ou en dessous s'il n'y a pas la place).</summary>
    private void Reposition()
    {
        var firstShow = !IsVisible;
        if (firstShow && BarVisuals.MotionAllowed) _card.Opacity = 0;
        if (firstShow) Show();
        UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(this);
        var w = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX);
        var h = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);
        var work = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(_anchor.X, _anchor.Y)).WorkingArea;
        var (x, y) = Appearance.PlaceBar(_anchor.X, _anchor.Y, w, h,
            (int)(10 * dpi.DpiScaleY), (int)(24 * dpi.DpiScaleY), work.Left, work.Top, work.Right, work.Bottom, _position,
            (int)Math.Ceiling(12 * dpi.DpiScaleY));
        _pixelBounds = new System.Drawing.Rectangle(x, y, w, h);
        WindowStyles.MoveTo(this, x, y);
        if (firstShow) BarVisuals.AnimateIn(_card, fromBelow: y < _anchor.Y);
    }
}
