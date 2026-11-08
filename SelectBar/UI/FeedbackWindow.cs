using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SelectBar.Online;
using SelectBar.Support;
using WpfUi = Wpf.Ui.Controls;
using SelectBar.Localization;

namespace SelectBar.UI;

/// <summary>Données techniques remplies par AppController (la fenêtre ne lit aucun état interne).</summary>
public sealed record FeedbackContext(string Version, string Edition, string Os, string Arch, string LogPath, string Endpoint);

/// <summary>
/// Fenêtre « Signaler un problème » (V3-09) : type, description, e-mail facultatif, consentement obligatoire, capture de l'écran et journaux
/// DÉCOCHÉS par défaut. Rien n'est envoyé sans clic sur Envoyer ; pas de nouvel essai automatique. Pendant l'envoi, les champs et les
/// boutons sont désactivés ; fermer la fenêtre annule l'envoi.
/// </summary>
public sealed class FeedbackWindow
{
    private readonly FeedbackContext _ctx;
    private readonly WpfUi.FluentWindow _w;

    private readonly ComboBox _type;
    private readonly WpfUi.TextBox _message;
    private readonly TextBlock _count;
    private readonly WpfUi.TextBox _email;
    private readonly CheckBox _consent;
    private readonly WpfUi.Button _captureBtn;
    private readonly StackPanel _shotPanel;
    private readonly Image _thumb;
    private readonly WpfUi.Button _removeBtn;
    private readonly CheckBox _logs;
    private readonly TextBlock _status;
    private readonly ProgressBar _progress;
    private readonly WpfUi.Button _cancel;
    private readonly WpfUi.Button _send;

    private byte[]? _shot;
    private bool _sending;
    private bool _done;
    private CancellationTokenSource? _cts;

    private FeedbackWindow(Window? owner, FeedbackContext ctx)
    {
        _ctx = ctx;
        var (w, body) = Dialogs.Create(owner, L.Get("Feedback_Title"), 480);
        _w = w;

        body.Children.Add(Theme.Sub(L.Get("Feedback_Intro")));

        body.Children.Add(Theme.SectionHeader(L.Get("Feedback_TypeHeader")));
        _type = new ComboBox { FontFamily = Theme.Font, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Stretch };
        System.Windows.Automation.AutomationProperties.SetName(_type, L.Get("Feedback_TypeHeader"));
        foreach (var t in FeedbackLogic.Types) _type.Items.Add(new ComboBoxItem { Content = t.Label, Tag = t.Code });
        _type.SelectionChanged += (_, _) => Refresh();
        body.Children.Add(_type);

        body.Children.Add(Theme.SectionHeader(L.Get("Feedback_DescribeHeader")));
        _message = new WpfUi.TextBox
        {
            Height = 120, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = FeedbackLogic.MaxMessageLength,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = Theme.Font, FontSize = 14,
            PlaceholderText = L.Get("Feedback_MessagePlaceholder")
        };
        System.Windows.Automation.AutomationProperties.SetName(_message, L.Get("Feedback_MessageName"));
        _count = Theme.Sub("0 / " + FeedbackLogic.MaxMessageLength);
        _message.TextChanged += (_, _) => { _count.Text = _message.Text.Trim().Length + " / " + FeedbackLogic.MaxMessageLength; Refresh(); };
        body.Children.Add(_message);
        body.Children.Add(_count);

        body.Children.Add(Theme.SectionHeader(L.Get("Feedback_EmailHeader")));
        _email = Theme.Input(432);
        _email.PlaceholderText = L.Get("Feedback_EmailPlaceholder");
        System.Windows.Automation.AutomationProperties.SetName(_email, L.Get("Feedback_EmailName"));
        _email.TextChanged += (_, _) => Refresh();
        body.Children.Add(_email);

        _consent = new CheckBox
        {
            Content = new TextBlock
            {
                Text = L.Get("Feedback_Consent"),
                TextWrapping = TextWrapping.Wrap, FontFamily = Theme.Font, FontSize = 14
            },
            Margin = new Thickness(0, 12, 0, 0)
        };
        _consent.Click += (_, _) => Refresh();
        body.Children.Add(_consent);

        body.Children.Add(Theme.SectionHeader(L.Get("Feedback_AttachmentsHeader")));
        _captureBtn = Theme.Btn(L.Get("Feedback_AttachShot"), OnCapture);
        _thumb = new Image { Width = 240, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left };
        System.Windows.Automation.AutomationProperties.SetName(_thumb, L.Get("Feedback_ThumbName"));
        _removeBtn = Theme.Btn(L.Get("Feedback_RemoveShot"), OnRemoveShot);
        _removeBtn.Margin = new Thickness(0, 8, 0, 0);
        _shotPanel = new StackPanel { Visibility = Visibility.Collapsed };
        _shotPanel.Children.Add(new Border
        {
            BorderThickness = new Thickness(1), BorderBrush = Theme.RowSeparator, CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Left, Child = _thumb
        });
        _shotPanel.Children.Add(_removeBtn);
        body.Children.Add(_captureBtn);
        body.Children.Add(_shotPanel);

        _logs = Theme.Check(L.Get("Feedback_AttachLogs"), false);
        _logs.Margin = new Thickness(0, 12, 0, 0);
        body.Children.Add(_logs);
        body.Children.Add(Theme.Sub(L.Get("Feedback_LogsHelp")));
        var viewLogs = Theme.LinkButton(L.Get("Feedback_ViewLog"), OnViewLog);
        viewLogs.HorizontalAlignment = HorizontalAlignment.Left;
        body.Children.Add(viewLogs);

        _status = Theme.Label("", 12, Theme.Warn);
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Margin = new Thickness(0, 12, 0, 0);
        body.Children.Add(_status);

        _progress = new ProgressBar { Height = 4, IsIndeterminate = true, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0) };
        System.Windows.Automation.AutomationProperties.SetName(_progress, L.Get("Feedback_SendingName"));
        body.Children.Add(_progress);

