using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SelectBar.Actions;
using SelectBar.Extensions;
using SelectBar.Packaging;
using SelectBar.Online;
using SelectBar.Settings;
using SelectBar.Update;
using Wpf.Ui.Abstractions;
using ThemeWatcher = Wpf.Ui.Appearance.SystemThemeWatcher;
using WpfUi = Wpf.Ui.Controls;
using SelectBar.Localization;

namespace SelectBar.UI;

/// <summary>Ce dont la fenêtre de réglages a besoin ; « Appliquer » passe par Apply (sauvegarde + application), une seule fois par lot de modifications.</summary>
public sealed class SettingsHost
{
    public required AppSettings Settings { get; init; }
    public required ExcludedApps Apps { get; init; }
    public required ExcludedSites Sites { get; init; }
    public required string ExtensionsDirectory { get; init; }
    public required Func<List<ActionDefinition>> LoadActions { get; init; }
    /// <summary>Enregistre et applique les réglages ; renvoie un avertissement à afficher, ou null.</summary>
    public required Func<string?> Apply { get; init; }
    /// <summary>Chiffrement de la clé API (DPAPI).</summary>
    public required ApiKeyVault Vault { get; init; }
    public required OllamaClient Ollama { get; init; }
    /// <summary>Installation et pilotage d'Ollama dans l'application (tâches d'arrière-plan qui survivent à la fenêtre).</summary>
    public required LocalAiManager LocalAi { get; init; }
    /// <summary>Liste réelle des modèles d'un fournisseur en ligne (GET {base}/models avec la clé de l'utilisateur).</summary>
    public required LlmModelClient Models { get; init; }
    /// <summary>Ping minimal du modèle configuré.</summary>
    public required Func<Task<ResultPayload>> TestLlm { get; init; }
    /// <summary>Ouvre une page web (https seulement, via LinkSafety) : jamais d'exécution ni de téléchargement.</summary>
    public required Action<string> OpenLink { get; init; }
    /// <summary>Ouvre le dossier des extensions dans l'Explorateur (via le lanceur unique journalisé).</summary>
    public required Action OpenExtensionsFolder { get; init; }
    /// <summary>Mises à jour du site (V3-07) : état, téléchargement, lancement. Null hors application.</summary>
    public UpdateService? Updates { get; init; }
    /// <summary>Vérification immédiate (« Rechercher maintenant ») passée par le contrôleur, qui enregistre l'heure du contrôle.</summary>
    public Func<Task>? CheckUpdatesNow { get; init; }
    /// <summary>Ouvre « Signaler un problème » (V3-09) au-dessus des réglages. Null : pas de ligne dans À propos.</summary>
    public Action? ReportProblem { get; init; }
}

/// <summary>
/// Réglages Windows 11 Fluent : FluentWindow à fond Mica, barre de titre intégrée (icône, « Selecto AI », badge Démo/Pro) et RAIL de
/// navigation à gauche (NavigationView : panneau compact de 48 px qui s'ouvre à 220 px) avec quatre pages aux noms fixes : Général,
/// Actions, IA, À propos. Un réglage par carte (libellé 14 px, aide 12 px grise, contrôle à droite). La fenêtre se redimensionne de
/// 720 x 520 à plein écran ; les pages défilent à la molette ou au toucher, sans barre visible. Thème clair/sombre et accent du système
/// suivis automatiquement. Les réglages ne s'enregistrent qu'avec « Appliquer » (le bouton devient alors « Fermer », avec le message
/// « Réglages enregistrés ») ; « Rétablir » revient au dernier état enregistré ; fermer avec des changements demande une confirmation.
/// </summary>
public sealed partial class SettingsWindow : WpfUi.FluentWindow, INavigationViewPageProvider
{
    private const int TabGeneral = 0, TabActions = 1, TabAi = 2, TabAbout = 3;
    private const int PageSize = 9;               // lignes de la liste d'actions visibles sans « Afficher plus »

    // types « marqueurs » : le NavigationView les associe aux pages que fournit GetPage
    private sealed class GeneralPage { }
    private sealed class ActionsPage { }
    private sealed class AiPage { }
    private sealed class AboutPage { }
    private static readonly Type[] PageTypes = { typeof(GeneralPage), typeof(ActionsPage), typeof(AiPage), typeof(AboutPage) };
    private static readonly string[] TabKeys = { "Settings_Tab_General", "Settings_Tab_Actions", "Settings_Tab_Ai", "Settings_Tab_About" };
    private static string TabName(int i) => L.Get(TabKeys[i]);
    private static readonly string[] TabGlyphs = { "\uE713", "\uE71D", "\uE99A", "\uE946" };   // Segoe Fluent Icons

    private readonly SettingsHost _host;
    private AppSettings S => _host.Settings;
    // enregistrement différé (V3-02) : « _saved » = dernier état enregistré (ouverture ou Appliquer)
    private AppSettings _saved;
    private List<string> _savedApps = new(), _savedSites = new();
    private bool _extrasDirty;                     // listes d'exclusion modifiées depuis le dernier enregistrement
    private WpfUi.Button? _primary, _revert;
    private string? _flash;                        // « Réglages enregistrés » pendant quelques secondes
    private readonly System.Windows.Threading.DispatcherTimer _flashTimer = new();
    private List<ActionDefinition> _actions;
    private List<string> _order;

    private int _tab;
    private int? _pendingTab;                      // onglet demandé avant que le rail soit prêt
    private bool _navReady;
    private string _filter = "";
    private bool _showAllActions;
    private readonly TextBlock _status = new() { FontSize = 12, FontFamily = Theme.Font, VerticalAlignment = VerticalAlignment.Center };
    private readonly WpfUi.NavigationView _nav = new();
    private ContentControl _body = new();
    private ScrollViewer? _scroll;
    private ContentControl? _previewHost;
    private StackPanel? _rows;
    private TextBlock? _heading;                   // titre de la page : relu au changement de langue
    private bool _langRestartNote;                 // langue changée alors que d'autres fenêtres étaient ouvertes

