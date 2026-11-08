using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SelectBar.Actions;
using SelectBar.Online;
using SelectBar.Settings;
using SelectBar.Localization;

namespace SelectBar.UI;

/// <summary>
/// Onglet IA : « Actions en ligne » (colonne de gauche) et assistant « Modèle d'IA » (colonne de droite). Trois grosses cartes
/// (Sur mon PC / Service en ligne / Plus tard) puis des ÉTAPES SUCCESSIVES : un seul écran à la fois, un bouton principal par étape,
/// bons défauts. Rien ne se télécharge sans clic explicite ; « Annuler » est toujours là.
/// </summary>
public sealed partial class SettingsWindow
{
    private enum AiMode { Overview, Local, Online }

    private AiMode _aiMode = AiMode.Overview;
    private bool _changing;
    private StackPanel? _aiHost;
    private LocalAiStatus? _local;                   // null = recherche en cours
    private CancellationTokenSource? _aiCts;
    private ProgressBar? _pullBar;
    private TextBlock? _pullText;
    private TextBlock? _pullTitle;
    private LocalAiPhase _lastPhase = LocalAiPhase.Idle;
    private bool _localSubscribed;
    private bool _ollamaVerified;                    // « Prêt » local seulement après une détection réelle
    private bool _changingModel;
    private bool _showPowerful;
    private string _selectedModelId = "";
    private string _typedKey = "";
    private string? _aiMessage;
    private bool _aiMessageIsError;

    // service en ligne : étape 1 (service + clé), étape 2 (modèle)
    private int _onlineStep = 1;
    private string _onlineProvider = LlmPresets.OpenAi;
    private string _customUrl = "";
    private IReadOnlyList<LlmModelInfo>? _models;
    private string? _modelsNote;
    private string _selectedModel = "";

    // ---------------------------------------------------------------- onglet IA

    private UIElement BuildAiTab()
    {
        var left = new StackPanel();
        left.Children.Add(Theme.SectionHeader(L.Get("Settings_OnlineActions"), true));
        left.Children.Add(BuildOnlineSection());
        var right = new StackPanel();
        right.Children.Add(Theme.SectionHeader(L.Get("Settings_AiModel"), true));
        right.Children.Add(BuildAiSection());
        right.Children.Add(Theme.Group(
            Theme.Row(L.Get("Settings_PrepareAi"), L.Get("Settings_PrepareAiHelp"),
                Theme.Switch(S.PrepareAiAtStartup, v => { S.PrepareAiAtStartup = v; Edited(); return true; }, L.Get("Settings_PrepareAi")))));
        return Columns(left, right, 1, 1.3);
    }

    // ---------------------------------------------------------------- actions en ligne

    private UIElement BuildOnlineSection()
    {
        var email = Theme.Input(150);
        email.Text = S.MyMemoryEmail ?? "";
        email.ToolTip = L.Get("Settings_Optional");
        email.PlaceholderText = L.Get("Settings_Optional");
        System.Windows.Automation.AutomationProperties.SetName(email, L.Get("Settings_MyMemoryEmail"));
        email.LostFocus += (_, _) =>
        {
            var v = email.Text.Trim();
            S.MyMemoryEmail = v.Length == 0 ? null : v;
            Edited();
        };

        var dict = new ComboBox { Width = 150, FontFamily = Theme.Font, FontSize = 14 };
        var dictOptions = new[] { ("auto", L.Get("Settings_Automatic")), ("fr", "Français"), ("en", L.Get("Settings_LangEnglish")) };
        foreach (var (_, name) in dictOptions) dict.Items.Add(name);
        dict.SelectedIndex = Math.Max(0, Array.FindIndex(dictOptions, o => o.Item1 == (S.DictionaryLanguage ?? "auto").ToLowerInvariant()));
        dict.SelectionChanged += (_, _) =>
        {
            if (dict.SelectedIndex < 0) return;
            S.DictionaryLanguage = dictOptions[dict.SelectedIndex].Item1;
            Edited();
        };

        return Theme.Group(
            Theme.Row(L.Get("Settings_OnlineActions"),
                L.Get("Settings_OnlineHelp"),
                Theme.Switch(S.OnlineActionsEnabled, v => { S.OnlineActionsEnabled = v; Edited(); return true; }, L.Get("Settings_OnlineActions"))),
            Theme.Row(L.Get("Settings_TranslateTo"), null, LanguageCombo(S.TargetLanguage, v => S.TargetLanguage = v)),
            Theme.Row(L.Get("Settings_FallbackLang"), L.Get("Settings_FallbackHelp"), LanguageCombo(S.FallbackLanguage, v => S.FallbackLanguage = v)),
            Theme.Row(L.Get("Settings_Dictionary"), L.Get("Settings_DictionaryHelp"), dict),
            Theme.Row(L.Get("Settings_MyMemoryEmail"), L.Get("Settings_MyMemoryHelp"), email));
    }

