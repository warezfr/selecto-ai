using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SelectBar.Setup.Install;
using SelectBar.Setup.Logic;
using Ui = Wpf.Ui.Controls;

namespace SelectBar.Setup.UI;

/// <summary>
/// Fenêtre unique de l'installateur et du désinstallateur (760 x 480, Fluent, Mica). Les écrans sont construits en code
/// (voir SetupWindow.Pages.cs) et enchaînés par la machine à états SetupFlow.
/// </summary>
internal sealed partial class SetupWindow : Ui.FluentWindow
{
    private readonly SetupOptions _o;
    private readonly SetupMode _mode;
    private readonly Grid _host = new();
    private string _lang;
    private SetupScreen _screen;
    private string _dir;
    private bool _desktop;
    private bool _startup;
    private bool _optionsOpen;
    private bool _launchAfter = true;
    private bool _purge;
    private bool _busy;
    private bool _forceClose;
    private bool _lastWasUninstall;
    private string? _installedVersion;
    private string _errorMessage = "";
    private string _errorDetails = "";
    private bool _wasRunning;
    private string? _runningExe;
    private CancellationTokenSource? _cts;
    private Action? _toggleOptions;

    // éléments de la page de progression (null hors de cet écran)
    private RingControl? _ring;
    private TextBlock? _percentText;
    private TextBlock? _stepText;
    private TextBlock? _detailText;
    private Ui.Button? _cancelButton;

    public int ExitCode { get; private set; }

    public SetupWindow(SetupOptions options, string lang)
    {
        _o = options;
        _lang = lang;
        _mode = options.Mode;
        if (options.DemoScreen is SetupScreen.UninstallConfirm or SetupScreen.UninstallProgress or SetupScreen.UninstallDone) _mode = SetupMode.Uninstall;

        var installed = _o.Demo ? new InstalledInfo(null, null) : SystemOps.ReadInstalled();
        _installedVersion = installed.Version;
        _dir = InstallPaths.ChooseInstallDir(_o.Dir, installed.Location, SystemOps.LocalAppData);
        if (_mode == SetupMode.Uninstall) _dir = InstallPaths.NormalizeDir(_o.RelocatedDir ?? installed.Location ?? _dir);
        _desktop = _o.DesktopShortcut;
        try { _startup = _o.Startup || (!_o.Demo && new RegistryRunKey().Get(SelectBar.Settings.StartupEntries.Name) != null); } catch { _startup = _o.Startup; }

        Title = Strings.Format(_mode == SetupMode.Uninstall ? "window.uninstall" : "window.install", _lang, BuildInfo.DisplayName);
        Width = 760;
        Height = 480;
        ResizeMode = ResizeMode.CanMinimize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowBackdropType = Ui.WindowBackdropType.Mica;
        ExtendsContentIntoTitleBar = true;
        FontFamily = SetupTheme.Body;
        FontSize = 14;
        SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var bar = new Ui.TitleBar { Title = Title, ShowMaximize = false, ShowMinimize = true };
        Grid.SetRow(bar, 0);
        root.Children.Add(bar);
        Grid.SetRow(_host, 1);
        root.Children.Add(_host);
        Content = root;

        Closing += OnClosing;
        ContentRendered += OnFirstRender;
    }

    private bool IsUninstall => _mode == SetupMode.Uninstall;

    private string T(string key) => Strings.Get(key, _lang);
    private string T(string key, params object[] args) => Strings.Format(key, _lang, args);

    private bool _started;

    private void OnFirstRender(object? sender, EventArgs e)
    {
        if (_started) return;
        _started = true;
        if (_o.Demo) { DemoStart(); return; }
        Go(SetupFlow.Initial(_mode));
    }

    // ---------- navigation ----------

    private void Go(SetupScreen screen, bool animate = true)
    {
        _screen = screen;
        _ring = null; _percentText = null; _stepText = null; _detailText = null; _cancelButton = null;
        var page = BuildPage(screen, animate);
        var old = _host.Children.Count > 0 ? _host.Children[0] : null;
        if (old != null)
        {
            old.IsHitTestVisible = false;
            if (animate) Motion.FadeOut(old, 120, () => _host.Children.Remove(old));
            else _host.Children.Remove(old);
        }
        _host.Children.Add(page);
        if (animate) Motion.FadeSlideIn(page, 280, 8, 90);
    }

    private FrameworkElement BuildPage(SetupScreen screen, bool animate) => screen switch
    {
        SetupScreen.Welcome => BuildWelcome(animate),
        SetupScreen.Progress => BuildProgress(false),
        SetupScreen.Done => BuildDone(false),
        SetupScreen.Error => BuildError(),
        SetupScreen.UninstallConfirm => BuildUninstallConfirm(),
        SetupScreen.UninstallProgress => BuildProgress(true),
        _ => BuildDone(true),
    };

    private void Fire(SetupEvent e) => Go(SetupFlow.Next(_screen, e, _mode));

    // ---------- opérations ----------

    private void StartInstall()
    {
        if (_busy) return;
        _lastWasUninstall = false;
        Fire(SetupEvent.StartInstall);
        _ = RunInstallAsync();
    }

