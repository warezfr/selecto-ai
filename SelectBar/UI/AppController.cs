using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using SelectBar.Localization;
using SelectBar.Actions;
using SelectBar.Extensions;
using SelectBar.Input;
using SelectBar.Packaging;
using SelectBar.Online;
using SelectBar.Selection;
using SelectBar.Support;
using SelectBar.Update;
using SelectBar.Settings;

namespace SelectBar.UI;

/// <summary>Chef d'orchestre : hook souris -> sélection -> filtrage -> barre -> action.</summary>
public sealed class AppController : IDisposable
{
    private const int SettleDelayMs = 75;

    private readonly CaptureOptions _capture = CaptureOptions.Current;
    private readonly SettingsStore _store;
    private readonly MouseHook _hook;
    private readonly GestureDetector _gesture = new();
    private readonly MessageWindow _messages = new();
    private readonly FloatingBar _bar = new();
    private readonly ResultPopup _popup = new();
    private readonly ApiKeyVault _vault = new(new SecretStore());
    private readonly ResultService _results;
    private readonly OllamaClient _ollama = new(OnlineHttp.Client);
    private readonly LocalAiManager _localAi;
    private readonly LlmModelClient _modelClient = new(OnlineHttp.Client);
    private readonly ResultPopupActions _popupActions;
    private readonly WindowsActionServices _services = new();
    private readonly SelectionService _selection;
    private readonly uint _ownPid = (uint)Environment.ProcessId;
    private readonly TrayIcon _tray;
    private readonly SpellService _spell;
    private readonly CancellationTokenSource _lifetime = new();
    /// <summary>Mises à jour du site (V3-07) : désactivées en édition Store et en mode capture.</summary>
    private readonly UpdateService _updates;
    private readonly DispatcherTimer _firstUpdateTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    /// <summary>Déclenche la vérification toutes les heures ; le délai de 24 h est appliqué par UpdateLogic.ShouldCheck (résiste à la veille).</summary>
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromHours(1) };

    private AppSettings _settings;
    private ExcludedApps _excluded;
    private ExcludedSites _sites;
    private List<ActionDefinition> _all = new();
    private SettingsWindow? _settingsWindow;
    private WelcomeWindow? _welcome;
    private TrayFlyout? _flyout;
    private long _flyoutClosedAt;
    private readonly bool _settingsFileExisted;       // premier lancement : aucune configuration avant ce démarrage
    private ActionContext? _currentContext;
    private int _generation;
    private bool _downInsideBar;
    private (int X, int Y) _anchor;
    private (int X, int Y) _lastCursor;

    /// <param name="preloaded">Réglages déjà lus par Program (évite une seconde lecture du fichier) ; null = lecture ici.</param>
    public AppController(AppSettings? preloaded = null)
    {
        Log.Write("ctor: début");
        _store = new SettingsStore { ReadOnly = _capture.CaptureMode };   // mode capture : jamais d'écriture des réglages
        _updates = new UpdateService(OnlineHttp.Client,
            _capture.CaptureMode && _capture.PretendVersion is { } pv ? Version.Parse(pv) : typeof(AppController).Assembly.GetName().Version ?? new Version(0, 0, 0), // capture : --pretend-version
            !_capture.CaptureMode || _capture.UpdateUrl != null); // capture : vérification active seulement avec --update-url (scénario de mise à jour en VM)
        _hook = new MouseHook(acceptInjected: _capture.CaptureMode);
        _settingsFileExisted = File.Exists(_store.SettingsPath);
        _settings = preloaded ?? _store.Load();
        if (_capture.CaptureMode) _settings.OnlineActionsEnabled = true;  // jeu de réglages en mémoire uniquement
        _excluded = _store.LoadExcluded();
        _sites = _store.LoadExcludedSites();
        _spell = new SpellService(() => _settings.SpellLanguage);
        _selection = new SelectionService(new ClipboardSelectionReader(_messages));
        _services.CursorProvider = () => _lastCursor;
        OnlineLog.Sink = Log.Write;
        _localAi = new LocalAiManager(OnlineHttp.Client, _ollama);
        _localAi.ModelReady = model => RunOnUi(() =>
        {
            // le modèle qui vient d'être téléchargé devient le modèle actif (même si la fenêtre de réglages est fermée)
            LlmPresets.Apply(_settings, LlmPresets.Ollama);
            _settings.LlmModel = model;
            _settings.LlmApiKeyProtected = null;
            ApplyAll();
        });
        _localAi.ModelDeleted = model => RunOnUi(() =>
        {
            if (_settings.LlmProvider == LlmPresets.Ollama && string.Equals(_settings.LlmModel, model, StringComparison.OrdinalIgnoreCase))
            {
                _settings.LlmModel = "";
                ApplyAll();
            }
        });
        // le moteur Ollama installé par Selecto AI démarre à la première action IA
        _results = new ResultService(() => _settings, OnlineHttp.Client, () => _vault.Unprotect(_settings.LlmApiKeyProtected),
            ct => _localAi.EnsureRunningAsync(ct));
        _popupActions = new ResultPopupActions
        {
            Run = _results.RunAsync,
            Replace = _services.PasteTextAsync,
            Copy = _services.CopyToClipboard,
            OpenFallback = _services.OpenFallback,
            OpenAiSetup = preset => OpenSettings(preset)
        };
        _services.ResultPresenter = request =>
        {
            _popup.Present(request, _anchor, _settings.BarPosition, _popupActions);
            return Task.CompletedTask;
        };
        _services.ExtensionResultPresenter = (text, editable) =>
        {
            var extensionActions = new ResultPopupActions
            {
                Run = (request, _) => Task.FromResult(new ResultPayload { Body = request.Text, CanReplace = request.IsEditable }),
                Replace = _popupActions.Replace, Copy = _popupActions.Copy,
                OpenFallback = _popupActions.OpenFallback, OpenAiSetup = _popupActions.OpenAiSetup
            };
            _popup.Present(new ResultRequest { Text = text, IsEditable = editable, Kind = ResultKind.Explain }, _anchor, _settings.BarPosition, extensionActions);
        };
        _firstUpdateTimer.Tick += (_, _) => { _firstUpdateTimer.Stop(); _ = RunUpdateCheckAsync(manual: false); };
        _updateTimer.Tick += (_, _) => _ = RunUpdateCheckAsync(manual: false);
        Log.Write("ctor: actions à charger");
        ReloadActions();
        // Changement de langue : les titres des actions intégrées sont relus (les extensions gardent leur titre)
        L.CultureChanged += () =>
        {
            var extensions = _all.Where(a => !a.BuiltIn).ToList();
            _all = BuiltInActions.Create(_services, () => _settings, _spell.Suggest, _services.PasteTextAsync, _services);
            _all.AddRange(extensions);
        };
        Log.Write("ctor: actions chargées");

        _tray = new TrayIcon(
            isEnabled: () => _settings.Enabled,
            toggle: ToggleEnabled,
            openSettings: () => OpenSettings(),
            openExtensions: () => OpenExtensionsFolder("ui:tray.extensions", LaunchSource.Tray),
            quit: RequestQuit,
            visible: TrayShouldBeVisible(),
            flyout: p => ToggleFlyout(p.X, p.Y),
            reportProblem: () => ShowFeedback(null));
        // pastille sur l'icône tant qu'une mise à jour est disponible ; en capture, une mise à jour factice (--fake-update) sans réseau
        _updates.Changed += OnUpdateStateChanged;
        if (_capture.CaptureMode && _capture.UpdateUrl is { } updateUrl) { _updates.ManifestUrl = updateUrl; UpdateLogic.CaptureOrigin = new Uri(updateUrl); Log.Write("Capture : latest.json remplacé par " + updateUrl); }
        if (_capture.CaptureMode && _capture.FakeUpdateVersion is { } fakeVersion)
            _updates.ForceAvailableForCapture(new UpdateInfo(fakeVersion, "", "", 0, null));

        _bar.ActionInvoked += OnActionInvoked;
        _hook.LeftDown += OnLeftDown;
        _hook.LeftUp += OnLeftUp;
        _hook.OtherButtonDown += DismissAll;
        _hook.Wheel += OnWheel;
        _hook.KeyDown += OnKeyDown;
        _messages.HotkeyPressed += OnHotkey;
    }

    public void Start()
    {
        _store.Save(_settings); // crée le dossier et les fichiers au premier lancement
        if (!File.Exists(_store.ExcludedPath)) _store.SaveExcluded(_excluded);
        if (!File.Exists(_store.ExcludedSitesPath)) _store.SaveExcludedSites(_sites);
        _hook.Start();
        Log.Write(LocalAiTiming.StartupSummary(LocalAiManager.TotalMemoryBytes() / (1024.0 * 1024 * 1024), LocalAiManager.LogicalCores(), LocalAiManager.HasAvx2(), _settings.LlmModel));
        if (!_capture.CaptureMode && WarmupPlan.ShouldWarm(_settings.PrepareAiAtStartup, _settings.LlmProvider == LlmPresets.Ollama, _localAi.IsEngineInstalled)) _ = _localAi.WarmUpAsync(_settings.LlmModel, CancellationToken.None);
        ApplyHotkey();
        if (!_capture.CaptureMode) StartupManager.Set(_settings.StartWithWindows);
        if (_updates.Enabled)
        {
            _firstUpdateTimer.Start();                  // 30 s après le démarrage, puis une vérification par heure au plus une fois par 24 h
            _updateTimer.Start();
        }
        if (!_capture.CaptureMode && FirstRunFlow.ShouldShow(_settingsFileExisted, _settings.FirstRunCompleted, _capture.CaptureMode)) ShowWelcome();
        ShowOnlineActionsToastOnce();
    }

    /// <summary>Premier lancement : fenêtre « Bienvenue » (jamais bloquante) ; sa fermeture mémorise qu'elle est terminée.</summary>
    private void ShowWelcome(WelcomeStep startStep = WelcomeStep.Intro, bool captureOnly = false)
    {
        if (_welcome != null) { _welcome.Activate(); return; }
        _welcome = new WelcomeWindow(new WelcomeHost
        {
            Settings = _settings,
            Apply = ApplyAll,
            Vault = _vault,
            LocalAi = _localAi,
            Models = _modelClient,
            TestLlm = () => _results.TestLlmAsync(CancellationToken.None),
            OpenLink = url => _services.OpenUrlAs("ui:welcome.link", LaunchSource.Settings, url),
            Finished = () =>
            {
                // --open-welcome (capture) : aucune modification des réglages ni installation
                if (!captureOnly) { _settings.FirstRunCompleted = true; ApplyAll(); }
                _welcome = null;
            }
        }, startStep);
        _welcome.Show();
    }

    /// <summary>Badge de l'icône : visible tant qu'une mise à jour est disponible (le changement peut venir d'un autre thread).</summary>
    private void OnUpdateStateChanged()
    {
        var on = _updates.Available is not null;
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess()) _tray.SetUpdateBadge(on);
        else dispatcher.BeginInvoke(new Action(() => _tray.SetUpdateBadge(on)));
    }

    /// <summary>Actions de premier niveau activées, pour « 12 actions » dans le flyout.</summary>
    private int CountBarActions() => _all.Count(a => a.ParentId == null && !_settings.DisabledActions.Contains(a.Id, StringComparer.OrdinalIgnoreCase));

    /// <summary>Clic gauche sur l'icône : ouvre ou ferme le flyout Fluent ancré à la barre des tâches.</summary>
    private void ToggleFlyout(int x, int y, bool keepOpen = false)
    {
        if (_flyout != null) { _flyout.Close(); return; }
        // un clic sur l'icône pendant que le flyout est ouvert l'a déjà fermé (perte d'activation) : on n'en rouvre pas un aussitôt
        if (Environment.TickCount64 - _flyoutClosedAt < 400) return;
        try
        {
            var flyout = new TrayFlyout(new TrayFlyoutActions
            {
                Edition = EditionInfo.Current,
                IsEnabled = () => _settings.Enabled,
                Toggle = ToggleEnabled,
                OpenSettings = () => OpenSettings(),
                OpenExtensions = () => OpenExtensionsFolder("ui:tray.extensions", LaunchSource.Tray),
                Quit = RequestQuit,
                ReportProblem = () => ShowFeedback(null),
                AvailableUpdate = () => _updates.Available,
                InstallUpdate = OpenAbout,   // même chemin que « Installer » du toast de mise à jour
                OpenAbout = OpenAbout,
                ActionCount = CountBarActions
            }) { KeepOpenOnDeactivate = keepOpen };
            flyout.Dismissed += () => { _flyout = null; _flyoutClosedAt = Environment.TickCount64; };
            _flyout = flyout;
            DismissAll();
            flyout.ShowAt(x, y);
        }
        catch (Exception ex)
        {
            Log.Write("Flyout : " + ex);
            _flyout = null;
        }
    }

    /// <summary>L'icône reste visible si l'utilisateur ne l'a pas masquée.</summary>
    private bool TrayShouldBeVisible() => _settings.ShowTrayIcon;

    /// <summary>Quitter (menu de la zone de notification, flyout) : confirmation selon QuitPolicy, puis arrêt.</summary>
    private void RequestQuit()
    {
        if (QuitPolicy.ShouldAsk(_settings.ConfirmQuit, false, CaptureOptions.Current.CaptureMode, false))
        {
            var (quit, dontAskAgain) = QuitDialog.Show(null);
            if (!quit) return; // Annuler : rien n'est enregistré, même si la case « Ne plus demander » a été cochée.
            if (dontAskAgain)
            {
                _settings.ConfirmQuit = false;
                _store.Save(_settings);
            }
        }
        // Sortie confirmée : verrou d'écriture après le dernier enregistrement voulu. La fenêtre Réglages (même instance de
        // réglages, copie « enregistrée » antérieure à la case cochée) ne peut plus réécrire ConfirmQuit = true pendant l'arrêt.
        _store.Lock();
        QuitWithoutAsking();
    }

    /// <summary>Arrêt sans question : fin de session Windows (une boîte bloquerait l'arrêt) ou sortie déjà confirmée.</summary>
    public void QuitWithoutAsking() => Application.Current.Shutdown();

    /// <summary>
    /// Vérifie la présence d'une nouvelle version. Automatique : seulement si l'interrupteur est activé et que 24 h se sont écoulées ;
    /// manuel (bouton « Rechercher maintenant ») : toujours, le résultat s'affiche dans À propos sans notification.
    /// </summary>
    private async Task RunUpdateCheckAsync(bool manual)
    {
        if (!_updates.Enabled || _lifetime.IsCancellationRequested) return;
        if (!manual && (!_settings.AutoCheckUpdates || !UpdateLogic.ShouldCheck(_settings.LastUpdateCheckUtc, DateTime.UtcNow))) return;
        _settings.LastUpdateCheckUtc = DateTime.UtcNow;
        SaveUpdateState();
        var state = await _updates.CheckAsync(_lifetime.Token);
        if (manual || state != UpdateState.Available || _updates.Available is not { } info) return;
        if (UpdateLogic.ShouldNotify(info.Version, _settings.DismissedUpdateVersion, _settings.DismissedUpdateAtUtc, DateTime.UtcNow)) ShowUpdateToast(info);
    }

    /// <summary>Notification « Selecto AI X.Y.Z est disponible » : « Installer » ouvre À propos (téléchargement et progression), « Plus tard » mémorise la version.</summary>
    private void ShowUpdateToast(UpdateInfo info)
    {
        try
        {
            ToastWindow.ShowCornerWithActions(
                L.Format("App_UpdateAvailable", info.Version),
                (L.Get("Settings_Install"), () => OpenSettings(showAbout: true)),
                (L.Get("Settings_Later"), () =>
                {
                    _settings.DismissedUpdateVersion = info.Version;
                    _settings.DismissedUpdateAtUtc = DateTime.UtcNow;
                    SaveUpdateState();
                }));
        }
        catch (Exception ex) { Log.Write("Toast de mise à jour : " + ex.Message); }
    }

    /// <summary>
    /// Enregistre seulement les champs de mise à jour, sur une relecture du fichier : les réglages modifiés mais non appliqués
    /// dans la fenêtre Réglages ne sont pas écrits par une vérification en arrière-plan.
    /// </summary>
    private void SaveUpdateState()
    {
        if (_capture.CaptureMode) return;
        try
        {
            var disk = _store.Load();
            disk.LastUpdateCheckUtc = _settings.LastUpdateCheckUtc;
            disk.DismissedUpdateVersion = _settings.DismissedUpdateVersion;
            disk.DismissedUpdateAtUtc = _settings.DismissedUpdateAtUtc;
            _store.Save(disk);
        }
        catch (Exception ex) { Log.Write("Mises à jour : enregistrement impossible : " + ex.Message); }
    }

    /// <summary>Les exemples de la 3.0 ont été renommés en .js.ancien (non supprimés) : on le dit une fois.</summary>
    private void RetiredSamplesToast()
    {
        try { ToastWindow.ShowCorner(L.Get("Ext_SamplesRenamed")); }
        catch (Exception ex) { Log.Write("Toast exemples d'extension : " + ex.Message); }
    }

    /// <summary>Mise à niveau vers la v3 : les actions en ligne viennent d'être activées ; on le dit une seule fois.</summary>
    private void ShowOnlineActionsToastOnce()
    {
        if (_capture.CaptureMode || !_settings.OnlineActionsJustEnabled) return;
        _settings.OnlineActionsJustEnabled = false;
        try { ToastWindow.ShowCorner(L.Get("App_AiEnabledToast")); }
        catch (Exception ex) { Log.Write("Toast actions en ligne : " + ex.Message); }
    }

    private void ReloadActions()
    {
        _all = BuiltInActions.Create(_services, () => _settings, _spell.Suggest, _services.PasteTextAsync, _services);
        _all.AddRange(ExtensionLoader.LoadAll(_store.ExtensionsDirectory, _services, _settings, RetiredSamplesToast));
    }

    private static void RunOnUi(Action a)
    {
        var d = Application.Current?.Dispatcher;
        if (d == null || d.CheckAccess()) a(); else d.BeginInvoke(a);
    }

    private void ToggleEnabled()
    {
        _settings.Enabled = !_settings.Enabled;
        _store.Save(_settings);
        if (!_settings.Enabled) DismissAll();
        _tray.Refresh();
    }

    /// <summary>Retourne un avertissement si Windows refuse le raccourci.</summary>
    private string? ApplyHotkey()
    {
        if (_settings.HotkeyEnabled && HotkeyParser.TryParse(_settings.Hotkey, out var mods, out var vk))
        {
            if (_messages.SetHotkey(mods, vk)) return null;
            Log.Write("Raccourci global refusé par Windows : " + _settings.Hotkey);
            return L.Get("App_ShortcutRefused");
        }
        _messages.SetHotkey(0, 0);
        return null;
    }

    /// <summary>Enregistre tous les réglages et les applique sans redémarrage.</summary>
    private string? ApplyAll()
    {
        _settings.BarScalePercent = Appearance.ClampScale(_settings.BarScalePercent);
        _settings.BarPosition = Appearance.NormalizePosition(_settings.BarPosition);
        _store.Save(_settings);
        _store.SaveExcluded(_excluded);
        _store.SaveExcludedSites(_sites);
        if (!_capture.CaptureMode) StartupManager.Set(_settings.StartWithWindows);
        _tray.SetVisible(TrayShouldBeVisible());
        _tray.Refresh();
        if (!_settings.Enabled) DismissAll();
        return ApplyHotkey();
    }

    private void OpenExtensionsFolder(string origin, LaunchSource source) =>
        _services.OpenFolderAs(origin, source, _store.ExtensionsDirectory);

    /// <summary>« Signaler un problème » (V3-09) : même fenêtre depuis la zone de notification, le flyout et la page À propos.</summary>
    private void ShowFeedback(Window? owner)
    {
        var endpoint = FeedbackServer.ResolveBaseUrl(Environment.GetEnvironmentVariable) + FeedbackServer.FeedbackPath;
        FeedbackWindow.Open(owner, new FeedbackContext(
            OnlineHttp.VersionText(),
            EditionInfo.Current.ToString().ToLowerInvariant(),
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
            Log.Path, endpoint));
    }

    /// <param name="aiPreset">"ollama" (sur mon PC) ou un fournisseur en ligne : ouvre directement l'assistant de l'onglet IA.</param>
    public void OpenSettings(string? aiPreset = null, bool showAbout = false)
    {
        _popup.HidePopup();
        if (_settingsWindow != null)
        {
            if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Activate();
            if (aiPreset != null) _settingsWindow.ShowAiAssistant(aiPreset);
            if (showAbout) _settingsWindow.ShowAbout();
            return;
        }
        ReloadActions();
        _settingsWindow = new SettingsWindow(new SettingsHost
        {
            Settings = _settings,
            Apps = _excluded,
            Sites = _sites,
            ExtensionsDirectory = _store.ExtensionsDirectory,
            LoadActions = () => { ReloadActions(); return _all; },
            Apply = ApplyAll,
            Vault = _vault,
            OpenLink = url => _services.OpenUrlAs("ui:settings.keys", LaunchSource.Settings, url),
            OpenExtensionsFolder = () => OpenExtensionsFolder("ui:settings.extensions", LaunchSource.Settings),
            Ollama = _ollama,
            LocalAi = _localAi,
            Models = _modelClient,
            TestLlm = () => _results.TestLlmAsync(CancellationToken.None),
            Updates = _updates,
            CheckUpdatesNow = () => RunUpdateCheckAsync(manual: true),
            ReportProblem = () => ShowFeedback(_settingsWindow)
        });
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        if (aiPreset != null) _settingsWindow.ShowAiAssistant(aiPreset);
        if (showAbout) _settingsWindow.ShowAbout();
    }

    /// <summary>Ouvre les réglages sur l'onglet À propos (menu de la zone de notification, lien de mise à jour).</summary>
    public void OpenAbout() => OpenSettings(null, showAbout: true);

    /// <summary>--open-settings / --window-pos / --open-welcome / --open-flyout : ouvre les réglages directement sur l'onglet voulu (sans clic).</summary>
    public void ApplyCaptureOptions()
    {
        if (_capture.OpenWelcome is { } w) ShowWelcome((WelcomeStep)(w - 1), captureOnly: true);
        if (_capture.OpenFlyout)
        {
            // ancrage comme au clic sur l'icône : coin bas-droit de la zone de notification de l'écran principal
            var screen = System.Windows.Forms.Screen.PrimaryScreen ?? System.Windows.Forms.Screen.AllScreens[0];
            var wa = screen.WorkingArea;
            ToggleFlyout(wa.Right - 24, screen.Bounds.Bottom - 8, keepOpen: true);
        }
        if (_capture.ShowBarText is { } text) ShowCaptureBar(text);
        if (_capture.OpenBarMenu is { } menu && !_bar.OpenMenu(menu)) Log.Write("Capture : sous-menu introuvable « " + menu + " »");
        if (_capture.OpenSettings is not { } tab) return;
        OpenSettings();
        if (_settingsWindow == null) return;
        if (_capture.WindowPos is { } pos) _settingsWindow.PlaceAt(pos.X, pos.Y);
        switch (tab)
        {
            case SettingsTabArg.General: _settingsWindow.ShowGeneral(); break;
            case SettingsTabArg.Actions: _settingsWindow.ShowActions(); break;
            case SettingsTabArg.Ai: _settingsWindow.ShowAi(); break;
            case SettingsTabArg.About: _settingsWindow.ShowAbout(); break;
        }
    }

    /// <summary>Mode capture (--show-bar) : affiche la barre pour un texte comme après une sélection. Position : --window-pos, sinon centre de l'écran principal.</summary>
    private void ShowCaptureBar(string text)
    {
        var wa = (System.Windows.Forms.Screen.PrimaryScreen ?? System.Windows.Forms.Screen.AllScreens[0]).WorkingArea;
        var (x, y) = _capture.WindowPos ?? ((wa.Left + wa.Right) / 2, (wa.Top + wa.Bottom) / 2);
        var ctx = TextClassifier.BuildContext(text, _capture.ShowBarEditable, null, p => File.Exists(p) || Directory.Exists(p));
        ShowBarFor(ctx, x, y);
    }

    // ---- événements souris / clavier (thread UI, via le hook) ----

    private void OnLeftDown(int x, int y)
    {
        _lastCursor = (x, y);
        // le hook voit le clic AVANT WPF : un clic sur la barre ou le popup ne doit rien fermer
        _downInsideBar = _bar.ContainsPoint(x, y) || _popup.ContainsPoint(x, y);
        if (_downInsideBar) return;
        DismissAll();
        _gesture.OnMouseDown(x, y, Environment.TickCount64);
    }

    private async void OnLeftUp(int x, int y)
    {
        _lastCursor = (x, y);
        if (_downInsideBar) { _downInsideBar = false; return; }
        if (_gesture.OnMouseUp(x, y) == GestureKind.None) return;
        if (!_settings.Enabled) return;

        var gen = ++_generation;
        try
        {
            await Task.Delay(SettleDelayMs);
            if (gen != _generation) return;
            await TriggerAsync(x, y, gen);
        }
        catch (Exception ex) { Log.Write("Déclenchement : " + ex); }
    }

    /// <summary>
    /// Raccourci global : affiche la barre sur la sélection en cours (sélection au clavier : Ctrl+A, Maj+flèches). Le raccourci est encore
    /// enfoncé à ce moment-là ; on attend son relâchement, sinon le Ctrl+C du repli presse-papiers partirait avec la touche Maj (Ctrl+Maj+C).
    /// </summary>
    private async void OnHotkey()
    {
        var gen = ++_generation;
        try
        {
            var waited = 0;
            while (NativeMethods.ModifiersDown() && waited < ModifierReleaseTimeoutMs)
            {
                await Task.Delay(25);
                waited += 25;
            }
            if (gen != _generation) return;
            var (x, y) = NativeMethods.TryGetCaretPosition() ?? (NativeMethods.GetCursorPos(out var p) ? (p.X, p.Y) : _lastCursor);
            _lastCursor = (x, y);
            await TriggerAsync(x, y, gen);
        }
        catch (Exception ex) { Log.Write("Raccourci : " + ex); }
    }

    private const int ModifierReleaseTimeoutMs = 600;

    private async Task TriggerAsync(int x, int y, int gen)
    {
        var exe = NativeMethods.GetForegroundExe(out var pid);
        if (pid == _ownPid) return;                       // la barre elle-même / nos fenêtres
        if (_excluded.Contains(exe)) return;

        if (_sites.Count > 0 && BrowserProbe.IsBrowser(exe))
        {
            var probeTask = Task.Run(BrowserProbe.Read);
            if (await Task.WhenAny(probeTask, Task.Delay(400)) == probeTask)
            {
                var (url, title) = probeTask.Result;
                if (_sites.IsExcluded(url, title)) return;
            }
            if (gen != _generation) return;
        }

        var sel = await _selection.ReadAsync(x, y, exe);
        if (sel == null || gen != _generation) return;    // sélection vide ou nouveau clic entre-temps

        var clipHasText = false;
        try { clipHasText = Clipboard.ContainsText(); } catch { }
        // E/S disque hors thread UI, avec délai : un partage réseau injoignable ne doit pas figer la barre.
        var build = Task.Run(() => TextClassifier.BuildContext(sel.Text, sel.IsEditable, exe,
            p => File.Exists(p) || Directory.Exists(p), clipHasText, sel.Html, sel.Rtf));
        var ctx = await Task.WhenAny(build, Task.Delay(800)) == build
            ? build.Result
            : TextClassifier.BuildContext(sel.Text, sel.IsEditable, exe, _ => false, clipHasText, sel.Html, sel.Rtf);
        if (gen != _generation) return;
        ShowBarFor(ctx, x, y);
    }

    /// <summary>Filtre les actions pour ce contexte et affiche la barre au point (x, y) : chemin commun à la sélection réelle et au mode capture.</summary>
    private void ShowBarFor(ActionContext ctx, int x, int y)
    {
        var actions = ActionFilter.Select(_all, ctx, _settings);
        // Actions inline : leur texte est calculé maintenant ; une action sans texte n'apparaît pas.
        var inlineTexts = new Dictionary<string, string>();
        foreach (var action in actions)
        {
            var text = action.InlineText?.Invoke(ctx);
            if (!string.IsNullOrWhiteSpace(text)) inlineTexts[action.Id] = text;
        }
        actions = actions.Where(action => action.InlineText == null || inlineTexts.ContainsKey(action.Id)).ToList();
        if (actions.Count == 0) return;

        _currentContext = ctx;
        _anchor = (x, y);
        _bar.ShowAt(x, y, actions, _settings, inlineTexts);
    }

    private async void OnActionInvoked(ActionDefinition action)
    {
        var ctx = _currentContext;
        DismissBar();
        if (ctx == null) return;
        var origin = LaunchPolicy.OriginOf(action);
        Log.Write($"Clic sur l'action {origin} (pointeur {_lastCursor.X},{_lastCursor.Y})");
        // tout lancement externe demandé pendant cette action porte son origine ; hors de ce clic, il est refusé
        using var scope = _services.Launcher.Scope(origin, action.BuiltIn ? LaunchSource.Button : LaunchSource.Extension);
        try { await action.Execute(ctx); }
        catch (Exception ex) { Log.Write($"Action {action.Id} : {ex.Message}"); }
    }

    private void DismissBar()
    {
        _generation++;
        _currentContext = null;
        _bar.HideBar();
    }

    private void DismissAll()
    {
        DismissBar();
        _popup.HidePopup();
    }

    private void OnWheel(int x, int y)
    {
        if (_popup.ContainsPoint(x, y)) return;   // la molette dans le popup le fait défiler
        DismissAll();
    }

    private void OnKeyDown(uint vk)
    {
        if (IsModifier(vk)) return;
        if (vk == 0x1B) DismissAll();             // Échap ferme barre et popup
        else DismissBar();                        // une frappe ferme la barre, pas le résultat en cours de lecture
    }

    private static bool IsModifier(uint vk) => vk is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;

    public void Dispose()
    {
        _lifetime.Cancel();
        _firstUpdateTimer.Stop();
        _updateTimer.Stop();
        _localAi.Cancel();
        _localAi.StopOwned();                     // seulement le moteur lancé par Selecto AI
        _hook.Dispose();
        _messages.Dispose();
        _tray.Dispose();
        try { _flyout?.Close(); } catch { }
        _bar.Close();
        _popup.Close();
    }
}
