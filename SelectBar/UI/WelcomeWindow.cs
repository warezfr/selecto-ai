using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SelectBar.Packaging;
using SelectBar.Online;
using SelectBar.Settings;
using WpfUi = Wpf.Ui.Controls;
using SelectBar.Localization;

namespace SelectBar.UI;

/// <summary>Ce dont la fenêtre « Bienvenue » a besoin (branché par AppController) ; les classes de logique sont celles des réglages.</summary>
public sealed class WelcomeHost
{
    public required AppSettings Settings { get; init; }
    public required Func<string?> Apply { get; init; }
    public required ApiKeyVault Vault { get; init; }
    public required LocalAiManager LocalAi { get; init; }
    public required LlmModelClient Models { get; init; }
    public required Func<Task<ResultPayload>> TestLlm { get; init; }
    public required Action<string> OpenLink { get; init; }
    /// <summary>Appelé à la fermeture (« C'est parti », « Passer » ou croix) : mémorise que le premier lancement est terminé.</summary>
    public required Action Finished { get; init; }
}

/// <summary>
/// Fenêtre « Bienvenue » du premier lancement, en trois écrans courts avec indicateur de progression : 1) ce que fait Selecto AI,
/// 2) niveau d'IA (Aucune / IA locale gratuite / Mon fournisseur), 3) logiciel libre et gratuit (lien GitHub). « Passer » est toujours
/// disponible ; rien n'est bloquant. L'enchaînement vient de FirstRunFlow (logique pure testée).
/// </summary>
public sealed class WelcomeWindow : WpfUi.FluentWindow
{
    private readonly WelcomeHost _host;
    private AppSettings S => _host.Settings;
    private WelcomeStep _step = WelcomeStep.Intro;
    private WelcomeAiChoice _choice = WelcomeAiChoice.Local;
    private readonly ContentControl _screen = new();
    private readonly StackPanel _dots = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _progress = new() { FontSize = 12, FontFamily = Theme.Font, Foreground = Theme.SubText, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
    private StackPanel? _aiDetails;
    private ProgressBar? _pullBar;
    private TextBlock? _pullText;
    private string _aiMessage = "";
    private bool _aiMessageIsError;
    private bool _wantModel;                       // le moteur vient d'être demandé : le modèle recommandé suit tout seul
    private bool _busyOnline;
    private string _onlineProvider = LlmPresets.OpenAi;
    private string _typedKey = "";
    private CancellationTokenSource? _cts;

    public WelcomeWindow(WelcomeHost host, WelcomeStep startStep = WelcomeStep.Intro)
    {
        WindowFactory.Apply(this);
        _host = host;
        _step = startStep;
        Title = L.Get("Welcome_Title");
        Width = 680; Height = 600; MinHeight = 560;
        ResizeMode = ResizeMode.CanMinimize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ExtendsContentIntoTitleBar = true;
        WindowBackdropType = WpfUi.WindowBackdropType.Mica;
        FontFamily = Theme.Font;
        System.Windows.Automation.AutomationProperties.SetName(this, L.Get("Welcome_Title"));

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new WpfUi.TitleBar { Title = L.Get("Welcome_TitleBar"), ShowMinimize = true, ShowMaximize = false, CanMaximize = false });

        _screen.Margin = new Thickness(40, 8, 40, 8);
        Grid.SetRow(_screen, 1);
        root.Children.Add(_screen);

        var footer = new Grid { Margin = new Thickness(40, 8, 40, 24) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(_dots);
        left.Children.Add(_progress);
        footer.Children.Add(left);
        Grid.SetColumn(_buttons, 1);
        footer.Children.Add(_buttons);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        Content = root;

        _host.LocalAi.Changed += OnLocalAiChanged;
        Closed += (_, _) =>
        {
            _host.LocalAi.Changed -= OnLocalAiChanged;
            try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
            _host.Finished();       // les téléchargements en cours continuent en arrière-plan
        };
        GoToStep(_step);
    }

    // ---------------------------------------------------------------- navigation

    private void GoToStep(WelcomeStep step)
    {
        _step = step;
        _aiDetails = null; _pullBar = null; _pullText = null;
        _screen.Content = step switch
        {
            WelcomeStep.Intro => BuildIntro(),
            WelcomeStep.Ai => BuildAi(),
            _ => BuildFree()
        };
        BuildChrome();
    }

    private void BuildChrome()
    {
        _dots.Children.Clear();
        foreach (var on in FirstRunFlow.Dots(_step))
            _dots.Children.Add(new Border
            {
                Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 6, 0),
                Background = on ? Theme.Accent : Theme.ButtonBorder
            });
        _progress.Text = FirstRunFlow.ProgressText(_step);
        System.Windows.Automation.AutomationProperties.SetName(_dots, L.Get("Welcome_StepPrefix") + FirstRunFlow.ProgressText(_step));

        _buttons.Children.Clear();
        if (!FirstRunFlow.IsLast(_step))
        {
            // marge 8 px entre tous les boutons du pied (WPF miroite les marges sous FlowDirection)
            var skip = Theme.LinkButton(L.Get("Welcome_Skip"), Close, automationName: L.Get("Welcome_SkipName"));
            skip.Margin = new Thickness(0, 0, 8, 0);
            _buttons.Children.Add(skip);
        }
        if (FirstRunFlow.CanGoBack(_step))
            _buttons.Children.Add(Theme.Btn(L.Get("Welcome_Back"), () => GoToStep(FirstRunFlow.Back(_step))));
        var next = Theme.Btn(FirstRunFlow.PrimaryLabel(_step), () =>
        {
            if (FirstRunFlow.IsLast(_step)) Close(); else GoToStep(FirstRunFlow.Next(_step));
        }, primary: true);
        next.Margin = new Thickness(0);
        next.IsDefault = true;
        _buttons.Children.Add(next);
    }