    public SettingsWindow(SettingsHost host)
    {
        WindowFactory.Apply(this);
        _host = host;
        if (host.Updates is { } updates)
        {
            updates.Changed += OnUpdateChanged;
            Closed += (_, _) => updates.Changed -= OnUpdateChanged;
        }
        _saved = Clone(host.Settings);
        _savedApps = host.Apps.Items.ToList();
        _savedSites = host.Sites.Items.ToList();
        _flashTimer.Interval = TimeSpan.FromMilliseconds(SettingsDraft.SavedMessageMs);
        _flashTimer.Tick += (_, _) => { _flashTimer.Stop(); _flash = null; UpdateStatus(); };
        _actions = ActionFilter.Order(host.LoadActions(), host.Settings);
        _order = TopLevelIds();

        Title = CurrentTitle();
        // jamais plus haute que la zone de travail de l'écran (barre des tâches déduite) : sur un écran de 720 px de haut, 680 débordait
        var workHeight = SystemParameters.WorkArea.Height;
        Width = 960; Height = Math.Min(680, workHeight); MinWidth = 720; MinHeight = Math.Min(520, workHeight);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ExtendsContentIntoTitleBar = true;
        WindowBackdropType = WpfUi.WindowBackdropType.Mica;      // repli automatique : couleur unie si Mica est indisponible
        FontFamily = Theme.Font;
        System.Windows.Automation.AutomationProperties.SetName(this, L.Get("Settings_WindowName"));

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var titleBar = new WpfUi.TitleBar { Title = "Selecto AI", Header = BuildTitleHeader() };
        root.Children.Add(titleBar);

        _nav.PaneDisplayMode = WpfUi.NavigationViewPaneDisplayMode.Left;
        _nav.OpenPaneLength = 220;
        _nav.CompactPaneLength = 48;
        _nav.IsPaneOpen = false;
        _nav.IsPaneToggleVisible = true;
        _nav.IsBackButtonVisible = WpfUi.NavigationViewBackButtonVisible.Collapsed;
        _nav.SetPageProviderService(this);
        for (var i = 0; i < TabKeys.Length; i++)
        {
            var item = new WpfUi.NavigationViewItem
            {
                Content = TabName(i), TargetPageType = PageTypes[i],
                Icon = new WpfUi.FontIcon { Glyph = TabGlyphs[i], FontFamily = Theme.IconFont }
            };
            System.Windows.Automation.AutomationProperties.SetName(item, TabName(i));
            if (i == TabAbout) _nav.FooterMenuItems.Add(item); else _nav.MenuItems.Add(item);
        }
        Grid.SetRow(_nav, 1);
        root.Children.Add(_nav);

        var footer = BuildFooter();
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        Content = root;

        _nav.Loaded += (_, _) =>
        {
            if (_navReady) return;
            _navReady = true;
            GoTo(_pendingTab ?? _tab);
        };
        SourceInitialized += (_, _) =>
        {
            try { ThemeWatcher.Watch(this, WpfUi.WindowBackdropType.Mica, true); }
            catch (Exception ex) { Log.Write("Réglages : suivi du thème impossible : " + ex.Message); }
        };
        ContentRendered += (_, _) => LogLayout("Réglages affichés");
        FluentTheme.Changed += OnThemeChanged;
        Closed += (_, _) =>
        {
            FluentTheme.Changed -= OnThemeChanged;
            try { ThemeWatcher.UnWatch(this); } catch { }
            _flashTimer.Stop();
        };
    }

    private static string CurrentTitle() => EditionInfo.WindowTitle();

    /// <summary>Page demandée par le rail : on construit son contenu (la logique de chaque page reste dans BuildTab).</summary>
    public object? GetPage(Type pageType)
    {
        var tab = Array.IndexOf(PageTypes, pageType);
        _tab = tab < 0 ? TabGeneral : tab;
        _body = new ContentControl();
        var page = new StackPanel { Margin = new Thickness(36, 8, 36, 24), MaxWidth = 1000, HorizontalAlignment = HorizontalAlignment.Stretch };
        var heading = Theme.Label(TabName(_tab), 28, Theme.Text, FontWeights.SemiBold);
        heading.Margin = new Thickness(0, 0, 0, 16);
        _heading = heading;
        System.Windows.Automation.AutomationProperties.SetName(page, TabName(_tab));
        page.Children.Add(heading);
        page.Children.Add(_body);
        _scroll = Theme.ThinScroll(page);
        page.FlowDirection = Theme.Flow;
        _scroll.FlowDirection = Theme.Flow;
        Render();
        return _scroll;
    }