        _cancel = Theme.Btn(L.Get("Feedback_Cancel"), () => _w.Close());
        _cancel.IsCancel = true;
        _send = Theme.Btn(L.Get("Feedback_Send"), OnSend, primary: true);
        _send.IsDefault = true;
        _send.Margin = new Thickness(0);
        body.Children.Add(Dialogs.ButtonRow(_cancel, _send));

        _w.Closing += (_, _) => _cts?.Cancel();
        _w.Loaded += (_, _) => _type.Focus();
        UpdateShot();
        Refresh();
    }

    /// <summary>Ouvre la fenêtre (modale) au-dessus de <paramref name="owner"/> ; null : centrée à l'écran.</summary>
    public static void Open(Window? owner, FeedbackContext context)
    {
        var view = new FeedbackWindow(owner, context);
        view._w.ShowDialog();
    }

    private string? SelectedType => (_type.SelectedItem as ComboBoxItem)?.Tag as string;

    private bool Busy => _sending;

    /// <summary>Active « Envoyer » seulement quand le formulaire est valide ; affiche le motif tant que l'utilisateur écrit.</summary>
    private void Refresh()
    {
        if (_done) return;
        var error = UpdateSendState();
        if (!Busy)
        {
            var typing = _message.Text.Length > 0 || _email.Text.Length > 0 || _consent.IsChecked == true || SelectedType is not null;
            _status.Foreground = Theme.Warn;
            _status.Text = typing && error is not null ? error : "";
        }
    }

    /// <summary>Met « Envoyer » à jour selon la validation ; renvoie le motif du refus, null si le formulaire est valide.</summary>
    private string? UpdateSendState()
    {
        var shotSizes = _shot is null ? Array.Empty<long>() : new long[] { _shot.Length };
        var error = FeedbackLogic.ValidateDraft(SelectedType, _message.Text, _email.Text, _consent.IsChecked == true, shotSizes, 0);
        _send.IsEnabled = !Busy && !_done && error is null;
        return error;
    }

    private void UpdateShot()
    {
        _captureBtn.Visibility = _shot is null ? Visibility.Visible : Visibility.Collapsed;
        _shotPanel.Visibility = _shot is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void OnCapture()
    {
        // la fenêtre devient transparente le temps de la capture : elle n'apparaît pas sur l'image
        var failed = false;
        _captureBtn.IsEnabled = false;
        _w.Opacity = 0;
        try
        {
            await Task.Delay(350);
            _shot = ScreenCapture.CapturePrimaryScreenPng();
            _thumb.Source = Decode(_shot);
        }
        catch (Exception ex)
        {
            Log.Write("Signalement : capture impossible : " + ex.Message);
            failed = true;
        }
        finally
        {
            _w.Opacity = 1;
            _captureBtn.IsEnabled = true;
            UpdateShot();
            Refresh();
        }
        if (failed) _status.Text = L.Get("Feedback_ShotFailed");
    }

    private static BitmapImage Decode(byte[] png)
    {
        var image = new BitmapImage();
        using var ms = new MemoryStream(png);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = ms;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void OnRemoveShot()
    {
        _shot = null;
        _thumb.Source = null;
        UpdateShot();
        Refresh();
    }

    /// <summary>Affiche le texte exact qui partira avec les journaux (lecture seule).</summary>
    private void OnViewLog()
    {
        var text = FeedbackClient.ReadSanitizedLog(_ctx.LogPath);
        var (w, body) = Dialogs.Create(_w, L.Get("Feedback_LogTitle"), 720);
        body.Children.Add(Theme.Sub(L.Get("Feedback_LogIntro")));
        body.Children.Add(new WpfUi.TextBox
        {
            Text = text ?? L.Get("Feedback_NoLog"), IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            Height = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0)
        });
        var close = Theme.Btn(L.Get("Feedback_Close"), () => w.Close(), primary: true);
        close.IsDefault = true;
        close.Margin = new Thickness(0);
        body.Children.Add(Dialogs.ButtonRow(close));
        w.Loaded += (_, _) => close.Focus();
        w.ShowDialog();
    }

    private async void OnSend()
    {
        if (_sending || _done) return;
        var type = SelectedType ?? "";
        var message = _message.Text.Trim();
        var email = _email.Text.Trim();

        // journal relu et nettoyé au moment de l'envoi, seulement si la case est cochée
        byte[]? logBytes = null;
        if (_logs.IsChecked == true)
        {
            var text = FeedbackClient.ReadSanitizedLog(_ctx.LogPath);
            if (text is not null) logBytes = Encoding.UTF8.GetBytes(text);
        }

        var shot = _shot;
        var shotSizes = shot is null ? Array.Empty<long>() : new long[] { shot.Length };
        var error = FeedbackLogic.ValidateDraft(type, message, email, _consent.IsChecked == true, shotSizes, logBytes?.Length ?? 0);
        if (error is not null)
        {
            _status.Text = error;
            return;
        }

        var submission = new FeedbackSubmission(
            type, message, email.Length == 0 ? null : email,
            _ctx.Version, _ctx.Edition, _ctx.Os, _ctx.Arch,
            shot is null ? null : new FeedbackAttachment("capture.png", "image/png", shot),
            logBytes is null ? null : new FeedbackAttachment("selecto-journal.txt", "text/plain; charset=utf-8", logBytes));

        FeedbackOutcome outcome;
        _sending = true;
        SetBusy(true);
        _cts = new CancellationTokenSource();
        try
        {
            outcome = await new FeedbackClient(OnlineHttp.Client, _ctx.Endpoint).SendAsync(submission, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            outcome = new FeedbackOutcome(false, "", L.Get("Feedback_Interrupted"));
        }
        finally
        {
            _sending = false;
            _cts?.Dispose();
            _cts = null;
        }
        SetBusy(false);

        if (outcome.Ok)
        {
            _done = true;
            SetFormEnabled(false);
            _status.Foreground = Theme.StatusOn;
            _status.Text = FeedbackLogic.ThankYou(outcome.Ticket);
            _send.IsEnabled = false;
            _cancel.Content = L.Get("Feedback_Close");
            _cancel.IsCancel = true;
        }
        else
        {
            // échec : le formulaire reste rempli, l'utilisateur peut corriger ou réessayer de lui-même
            UpdateSendState();
            _status.Foreground = Theme.Warn;
            _status.Text = outcome.Error;
        }
    }

    private void SetBusy(bool busy)
    {
        _progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        SetFormEnabled(!busy);
        if (busy)
        {
            _send.IsEnabled = false;
            _status.Foreground = Theme.SubText;
            _status.Text = L.Get("Feedback_SendingStatus");
        }
    }

    private void SetFormEnabled(bool enabled)
    {
        _type.IsEnabled = enabled;
        _message.IsEnabled = enabled;
        _email.IsEnabled = enabled;
        _consent.IsEnabled = enabled;
        _logs.IsEnabled = enabled;
        _captureBtn.IsEnabled = enabled;
        _removeBtn.IsEnabled = enabled;
        _cancel.IsEnabled = enabled || _done;
    }
}