    private static TextBlock Heading(string text)
    {
        var t = Theme.Label(text, 28, Theme.Text, FontWeights.SemiBold);
        t.Margin = new Thickness(0, 8, 0, 12);
        return t;
    }

    private static TextBlock Body(string text)
    {
        var t = Theme.Label(text, 14, Theme.Text);
        t.Margin = new Thickness(0, 0, 0, 8);
        return t;
    }

    // ---------------------------------------------------------------- écran 1 : ce que fait Selecto AI

    private UIElement BuildIntro()
    {
        var page = new StackPanel();
        page.Children.Add(Heading(L.Get("Welcome_Title")));
        page.Children.Add(Body(L.Get("Welcome_Text1")
                               + L.Get("Welcome_Text2")));

        // démonstration : un texte sélectionné et la barre qui apparaît (animation légère, coupée si Windows réduit les animations)
        var inner = new StackPanel { Orientation = Orientation.Horizontal };
        BarVisuals.Current = BarPalette.For(S.BarStyle);
        inner.Children.Add(BarVisuals.Button("", "", L.Get("Welcome_Demo_Search"), true, null));
        inner.Children.Add(BarVisuals.Button(null, L.Get("Welcome_Demo_Copy"), L.Get("Welcome_Demo_Copy"), false, null));
        inner.Children.Add(BarVisuals.Separator());
        inner.Children.Add(BarVisuals.Button(null, L.Get("Welcome_Demo_Translate"), L.Get("Welcome_Demo_Translate"), false, null));
        inner.Children.Add(BarVisuals.Button(null, L.Get("Action_Ai"), L.Get("Action_Ai"), false, null));
        var pill = BarVisuals.CreatePill(inner, 1.0);
        pill.HorizontalAlignment = HorizontalAlignment.Center;
        System.Windows.Automation.AutomationProperties.SetName(pill, L.Get("Welcome_BarExample"));
        if (BarVisuals.MotionAllowed)
        {
            var anim = new DoubleAnimation(0.25, 1, TimeSpan.FromMilliseconds(700))
            {
                AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            pill.BeginAnimation(OpacityProperty, anim);
        }
        var sample = new Border
        {
            Background = Theme.Solid(FluentTheme.IsDark ? ThemePalette.AccentDark : ThemePalette.Accent, 0.28), Padding = new Thickness(8, 2, 8, 2),
            HorizontalAlignment = HorizontalAlignment.Center, Child = Theme.Label(L.Get("Welcome_SelectThis"), 18)
        };
        var demo = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
        demo.Children.Add(pill);
        demo.Children.Add(sample);
        page.Children.Add(Theme.Line(new Border { MinHeight = 140, Padding = new Thickness(0, 16, 0, 16), Child = demo }));
        if (S.HotkeyEnabled && HotkeyParser.TryParse(S.Hotkey, out _, out _))
        {
            var hint = Theme.Label(L.Format("Welcome_ShortcutHint", HotkeyParser.Display(S.Hotkey)), 14, Theme.Text);
            hint.Margin = new Thickness(0, 4, 0, 4);
            page.Children.Add(hint);
        }
        page.Children.Add(Theme.Sub(L.Get("Welcome_TrayNote")));
        return page;
    }

    // ---------------------------------------------------------------- écran 2 : niveau d'IA

    private UIElement BuildAi()
    {
        var page = new StackPanel();
        page.Children.Add(Heading(L.Get("Welcome_AiLevel")));
        page.Children.Add(Body(L.Get("Welcome_AiLevelText")));
        foreach (var c in new[] { WelcomeAiChoice.None, WelcomeAiChoice.Local, WelcomeAiChoice.Online })
            page.Children.Add(ChoiceCard(c));
        // actions en ligne : activées par défaut ; le choix « Aucune IA » ne coupe pas Traduire et Dictionnaire
        var online = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        online.Children.Add(Theme.Sub(L.Get("Welcome_OnlineNote")));
        WpfUi.Button? disable = null;
        disable = Theme.Btn(L.Get("Welcome_Disable"), () =>
        {
            S.OnlineActionsEnabled = false;
            _host.Apply();
            if (disable != null) { disable.Content = L.Get("Welcome_Disabled"); disable.IsEnabled = false; }
        });
        disable.IsEnabled = S.OnlineActionsEnabled;
        if (!S.OnlineActionsEnabled) disable.Content = L.Get("Welcome_Disabled");
        disable.Margin = new Thickness(0, 6, 0, 0);
        online.Children.Add(disable);
        page.Children.Add(online);
        _aiDetails = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        page.Children.Add(_aiDetails);
        RenderAiDetails();
        return page;
    }

    private Border ChoiceCard(WelcomeAiChoice c)
    {
        var selected = _choice == c;
        var label = FirstRunFlow.AiChoiceLabel(c);
        var desc = c switch
        {
            WelcomeAiChoice.None => L.Get("Welcome_NoAi"),
            WelcomeAiChoice.Local => L.Get("Welcome_LocalAi"),
            _ => L.Get("Welcome_OnlineAi")
        };
        var stack = new StackPanel();
        stack.Children.Add(Theme.Label(label, 14, Theme.Text, FontWeights.SemiBold));
        stack.Children.Add(Theme.Label(desc, 12, Theme.SubText));
        var card = new Border
        {
            Background = Theme.CardBg, BorderBrush = selected ? Theme.Accent : Theme.ButtonBorder, BorderThickness = new Thickness(selected ? 2 : 1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(0, 0, 0, 4), Child = stack,
            Focusable = true
        };
        System.Windows.Automation.AutomationProperties.SetName(card, label + (selected ? L.Get("Welcome_Chosen") : ""));
        void Pick()
        {
            _choice = c;
            _aiMessage = "";
            GoToStep(WelcomeStep.Ai);
        }
        card.MouseLeftButtonUp += (_, _) => Pick();
        card.KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { e.Handled = true; Pick(); } };
        return card;
    }

    private void RenderAiDetails()
    {
        var host = _aiDetails;
        if (host == null) return;
        host.Children.Clear();
        _pullBar = null; _pullText = null;
        switch (_choice)
        {
            case WelcomeAiChoice.None:
                host.Children.Add(Theme.Sub(L.Get("Welcome_AddLaterAi")));
                break;
            case WelcomeAiChoice.Local:
                RenderLocalDetails(host);
                break;
            default:
                RenderOnlineDetails(host);
                break;
        }
        if (_aiMessage.Length > 0)
        {
            var m = Theme.Label(_aiMessage, 12, _aiMessageIsError ? Theme.Warn : Theme.StatusOn);
            m.Margin = new Thickness(0, 6, 0, 0);
            host.Children.Add(m);
        }
    }

    private bool LocalReady => S.LlmProvider == LlmPresets.Ollama && !string.IsNullOrEmpty(S.LlmModel) && !_host.LocalAi.Busy && _host.LocalAi.IsEngineInstalled;

    private void RenderLocalDetails(StackPanel host)
    {
        var la = _host.LocalAi;
        var ram = LocalAiManager.TotalMemoryBytes();
        var rec = ModelCatalog.Recommend(ram, LocalAiManager.LogicalCores(), LocalAiManager.HasAvx2());
        if (la.Busy)
        {
            host.Children.Add(Theme.Label(la.Activity, 14, Theme.Text, FontWeights.SemiBold));
            _pullBar = new ProgressBar { Minimum = 0, Maximum = 1, Height = 6, Margin = new Thickness(0, 6, 0, 0), IsIndeterminate = true };
            _pullText = Theme.Sub("");
            _pullText.Margin = new Thickness(0, 4, 0, 0);
            host.Children.Add(_pullBar);
            host.Children.Add(_pullText);
            UpdateProgress();
            host.Children.Add(Theme.Sub(L.Get("Welcome_ContinueDownload")));
            var cancel = Theme.Btn(L.Get("Welcome_Cancel"), () => { _wantModel = false; la.Cancel(); });
            cancel.Margin = new Thickness(0, 8, 0, 0);
            host.Children.Add(cancel);
            return;
        }
        if (LocalReady)
        {
            host.Children.Add(Theme.Label(L.Get("Welcome_ReadyPrefix") + S.LlmModel, 14, Theme.StatusOn, FontWeights.SemiBold));
            return;
        }
        var size = la.IsEngineInstalled ? rec.SizeText : OllamaParsers.FormatSize(LocalAiManager.ApproxEngineBytes(LocalAiManager.IsArm64) + rec.SizeBytes);
        host.Children.Add(Theme.Sub((ram > 0 ? ModelCatalog.FormatRam(ram) + ". " : "") + L.Format("Welcome_RecommendedModel", rec.Label, L.Ltr(rec.SizeText))));
        host.Children.Add(Theme.Sub(ModelCatalog.SpeedHint(rec)));
        if (la.Message != null)
        {
            var lm = Theme.Label(la.Message, 12, la.MessageIsError ? Theme.Warn : Theme.StatusOn);
            lm.Margin = new Thickness(0, 4, 0, 0);
            host.Children.Add(lm);
        }
        var install = Theme.Btn(L.Format("Welcome_InstallLocal", L.Ltr(size)), () =>
        {
            _aiMessage = "";
            _wantModel = true;
            if (!la.IsEngineInstalled) la.StartInstall(rec.SizeBytes);
            else la.StartPull(rec.Id, rec.SizeBytes);
            RenderAiDetails();
        }, primary: true);
        install.Margin = new Thickness(0, 8, 0, 0);
        host.Children.Add(install);
    }

    private void UpdateProgress()
    {
        var la = _host.LocalAi;
        if (_pullBar == null || _pullText == null) return;
        var f = la.Progress?.Fraction;
        _pullBar.IsIndeterminate = f == null;
        if (f != null) _pullBar.Value = f.Value;
        _pullText.Text = la.Progress?.Describe() ?? "";
    }

    private void OnLocalAiChanged() => Dispatcher.BeginInvoke(new Action(() =>
    {
        if (_step != WelcomeStep.Ai || _choice != WelcomeAiChoice.Local) return;
        var la = _host.LocalAi;
        if (la.Busy && _pullBar != null) { UpdateProgress(); return; }
        // le moteur est installé : le modèle recommandé suit (même parcours que les réglages, sans quitter la fenêtre)
        if (!la.Busy && _wantModel && la.IsEngineInstalled && la.MessageIsError == false && !LocalReady)
        {
            _wantModel = false;
            var rec = ModelCatalog.Recommend(LocalAiManager.TotalMemoryBytes(), LocalAiManager.LogicalCores(), LocalAiManager.HasAvx2());
            la.StartPull(rec.Id, rec.SizeBytes);
        }
        else if (!la.Busy) _wantModel = false;
        RenderAiDetails();
    }));

    private void RenderOnlineDetails(StackPanel host)
    {
        var presets = LlmPresets.Online.Where(p => p.Id != LlmPresets.Custom).ToList();
        if (presets.All(p => p.Id != _onlineProvider)) _onlineProvider = presets[0].Id;
        var combo = new ComboBox { MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Stretch, FontFamily = Theme.Font, FontSize = 14 };
        foreach (var p in presets) combo.Items.Add(p.Label);
        combo.SelectedIndex = Math.Max(0, presets.FindIndex(p => p.Id == _onlineProvider));
        System.Windows.Automation.AutomationProperties.SetName(combo, L.Get("Welcome_ProviderName"));
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0) { _onlineProvider = presets[combo.SelectedIndex].Id; _aiMessage = ""; }
        };
        host.Children.Add(combo);