    private UIElement BuildTitleHeader()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (AppIcon.Image() is { } icon)
            row.Children.Add(new Image { Source = icon, Width = 16, Height = 16, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new TextBlock { Text = "Selecto AI", FontSize = 12, FontFamily = Theme.Font, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    /// <summary>Le thème ou l'accent de Windows a changé : les pinceaux de Theme sont déjà à jour, on redessine la page.</summary>
    private void OnThemeChanged()
    {
        if (IsLoaded) Render();
    }

    /// <summary>Ouvre l'onglet À propos.</summary>
    public void ShowAbout() => GoTo(TabAbout);
    public void ShowGeneral() => GoTo(TabGeneral);
    public void ShowActions() => GoTo(TabActions);
    public void ShowAi() => GoTo(TabAi);

    /// <summary>Mode capture : place la fenêtre à des coordonnées d'écran fixes (captures reproductibles).</summary>
    public void PlaceAt(double x, double y)
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = x; Top = y;
    }

    private List<string> TopLevelIds() => _actions.Where(a => a.ParentId == null).Select(a => a.Id).ToList();

    private static AppSettings Clone(AppSettings s) =>
        System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(s)) ?? new AppSettings();

    // ---------------------------------------------------------------- squelette

    /// <summary>Pied de fenêtre : état Actif / En pause (ou « Réglages enregistrés ») à gauche, « Rétablir » et Appliquer / Fermer à droite.</summary>
    private UIElement BuildFooter()
    {
        var grid = new Grid { Margin = new Thickness(36, 8, 24, 16) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _revert = Theme.Btn(L.Get("Settings_Revert"), Revert);
        row.Children.Add(_revert);
        _primary = Theme.Btn(SettingsDraft.PrimaryLabel(false), OnPrimary, primary: true);
        _primary.Margin = new Thickness(0);
        row.Children.Add(_primary);
        Grid.SetColumn(row, 1);
        grid.Children.Add(_status);
        grid.Children.Add(row);
        UpdateFooter();
        return grid;
    }

    /// <summary>Changements en attente (réglages ou listes d'exclusion) : pas encore enregistrés.</summary>
    private bool Dirty => SettingsDraft.HasChanges(_saved, S) || _extrasDirty;

    /// <summary>Un réglage a changé : le pied suit (Appliquer, Rétablir actif). Rien n'est enregistré ici.</summary>
    private void Edited() => UpdateFooter();

    private void UpdateFooter()
    {
        if (_primary == null || _revert == null) return;
        var dirty = Dirty;
        _primary.Content = SettingsDraft.PrimaryLabel(dirty);
        _revert.IsEnabled = SettingsDraft.RevertEnabled(dirty);
    }

    /// <summary>Bouton principal : Appliquer s'il reste des changements, sinon Fermer.</summary>
    private void OnPrimary()
    {
        if (Dirty) Commit();
        else Close();
    }

    /// <summary>Remet les réglages tels qu'à l'enregistrement précédent (ou à l'ouverture) ; rien n'est enregistré.</summary>
    private void Revert()
    {
        RestoreSaved();
        Render();
        UpdateFooter();
    }

    /// <summary>Copie les réglages et les listes d'exclusion enregistrés dans l'état courant.</summary>
    private void RestoreSaved()
    {
        var snapshot = Clone(_saved);
        foreach (var p in typeof(AppSettings).GetProperties().Where(p => p.CanRead && p.CanWrite))
            p.SetValue(S, p.GetValue(snapshot));
        foreach (var a in _host.Apps.Items.ToList()) _host.Apps.Remove(a);
        foreach (var a in _savedApps) _host.Apps.Add(a);
        foreach (var s in _host.Sites.Items.ToList()) _host.Sites.Remove(s);
        foreach (var s in _savedSites) _host.Sites.Add(s);
        _extrasDirty = false;
        ReloadActions();
        _order = TopLevelIds();
    }

    /// <summary>Les listes d'exclusion diffèrent-elles de leur état enregistré ?</summary>
    private void RefreshExtrasDirty() =>
        _extrasDirty = !_savedApps.SequenceEqual(_host.Apps.Items) || !_savedSites.SequenceEqual(_host.Sites.Items);

    /// <summary>Fermeture avec changements : Enregistrer (puis fermer), Ne pas enregistrer (revenir) ou Annuler (rester).</summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel) return;
        if (!SettingsDraft.ShouldPromptOnClose(SettingsDraft.HasChanges(_saved, S), _extrasDirty)) return;
        switch (Dialogs.AskUnsaved(this, Title, L.Get("Settings_UnsavedQuestion")))
        {
            case Dialogs.UnsavedChoice.Save: Commit(); break;
            case Dialogs.UnsavedChoice.Discard: RestoreSaved(); break;
            default: e.Cancel = true; break;
        }
    }

    private UIElement BuildTab(int tab) => tab switch
    {
        TabGeneral => BuildGeneral(),
        TabActions => BuildActionsTab(),
        TabAi => BuildAiTab(),
        _ => BuildAbout()
    };

    private void Render()
    {
        UpdateStatus();
        _previewHost = null; _rows = null; _aiHost = null;
        _body.Content = BuildTab(_tab);
        _body.FlowDirection = Theme.Flow;
        if (_scroll != null) _scroll.FlowDirection = Theme.Flow;
        if (IsLoaded)
            Dispatcher.BeginInvoke(new Action(() => LogLayout("Page " + TabName(_tab))), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void LogLayout(string what)
    {
        try
        {
            Log.Write($"{what} : fenêtre {ActualWidth:0} x {ActualHeight:0} px, page {TabName(_tab)} {_body.ActualHeight:0} px (sans barre de défilement visible)");
        }
        catch { }
    }

    private void UpdateStatus()
    {
        if (_flash != null)
        {
            _status.Text = _flash;
            _status.Foreground = Theme.StatusOn;
            return;
        }
        _status.Text = S.Enabled ? L.Get("Settings_StatusActive") : L.Get("Settings_StatusPaused");
        _status.Foreground = S.Enabled ? Theme.StatusOn : Theme.Warn;
    }

    /// <summary>Enregistre et applique les réglages (et les listes), puis passe le pied en « Fermer » avec un message bref.</summary>
    private void Commit()
    {
        var before = L.Culture.TwoLetterISOLanguageName;
        var warn = _host.Apply();
        if (warn != null) Dialogs.Info(this, Title, warn);
        _saved = Clone(S);
        _savedApps = _host.Apps.Items.ToList();
        _savedSites = _host.Sites.Items.ToList();
        _extrasDirty = false;
        ApplyLanguage(before);
        _flash = SettingsDraft.SavedMessage;
        _flashTimer.Stop();
        _flashTimer.Start();
        UpdateStatus();
        UpdateFooter();
    }

    /// <summary>Applique la langue enregistrée tout de suite (L.Culture) ; <paramref name="before"/> est la langue avant l'enregistrement. Les autres fenêtres ouvertes gardent l'ancienne langue jusqu'au redémarrage.</summary>
    private void ApplyLanguage(string before)
    {
        var code = UiLanguageResolver.Resolve(S.UiLanguage, CultureInfo.CurrentUICulture.Name);
        if (code != L.Culture.TwoLetterISOLanguageName) L.Culture = UiCulture.Make(code);
        if (code == before) return;
        _langRestartNote = OtherWindowsOpen();
        RefreshLabels();
    }

    /// <summary>D'autres fenêtres de l'application (barre, accueil, signalement…) sont-elles visibles ?</summary>
    private bool OtherWindowsOpen() =>
        System.Windows.Application.Current?.Windows.OfType<Window>().Any(w => !ReferenceEquals(w, this) && w.IsVisible) == true;

    /// <summary>Relit les libellés de la fenêtre dans la langue courante : titre, rail, pied, badge, titre de page, actions et contenu.</summary>
    private void RefreshLabels()
    {
        Title = CurrentTitle();
        System.Windows.Automation.AutomationProperties.SetName(this, L.Get("Settings_WindowName"));
        foreach (var item in _nav.MenuItems.Cast<object>().Concat(_nav.FooterMenuItems.Cast<object>()).OfType<WpfUi.NavigationViewItem>())
        {
            var i = Array.IndexOf(PageTypes, item.TargetPageType);
            if (i < 0) continue;
            item.Content = TabName(i);
            System.Windows.Automation.AutomationProperties.SetName(item, TabName(i));
        }
        if (_heading != null) _heading.Text = TabName(_tab);
        if (_revert != null) _revert.Content = L.Get("Settings_Revert");
        ReloadActions();
        UpdateStatus();
        UpdateFooter();
        Render();
    }

    /// <summary>Va à une page ; si le rail n'est pas encore prêt, l'ouverture se fait à son chargement.</summary>
    private void GoTo(int tab)
    {
        if (!_navReady) { _pendingTab = tab; _tab = tab; return; }
        var ok = false;
        try { ok = _nav.Navigate(PageTypes[tab]); }
        catch (Exception ex) { Log.Write("Réglages : navigation impossible : " + ex.Message); }
        if (!ok) { _tab = tab; Render(); }     // déjà sur cette page (ou rail indisponible) : on redessine
    }


    /// <summary>Bouton de ligne : sans marge, aligné à droite.</summary>
    private static WpfUi.Button RowBtn(string text, Action onClick, bool primary = false)
    {
        var b = Theme.Btn(text, onClick, primary);
        b.Margin = new Thickness(0);
        b.MinWidth = 0;
        return b;
    }

    // ---------------------------------------------------------------- Général

    /// <summary>Le rail laisse toute la largeur au contenu : les deux anciennes colonnes s'empilent (même ordre).</summary>
    private static StackPanel Columns(UIElement left, UIElement right, double leftStar = 1, double rightStar = 1)
    {
        var g = new StackPanel();
        g.Children.Add(left);
        g.Children.Add(right);
        return g;
    }

    private UIElement BuildGeneral()
    {
        var left = new StackPanel();
        left.Children.Add(BuildLanguageGroup());
        left.Children.Add(Theme.SectionHeader(L.Get("Settings_AppearanceSection")));
        left.Children.Add(Theme.Group(
            Theme.Row(L.Get("Settings_AutoShow"), L.Get("Settings_AutoShowHelp"),
                Theme.Switch(S.Enabled, v => { S.Enabled = v; Edited(); UpdateStatus(); return true; }, L.Get("Settings_AutoShow"))),
            Theme.Row(L.Get("Settings_Shortcut"),
                S.HotkeyEnabled ? L.Format("Settings_ShortcutRowHelp", HotkeyParser.Display(S.Hotkey)) : L.Get("Settings_ShortcutNoneHelp"),
                RowBtn(S.HotkeyEnabled ? HotkeyParser.Display(S.Hotkey) : L.Get("Settings_None"), EditShortcut))));

        left.Children.Add(Theme.SectionHeader(L.Get("Settings_ExcludedHeader")));
        left.Children.Add(Theme.Group(
            Theme.Row(L.Get("Settings_ExcludedApps"), _host.Apps.Items.Count == 0 ? L.Get("Settings_NoneApps") : L.Plural(_host.Apps.Items.Count, "Settings_AppOne", "Settings_AppOther"), RowBtn(L.Get("Settings_Manage"), EditApps)),
            Theme.Row(L.Get("Settings_ExcludedSites"), _host.Sites.Count == 0 ? L.Get("Settings_NoneSites") : L.Plural(_host.Sites.Count, "Settings_SiteOne", "Settings_SiteOther"), RowBtn(L.Get("Settings_Manage"), EditSites))));

        var right = new StackPanel();
        right.Children.Add(Theme.SectionHeader(L.Get("Settings_AppearanceHeader"), true));
        right.Children.Add(BuildAppearanceGroup());

        right.Children.Add(Theme.SectionHeader(L.Get("Settings_StartupHeader")));
        right.Children.Add(Theme.Group(
            Theme.Row(L.Get("Settings_StartWithWindows"), L.Get("Settings_StartupHelp"),
                Theme.Switch(StartupManager.IsEnabled() || S.StartWithWindows, v => { S.StartWithWindows = v; Edited(); return true; }, L.Get("Settings_StartWithWindows"))),
            Theme.Row(L.Get("Settings_TrayIcon"), L.Get("Settings_TrayIconHelp"),
                Theme.Switch(S.ShowTrayIcon, v =>
                {
                    if (!v && !Dialogs.Ask(this, Title,
                            L.Get("Settings_TrayHiddenWarning"),
                            "OK", L.Get("Settings_Cancel"))) return false;
                    S.ShowTrayIcon = v;
                    Edited();
                    return true;
                }, L.Get("Settings_TrayIcon")))));
        return Columns(left, right, 1, 1);
    }

    /// <summary>Premier réglage de Général : langue de l'interface. Appliquée à l'enregistrement (Appliquer), sans redémarrage.</summary>
    private UIElement BuildLanguageGroup()
    {
        var names = new[] { L.Get("Settings_UiLanguage_Auto"), "Français", "English", "Español", "العربية" };
        var box = new ComboBox { Width = 200, FontFamily = Theme.Font, FontSize = 14 };
        foreach (var name in names) box.Items.Add(name);
        box.SelectedIndex = Math.Max(0, Array.IndexOf(UiLanguageChoices.Codes, UiLanguageChoices.Normalize(S.UiLanguage)));
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedIndex < 0) return;
            S.UiLanguage = UiLanguageChoices.Codes[box.SelectedIndex];
            Edited();
        };
        System.Windows.Automation.AutomationProperties.SetName(box, L.Get("Settings_UiLanguage"));
        var group = Theme.Group(Theme.Row(L.Get("Settings_UiLanguage"), null, box));
        if (!_langRestartNote) return group;

        var stack = new StackPanel();
        stack.Children.Add(group);
        var note = Theme.Label(L.Get("Settings_UiLanguage_RestartNote"), 12, Theme.Warn);
        note.TextWrapping = TextWrapping.Wrap;
        note.Margin = new Thickness(4, 6, 0, 8);
        stack.Children.Add(note);
        return stack;
    }

    private UIElement BuildAppearanceGroup()
    {
        var value = Theme.Label(Appearance.ClampScale(S.BarScalePercent) + " %", 12, Theme.SubText);
        value.Width = 52; value.Margin = new Thickness(12, 0, 0, 0); value.TextAlignment = TextAlignment.Right; value.VerticalAlignment = VerticalAlignment.Center;
        var slider = new Slider
        {
            Minimum = Appearance.MinScale, Maximum = Appearance.MaxScale, TickFrequency = 5, IsSnapToTickEnabled = true,
            SmallChange = 5, LargeChange = 10, Value = Appearance.ClampScale(S.BarScalePercent), Width = 160, VerticalAlignment = VerticalAlignment.Center
        };
        slider.ValueChanged += (_, e) =>
        {
            S.BarScalePercent = Appearance.ClampScale((int)Math.Round(e.NewValue));
            value.Text = S.BarScalePercent + " %";
            RefreshPreview();
            Edited();
        };
        System.Windows.Automation.AutomationProperties.SetName(slider, L.Get("Settings_BarSize"));
        var sizeControl = new StackPanel { Orientation = Orientation.Horizontal };
        sizeControl.Children.Add(slider); sizeControl.Children.Add(value);

        var position = Theme.Segmented(new[] { L.Get("Settings_PosAbove"), L.Get("Settings_PosBelow") },
            Appearance.NormalizePosition(S.BarPosition) == Appearance.Above ? 0 : 1,
            i => { S.BarPosition = i == 0 ? Appearance.Above : Appearance.Below; RefreshPreview(); Edited(); });

        var styleIndex = Appearance.NormalizeStyle(S.BarStyle) switch { Appearance.Light => 1, Appearance.Dark => 2, _ => 0 };
        var style = Theme.Segmented(new[] { L.Get("Settings_Automatic"), L.Get("Settings_Light"), L.Get("Settings_Dark") }, styleIndex,
            i => { S.BarStyle = i == 1 ? Appearance.Light : i == 2 ? Appearance.Dark : Appearance.Auto; RefreshPreview(); Edited(); });

        _previewHost = new ContentControl { Margin = new Thickness(0, 6, 0, 6), HorizontalAlignment = HorizontalAlignment.Center };
        var previewRow = Theme.Line(new Border { MinHeight = 50, Child = _previewHost });
        RefreshPreview();

        return Theme.Group(
            Theme.Row(L.Get("Settings_Size"), null, sizeControl),
            Theme.Row(L.Get("Settings_Position"), null, position),
            Theme.Row(L.Get("Settings_BarStyle"), L.Get("Settings_StyleHelp"), style),
            previewRow);
    }

    private void RefreshPreview()
    {
        if (_previewHost == null) return;
        var scale = Appearance.ClampScale(S.BarScalePercent) / 100.0;
        BarVisuals.Current = BarPalette.For(S.BarStyle);
        var inner = new StackPanel { Orientation = Orientation.Horizontal };
        inner.Children.Add(BarVisuals.Button(ActionGlyphs.ForBar(BuiltInActions.Search), "", L.Get("Settings_Search"), true, null));
        inner.Children.Add(BarVisuals.Button(null, L.Get("Settings_Copy"), L.Get("Settings_Copy"), false, null));
        inner.Children.Add(BarVisuals.Separator());
        inner.Children.Add(BarVisuals.Button(null, L.Get("Settings_Paste"), L.Get("Settings_Paste"), false, null));
        var pill = BarVisuals.CreatePill(inner, scale);
        pill.HorizontalAlignment = HorizontalAlignment.Center;

        var word = new Border
        {
            Background = Theme.Solid(FluentTheme.IsDark ? ThemePalette.AccentDark : ThemePalette.Accent, 0.28), Padding = new Thickness(6, 1, 6, 1),
            HorizontalAlignment = HorizontalAlignment.Center, Child = Theme.Label(L.Get("Settings_Example"), 18)
        };
        var stack = new StackPanel { MinHeight = 84 + (scale - 1) * 40 };
        if (Appearance.NormalizePosition(S.BarPosition) == Appearance.Above) { stack.Children.Add(pill); stack.Children.Add(word); }
        else { stack.Children.Add(word); stack.Children.Add(pill); }
        _previewHost.Content = stack;
    }

    // ---------------------------------------------------------------- Actions

    private bool IsActionOn(string id) =>
        !S.DisabledActions.Contains(id, StringComparer.OrdinalIgnoreCase);

    private UIElement BuildActionsTab()
    {
        // colonne de gauche : la liste des actions (9 lignes sans défiler) ; colonne de droite : recommandation et extensions
        var left = new StackPanel();
        left.Children.Add(Theme.SectionHeader(L.Get("Settings_BarActionsHeader"), true));
        var search = Theme.Input(double.NaN);
        search.HorizontalAlignment = HorizontalAlignment.Stretch;
        search.Text = _filter;
        search.ToolTip = L.Get("Settings_SearchAction");
        search.PlaceholderText = L.Get("Settings_SearchAction");
        System.Windows.Automation.AutomationProperties.SetName(search, L.Get("Settings_SearchAction"));
        search.TextChanged += (_, _) => { _filter = search.Text; RebuildRows(); };
        var searchRow = Theme.Line(new Border { MinHeight = 38, Padding = new Thickness(0, 4, 0, 4), Child = search });
        _rows = new StackPanel();
        RebuildRows();
        left.Children.Add(Theme.Group(searchRow, _rows));

        var disabledTitles = _actions.Where(a => a.BuiltIn && a.ParentId == null && !IsActionOn(a.Id))
            .Select(a => a.Title).ToList();
        var right = new StackPanel();
        right.Children.Add(Theme.SectionHeader(L.Get("Settings_DailyHeader"), true));
        right.Children.Add(Theme.Group(Theme.Row(
            L.Get("Settings_Today"), Recommendation.Daily(S, _actions.Count(a => !a.BuiltIn), disabledTitles), null)));

        var installed = BuildInstalledExtensionRows();
        if (installed.Count == 0) installed.Add(Theme.Row(L.Get("Settings_NoExtension"), L.Get("Settings_JsFiles"), null));
        installed.Add(Theme.Row(L.Get("Settings_AddExtension"), L.Get("Settings_ChooseJs"), RowBtn(L.Get("Settings_AddButton"), AddExtension, primary: true)));
        installed.Add(Theme.Row(L.Get("Settings_GetExtensions"), L.Get("Settings_GetExtensionsHelp"),
            RowBtn(L.Get("Settings_Browse"), () => OpenExternal(AboutLinks.ExtensionsCatalogUrl(L.Culture.TwoLetterISOLanguageName)))));
        installed.Add(Theme.Row(L.Get("Settings_ExtFolder"), L.Get("Settings_ExtFolderHelp"), RowBtn(L.Get("Settings_Open"), _host.OpenExtensionsFolder)));
        right.Children.Add(Theme.SectionHeader(L.Get("Settings_InstalledExtensions")));
        right.Children.Add(Theme.Group(installed.ToArray()));
        right.Children.Add(BuildBuiltInExtensions());
        return Columns(left, right, 1.6, 1);
    }

    private void RebuildRows()
    {
        if (_rows == null) return;
        _rows.Children.Clear();
        var f = _filter.Trim();
        var defs = new List<ActionDefinition>();
        foreach (var id in _order)
        {
            var def = _actions.FirstOrDefault(a => a.Id == id);
            if (def == null) continue;
            if (f.Length > 0 && def.Title.IndexOf(f, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
            defs.Add(def);
        }
        var paged = f.Length == 0 && !_showAllActions && defs.Count > PageSize;
        foreach (var def in paged ? defs.Take(PageSize) : defs) _rows.Children.Add(BuildRow(def));
        if (f.Length == 0 && defs.Count > PageSize)
        {
            var more = Theme.LinkButton(paged ? L.Format("Settings_ShowMore", defs.Count - PageSize) : L.Get("Settings_ShowLess"), () => { _showAllActions = !_showAllActions; RebuildRows(); });
            more.HorizontalAlignment = HorizontalAlignment.Left;
            _rows.Children.Add(Theme.Line(new Border { MinHeight = 30, Child = more }));
        }
    }

    private UIElement BuildRow(ActionDefinition def)
    {
        var id = def.Id;
        var tools = new StackPanel { Orientation = Orientation.Horizontal };
        if (id == BuiltInActions.Search || id == BuiltInActions.Spell)
        {
            var gear = Theme.LinkButton("\uE713", () =>
            {
                if (id == BuiltInActions.Search) SearchOptions();
                else SpellOptions();
            }, icon: true, automationName: L.Get("Settings_OptionsOf") + def.Title);
            gear.ToolTip = L.Get("Settings_Options");
            tools.Children.Add(gear);
        }
        var up = Theme.LinkButton("\uE70E", () => Move(id, -1), icon: true, automationName: L.Get("Settings_MoveUpOf") + def.Title); up.ToolTip = L.Get("Settings_MoveUp");
        var down = Theme.LinkButton("\uE70D", () => Move(id, +1), icon: true, automationName: L.Get("Settings_MoveDownOf") + def.Title); down.ToolTip = L.Get("Settings_MoveDown");
        tools.Children.Add(up); tools.Children.Add(down);
        var sw = Theme.Switch(IsActionOn(id), v => SetActionOn(id, v), def.Title);
        sw.Margin = new Thickness(10, 0, 0, 0);
        tools.Children.Add(sw);

        var glyph = ActionGlyphs.ForList(id);
        return Theme.Row(def.Title + (def.BuiltIn ? "" : L.Get("Settings_ExtensionTag")), null, tools,
            glyph ?? def.Icon ?? def.Title.Substring(0, 1), glyph != null, 36);
    }

    /// <summary>Active ou désactive une action (le texte part en ligne seulement si « Actions en ligne » est activé).</summary>
    private bool SetActionOn(string id, bool on)
    {
        if (on) S.DisabledActions.RemoveAll(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase));
        else if (!S.DisabledActions.Contains(id, StringComparer.OrdinalIgnoreCase)) S.DisabledActions.Add(id);
        Edited();
        return true;
    }

    private void Move(string id, int delta)
    {
        var i = _order.IndexOf(id); var j = i + delta;
        if (i < 0 || j < 0 || j >= _order.Count) return;
        (_order[i], _order[j]) = (_order[j], _order[i]);
        S.ActionOrder = _order.ToList();
        Edited();
        RebuildRows();
    }

    private void AddExtension()
    {
        var dlg = new OpenFileDialog { Filter = L.Get("Settings_JsFilter"), Title = L.Get("Settings_AddExtension"), Multiselect = true };
        if (dlg.ShowDialog(this) != true) return;
        var added = 0;
        var refused = new List<string>();
        foreach (var file in dlg.FileNames)
        {
            try
            {
                var result = ExtensionImporter.ImportFile(file);
                foreach (var script in result.Scripts)
                {
                    Directory.CreateDirectory(_host.ExtensionsDirectory);
                    var name = ExtensionImporter.UniqueFileName(Path.GetFileNameWithoutExtension(script.FileName),
                        f => File.Exists(Path.Combine(_host.ExtensionsDirectory, f)));
                    File.WriteAllBytes(Path.Combine(_host.ExtensionsDirectory, name), script.Content);
                    added++;
                }
                refused.AddRange(result.Skipped.Select(SkipLine));
            }
            catch (Exception ex)
            {
                Log.Write("Ajout d'extension : " + ex.Message);
                refused.Add(L.Format("Import_SkipLine", Path.GetFileName(file), ex.Message));
            }
        }
        if (added > 0) { ReloadActions(); Render(); }
        if (refused.Count == 0) return;
        if (added == 0) refused.Insert(0, L.Get("Import_NoneAdded"));
        Dialogs.Info(this, Title, string.Join(Environment.NewLine, refused));
    }

    /// <summary>Ligne d'information pour un élément refusé : « nom : raison ».</summary>
    private static string SkipLine(ImportSkip skip)
    {
        var reason = skip.Issue switch
        {
            ImportIssue.InvalidHeader => L.Get("Settings_InvalidExt") + skip.Detail,
            ImportIssue.TooLarge => L.Format("Import_TooLarge", ExtensionImporter.MaxScriptBytes / 1024),
            ImportIssue.TooManyEntries => L.Format("Import_TooManyEntries", ExtensionImporter.MaxZipScripts),
            ImportIssue.OutsideRoot => L.Get("Import_OutsideRoot"),
            ImportIssue.BadName => L.Get("Import_BadName"),
            ImportIssue.NotArchive => L.Get("Import_NotArchive"),
            ImportIssue.NoScript => L.Get("Import_NoScript"),
            _ => L.Get("Import_NotScript"),
        };
        return L.Format("Import_SkipLine", skip.Name, reason);
    }

    private void ReloadActions()
    {
        _actions = ActionFilter.Order(_host.LoadActions(), S);
        var known = new HashSet<string>(_order);
        var ids = TopLevelIds();
        _order = _order.Where(ids.Contains).Concat(ids.Where(i => !known.Contains(i))).ToList();
    }

    /// <summary>Une ligne par extension utilisateur de premier niveau : titre, descriptif, nombre d'actions, interrupteur.
    /// L'état d'activation suit le même réglage que les extensions intégrées (<see cref="SetExtensionOn"/>).</summary>
    private List<UIElement> BuildInstalledExtensionRows()
    {
        var lang = L.Culture.TwoLetterISOLanguageName;
        var rows = new List<UIElement>();
        foreach (var ext in ExtensionInventory.List(_host.ExtensionsDirectory, lang))
        {
            var id = ext.Id;
            var sw = Theme.Switch(ExtensionCatalog.IsEnabled(S, id, ext.EnabledByDefault), v => SetExtensionOn(id, v), ext.Title);
            var detail = ext.ActionCount > 0 ? ActionCountText(ext.ActionCount) : null;
            rows.Add(Theme.Row(ext.Title, Isolate(ext.Description), sw, minHeight: 36, detail: detail));
        }
        return rows;
    }

    /// <summary>« N actions » selon la langue ; l'arabe a des formes pour 1, 2, 3 à 10 et 11 et plus.</summary>
    private static string ActionCountText(int n)
    {
        var ar = L.Culture.TwoLetterISOLanguageName == "ar";
        var key = n == 1 ? "Settings_ExtensionActionsOne"
            : ar && n == 2 ? "Settings_ExtensionActionsTwo"
            : ar && n >= 3 && n <= 10 ? "Settings_ExtensionActionsFew"
            : "Settings_ExtensionActionsOther";
        return L.Format(key, n);
    }

    /// <summary>En droite-à-gauche, isole le descriptif (RLI…PDI) et ajoute une marque RLM : un point final ou un mot latin reste à sa place.</summary>
    private static string? Isolate(string? text)
    {
        if (string.IsNullOrEmpty(text) || !L.Culture.TextInfo.IsRightToLeft) return text;
        return "\u2067" + text + "\u2069\u200F";
    }

    // ---------------------------------------------------------------- Extensions intégrées

    /// <summary>Une ligne par extension de premier niveau du catalogue (une sous-extension n'a pas de ligne : elle suit son parent).
    /// Le descriptif vient des en-têtes des fichiers intégrés (@description:xx).</summary>
    private UIElement BuildBuiltInExtensions()
    {
        var lang = L.Culture.TwoLetterISOLanguageName;
        var descriptions = ExtensionInventory.List(ExtensionLoader.BuiltInDirectory, lang)
            .ToDictionary(e => e.Id, e => e.Description, StringComparer.Ordinal);
        var rows = new List<UIElement>();
        foreach (var top in ExtensionCatalog.BuiltIns.Where(b => b.Parent == null))
        {
            var id = top.Id;
            var title = BuiltInTitle(top);
            var sw = Theme.Switch(ExtensionCatalog.IsEnabled(S, id), v => SetExtensionOn(id, v), title);
            rows.Add(Theme.Row(title, Isolate(descriptions.GetValueOrDefault(id)), sw, minHeight: 36));
        }
        var link = Theme.LinkButton(L.Get("Settings_WriteExtension"), () => OpenExternal(DocsUrl));
        link.HorizontalAlignment = HorizontalAlignment.Left;
        rows.Add(Theme.Line(new Border { MinHeight = 30, Child = link }));

        var help = new TextBlock
        {
            Text = L.Get("Settings_BuiltInExtensions_Help"), FontSize = 12, FontFamily = Theme.Font, TextAlignment = TextAlignment.Left,
            Foreground = Theme.SubText, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6)
        };
        var stack = new StackPanel();
        stack.Children.Add(Theme.SectionHeader(L.Get("Settings_BuiltInExtensions")));
        stack.Children.Add(help);
        stack.Children.Add(Theme.Group(rows.ToArray()));
        return stack;
    }

    /// <summary>Nom dans la langue d'affichage, sinon le français, sinon l'identifiant.</summary>
    private static string BuiltInTitle(BuiltInExtension b)
    {
        if (b.Names.TryGetValue(L.Culture.TwoLetterISOLanguageName, out var title)) return title;
        if (b.Names.TryGetValue("fr", out title)) return title;
        return b.Id;
    }

    /// <summary>Active ou désactive une extension intégrée dans le brouillon (sous-extensions comprises).</summary>
    private bool SetExtensionOn(string id, bool on)
    {
        SettingsDraft.SetExtensionEnabled(S, id, on);
        Edited();
        return true;
    }

    // ---------------------------------------------------------------- À propos

    private const string SiteUrl = "https://selecto.span.observer/";
    private const string DocsUrl = SiteUrl + "docs/";

    // Éléments de la ligne « Mise à jour », mis à jour à chaque changement d'état (RefreshUpdateUi).
    private TextBlock? _updateStatus = null;
    private ProgressBar? _updateBar = null;
    private WpfUi.Button? _updateButton = null;

    /// <summary>Lignes « Mise à jour » et « Rechercher automatiquement » (V3-07). Édition Store : aucune ligne (mises à jour par le Store).</summary>
    private UIElement[] BuildUpdateRows()
    {
#if EDITION_STORE
        // Store : les mises à jour passent par le Microsoft Store, pas de recherche dans l'application.
        return Array.Empty<UIElement>();
#else
        var updates = _host.Updates;
        if (updates is null || !updates.Enabled)
        {
            var off = RowBtn(L.Get("Settings_Search"), () => { });
            off.IsEnabled = false;
            off.ToolTip = L.Get("Settings_DisabledCapture");
            ToolTipService.SetShowOnDisabled(off, true);
            return new UIElement[] { Theme.Row(L.Get("Settings_Update"), L.Get("Settings_CheckDisabled"), off) };
        }

        _updateStatus = Theme.Label("", 12, Theme.SubText);
        _updateStatus.VerticalAlignment = VerticalAlignment.Center;
        _updateStatus.Margin = new Thickness(0, 0, 12, 0);
        _updateBar = new ProgressBar
        {
            Width = 120, Height = 4, Minimum = 0, Maximum = 1, Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0)
        };
        _updateButton = RowBtn(L.Get("Settings_CheckNow"), OnUpdateButton);
        var control = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        control.Children.Add(_updateStatus);
        control.Children.Add(_updateBar);
        control.Children.Add(_updateButton);
        RefreshUpdateUi();

        var auto = Theme.Switch(S.AutoCheckUpdates, v => { S.AutoCheckUpdates = v; Edited(); return true; }, L.Get("Settings_AutoUpdate"));
        return new UIElement[]
        {
            Theme.Row(L.Get("About_CheckUpdate"), null, control),
            Theme.Row(L.Get("Settings_AutoUpdate"), L.Get("Settings_AutoUpdateHelp"), auto)
        };
#endif
    }

