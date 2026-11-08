using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Microsoft.Win32;
using SelectBar.Input;
using SelectBar.Localization;

namespace SelectBar.UI;

/// <summary>Icône de zone de notification : clic gauche = flyout Fluent, clic droit = menu classique (Activer, Réglages, dossier des extensions, Quitter), double-clic = réglages.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _toggle;
    private readonly ToolStripMenuItem _settingsItem, _extensionsItem, _reportItem, _exitItem;
    private readonly Func<bool> _isEnabled;
    private readonly System.Windows.Forms.Timer _clickTimer = new() { Interval = SystemInformation.DoubleClickTime };
    private Icon _baseIcon;            // icône du thème et de l'état (ressource WPF ou repli GDI+), possédée ici
    private Icon? _badgedIcon;         // copie de _baseIcon avec la pastille de mise à jour, possédée ici
    private Icon? _badgedFrom;         // icône de base dont _badgedIcon est issue
    private string? _currentFile;
    private bool _paused;
    private bool _badge;
    private bool _disposed;

    public TrayIcon(Func<bool> isEnabled, Action toggle, Action openSettings, Action openExtensions, Action quit,
        bool visible = true, Action<Point>? flyout = null, Action? reportProblem = null)
    {
        _isEnabled = isEnabled;
        _toggle = new ToolStripMenuItem { CheckOnClick = false };
        _toggle.Click += (_, _) => toggle();
        _settingsItem = new ToolStripMenuItem();
        _settingsItem.Click += (_, _) => openSettings();
        _extensionsItem = new ToolStripMenuItem();
        _extensionsItem.Click += (_, _) => openExtensions();
        _reportItem = new ToolStripMenuItem { Visible = reportProblem != null };
        _reportItem.Click += (_, _) => reportProblem?.Invoke();
        _exitItem = new ToolStripMenuItem();
        _exitItem.Click += (_, _) => quit();
        ApplyLabels();

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[] { _toggle, new ToolStripSeparator(), _settingsItem, _extensionsItem, _reportItem, new ToolStripSeparator(), _exitItem });

        _baseIcon = LoadIcon(TrayIconChoice.FileName(ReadSystemUsesLightTheme(), false), out _currentFile) ?? MakeIcon();
        _icon = new NotifyIcon { Icon = _baseIcon, Text = "Selecto AI", ContextMenuStrip = menu, Visible = visible };
        _icon.DoubleClick += (_, _) => { _clickTimer.Stop(); openSettings(); };
        if (flyout != null)
        {
            // clic gauche = flyout, mais on attend la durée d'un double-clic pour ne pas l'ouvrir quand l'utilisateur ouvre les réglages
            var anchor = Point.Empty;
            _clickTimer.Tick += (_, _) => { _clickTimer.Stop(); flyout(anchor); };
            _icon.MouseClick += (_, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                anchor = Cursor.Position;
                _clickTimer.Stop();
                _clickTimer.Start();
            };
        }
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        L.CultureChanged += OnCultureChanged;
        Refresh();
    }

    /// <summary>Le menu est construit une fois : après un changement de langue, ses libellés sont réécrits (et l'état relu dans la nouvelle langue).</summary>
    private void OnCultureChanged()
    {
        if (_disposed) return;
        ApplyLabels();
        Refresh();
    }

    private void ApplyLabels()
    {
        _toggle.Text = L.Get("TrayMenu_Activate");
        _settingsItem.Text = L.Get("TrayMenu_Settings");
        _extensionsItem.Text = L.Get("TrayMenu_OpenExtensions");
        _reportItem.Text = L.Get("TrayMenu_Report");
        _exitItem.Text = L.Get("TrayMenu_Quit");
    }

    public void SetVisible(bool visible) => _icon.Visible = visible;

    /// <summary>Pastille bleue sur l'icône quand une mise à jour est disponible. Thread d'interface ; réappliquée au changement de thème.</summary>
    public void SetUpdateBadge(bool on)
    {
        if (_disposed || _badge == on) return;
        _badge = on;
        ShowIcon();
    }

    public void Refresh()
    {
        var on = _isEnabled();
        _toggle.Checked = on;
        _paused = !on;
        UpdateIcon();
        // NotifyIcon.Text est limité à 63 caractères
        var status = on ? L.Get("TrayMenu_StatusActive") : L.Get("TrayMenu_StatusOff");
        _icon.Text = L.Format("TrayMenu_Tooltip", status);
    }

    private static int? ReadSystemUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int v ? v : null;
        }
        catch { return null; }
    }

    // Le thème de la barre des tâches change : choisir à nouveau (l'événement peut venir d'un autre thread).
    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (_disposed) return;
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null) return;
        dispatcher.BeginInvoke(new Action(UpdateIcon));
    }

    private void UpdateIcon()
    {
        if (_disposed) return;
        var file = TrayIconChoice.FileName(ReadSystemUsesLightTheme(), _paused);
        if (file != _currentFile)
        {
            var next = LoadIcon(file, out var loaded);
            // ressource absente : on garde l'icône actuelle (repli GDI+ au démarrage)
            if (next != null)
            {
                var old = _baseIcon;
                _baseIcon = next;
                _currentFile = loaded;
                ShowIcon();
                old.Dispose();
                return;
            }
        }
        ShowIcon();
    }

    /// <summary>Affiche l'icône de base, ou sa copie avec la pastille si une mise à jour est disponible (recomposée seulement si la base change).</summary>
    private void ShowIcon()
    {
        if (_disposed) return;
        var previous = _badgedIcon;
        if (!_badge)
        {
            if (previous == null && ReferenceEquals(_icon.Icon, _baseIcon)) return;
            _badgedIcon = null;
            _icon.Icon = _baseIcon;
        }
        else if (previous == null || !ReferenceEquals(_badgedFrom, _baseIcon))
        {
            _badgedIcon = WithUpdateBadge(_baseIcon);
            _badgedFrom = _baseIcon;
            _icon.Icon = _badgedIcon;
            if (CaptureOptions.Current.CaptureMode) DumpBadgedIconForCapture(_badgedIcon);
        }
        else return;
        previous?.Dispose();
    }

    // Copie de l'icône (16 ou 32 px) avec un disque bleu #0067C0 de 8 px en bas à droite. Aucun fichier supplémentaire.
    private static Icon WithUpdateBadge(Icon source)
    {
        using var src = source.ToBitmap();
        var size = src.Width;
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.DrawImage(src, 0, 0, size, size);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var blue = new SolidBrush(Color.FromArgb(0x00, 0x67, 0xC0));
            g.FillEllipse(blue, size - 8, size - 8, 8, 8);
        }
        var h = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(h);
            return (Icon)tmp.Clone();
        }
        finally { NativeMethods.DestroyIcon(h); }
    }

    // Mode capture : icône composée (avec la pastille) écrite dans %TEMP% pour le testeur (PNG et .ico), chemin du .ico dans selecto.log.
    private static void DumpBadgedIconForCapture(Icon badged)
    {
        try
        {
            var temp = System.IO.Path.GetTempPath();
            using (var bmp = badged.ToBitmap())
                bmp.Save(System.IO.Path.Combine(temp, "selecto-tray-badge.png"), ImageFormat.Png);
            var icoPath = System.IO.Path.Combine(temp, "selecto-tray-badge.ico");
            using (var stream = System.IO.File.Create(icoPath))
                badged.Save(stream);
            Log.Write("Capture : icône tray " + icoPath);
        }
        catch (Exception ex) { Log.Write("Capture : icône tray impossible à enregistrer : " + ex.Message); }
    }

    // Charge la ressource WPF Assets\<fichier> à la taille de la zone de notification ; null si elle manque.
    private static Icon? LoadIcon(string file, out string? loadedFile)
    {
        loadedFile = null;
        try
        {
            var info = System.Windows.Application.GetResourceStream(new Uri(TrayIconChoice.PackUri(file)));
            if (info == null) return null;
            using var stream = info.Stream;
            var icon = new Icon(stream, SystemInformation.SmallIconSize);
            loadedFile = file;
            return icon;
        }
        catch (Exception ex)
        {
            Log.Write("Icône de zone de notification introuvable (" + file + ") : " + ex.Message);
            return null;
        }
    }

    // Repli : icône dessinée en code (GDI+) si la ressource manque.
    private static Icon MakeIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var back = new LinearGradientBrush(new Rectangle(0, 0, 32, 32), Color.FromArgb(0x4C, 0x8D, 0xFF), Color.FromArgb(0x7B, 0x5C, 0xFF), 45f);
            using var path = RoundedRect(new RectangleF(1, 6, 30, 20), 10);
            g.FillPath(back, path);
            using var white = new SolidBrush(Color.White);
            g.FillEllipse(white, 6, 12, 8, 8);
            using var dim = new SolidBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF));
            g.FillEllipse(dim, 17, 14, 4, 4);
            g.FillEllipse(dim, 23, 14, 4, 4);
        }
        var h = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(h);
            return (Icon)tmp.Clone();
        }
        finally { NativeMethods.DestroyIcon(h); }
    }

    private static GraphicsPath RoundedRect(RectangleF r, float d)
    {
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public void Dispose()
    {
        _disposed = true;
        _clickTimer.Stop();
        _clickTimer.Dispose();
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        L.CultureChanged -= OnCultureChanged;
        _icon.Visible = false;
        _icon.Dispose();
        _badgedIcon?.Dispose();
        _baseIcon.Dispose();
    }
}