        var key = new PasswordBox { MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Stretch, FontFamily = Theme.Font, FontSize = 14, Margin = new Thickness(0, 8, 0, 0), Password = _typedKey };
        key.PasswordChanged += (_, _) => _typedKey = key.Password;
        System.Windows.Automation.AutomationProperties.SetName(key, L.Get("Welcome_ApiKeyName"));
        host.Children.Add(key);

        var preset = presets.First(p => p.Id == _onlineProvider);
        var line = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        if (preset.KeysPage != null)
        {
            var page = preset.KeysPage;
            line.Children.Add(Theme.LinkButton(L.Get("Welcome_GetKey"), () => _host.OpenLink(page)));
        }
        host.Children.Add(line);
        host.Children.Add(Theme.Sub(LlmPresets.PrivacyNotice(preset.Id)));

        var go = Theme.Btn(_busyOnline ? L.Get("Welcome_Checking") : L.Get("Welcome_VerifySave"), () => VerifyOnline(preset), primary: true);
        go.IsEnabled = !_busyOnline;
        go.Margin = new Thickness(0, 8, 0, 0);
        host.Children.Add(go);
    }

    private async void VerifyOnline(LlmPreset preset)
    {
        var typed = _typedKey.Trim();
        if (typed.Length == 0 && preset.NeedsKey)
        {
            SetAiMessage(L.Format("Welcome_PasteKeyFirst", preset.Label), true);
            return;
        }
        if (!LlmUrlValidator.Validate(preset.BaseUrl, typed.Length > 0, out var normalized, out var urlError))
        {
            SetAiMessage(urlError ?? L.Get("Welcome_InvalidUrl"), true);
            return;
        }
        _busyOnline = true;
        SetAiMessage(L.Get("Welcome_FetchingModels"), false);
        try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
        var cts = _cts = new CancellationTokenSource();
        try
        {
            var result = await _host.Models.ListAsync(preset, normalized, typed, cts.Token);
            if (cts.IsCancellationRequested) return;
            var model = result.Error == null ? LlmModelLists.DefaultModel(result.Models) : null;
            if (model == null)
            {
                _busyOnline = false;
                SetAiMessage(result.Error ?? L.Get("Welcome_NoModel"), true);
                return;
            }
            LlmPresets.Apply(S, preset.Id);
            S.LlmModel = model;
            if (typed.Length > 0) ProviderKeys.Set(S, preset.Id, _host.Vault.Protect(typed));
            _host.Apply();
            _typedKey = "";
            SetAiMessage(L.Get("Welcome_Testing"), false);
            var test = await _host.TestLlm();
            _busyOnline = false;
            if (test.Error == null) SetAiMessage(L.Get("Welcome_ReadyPrefix") + model, false);
            else SetAiMessage(test.Error, true);
        }
        catch (OperationCanceledException) { _busyOnline = false; }
        catch (Exception ex)
        {
            Log.Write("Bienvenue : fournisseur d'IA : " + ex.Message);
            _busyOnline = false;
            SetAiMessage(L.Get("Welcome_CheckFailed"), true);
        }
    }

    private void SetAiMessage(string text, bool error)
    {
        _aiMessage = text;
        _aiMessageIsError = error;
        if (_step == WelcomeStep.Ai) RenderAiDetails();
    }

    // ---------------------------------------------------------------- écran 3 : logiciel libre et gratuit

    private UIElement BuildFree()
    {
        var page = new StackPanel();
        page.Children.Add(Heading(L.Get("Welcome_FreeTitle")));
        page.Children.Add(Body(L.Get("Welcome_FreeText")));
        var github = Theme.LinkButton(L.Get("Welcome_FreeGitHub"), () => _host.OpenLink(EditionInfo.GitHubUrl));
        github.HorizontalAlignment = HorizontalAlignment.Left;
        github.Margin = new Thickness(0, 8, 0, 0);
        System.Windows.Automation.AutomationProperties.SetName(github, EditionInfo.GitHubUrl);
        page.Children.Add(github);
        page.Children.Add(Theme.Sub(L.Get("Welcome_LetsGoNote")));
        return page;
    }
}
