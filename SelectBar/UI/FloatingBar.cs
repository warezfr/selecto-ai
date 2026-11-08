using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SelectBar.Actions;
using SelectBar.Localization;
using SelectBar.Input;
using SelectBar.Settings;

namespace SelectBar.UI;

/// <summary>Barre flottante sans bordure, jamais activée (ne vole pas le focus). Pilule acrylique Fluent, claire ou sombre, bouton principal en accent.</summary>
public sealed class FloatingBar : Window
{
    private readonly StackPanel _panel = new() { Orientation = Orientation.Horizontal };
    private readonly Border _pill;
    private System.Drawing.Rectangle _pixelBounds;

    // état pour reconstruire la barre quand un menu (Copilot) s'ouvre ou se ferme
    private IReadOnlyList<ActionDefinition> _actions = Array.Empty<ActionDefinition>();
    private AppSettings _settings = new();
    private (int X, int Y) _cursor;
    private IReadOnlyDictionary<string, string> _inlineTexts = new Dictionary<string, string>();
    private string? _openMenu;
    private readonly ClickGuard _guard = new();

    public FloatingBar()
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
        SizeToContent = SizeToContent.WidthAndHeight;
        _pill = BarVisuals.CreatePill(_panel, 1.0);
        Content = _pill;
        WindowStyles.MakeNoActivate(this);
    }

    /// <summary>Appelé quand l'utilisateur clique sur une action.</summary>
    public event Action<ActionDefinition>? ActionInvoked;

    public bool ContainsPoint(int x, int y) => IsVisible && _pixelBounds.Contains(x, y);

    /// <summary>Affiche la barre. <paramref name="inlineTexts"/> : texte des actions inline (identifiant -> texte), affiché à la place du bouton.</summary>
    public void ShowAt(int cursorX, int cursorY, IReadOnlyList<ActionDefinition> actions, AppSettings? settings = null,
        IReadOnlyDictionary<string, string>? inlineTexts = null)
    {
        _actions = actions;
        _inlineTexts = inlineTexts ?? new Dictionary<string, string>();
        _settings = settings ?? new AppSettings();
        _cursor = (cursorX, cursorY);
        _openMenu = null;
        _guard.Arm(Environment.TickCount64);   // les 400 ms qui suivent l'affichage ignorent tout clic
        Rebuild();
    }

    /// <summary>Ouvre le sous-menu de l'action <paramref name="id"/> (mode capture) ; false si la barre n'a pas ce sous-menu.</summary>
    public bool OpenMenu(string id)
    {
        if (!_actions.Any(a => a.ParentId == id)) return false;
        _openMenu = id;
        Rebuild();
        return true;
    }

    private void Rebuild()
    {
        var firstShow = !IsVisible;
        BarVisuals.Current = BarPalette.For(_settings.BarStyle);
        BarVisuals.Restyle(_pill);
        if (firstShow && BarVisuals.MotionAllowed) _pill.Opacity = 0;   // l'animation d'apparition la fait venir (140 ms)
        var scale = Appearance.ClampScale(_settings.BarScalePercent) / 100.0;
        _pill.LayoutTransform = new ScaleTransform(scale, scale);
        _panel.Children.Clear();

        if (_openMenu != null)
        {
            _panel.Children.Add(BarVisuals.Button(RtlLayout.Glyph("\uE72B", RtlLayout.IsRtl(L.Culture)), "", L.Get("Bar_Back"), false, Guarded("back", () => { _openMenu = null; Rebuild(); })));
            foreach (var child in _actions.Where(a => a.ParentId == _openMenu))
                _panel.Children.Add(MakeButton(child));
        }
        else
        {
            var top = _actions.Where(a => a.ParentId == null).ToList();
            for (var i = 0; i < top.Count; i++)
            {
                var a = top[i];
                var primaryBefore = i > 0 && top[i - 1].Id == BuiltInActions.Search;
                if (i > 0 && a.Id != BuiltInActions.Search && !primaryBefore) _panel.Children.Add(BarVisuals.Separator());
                _panel.Children.Add(MakeButton(a));
                if (!a.OpensMenu && _actions.Any(c => c.ParentId == a.Id))
                {
                    var id = a.Id;
                    _panel.Children.Add(BarVisuals.Button(RtlLayout.Glyph("\uE70D", RtlLayout.IsRtl(L.Culture)), "", L.Get("Bar_MoreChoices"), false, Guarded("moreChoices", () => { _openMenu = id; Rebuild(); })));
                }
            }
        }

        if (!IsVisible) Show();
        UpdateLayout();

        var dpi = VisualTreeHelper.GetDpi(this);
        var w = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX);
        var h = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);
        var work = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(_cursor.X, _cursor.Y)).WorkingArea;
        // la barre ne recouvre jamais le pointeur : au moins 12 px de marge (fenêtre comprise)
        var (x, y) = Appearance.PlaceBar(_cursor.X, _cursor.Y, w, h,
            (int)(10 * dpi.DpiScaleY), (int)(24 * dpi.DpiScaleY), work.Left, work.Top, work.Right, work.Bottom, _settings.BarPosition,
            (int)Math.Ceiling(12 * dpi.DpiScaleY));

        _pixelBounds = new System.Drawing.Rectangle(x, y, w, h);
        WindowStyles.MoveTo(this, x, y);
        if (firstShow) BarVisuals.AnimateIn(_pill, fromBelow: y < _cursor.Y);   // barre au-dessus du pointeur : elle monte de 4 px
        else _pill.Opacity = 1;
    }

    public void HideBar()
    {
        if (IsVisible) Hide();
        _pixelBounds = System.Drawing.Rectangle.Empty;
        _openMenu = null;
        _guard.Reset();
    }

    private UIElement MakeButton(ActionDefinition a)
    {
        if (_inlineTexts.TryGetValue(a.Id, out var text))
            return BarVisuals.Button(null, L.Rtl(text), a.Title, false, Guarded(a.Id, () => ActionInvoked?.Invoke(a)));
        var glyph = ActionGlyphs.ForBar(a.Id) ?? GlyphFromIcon(a.Icon);
        Action click = () => ActionInvoked?.Invoke(a);
        if (a.OpensMenu) click = () => { _openMenu = a.Id; Rebuild(); };
        return BarVisuals.Button(glyph, a.Icon ?? a.Title, a.Title, a.Id == BuiltInActions.Search, Guarded(a.Id, click));
    }

    /// <summary>Icône d'extension au format Segoe (quatre chiffres hexadécimaux, ex. E8D2) : glyphe. Sinon null (texte).</summary>
    private static string? GlyphFromIcon(string? icon)
    {
        if (icon is not { Length: 4 } || !icon.All(Uri.IsHexDigit)) return null;
        var code = Convert.ToInt32(icon, 16);
        return (code is < 0xD800 or > 0xDFFF) ? char.ConvertFromUtf32(code) : null;
    }

    /// <summary>Garde anti-clic accidentel : un clic dans les 400 ms après l'affichage est ignoré et journalisé.</summary>
    private Action Guarded(string what, Action action) => () =>
    {
        if (_guard.IsAccidental(Environment.TickCount64))
        {
            var p = System.Windows.Forms.Cursor.Position;
            Log.Write($"Clic ignoré sur la barre (affichée depuis moins de {ClickGuard.DelayMs} ms) : {what} en ({p.X},{p.Y})");
            return;
        }
        action();
    };
}