    private async Task RunInstallAsync()
    {
        _busy = true;
        _cts = new CancellationTokenSource();
        var progress = new Progress<ProgressInfo>(OnProgress);
        var request = new InstallRequest(_dir, _desktop, _startup, _o.Force, _lang);
        var token = _cts.Token;
        OpResult result;
        try
        {
            result = _o.Demo ? await DemoRunAsync(progress, token) : await Task.Run(() => InstallEngine.Run(request, progress, token));
        }
        catch (Exception ex) { result = OpResult.Fail(ex.Message, ex.ToString()); }
        _busy = false;
        _wasRunning = result.WasRunning;
        _runningExe = result.RunningExe;
        if (result.Success) { ExitCode = 0; Fire(SetupEvent.InstallSucceeded); }
        else if (result.Cancelled) { Fire(SetupEvent.Cancel); }
        else
        {
            ExitCode = 1;
            _errorMessage = result.Message ?? "";
            _errorDetails = result.Details ?? result.Message ?? "";
            Fire(SetupEvent.InstallFailed);
        }
    }

    private void StartUninstall()
    {
        if (_busy) return;
        _lastWasUninstall = true;
        Fire(SetupEvent.StartUninstall);
        _ = RunUninstallAsync();
    }

    private async Task RunUninstallAsync()
    {
        _busy = true;
        _cts = new CancellationTokenSource();
        var progress = new Progress<ProgressInfo>(OnProgress);
        var request = new UninstallRequest(_dir, _purge, _lang);
        var token = _cts.Token;
        OpResult result;
        try
        {
            result = _o.Demo ? await DemoRunAsync(progress, token) : await Task.Run(() => UninstallEngine.Run(request, progress, token));
        }
        catch (Exception ex) { result = OpResult.Fail(ex.Message, ex.ToString()); }
        _busy = false;
        if (result.Success) { ExitCode = 0; Fire(SetupEvent.UninstallSucceeded); }
        else
        {
            ExitCode = 1;
            _errorMessage = result.Message ?? "";
            _errorDetails = result.Details ?? result.Message ?? "";
            Fire(SetupEvent.UninstallFailed);
        }
    }

    private void Retry()
    {
        if (_busy) return;
        var uninstall = _lastWasUninstall;
        Fire(SetupEvent.Retry);
        _ = uninstall ? RunUninstallAsync() : RunInstallAsync();
    }

    private void OnProgress(ProgressInfo p)
    {
        if (_ring == null) return;
        _ring.SetAnimated(p.Percent);
        if (_percentText != null) _percentText.Text = ((int)Math.Round(p.Percent)).ToString() + " %";
        if (_stepText != null) _stepText.Text = StepLabel(p.Step);
        if (_detailText != null) _detailText.Text = p.Detail;
        if (_cancelButton != null && p.Step == InstallStep.Finalize) _cancelButton.IsEnabled = false;
    }

    private string StepLabel(InstallStep s)
    {
        var prefix = IsUninstall ? "ustep." : "step.";
        return T(prefix + s switch
        {
            InstallStep.Prepare => "prepare",
            InstallStep.Copy => "copy",
            InstallStep.Shortcuts => "shortcuts",
            _ => "finalize",
        });
    }

    private void CancelInstall()
    {
        if (_cts == null || !_busy) return;
        if (_cancelButton != null) { _cancelButton.IsEnabled = false; _cancelButton.Content = T("progress.cancelling"); }
        _cts.Cancel();
    }

    private void FinishAndClose()
    {
        if (_screen == SetupScreen.Done && !_o.Demo && _launchAfter)
        {
            var exe = InstallPaths.ExePath(_dir);
            SystemOps.Launch(exe);
        }
        _forceClose = true;
        Close();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_forceClose || !_busy) return;
        if (_screen == SetupScreen.UninstallProgress) { e.Cancel = true; return; }
        e.Cancel = true;
        var answer = MessageBox.Show(this, T("close.confirm"), Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes) CancelInstall();
    }

    // ---------- démonstration (--demo) : rien n'est installé ----------

    private async Task<OpResult> DemoRunAsync(IProgress<ProgressInfo> progress, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < ProgressSteps.DemoDuration)
        {
            if (ct.IsCancellationRequested) return OpResult.Cancel(T("err.cancelled"));
            var percent = ProgressSteps.DemoPercentAt(clock.Elapsed);
            progress.Report(new ProgressInfo(ProgressSteps.StepFor(percent), percent, ""));
            await Task.Delay(60);
        }
        progress.Report(new ProgressInfo(InstallStep.Finalize, 100, ""));
        await Task.Delay(250);
        return OpResult.Ok();
    }

    private async void DemoStart()
    {
        var screen = _o.DemoScreen ?? SetupFlow.Initial(_mode);
        _errorMessage = _errorDetails = T("err.demo");
        Go(screen, true);
        if (_o.Hold)
        {
            if (screen is SetupScreen.Progress or SetupScreen.UninstallProgress) OnProgress(new ProgressInfo(InstallStep.Copy, 45, "SelectoAI.exe"));
            return;
        }
        switch (screen)
        {
            case SetupScreen.Welcome:
                await Task.Delay(4500);
                if (_screen == SetupScreen.Welcome) StartInstall();
                break;
            case SetupScreen.UninstallConfirm:
                await Task.Delay(4500);
                if (_screen == SetupScreen.UninstallConfirm) StartUninstall();
                break;
            case SetupScreen.Progress:
                _lastWasUninstall = false;
                await RunInstallAsync();
                break;
            case SetupScreen.UninstallProgress:
                _lastWasUninstall = true;
                await RunUninstallAsync();
                break;
        }
    }
}
