using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using SelectBar.Setup.Install;
using SelectBar.Setup.Logic;
using Ui = Wpf.Ui.Controls;

namespace SelectBar.Setup.UI;

internal sealed partial class SetupWindow
{
    // ---------- petits constructeurs ----------

    private static TextBlock Tx(string text, double size = 14, FontWeight? weight = null, Brush? fg = null, TextAlignment align = TextAlignment.Left, Thickness? margin = null, bool wrap = true)
    {
        var t = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight ?? FontWeights.Normal,
            Foreground = fg ?? SetupTheme.Text,
            TextAlignment = align,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            Margin = margin ?? new Thickness(0),
            FontFamily = size >= 18 ? SetupTheme.Display : SetupTheme.Body,
        };
        return t;
    }

    private static Ui.Button Btn(string text, Action click, bool primary, double minWidth = 0)
    {
        var b = new Ui.Button
        {
            Content = text,
            Appearance = primary ? Ui.ControlAppearance.Primary : Ui.ControlAppearance.Secondary,
            MinWidth = minWidth,
            Padding = new Thickness(20, 8, 20, 8),
        };
        b.Click += (_, _) => click();
        return b;
    }

    private static Border Card(UIElement child, Thickness padding)
        => new()
        {
            CornerRadius = new CornerRadius(8),
            Background = SetupTheme.Card,
            BorderBrush = SetupTheme.CardBorder,
            BorderThickness = new Thickness(1),
            Padding = padding,
            Child = child,
        };

    // ---------- 1. Bienvenue ----------

    private FrameworkElement BuildWelcome(bool animate)
    {
        var g = new Grid { Margin = new Thickness(40, 0, 36, 28) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) };
        var logo = new LogoControl(92) { HorizontalAlignment = HorizontalAlignment.Left };
        left.Children.Add(logo);
        left.Children.Add(Tx(T("app.name"), 40, FontWeights.SemiBold, null, TextAlignment.Left, new Thickness(0, 18, 0, 0), false));
        left.Children.Add(Tx(T("app.tagline"), 18, FontWeights.Normal, SetupTheme.SubText, TextAlignment.Left, new Thickness(0, 2, 0, 0)));
        var versionLine = _installedVersion == null ? T("welcome.version", BuildInfo.Version, BuildInfo.Arch) : T("welcome.updating", _installedVersion);
        left.Children.Add(Tx(versionLine, 12.5, FontWeights.Normal, SetupTheme.SubText, TextAlignment.Left, new Thickness(0, 10, 0, 0)));

        var right = new Grid { Margin = new Thickness(8, 24, 0, 8) };
        var demo = BuildDemoPanel();
        var options = BuildOptionsCard(() => _toggleOptions?.Invoke());
        right.Children.Add(demo);
        right.Children.Add(options);
        demo.Visibility = _optionsOpen ? Visibility.Collapsed : Visibility.Visible;
        options.Visibility = _optionsOpen ? Visibility.Visible : Visibility.Collapsed;
        _toggleOptions = () =>
        {
            _optionsOpen = !_optionsOpen;
            var show = _optionsOpen ? options : demo;
            var hide = _optionsOpen ? demo : options;
            hide.Visibility = Visibility.Collapsed;
            show.Visibility = Visibility.Visible;
            Motion.FadeSlideIn(show, 220, 6);
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 30, 0, 0) };
        row.Children.Add(Btn(T(_installedVersion == null ? "welcome.install" : "welcome.update"), StartInstall, true, 168));
        var opt = new Ui.Button { Content = T("welcome.options"), Appearance = Ui.ControlAppearance.Transparent, Margin = new Thickness(10, 0, 0, 0), Padding = new Thickness(14, 8, 14, 8) };
        opt.Click += (_, _) => _toggleOptions?.Invoke();
        row.Children.Add(opt);
        left.Children.Add(row);

        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        g.Children.Add(left);
        g.Children.Add(right);
        logo.Play(animate);
        return g;
    }

    private FrameworkElement BuildDemoPanel()
    {
        var column = new StackPanel();

        // la barre flottante de démonstration
        var pill = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = SetupTheme.BarBack,
            Padding = new Thickness(6, 4, 6, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(92, 0, 0, 10),
            RenderTransform = new TranslateTransform(),
        };
        var icons = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var glyph in new[] { "", "", "" })
        {
            icons.Children.Add(new TextBlock { Text = glyph, FontFamily = SetupTheme.Icons, FontSize = 16, Width = 34, Padding = new Thickness(0, 7, 0, 0), TextAlignment = TextAlignment.Center, Foreground = SetupTheme.BarText });
        }
        icons.Children.Add(new TextBlock { Text = "✦", FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = 16, Width = 34, Padding = new Thickness(0, 6, 0, 0), TextAlignment = TextAlignment.Center, Foreground = new SolidColorBrush(SetupTheme.Hex(SelectBar.Settings.ThemePalette.AccentDark)) });
        pill.Child = icons;
        column.Children.Add(pill);

        var para = new TextBlock { FontSize = 15, LineHeight = 24, TextWrapping = TextWrapping.Wrap, Foreground = SetupTheme.Text };
        para.Inlines.Add(new System.Windows.Documents.Run(T("welcome.demoText1")));
        para.Inlines.Add(new System.Windows.Documents.Run(T("welcome.demoSelected")) { Background = SetupTheme.SelectionFill });
        para.Inlines.Add(new System.Windows.Documents.Run(T("welcome.demoText2")));
        column.Children.Add(para);
        column.Children.Add(Tx(T("welcome.demoText3"), 13, FontWeights.Normal, SetupTheme.SubText, TextAlignment.Left, new Thickness(0, 10, 0, 0)));
        foreach (var w in new double[] { 0.9, 0.7 })
            column.Children.Add(new Rectangle { Height = 7, RadiusX = 3.5, RadiusY = 3.5, Fill = SetupTheme.Separator, HorizontalAlignment = HorizontalAlignment.Left, Width = 300 * w, Margin = new Thickness(0, 12, 0, 0) });

        pill.Loaded += (_, _) =>
        {
            if (!Motion.Enabled) return;
            var tr = (TranslateTransform)pill.RenderTransform;
            var opacity = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(6), RepeatBehavior = RepeatBehavior.Forever };
            var shift = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(6), RepeatBehavior = RepeatBehavior.Forever };
            double[] times = { 0, 0.4, 1.0, 4.6, 5.3, 6.0 };
            double[] ops = { 0, 0, 1, 1, 0, 0 };
            double[] ys = { 4, 4, 0, 0, 4, 4 };
            for (var i = 0; i < times.Length; i++)
            {
                var kt = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(times[i]));
                opacity.KeyFrames.Add(new LinearDoubleKeyFrame(ops[i], kt));
                shift.KeyFrames.Add(new LinearDoubleKeyFrame(ys[i], kt));
            }
            pill.BeginAnimation(UIElement.OpacityProperty, opacity);
            tr.BeginAnimation(TranslateTransform.YProperty, shift);
        };
        pill.Unloaded += (_, _) =>
        {
            pill.BeginAnimation(UIElement.OpacityProperty, null);
            ((TranslateTransform)pill.RenderTransform).BeginAnimation(TranslateTransform.YProperty, null);
        };

        var card = Card(column, new Thickness(28, 26, 28, 28));
        card.VerticalAlignment = VerticalAlignment.Center;
        return card;
    }

    private FrameworkElement BuildOptionsCard(Action close)
    {
        var sp = new StackPanel();
        sp.Children.Add(Tx(T("opt.title"), 16, FontWeights.SemiBold));
        sp.Children.Add(Tx(T("opt.folder"), 12.5, FontWeights.Normal, SetupTheme.SubText, TextAlignment.Left, new Thickness(0, 14, 0, 4)));

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var box = new TextBox { Text = _dir, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(8, 6, 8, 6) };
        box.TextChanged += (_, _) => _dir = box.Text;
        var browse = Btn(T("opt.browse"), () =>
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { InitialDirectory = _dir };
            if (dlg.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(dlg.FolderName)) box.Text = dlg.FolderName;
        }, false);
        browse.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(browse, 1);
        row.Children.Add(box);
        row.Children.Add(browse);
        sp.Children.Add(row);

        var desktop = new CheckBox { Content = T("opt.desktop"), IsChecked = _desktop, Margin = new Thickness(0, 14, 0, 0) };
        desktop.Click += (_, _) => _desktop = desktop.IsChecked == true;
        var startup = new CheckBox { Content = T("opt.startup"), IsChecked = _startup, Margin = new Thickness(0, 4, 0, 0) };
        startup.Click += (_, _) => _startup = startup.IsChecked == true;
        sp.Children.Add(desktop);
        sp.Children.Add(startup);

        var lang = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        lang.Children.Add(new TextBlock { Text = T("opt.language"), Foreground = SetupTheme.SubText, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) });
        foreach (var (code, label) in new[] { ("fr", "Français"), ("en", "English") })
        {
            var rb = new RadioButton { Content = label, GroupName = "setup-lang", IsChecked = _lang == code, Margin = new Thickness(0, 0, 10, 0), MinWidth = 0 };
            var chosen = code;
            rb.Click += (_, _) =>
            {
                if (_lang == chosen) return;
                _lang = chosen;
                Title = Strings.Format(IsUninstall ? "window.uninstall" : "window.install", _lang, BuildInfo.DisplayName);
                Go(SetupScreen.Welcome, false);
            };
            lang.Children.Add(rb);
        }
        sp.Children.Add(lang);

        var ok = Btn(T("opt.ok"), () => close(), true, 96);
        ok.HorizontalAlignment = HorizontalAlignment.Right;
        ok.Margin = new Thickness(0, 16, 0, 0);
        sp.Children.Add(ok);
        var card = Card(sp, new Thickness(22, 18, 22, 20));
        card.VerticalAlignment = VerticalAlignment.Center;
        return card;
    }

    // ---------- 2. Progression ----------

    private FrameworkElement BuildProgress(bool uninstall)
    {
        var ring = new RingControl(150, 10);
        _ring = ring;
        _percentText = Tx("0 %", 30, FontWeights.SemiBold, null, TextAlignment.Center, null, false);
        _percentText.HorizontalAlignment = HorizontalAlignment.Center;
        _percentText.VerticalAlignment = VerticalAlignment.Center;
        var ringHost = new Grid { Width = 150, Height = 150, HorizontalAlignment = HorizontalAlignment.Center };
        ringHost.Children.Add(ring);
        ringHost.Children.Add(_percentText);

        _stepText = Tx(StepLabel(InstallStep.Prepare), 20, FontWeights.SemiBold, null, TextAlignment.Center, new Thickness(0, 22, 0, 0));
        _detailText = Tx("", 12, FontWeights.Normal, SetupTheme.SubText, TextAlignment.Center, new Thickness(0, 6, 0, 0), false);
        _detailText.TextTrimming = TextTrimming.CharacterEllipsis;
        _detailText.MaxWidth = 300;

        var col = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        col.Children.Add(ringHost);
        col.Children.Add(_stepText);
        col.Children.Add(_detailText);
        if (!uninstall)
        {
            _cancelButton = Btn(T("progress.cancel"), CancelInstall, false, 120);
            _cancelButton.HorizontalAlignment = HorizontalAlignment.Center;
            _cancelButton.Margin = new Thickness(0, 22, 0, 0);
            col.Children.Add(_cancelButton);
        }

        if (uninstall) return new Grid { Children = { col }, Margin = new Thickness(0, 0, 0, 24) };

        var g = new Grid { Margin = new Thickness(40, 0, 36, 28) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var carousel = new Carousel(new UIElement[]
        {
            Slide("", false, T("slide1.title"), T("slide1.text")),
            Slide("✦", true, T("slide2.title"), T("slide2.text")),
            Slide("", false, T("slide3.title"), T("slide3.text")),
        });
        var card = Card(carousel, new Thickness(0));
        card.Margin = new Thickness(0, 24, 0, 8);
        Grid.SetColumn(col, 0);
        Grid.SetColumn(card, 1);
        g.Children.Add(col);
        g.Children.Add(card);
        return g;
    }

    private static FrameworkElement Slide(string glyph, bool textGlyph, string title, string text)
    {
        var tile = new Border
        {
            Width = 56, Height = 56, CornerRadius = new CornerRadius(8), Background = SetupTheme.Accent, HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
            {
                Text = glyph, Foreground = SetupTheme.OnAccent, FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                FontFamily = textGlyph ? new FontFamily("Segoe UI Symbol") : SetupTheme.Icons,
            },
        };
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 300, Margin = new Thickness(0, 0, 0, 14) };
        sp.Children.Add(tile);
        sp.Children.Add(Tx(title, 20, FontWeights.SemiBold, null, TextAlignment.Center, new Thickness(0, 20, 0, 0)));
        sp.Children.Add(Tx(text, 14, FontWeights.Normal, SetupTheme.SubText, TextAlignment.Center, new Thickness(0, 8, 0, 0)));
        return new Grid { Children = { sp } };
    }

    // ---------- 3. Terminé (installation et désinstallation) ----------

    private FrameworkElement BuildDone(bool uninstall)
    {
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 480, Margin = new Thickness(0, 0, 0, 24) };
        var check = new CheckControl(96, uninstall ? SetupTheme.SubText : SetupTheme.Accent, SetupTheme.OnAccent) { HorizontalAlignment = HorizontalAlignment.Center };
        sp.Children.Add(check);
        sp.Children.Add(Tx(uninstall ? T("un.done", BuildInfo.DisplayName) : T("done.title", BuildInfo.DisplayName), 28, FontWeights.SemiBold, null, TextAlignment.Center, new Thickness(0, 22, 0, 0)));
        sp.Children.Add(Tx(uninstall ? T("un.doneTip") : T("done.tip"), 14, FontWeights.Normal, SetupTheme.SubText, TextAlignment.Center, new Thickness(0, 8, 0, 0)));
        if (!uninstall)
        {
            var launch = new CheckBox { Content = T("done.launch"), IsChecked = _launchAfter, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 24, 0, 0) };
            launch.Click += (_, _) => _launchAfter = launch.IsChecked == true;
            sp.Children.Add(launch);
        }
        var finish = Btn(uninstall ? T("err.close") : T("done.finish"), FinishAndClose, true, 168);
        finish.HorizontalAlignment = HorizontalAlignment.Center;
        finish.Margin = new Thickness(0, uninstall ? 30 : 16, 0, 0);
        sp.Children.Add(finish);
        return new Grid { Children = { sp } };
    }

    // ---------- 4. Erreur ----------

    private FrameworkElement BuildError()
    {
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Width = 560, Margin = new Thickness(0, 0, 0, 24) };
        var head = new StackPanel { Orientation = Orientation.Horizontal };
        var badge = new Grid { Width = 44, Height = 44 };
        badge.Children.Add(new Ellipse { Fill = SetupTheme.Danger });
        badge.Children.Add(new TextBlock { Text = "!", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = SetupTheme.OnAccent, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        head.Children.Add(badge);
        var title = Tx(_lastWasUninstall ? T("err.utitle") : T("err.title"), 24, FontWeights.SemiBold, null, TextAlignment.Left, new Thickness(16, 0, 0, 0), false);
        title.VerticalAlignment = VerticalAlignment.Center;
        head.Children.Add(title);
        sp.Children.Add(head);

        var details = new TextBox
        {
            Text = string.IsNullOrWhiteSpace(_errorMessage) ? _errorDetails : _errorMessage,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 120,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Margin = new Thickness(0, 20, 0, 0),
            Padding = new Thickness(10, 8, 10, 8),
        };
        sp.Children.Add(details);
        sp.Children.Add(Tx(T("err.log", SetupLog.FilePath), 12, FontWeights.Normal, SetupTheme.SubText, TextAlignment.Left, new Thickness(0, 8, 0, 0)));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 22, 0, 0) };
        var copy = Btn(T("err.copy"), () => { }, false);
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(_errorDetails + Environment.NewLine + SetupLog.FilePath); copy.Content = T("err.copied"); }
            catch (Exception ex) { SetupLog.Write("Presse-papiers : " + ex.Message); }
        };
        buttons.Children.Add(copy);
        var retry = Btn(T("err.retry"), Retry, true, 120);
        retry.Margin = new Thickness(10, 0, 0, 0);
        buttons.Children.Add(retry);
        var close = Btn(T("err.close"), () => { _forceClose = true; Close(); }, false);
        close.Margin = new Thickness(10, 0, 0, 0);
        buttons.Children.Add(close);
        sp.Children.Add(buttons);
        return new Grid { Children = { sp } };
    }

    // ---------- 5. Désinstallation : confirmation ----------

    private FrameworkElement BuildUninstallConfirm()
    {
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Width = 520, Margin = new Thickness(0, 0, 0, 24) };
        var logo = new LogoControl(64) { HorizontalAlignment = HorizontalAlignment.Left };
        sp.Children.Add(logo);
        logo.Play(true);
        sp.Children.Add(Tx(T("un.title", BuildInfo.DisplayName), 28, FontWeights.SemiBold, null, TextAlignment.Left, new Thickness(0, 18, 0, 0)));
        sp.Children.Add(Tx(T("un.body"), 14, FontWeights.Normal, SetupTheme.SubText, TextAlignment.Left, new Thickness(0, 8, 0, 0)));

        var purge = new CheckBox { Content = T("un.purge", "…"), IsChecked = _purge, Margin = new Thickness(0, 22, 0, 0) };
        purge.Click += (_, _) => _purge = purge.IsChecked == true;
        var hint = Tx(T("un.purgeHint"), 12, FontWeights.Normal, SetupTheme.SubText, TextAlignment.Left, new Thickness(28, 2, 0, 0));
        sp.Children.Add(purge);
        sp.Children.Add(hint);
        _ = FillPurgeSizeAsync(purge, hint);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 26, 0, 0) };
        buttons.Children.Add(Btn(T("un.button"), StartUninstall, true, 150));
        var cancel = Btn(T("un.cancel"), () => { _forceClose = true; Close(); }, false, 110);
        cancel.Margin = new Thickness(10, 0, 0, 0);
        buttons.Children.Add(cancel);
        sp.Children.Add(buttons);
        return new Grid { Children = { sp } };
    }

    private async Task FillPurgeSizeAsync(CheckBox purge, UIElement hint)
    {
        long bytes;
        bool exists;
        if (_o.Demo) { bytes = 1_500_000_000; exists = true; }
        else
        {
            var roaming = InstallPaths.RoamingDataDir(SystemOps.AppData);
            var local = InstallPaths.LocalDataDir(SystemOps.LocalAppData);
            (bytes, exists) = await Task.Run(() => (SystemOps.DirSize(roaming) + SystemOps.DirSize(local), System.IO.Directory.Exists(roaming) || System.IO.Directory.Exists(local)));
        }
        if (_screen != SetupScreen.UninstallConfirm) return;
        if (!exists) { purge.Visibility = Visibility.Collapsed; hint.Visibility = Visibility.Collapsed; _purge = false; return; }
        purge.Content = T("un.purge", SizeFormat.Format(bytes, true, _lang));
    }
}