    private ComboBox LanguageCombo(string current, Action<string> set)
    {
        var box = new ComboBox { Width = 150, FontFamily = Theme.Font, FontSize = 14 };
        foreach (var (code, _) in LanguageCodes.All) box.Items.Add(LanguageCodes.DisplayName(code));
        var cur = LanguageCodes.Normalize(current);
        box.SelectedIndex = Math.Max(0, LanguageCodes.All.ToList().FindIndex(l => l.Code == cur));
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedIndex < 0) return;
            set(LanguageCodes.All[box.SelectedIndex].Code);
            Edited();
        };
        return box;
    }

    // ---------------------------------------------------------------- assistant Modèle d'IA

    private UIElement BuildAiSection()
    {
        _aiHost = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };
        if (!_localSubscribed) { _host.LocalAi.Changed += OnLocalAiChanged; _localSubscribed = true; }
        if (_host.LocalAi.Busy && _aiMode == AiMode.Overview)
        {
            // une installation continue en arrière-plan : on rouvre l'assistant sur sa progression
            _aiMode = AiMode.Local;
            _lastPhase = _host.LocalAi.Phase;
            RenderAi();
            RefreshLocal(silent: true, force: true);
            return Theme.Line(_aiHost);
        }
        _ollamaVerified = false;
        RenderAi();
        if (S.LlmProvider == LlmPresets.Ollama && !_host.LocalAi.Busy) VerifyOllama(false);
        return Theme.Line(_aiHost);
    }

    private void TestNow()
    {
        if (S.LlmProvider == LlmPresets.Ollama) { _ollamaVerified = false; RenderAi(); VerifyOllama(true); }
        else SaveAndTest(null);
    }

    /// <summary>Détection réelle du moteur et du modèle configuré ; l'écran « Prêt » n'existe qu'après confirmation.</summary>
    private async void VerifyOllama(bool thenTest)
    {
        try { _aiCts?.Cancel(); } catch (ObjectDisposedException) { }
        var cts = _aiCts = new CancellationTokenSource();
        LocalAiStatus st;
        try
        {
            if (!_host.LocalAi.Busy) await _host.LocalAi.EnsureRunningAsync(cts.Token);   // moteur de Selecto AI éteint : on le démarre
            st = await _host.LocalAi.RefreshAsync(cts.Token);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            Log.Write("Vérification d'Ollama : " + ex.Message);
            st = new LocalAiStatus(false, _host.LocalAi.IsEngineInstalled, false, Array.Empty<OllamaModel>());
        }
        if (cts.IsCancellationRequested || _aiHost == null) return;
        _local = st;
        var state = AssistantLogic.ComputeAssistantState(st.EngineRunning, st.Models.Select(m => m.Name).ToList(), S.LlmModel,
            st.EngineInstalled, st.OwnedBySelectBar);
        if (state.Stage == AssistantStage.Ready)
        {
            _ollamaVerified = true;
            if (thenTest) SaveAndTest(null); else RenderAi();
            return;
        }
        _ollamaVerified = false;
        if (state.ClearActiveModel) { S.LlmModel = ""; Edited(); }      // le fournisseur choisi reste
        _aiMode = AiMode.Local;
        _changingModel = false;
        _aiMessage = state.Message;
        _aiMessageIsError = true;
        RenderAi();
    }

    /// <summary>Ouvre l'assistant sur l'étape voulue (appelé depuis le popup « Aucun modèle configuré »).</summary>
    /// <param name="preset">"ollama" = sur mon PC ; un identifiant de fournisseur ou "online" = service en ligne.</param>
    public void ShowAiAssistant(string preset)
    {
        _tab = TabAi;
        _changing = true;
        _aiMode = AiMode.Overview;
        Render();
        if (preset == LlmPresets.Ollama) StartLocal();
        else StartOnline(preset == "online" ? null : preset);
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        // détection automatique : on relance la recherche d'Ollama quand la fenêtre reprend le focus
        if (_aiHost == null || _host.LocalAi.Busy) return;
        if (_aiMode == AiMode.Local) RefreshLocal(silent: true);
        else if (_aiMode == AiMode.Overview && S.LlmProvider == LlmPresets.Ollama && AiConfigured) { _ollamaVerified = false; RenderAi(); VerifyOllama(false); }
    }

    protected override void OnClosed(EventArgs e)
    {
        try { _aiCts?.Cancel(); } catch (ObjectDisposedException) { }
        _host.LocalAi.Changed -= OnLocalAiChanged;   // les téléchargements, eux, continuent en arrière-plan
        base.OnClosed(e);
    }

    private bool AiConfigured => LlmPresets.IsConfigured(S);

    private void RenderAi()
    {
        if (_aiHost == null) return;
        var host = _aiHost;
        host.Children.Clear();
        _pullBar = null; _pullText = null; _pullTitle = null;

        switch (_aiMode)
        {
            case AiMode.Local: RenderLocal(host); break;
            case AiMode.Online: RenderOnline(host); break;
            default:
                if (AiConfigured && !_changing)
                {
                    if (S.LlmProvider == LlmPresets.Ollama && !_ollamaVerified) host.Children.Add(Intro(L.Get("Settings_CheckingModel")));
                    else RenderReady(host);
                }
                else RenderCards(host);
                break;
        }
        if (_aiMessage != null)
        {
            var m = Theme.Label(_aiMessage, 12, _aiMessageIsError ? Theme.Warn : Theme.StatusOn);
            m.Margin = new Thickness(0, 8, 0, 0);
            host.Children.Add(m);
        }
    }

    private static WrapPanel Buttons(params UIElement[] items)
    {
        var w = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        foreach (var i in items) w.Children.Add(i);
        return w;
    }

    private static TextBlock Intro(string text)
    {
        var t = Theme.Label(text, 13, Theme.Text);
        t.Margin = new Thickness(0, 0, 0, 4);
        return t;
    }

    private void ResetMessage() { _aiMessage = null; _aiMessageIsError = false; }

    // -- vue d'accueil

    private void RenderCards(StackPanel host)
    {
        host.Children.Add(Intro(L.Get("Settings_AiIntro")));
        host.Children.Add(Card(L.Get("Settings_OnMyPc"), L.Get("Settings_OnMyPcHelp"), StartLocal));
        host.Children.Add(Card(L.Get("Settings_OnlineService"), L.Get("Settings_OnlineServiceList"), () => StartOnline(null)));
        host.Children.Add(Card(L.Get("Settings_Later"), L.Get("Settings_LaterHelp"), () =>
        {
            LlmPresets.Apply(S, LlmPresets.None);
            _changing = false;
            ResetMessage();
            Edited();
            RenderAi();
        }));
        if (AiConfigured)
            host.Children.Add(Buttons(Theme.Btn(L.Get("Settings_Cancel"), () => { _changing = false; ResetMessage(); RenderAi(); })));
    }

    private Border Card(string title, string desc, Action click)
    {
        var stack = new StackPanel();
        stack.Children.Add(Theme.Label(title, 14, Theme.Text, FontWeights.SemiBold));
        var d = Theme.Label(desc, 12, Theme.SubText);
        d.Margin = new Thickness(0, 2, 0, 0);
        stack.Children.Add(d);
        var card = new Border
        {
            BorderBrush = Theme.ButtonBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 5, 0, 0), Cursor = Cursors.Hand, Background = Theme.WindowBg, Child = stack
        };
        card.MouseEnter += (_, _) => card.BorderBrush = Theme.Accent;
        card.MouseLeave += (_, _) => card.BorderBrush = Theme.ButtonBorder;
        MakeClickable(card, click);
        System.Windows.Automation.AutomationProperties.SetName(card, title);
        return card;
    }

    // -- état final

    private void RenderReady(StackPanel host)
    {
        var where = S.LlmProvider == LlmPresets.Ollama ? L.Get("Settings_OnYourPcOllama")
            : S.LlmProvider == LlmPresets.Custom ? L.Get("Settings_CustomServer")
            : LlmPresets.Get(S.LlmProvider)?.Label ?? L.Get("Settings_OnlineServiceWord");
        host.Children.Add(Theme.Label(L.Get("Settings_ReadyPrefix") + S.LlmModel, 18, Theme.StatusOn, FontWeights.SemiBold));
        var sub = Theme.Sub(L.Get("Settings_Works") + (S.LlmProvider == LlmPresets.Ollama || S.LlmProvider == LlmPresets.Custom ? where : L.Get("Settings_With") + where) + ". " + LlmPresets.PrivacyNotice(S.LlmProvider));
        sub.Margin = new Thickness(0, 2, 0, 0);
        host.Children.Add(sub);
        host.Children.Add(Buttons(
            Theme.Btn(L.Get("Settings_Change"), () => { _changing = true; ResetMessage(); RenderAi(); }),
            Theme.Btn(L.Get("Settings_Test"), TestNow)));
    }

    // -- étapes « Sur mon PC » : moteur, modèle, prêt (un seul écran à la fois, tout dans l'application)

    private void StartLocal()
    {
        _aiMode = AiMode.Local;
        _changingModel = false;
        _selectedModelId = "";
        ResetMessage();
        _lastPhase = _host.LocalAi.Phase;
        RefreshLocal(silent: false);
    }

    /// <summary>Relit l'état d'Ollama ; à l'ouverture de l'assistant, démarre le moteur de Selecto AI s'il est installé mais éteint.</summary>
    private async void RefreshLocal(bool silent, bool force = false)
    {
        try { _aiCts?.Cancel(); } catch (ObjectDisposedException) { }
        var cts = _aiCts = new CancellationTokenSource();
        if (!silent) { _local = null; RenderAi(); }
        LocalAiStatus state;
        try
        {
            if (!_host.LocalAi.Busy) await _host.LocalAi.EnsureRunningAsync(cts.Token);
            state = await _host.LocalAi.RefreshAsync(cts.Token);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            Log.Write("État d'Ollama : " + ex.Message);
            state = new LocalAiStatus(false, _host.LocalAi.IsEngineInstalled, false, Array.Empty<OllamaModel>());
        }
        if (cts.IsCancellationRequested || _aiMode != AiMode.Local) return;

        var changed = force || _local == null || _local.EngineRunning != state.EngineRunning || _local.EngineInstalled != state.EngineInstalled
            || _local.Models.Count != state.Models.Count || !_local.Models.Select(m => m.Name).SequenceEqual(state.Models.Select(m => m.Name));
        _local = state;
        if (changed) RenderAi();
    }

    private void OnLocalAiChanged() => Dispatcher.BeginInvoke(new Action(() =>
    {
        if (_aiHost == null || _aiMode != AiMode.Local) return;
        var la = _host.LocalAi;
        if (la.Busy && la.Phase == _lastPhase && _pullBar != null)
        {
            UpdateProgress();                                   // simple mise à jour de la barre
            return;
        }
        var wasBusy = _lastPhase != LocalAiPhase.Idle;
        _lastPhase = la.Phase;
        if (!la.Busy && wasBusy) RefreshLocal(silent: true, force: true);
        else RenderAi();
    }));

    private void UpdateProgress()
    {
        var la = _host.LocalAi;
        if (_pullBar == null || _pullText == null) return;
        var f = la.Progress?.Fraction;
        _pullBar.IsIndeterminate = f == null;
        if (f != null) _pullBar.Value = f.Value;
        _pullText.Text = la.Progress?.Describe() ?? "";
        if (_pullTitle != null) _pullTitle.Text = la.Activity;
    }

    private bool ActiveModelInstalled =>
        S.LlmProvider == LlmPresets.Ollama && !string.IsNullOrEmpty(S.LlmModel) && _local != null
        && _local.Models.Any(m => string.Equals(m.Name, S.LlmModel, StringComparison.OrdinalIgnoreCase));

    private void RenderLocal(StackPanel host)
    {
        var la = _host.LocalAi;
        host.Children.Add(Theme.Label(L.Get("Settings_OnMyPc"), 18, Theme.Text, FontWeights.SemiBold));

        if (la.Busy)
        {
            // une tâche tourne : seule sa progression est affichée
            _pullTitle = Theme.Label(la.Activity, 14, Theme.Text, FontWeights.SemiBold);
            _pullTitle.Margin = new Thickness(0, 8, 0, 0);
            _pullBar = new ProgressBar { Minimum = 0, Maximum = 1, Height = 8, Margin = new Thickness(0, 6, 0, 0), IsIndeterminate = true };
            _pullText = Theme.Sub("");
            _pullText.Margin = new Thickness(0, 4, 0, 0);
            host.Children.Add(_pullTitle);
            host.Children.Add(_pullBar);
            host.Children.Add(_pullText);
            UpdateProgress();
            host.Children.Add(Theme.Sub(L.Get("Settings_CanClose")));
            host.Children.Add(Buttons(Theme.Btn(L.Get("Settings_Cancel"), () => la.Cancel())));
            return;
        }
        if (la.Message != null)
        {
            var m = Theme.Label(la.Message, 12, la.MessageIsError ? Theme.Warn : Theme.StatusOn);
            m.Margin = new Thickness(0, 6, 0, 0);
            host.Children.Add(m);
        }

        var st = _local;
        if (st == null)
        {
            host.Children.Add(Intro(L.Get("Settings_SearchingEngine")));
            host.Children.Add(Buttons(Theme.Btn(L.Get("Settings_Close"), CancelAssistant)));
            return;
        }

        var steps = AssistantSteps.Compute(st.EngineRunning, ActiveModelInstalled, _changingModel);
        var ram = LocalAiManager.TotalMemoryBytes();
        var current = steps[0] != StepState.Done ? 0 : steps[1] != StepState.Done ? 1 : 2;

        if (current == 0)
        {
            // étape 1 sur 3 : moteur
            var (box, inner) = StepBox(1, L.Get("Settings_EngineStep"), steps[0]);
            var arm = LocalAiManager.IsArm64;
            if (st.EngineInstalled)
            {
                inner.Children.Add(Theme.Sub(L.Get("Settings_EngineOff")));
                inner.Children.Add(Buttons(Theme.Btn(L.Get("Settings_StartEngine"), () => RefreshLocal(silent: false), primary: true)));
            }
            else
            {
                inner.Children.Add(Theme.Sub(L.Get("Settings_EngineHelp")));
                inner.Children.Add(Buttons(Theme.Btn(L.Format("Settings_InstallEngine", L.Ltr(OllamaParsers.FormatSize(LocalAiManager.ApproxEngineBytes(arm)))),
                    () => { var rec = ModelCatalog.Recommend(ram, LocalAiManager.LogicalCores(), LocalAiManager.HasAvx2()); _host.LocalAi.StartInstall((ModelCatalog.Find(_selectedModelId) ?? rec).SizeBytes); }, primary: true)));
            }
            host.Children.Add(box);
        }
        else if (current == 1)
        {
            // étape 2 sur 3 : modèle
            var (box, inner) = StepBox(2, L.Get("Settings_ModelStep"), steps[1]);
            RenderModelChoice(inner, st, ram, la);
            host.Children.Add(box);
        }
        else
        {
            // étape 3 sur 3 : prêt
            var (box, inner) = StepBox(3, L.Get("Settings_ReadyStep"), steps[2]);
            inner.Children.Add(Theme.Label(L.Get("Settings_ReadyPrefix") + S.LlmModel, 14, Theme.StatusOn, FontWeights.SemiBold));
            inner.Children.Add(Buttons(
                Theme.Btn(L.Get("Settings_Done"), () => { _aiMode = AiMode.Overview; _changing = false; ResetMessage(); RenderAi(); }, primary: true),
                Theme.Btn(L.Get("Settings_Change"), () => { _changingModel = true; RenderAi(); }),
                Theme.Btn(L.Get("Settings_Test"), TestNow)));
            host.Children.Add(box);
        }
        host.Children.Add(Buttons(Theme.Btn(L.Get("Settings_Close"), CancelAssistant)));
    }

    private static (Border Box, StackPanel Inner) StepBox(int number, string title, StepState state)
    {
        var inner = new StackPanel();
        inner.Children.Add(Theme.Label(L.Format("Settings_StepOf", number, title, AssistantSteps.Glyph(state)), 14, Theme.Text, FontWeights.SemiBold));
        var box = new Border
        {
            BorderBrush = Theme.Accent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 6, 0, 0), Background = Theme.WindowBg, Child = inner
        };
        System.Windows.Automation.AutomationProperties.SetName(box, L.Format("Settings_StepName", number, title));
        return (box, inner);
    }

    private void RenderModelChoice(StackPanel parent, LocalAiStatus st, long ram, LocalAiManager la)
    {
        var rec = ModelCatalog.Recommend(ram, LocalAiManager.LogicalCores(), LocalAiManager.HasAvx2());
        if (_selectedModelId.Length == 0) _selectedModelId = rec.Id;
        parent.Children.Add(Theme.Sub(ram > 0 ? ModelCatalog.FormatRam(ram) : L.Get("Settings_ChooseModel")));
        parent.Children.Add(Theme.Sub(ModelCatalog.SpeedHint(rec)));
        var installed = new HashSet<string>(st.Models.Select(m => m.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var m in ModelCatalog.Main) parent.Children.Add(ModelCard(m, m.Id == rec.Id && ram > 0, installed.Contains(m.Id)));
        if (ModelCatalog.AllowsPowerful(ram))
        {
            var more = new Expander
            {
                Header = L.Get("Settings_MorePowerful"), IsExpanded = _showPowerful, Margin = new Thickness(0, 6, 0, 0),
                Content = ModelCard(ModelCatalog.Powerful, false, installed.Contains(ModelCatalog.Powerful.Id)),
                FontFamily = Theme.Font, FontSize = 14
            };
            more.Expanded += (_, _) => _showPowerful = true;
            more.Collapsed += (_, _) => _showPowerful = false;
            parent.Children.Add(more);
        }

        var chosen = ModelCatalog.Find(_selectedModelId) ?? rec;
        if (!la.Busy)
        {
            var already = installed.Contains(chosen.Id);
            var row = new List<UIElement>
            {
                already
                    ? Theme.Btn(L.Get("Settings_UseModel"), () => { _changingModel = false; FinishLocal(chosen.Id); }, primary: true)
                    : Theme.Btn(L.Format("Settings_Download", L.Ltr(chosen.SizeText)), () => { _changingModel = false; la.StartPull(chosen.Id, chosen.SizeBytes); }, primary: true)
            };
            if (_changingModel) row.Add(Theme.Btn(L.Get("Settings_Cancel"), () => { _changingModel = false; RenderAi(); }));
            parent.Children.Add(Buttons(row.ToArray()));
        }

        if (st.Models.Count > 0)
        {
            var title = Theme.Label(L.Get("Settings_InstalledModels"), 12, Theme.SubText);
            title.Margin = new Thickness(0, 8, 0, 2);
            parent.Children.Add(title);
            foreach (var m in st.Models)
            {
                var row = new DockPanel { Margin = new Thickness(0, 1, 0, 0), LastChildFill = true };
                var del = Theme.LinkButton(L.Get("Settings_Delete"), () => ConfirmDeleteModel(m));
                DockPanel.SetDock(del, Dock.Right);
                row.Children.Add(del);
                row.Children.Add(Theme.Label(m.Size > 0 ? $"{m.Name} ({OllamaParsers.FormatSize(m.Size)})" : m.Name, 12, Theme.Text));
                parent.Children.Add(row);
            }
        }
    }

    private Border ModelCard(ModelChoice m, bool recommended, bool isInstalled)
    {
        var selected = string.Equals(_selectedModelId, m.Id, StringComparison.OrdinalIgnoreCase);
        var stack = new StackPanel();
        var head = new WrapPanel();
        head.Children.Add(Theme.Label($"{m.Label} — {m.SizeText}", 14, Theme.Text, FontWeights.SemiBold));
        if (recommended) head.Children.Add(Badge(ModelCatalog.RecommendedBadge, Theme.Accent));
        if (isInstalled) head.Children.Add(Badge(L.Get("Settings_Installed"), Theme.StatusOn));
        stack.Children.Add(head);
        var d = Theme.Label(m.Description, 12, Theme.SubText);
        d.Margin = new Thickness(0, 1, 0, 0);
        stack.Children.Add(d);
        var card = new Border
        {
            BorderBrush = selected ? Theme.Accent : Theme.ButtonBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 5, 0, 0), Cursor = Cursors.Hand, Background = Theme.WindowBg, Child = stack
        };
        MakeClickable(card, () => { _selectedModelId = m.Id; RenderAi(); });
        System.Windows.Automation.AutomationProperties.SetName(card, $"{m.Label}, {m.SizeText}" + (recommended ? L.Get("Settings_RecommendedSuffix") : ""));
        return card;
    }

    /// <summary>Carte cliquable à la souris ET au clavier (Tab pour l'atteindre, Entrée ou Espace pour la choisir).</summary>
    private static void MakeClickable(Border card, Action click)
    {
        card.Focusable = true;
        card.FocusVisualStyle = null;
        card.MouseLeftButtonUp += (_, _) => click();
        card.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Space) { e.Handled = true; click(); }
        };
        card.GotKeyboardFocus += (_, _) => card.BorderThickness = new Thickness(2);
        card.LostKeyboardFocus += (_, _) => card.BorderThickness = new Thickness(1);
    }

    private static Border Badge(string text, Brush color)
    {
        var t = Theme.Label(text, 12, Theme.OnAccent, FontWeights.SemiBold);
        return new Border { Background = color, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(8, 0, 0, 0), Child = t, VerticalAlignment = VerticalAlignment.Center, CornerRadius = new CornerRadius(4) };
    }

    private void ConfirmDeleteModel(OllamaModel m)
    {
        var size = m.Size > 0 ? L.Get("Settings_Frees") + OllamaParsers.FormatSize(m.Size) + "." : "";
        var ok = Dialogs.Ask(this, Title, L.Format("Settings_DeleteModelQuestion", m.Name, size), L.Get("Settings_Delete"), L.Get("Settings_Cancel"));
        if (!ok) return;
        var wasActive = S.LlmProvider == LlmPresets.Ollama && string.Equals(S.LlmModel, m.Name, StringComparison.OrdinalIgnoreCase);
        _host.LocalAi.StartDeleteModel(m.Name, wasActive);
    }

    /// <summary>Désinstalle le moteur IA de SelectBar et ses modèles (bouton de l'onglet À propos) ; la progression s'affiche dans l'onglet IA.</summary>
    private void ConfirmUninstall()
    {
        var la = _host.LocalAi;
        var size = OllamaParsers.FormatSize(la.InstalledBytes());
        var ok = Dialogs.Ask(this, Title,
            L.Get("Settings_UninstallEngineQuestion") + (size.Length > 0 ? L.Get("Settings_Frees") + size + "." : "") +
            "\n" + L.Get("Settings_OllamaUntouched"), L.Get("Settings_Uninstall"), L.Get("Settings_Cancel"));
        if (!ok) return;
        if (S.LlmProvider == LlmPresets.Ollama) { S.LlmModel = ""; Edited(); }
        _local = null;
        la.StartUninstall();
        _aiMode = AiMode.Overview;                 // BuildAiSection rouvre l'assistant sur la progression
        _changing = false;
        GoTo(TabAi);
    }

    private void FinishLocal(string model) => SaveAndTest(() =>
    {
        LlmPresets.Apply(S, LlmPresets.Ollama);
        S.LlmModel = model;
        S.LlmApiKeyProtected = null;
    });

    // -- étapes « Service en ligne » : ① service + clé, ② modèle

    private static bool IsOnlineProvider(string? id) => LlmPresets.Online.Any(p => p.Id == id);

    private void StartOnline(string? preferred)
    {
        _aiMode = AiMode.Online;
        ResetMessage();
        _typedKey = "";
        _onlineStep = 1;
        _models = null;
        _modelsNote = null;
        _onlineProvider = IsOnlineProvider(preferred) ? preferred!
            : IsOnlineProvider(S.LlmProvider) ? S.LlmProvider : LlmPresets.OpenAi;
        _selectedModel = S.LlmProvider == _onlineProvider ? S.LlmModel : "";
        _customUrl = S.LlmProvider == LlmPresets.Custom ? S.LlmBaseUrl : "";
        RenderAi();
    }

    private LlmPreset OnlinePreset => LlmPresets.Get(_onlineProvider) ?? LlmPresets.Online[0];

    private void RenderOnline(StackPanel host)
    {
        if (_onlineStep == 2) RenderOnlineStep2(host); else RenderOnlineStep1(host);
    }

    private void RenderOnlineStep1(StackPanel host)
    {
        var preset = OnlinePreset;
        host.Children.Add(Theme.Label(L.Get("Settings_OnlineServiceStep"), 18, Theme.Text, FontWeights.SemiBold));
        host.Children.Add(Intro(L.Get("Settings_OnlineStep1")));

        var combo = new ComboBox { MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Stretch, FontFamily = Theme.Font, FontSize = 14, Margin = new Thickness(0, 4, 0, 0) };
        foreach (var p in LlmPresets.Online) combo.Items.Add(p.Label);
        combo.SelectedIndex = Math.Max(0, LlmPresets.Online.ToList().FindIndex(p => p.Id == preset.Id));
        System.Windows.Automation.AutomationProperties.SetName(combo, L.Get("Settings_OnlineServiceStep"));
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex < 0) return;
            var id = LlmPresets.Online[combo.SelectedIndex].Id;
            if (id == _onlineProvider) return;
            _onlineProvider = id;
            _typedKey = "";
            _models = null;
            _modelsNote = null;
            _selectedModel = S.LlmProvider == id ? S.LlmModel : "";
            ResetMessage();
            Dispatcher.BeginInvoke(new Action(RenderAi));      // le choix est fait : on redessine après la fin de l'événement
        };
        host.Children.Add(combo);

        if (preset.Id == LlmPresets.Custom)
        {
            host.Children.Add(FieldLabel(L.Get("Settings_ServerAddress")));
            var url = Theme.Input(double.NaN);
            url.HorizontalAlignment = HorizontalAlignment.Stretch;
            url.MaxWidth = 420;
            url.Text = _customUrl;
            url.TextChanged += (_, _) => _customUrl = url.Text;
            host.Children.Add(url);
        }

        var hasStored = ProviderKeys.Get(S, preset.Id) != null;
        host.Children.Add(FieldLabel(preset.NeedsKey ? L.Get("Settings_ApiKey") : L.Get("Settings_ApiKeyOptional")));
        var key = new PasswordBox { MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Stretch, FontFamily = Theme.Font, FontSize = 14, Password = _typedKey };
        key.PasswordChanged += (_, _) => _typedKey = key.Password;
        System.Windows.Automation.AutomationProperties.SetName(key, L.Get("Settings_ApiKeyPrefix") + preset.Label);
        host.Children.Add(key);

        var line = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
        if (preset.KeysPage != null)
        {
            var page = preset.KeysPage;
            line.Children.Add(Theme.LinkButton(L.Get("Settings_GetKey"), () => _host.OpenLink(page)));
        }
        if (hasStored) line.Children.Add(Theme.Label(L.Get("Settings_KeySaved"), 12, Theme.SubText));
        host.Children.Add(line);

        var privacy = Theme.Sub(LlmPresets.PrivacyNotice(preset.Id));
        privacy.Margin = new Thickness(0, 4, 0, 0);
        host.Children.Add(privacy);

        host.Children.Add(Buttons(
            Theme.Btn(L.Get("Settings_Continue"), ContinueOnline, primary: true),
            Theme.Btn(L.Get("Settings_Cancel"), CancelAssistant)));
    }

    /// <summary>Étape ① terminée : vérifie la clé en récupérant la liste réelle des modèles du service, puis passe à l'étape ②.</summary>
    private async void ContinueOnline()
    {
        var preset = OnlinePreset;
        var typed = _typedKey.Trim();
        var key = typed.Length > 0 ? typed : _host.Vault.Unprotect(ProviderKeys.Get(S, preset.Id));
        if (preset.NeedsKey && string.IsNullOrEmpty(key))
        {
            _aiMessage = L.Format("Settings_PasteKeyFirst", preset.Label);
            _aiMessageIsError = true;
            RenderAi();
            return;
        }
        var baseUrl = preset.Id == LlmPresets.Custom ? _customUrl : preset.BaseUrl;
        if (!LlmUrlValidator.Validate(baseUrl, !string.IsNullOrEmpty(key), out var normalized, out var urlError))
        {
            _aiMessage = urlError;
            _aiMessageIsError = true;
            RenderAi();
            return;
        }
        if (preset.Id == LlmPresets.Custom) _customUrl = normalized;

        _aiMessage = L.Get("Settings_FetchingModels");
        _aiMessageIsError = false;
        RenderAi();
        try { _aiCts?.Cancel(); } catch (ObjectDisposedException) { }
        var cts = _aiCts = new CancellationTokenSource();
        ModelListResult result;
        try { result = await _host.Models.ListAsync(preset, normalized, key, cts.Token); }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            Log.Write("Liste des modèles : " + ex.Message);
            result = new ModelListResult(Array.Empty<LlmModelInfo>(), L.Get("Settings_ModelListFailed"));
        }
        if (cts.IsCancellationRequested || _aiHost == null || _aiMode != AiMode.Online) return;

        if (result.Error != null && preset.Id != LlmPresets.Custom)
        {
            // clé refusée, quota, service injoignable : on reste à l'étape ① avec un message lisible
            _aiMessage = result.Error;
            _aiMessageIsError = true;
            RenderAi();
            return;
        }
        _models = result.Models;
        _modelsNote = result.Error != null ? result.Error + L.Get("Settings_TypeModelName") : null;
        if (_selectedModel.Length == 0 || !result.Models.Any(m => m.Id == _selectedModel))
            _selectedModel = LlmModelLists.DefaultModel(result.Models) ?? _selectedModel;
        _onlineStep = 2;
        ResetMessage();
        RenderAi();
    }

    private void RenderOnlineStep2(StackPanel host)
    {
        var preset = OnlinePreset;
        host.Children.Add(Theme.Label(preset.Label, 18, Theme.Text, FontWeights.SemiBold));
        host.Children.Add(Intro(L.Get("Settings_OnlineStep2")));

        if (_models != null && _models.Count > 0)
        {
            var models = _models;
            var combo = new ComboBox { MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Stretch, FontFamily = Theme.Font, FontSize = 14, Margin = new Thickness(0, 4, 0, 0) };
            for (var i = 0; i < models.Count; i++)
            {
                var m = models[i];
                combo.Items.Add((m.Name == m.Id ? m.Id : $"{m.Name} ({m.Id})") + (i == 0 ? L.Get("Settings_Suggested") : ""));
            }
            var idx = models.ToList().FindIndex(m => m.Id == _selectedModel);
            combo.SelectedIndex = Math.Max(0, idx);
            _selectedModel = models[combo.SelectedIndex].Id;
            System.Windows.Automation.AutomationProperties.SetName(combo, L.Get("Settings_ModelStep"));
            combo.SelectionChanged += (_, _) => { if (combo.SelectedIndex >= 0) _selectedModel = models[combo.SelectedIndex].Id; };
            host.Children.Add(combo);
        }
        else
        {
            host.Children.Add(FieldLabel(L.Get("Settings_ModelName")));
            var box = Theme.Input(double.NaN);
            box.HorizontalAlignment = HorizontalAlignment.Stretch;
            box.MaxWidth = 420;
            box.Text = _selectedModel;
            box.TextChanged += (_, _) => _selectedModel = box.Text;
            host.Children.Add(box);
        }
        if (_modelsNote != null)
        {
            var n = Theme.Sub(_modelsNote);
            n.Margin = new Thickness(0, 4, 0, 0);
            host.Children.Add(n);
        }
        var privacy = Theme.Sub(LlmPresets.PrivacyNotice(preset.Id));
        privacy.Margin = new Thickness(0, 4, 0, 0);
        host.Children.Add(privacy);

        host.Children.Add(Buttons(
            Theme.Btn(L.Get("Settings_TestConnection"), FinishOnline, primary: true),
            Theme.Btn(L.Get("Settings_Back"), () => { _onlineStep = 1; ResetMessage(); RenderAi(); }),
            Theme.Btn(L.Get("Settings_Cancel"), CancelAssistant)));
    }

    private void FinishOnline()
    {
        var preset = OnlinePreset;
        var model = _selectedModel.Trim();
        if (model.Length == 0)
        {
            _aiMessage = L.Get("Settings_ChooseFromList");
            _aiMessageIsError = true;
            RenderAi();
            return;
        }
        var typed = _typedKey.Trim();
        var url = _customUrl;
        SaveAndTest(() =>
        {
            LlmPresets.Apply(S, preset.Id);
            if (preset.Id == LlmPresets.Custom) S.LlmBaseUrl = url;
            S.LlmModel = model;
            if (typed.Length > 0) ProviderKeys.Set(S, preset.Id, _host.Vault.Protect(typed));
        });
        _typedKey = "";
    }

    private void CancelAssistant()
    {
        try { _aiCts?.Cancel(); } catch (ObjectDisposedException) { }
        _aiMode = AiMode.Overview;
        if (AiConfigured) _changing = false;
        ResetMessage();
        RenderAi();
    }

    // -- enregistrement et test

    private async void SaveAndTest(Action? save)
    {
        // enregistrement immédiat : installation/choix de l'IA locale, clé API, « Utiliser ce modèle » (pas d'enregistrement différé)
        if (save != null) { save(); Commit(); }
        _aiMessage = L.Get("Settings_Testing");
        _aiMessageIsError = false;
        RenderAi();
        ResultPayload result;
        try { result = await _host.TestLlm(); }
        catch (Exception ex)
        {
            Log.Write("Test du modèle : " + ex.Message);
            result = ResultPayload.Failure(L.Get("Settings_TestFailed"));
        }
        if (_aiHost == null) return;
        if (result.Error == null)
        {
            _aiMode = AiMode.Overview;
            _changing = false;
            _ollamaVerified = true;
            _aiMessage = L.Get("Settings_ConnectionOk");
            _aiMessageIsError = false;
        }
        else
        {
            _aiMessage = result.Error == L.Get("Settings_NoModelConfigured") ? L.Get("Settings_ChooseModelFirst") : result.Error;
            _aiMessageIsError = true;
        }
        RenderAi();
    }

    private static TextBlock FieldLabel(string text)
    {
        var t = Theme.Label(text, 12, Theme.SubText);
        t.Margin = new Thickness(0, 6, 0, 2);
        return t;
    }
}