    private void OnUpdateChanged()
    {
        if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(new Action(RefreshUpdateUi));
    }

    /// <summary>Met la ligne à jour : état (« À jour », « X.Y.Z disponible », « Vérification impossible »), bouton, progression.</summary>
    private void RefreshUpdateUi()
    {
        var up = _host.Updates;
        if (up is null || _updateStatus is null || _updateButton is null || _updateBar is null) return;

        var label = up.State switch
        {
            UpdateState.Checking => L.Get("Settings_Checking"),
            UpdateState.UpToDate => L.Get("Settings_UpToDate"),
            UpdateState.Available => up.Available?.Version + L.Get("Settings_Available"),
            UpdateState.Downloading => up.Available?.Version + L.Get("Settings_Available"),
            UpdateState.Failed => L.Get("Settings_CheckFailed"),
            _ => L.Get("Settings_NotChecked")
        };
        if (up.LastError is { } error) label += " · " + error;
        _updateStatus.Text = label;
        Brush tone = Theme.SubText;
        if (up.State == UpdateState.Available || up.State == UpdateState.Downloading) tone = Theme.StatusOn;
        else if (up.State == UpdateState.Failed || up.LastError is not null) tone = Theme.Warn;
        _updateStatus.Foreground = tone;

        if (up.State == UpdateState.Downloading)
        {
            _updateButton.Content = L.Get("Settings_Downloading");
            _updateButton.IsEnabled = false;
        }
        else if (up.State == UpdateState.Checking)
        {
            _updateButton.Content = L.Get("Settings_CheckNow");
            _updateButton.IsEnabled = false;
        }
        else if (up.State == UpdateState.Available && up.Available is not null)
        {
            _updateButton.Content = L.Get("Settings_Install");
            _updateButton.IsEnabled = true;
        }
        else
        {
            _updateButton.Content = L.Get("Settings_CheckNow");
            _updateButton.IsEnabled = _host.CheckUpdatesNow is not null;
        }

        var downloading = up.State == UpdateState.Downloading;
        _updateBar.Visibility = downloading ? Visibility.Visible : Visibility.Collapsed;
        _updateBar.IsIndeterminate = downloading && double.IsNaN(up.Progress);
        _updateBar.Value = double.IsNaN(up.Progress) ? 0 : up.Progress;
    }

    /// <summary>« Rechercher maintenant » ou « Installer ». L'installation télécharge, vérifie le SHA-256, lance l'installateur puis ferme l'application.</summary>
    private async void OnUpdateButton()
    {
        var up = _host.Updates;
        if (up is null) return;
        if (up.State == UpdateState.Available && up.Available is { } info)
        {
            var path = await up.DownloadAndVerifyAsync(info, new Progress<double>(_ => RefreshUpdateUi()), CancellationToken.None);
            if (path is not null) up.LaunchInstallerAndExit(path, () => System.Windows.Application.Current.Shutdown());
            RefreshUpdateUi();
            return;
        }
        if (_host.CheckUpdatesNow is { } check) await check();
        RefreshUpdateUi();
    }

    /// <summary>Architecture du processus, pour la ligne de version de À propos : x64 ou ARM64.</summary>
    private static string ArchLabel() => System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
    {
        System.Runtime.InteropServices.Architecture.Arm64 => "ARM64",
        System.Runtime.InteropServices.Architecture.X64 => "x64",
        var other => other.ToString(),
    };

    /// <summary>Ouvre une adresse web ou un lien du Store depuis À propos.</summary>
    private static void OpenExternal(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Write("Ouverture du lien : " + ex.Message); }
    }

    private UIElement BuildAbout()
    {
        var version = typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        var la = _host.LocalAi;
        var installed = la.HasPrivateInstall;
        var size = installed ? OllamaParsers.FormatSize(la.InstalledBytes()) : "";
        var uninstall = RowBtn(L.Get("Settings_UninstallEllipsis"), ConfirmUninstall);
        uninstall.IsEnabled = installed && !la.Busy;

        var identity = new Grid();
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        texts.Children.Add(Theme.Label("Selecto AI", 20, Theme.Text, FontWeights.SemiBold));
        texts.Children.Add(Theme.Sub(L.Format("About_VersionLine", version, ArchLabel())));
        var headline = Theme.Label(L.Get("About_FreeSoftware"), 14, Theme.StatusOn, FontWeights.SemiBold);
        headline.Margin = new Thickness(0, 4, 0, 0);
        texts.Children.Add(headline);
        if (AppIcon.Image() is { } logo)
        {
            var img = new Image { Source = logo, Width = 64, Height = 64, VerticalAlignment = VerticalAlignment.Center };
            System.Windows.Automation.AutomationProperties.SetName(img, L.Get("Settings_LogoName"));
            identity.Children.Add(img);
        }
        Grid.SetColumn(texts, 1);
        identity.Children.Add(texts);

        var stack = new StackPanel();
        stack.Children.Add(Theme.Line(identity));
        var aboutRows = new List<UIElement>
        {
            Theme.Row(L.Get("About_Version"), null, Theme.Label(VersionRowText(version), 12, Theme.SubText))
        };
        aboutRows.AddRange(BuildUpdateRows());
        stack.Children.Add(Theme.Group(aboutRows.ToArray()));

        if (_host.ReportProblem is { } report)
        {
            stack.Children.Add(Theme.SectionHeader(L.Get("Settings_HelpHeader")));
            stack.Children.Add(Theme.Group(Theme.Row(L.Get("Settings_ReportProblem"),
                L.Get("Settings_ReportHelp"),
                RowBtn(L.Get("Settings_Open"), report))));
        }

        var links = AboutLinks.For(L.Culture.TwoLetterISOLanguageName);
        stack.Children.Add(Theme.Group(links
            .Select(link => (UIElement)Theme.Row(L.Get(link.LabelKey), null, RowBtn(L.Get("Settings_Open"), () => OpenExternal(link.Url))))
            .ToArray()));

        stack.Children.Add(Theme.SectionHeader(L.Get("Mit_Title")));
        stack.Children.Add(Theme.Group(Theme.Row(L.Get("Mit_Name"), L.Get("Mit_Help"), RowBtn(L.Get("Mit_Read"), ShowMitLicense))));

        stack.Children.Add(Theme.SectionHeader(L.Get("Settings_FilesHeader")));
        stack.Children.Add(Theme.Group(
            Theme.Row(L.Get("Settings_ExtFolder"), L.Get("Settings_ExtFilesHelp"), RowBtn(L.Get("Settings_Open"), _host.OpenExtensionsFolder)),
            Theme.Row(L.Get("Settings_UninstallLocalAi"),
                installed ? L.Get("Settings_RemoveEngine") + (size.Length > 0 ? " (" + size + ")." : ".")
                          : L.Get("Settings_NoEngine"),
                uninstall)));
        var note = Theme.Sub(L.Get("Settings_EngineNote1") + la.Paths.Root + L.Get("Settings_EngineNote2") + OllamaEngineInfo.Host
                             + L.Get("Settings_EngineNote3"));
        note.Margin = new Thickness(0, 8, 0, 0);
        stack.Children.Add(note);
        return stack;
    }

    /// <summary>« 4.0.0 » ou « 4.0.0 · Microsoft Store ».</summary>
    private static string VersionRowText(string version) =>
        EditionInfo.PackagingLabel(EditionInfo.Current) is { Length: > 0 } label ? version + " · " + label : version;

    /// <summary>Texte de la licence MIT dans une fenêtre défilante (À propos › Licence).</summary>
    private void ShowMitLicense()
    {
        var (w, body) = MakeDialog(L.Get("Mit_Title"), 560);
        body.Children.Add(Theme.Sub(L.Get("Mit_Intro")));
        var text = new TextBox
        {
            Text = MitLicense.Text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontSize = 13, FontFamily = Theme.Font,
            Height = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 8, 0, 0),
            FlowDirection = FlowDirection.LeftToRight
        };
        System.Windows.Automation.AutomationProperties.SetName(text, L.Get("Mit_Title"));
        body.Children.Add(text);
        var close = Theme.Btn(L.Get("Settings_Close"), () => w.Close(), primary: true);
        close.IsDefault = true;
        close.Margin = new Thickness(0);
        body.Children.Add(ButtonRow(Theme.Btn(L.Get("Mit_OpenGitHub"), () => OpenExternal(EditionInfo.GitHubUrl)), close));
        w.Loaded += (_, _) => close.Focus();
        w.ShowDialog();
    }

    // ---------------------------------------------------------------- boîtes de dialogue

    private (WpfUi.FluentWindow Window, StackPanel Body) MakeDialog(string title, double width = 440) => Dialogs.Create(this, title, width);

    private static StackPanel ButtonRow(params UIElement[] buttons) => Dialogs.ButtonRow(buttons);

    private void EditApps() => EditList(L.Get("Settings_ExcludedApps"),
        L.Get("Settings_AppsHelp"),
        () => _host.Apps.Items, _host.Apps.Add, _host.Apps.Remove, PickExe);

    private void EditSites() => EditList(L.Get("Settings_ExcludedSitesName"),
        L.Get("Settings_SitesHelp"),
        () => _host.Sites.Items, _host.Sites.Add, _host.Sites.Remove, null);

    private static string? PickExe()
    {
        var dlg = new OpenFileDialog { Filter = L.Get("Settings_ExeFilter"), Title = L.Get("Settings_ChooseApp") };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private void EditList(string title, string help, Func<IReadOnlyList<string>> items, Func<string, bool> add, Func<string, bool> remove, Func<string?>? pick)
    {
        var (w, body) = MakeDialog(title, 560);
        body.Children.Add(Theme.Title(title));
        var h = Theme.Sub(help); h.Margin = new Thickness(0, 4, 0, 10);
        body.Children.Add(h);

        var list = new ListBox
        {
            MinHeight = 150, MaxHeight = 300, BorderBrush = Theme.ButtonBorder, BorderThickness = new Thickness(1), FontFamily = Theme.Font, FontSize = 14
        };
        void Refill() { list.Items.Clear(); foreach (var i in items()) list.Items.Add(i); }
        Refill();
        body.Children.Add(list);

        var input = Theme.Input(240);
        var addRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        void DoAdd(string text)
        {
            if (add(text)) { input.Clear(); Refill(); RefreshExtrasDirty(); Edited(); }
            else Dialogs.Info(w, Title, L.Get("Settings_InvalidValue"));
        }
        input.KeyDown += (_, e) => { if (e.Key == Key.Enter) DoAdd(input.Text); };
        input.Margin = new Thickness(0, 0, 8, 0);
        addRow.Children.Add(input);
        addRow.Children.Add(Theme.Btn(L.Get("Settings_Add"), () => DoAdd(input.Text)));
        if (pick != null) addRow.Children.Add(Theme.Btn(L.Get("Settings_ChooseExe"), () => { var f = pick(); if (f != null) DoAdd(f); }));
        body.Children.Add(addRow);

        body.Children.Add(ButtonRow(
            Theme.Btn(L.Get("Settings_Remove"), () => { if (list.SelectedItem is string s && remove(s)) { Refill(); RefreshExtrasDirty(); Edited(); } }),
            Theme.Btn(L.Get("Settings_Close"), () => w.Close(), primary: true)));
        w.ShowDialog();
        Render();
    }

    private void EditShortcut()
    {
        var (w, body) = MakeDialog(L.Get("Settings_Shortcut"), 440);
        body.Children.Add(Theme.Title(L.Get("Settings_Shortcut")));
        var h = Theme.Sub(L.Get("Settings_ShortcutHelp"));
        h.Margin = new Thickness(0, 4, 0, 10);
        body.Children.Add(h);

        var pending = S.HotkeyEnabled ? S.Hotkey : "";
        var box = Theme.Input(240);
        box.IsReadOnly = true; box.IsReadOnlyCaretVisible = false; box.Text = HotkeyParser.Display(pending); box.HorizontalAlignment = HorizontalAlignment.Left;
        var hint = Theme.Sub(""); hint.Margin = new Thickness(0, 6, 0, 0);
        box.PreviewKeyDown += (_, e) =>
        {
            e.Handled = true;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var token = HotkeyParser.KeyToToken(key.ToString());
            if (token == null) return;
            var m = Keyboard.Modifiers;
            var f = HotkeyParser.Format((m & ModifierKeys.Control) != 0, (m & ModifierKeys.Alt) != 0,
                (m & ModifierKeys.Shift) != 0, (m & ModifierKeys.Windows) != 0, token);
            if (f == null) { hint.Text = L.Get("Settings_AddModifier"); return; }
            pending = f; box.Text = HotkeyParser.Display(f); hint.Text = "";
        };
        body.Children.Add(box);
        body.Children.Add(hint);

        body.Children.Add(ButtonRow(
            Theme.Btn(L.Get("Settings_Clear"), () => { S.HotkeyEnabled = false; pending = ""; box.Text = ""; Edited(); }),
            Theme.Btn(L.Get("Settings_Save"), () =>
            {
                if (!HotkeyParser.TryParse(pending, out _, out _)) { hint.Text = L.Get("Settings_InvalidShortcut"); return; }
                S.Hotkey = pending; S.HotkeyEnabled = true; Edited();
                w.Close();
            }, primary: true)));
        w.ShowDialog();
        Render();
    }

    private void SearchOptions()
    {
        var (w, body) = MakeDialog(L.Get("Settings_SearchEngineTitle"), 420);
        body.Children.Add(Theme.Title(L.Get("Settings_SearchEngine")));
        var radios = new List<(RadioButton R, string Id)>();
        foreach (var e in SearchEngines.All)
        {
            var r = Theme.Radio(e.Name, "engine", string.Equals(S.SearchEngineId, e.Id, StringComparison.OrdinalIgnoreCase));
            radios.Add((r, e.Id)); body.Children.Add(r);
        }
        var custom = Theme.Radio(L.Get("Settings_CustomEngine"), "engine", S.SearchEngineId == SearchEngines.CustomId);
        radios.Add((custom, SearchEngines.CustomId)); body.Children.Add(custom);
        var url = Theme.Input(360); url.HorizontalAlignment = HorizontalAlignment.Left;
        url.Text = S.CustomSearchUrl ?? "https://exemple.com/?q=***";
        body.Children.Add(url);
        if (!radios.Any(x => x.R.IsChecked == true)) radios[0].R.IsChecked = true;

        body.Children.Add(ButtonRow(Theme.Btn(L.Get("Settings_Save"), () =>
        {
            var chosen = radios.First(x => x.R.IsChecked == true).Id;
            var u = url.Text.Trim();
            if (chosen == SearchEngines.CustomId && !(u.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && u.Contains(SearchEngines.Placeholder)))
            {
                Dialogs.Info(w, Title, L.Get("Settings_CustomUrlRule"));
                return;
            }
            S.SearchEngineId = chosen;
            S.CustomSearchUrl = u.Contains(SearchEngines.Placeholder) ? u : null;
            Edited(); w.Close();
        }, primary: true)));
        w.ShowDialog();
    }

    private void SpellOptions()
    {
        var (w, body) = MakeDialog(L.Get("Settings_SpellingTitle"), 380);
        body.Children.Add(Theme.Title(L.Get("Settings_SpellLanguage")));
        var h = Theme.Sub(L.Get("Settings_SpellHelp"));
        h.Margin = new Thickness(0, 4, 0, 8);
        body.Children.Add(h);
        var langs = new[] { ("Français", "fr-FR"), ("English (US)", "en-US"), ("English (UK)", "en-GB"), ("Español", "es-ES"), ("Deutsch", "de-DE"), ("Italiano", "it-IT") };
        foreach (var (name, code) in langs)
        {
            var r = Theme.Radio(name, "lang", string.Equals(S.SpellLanguage, code, StringComparison.OrdinalIgnoreCase));
            var c = code;
            r.Checked += (_, _) => { S.SpellLanguage = c; Edited(); };
            body.Children.Add(r);
        }
        body.Children.Add(ButtonRow(Theme.Btn(L.Get("Settings_Close"), () => w.Close(), primary: true)));
        w.ShowDialog();
    }
}
