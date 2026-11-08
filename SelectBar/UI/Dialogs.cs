using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SelectBar.Localization;
using WpfUi = Wpf.Ui.Controls;

namespace SelectBar.UI;

/// <summary>
/// Boîtes de dialogue et de message Fluent (FluentWindow à fond Mica, barre de titre intégrée, boutons à rayon 4). Elles bloquent
/// comme MessageBox.Show : le code appelant garde sa forme synchrone (aucun changement de comportement).
/// </summary>
internal static class Dialogs
{
    /// <summary>Fenêtre de dialogue modale vide : renvoie la fenêtre et le panneau où poser le contenu.</summary>
    public static (WpfUi.FluentWindow Window, StackPanel Body) Create(Window? owner, string title, double width = 440)
    {
        var w = new WpfUi.FluentWindow
        {
            Title = title, Width = width, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            MinHeight = 0, MinWidth = 0, // FluentWindow impose sinon une hauteur minimale : grand vide sous les boutons
            ShowInTaskbar = false, ExtendsContentIntoTitleBar = true, WindowBackdropType = WpfUi.WindowBackdropType.Mica,
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            FontFamily = Theme.Font
        };
        if (owner != null && owner.IsLoaded) w.Owner = owner;
        var bar = new WpfUi.TitleBar { Title = title, ShowMinimize = false, ShowMaximize = false, CanMaximize = false };
        var body = new StackPanel { Margin = new Thickness(24, 8, 24, 24) };
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(body, 1);
        root.Children.Add(bar);
        root.Children.Add(body);
        w.Content = root;
        w.KeyDown += (_, e) => { if (e.Key == Key.Escape) w.Close(); };
        WindowFactory.Apply(w);
        return (w, body);
    }

    public static StackPanel ButtonRow(params UIElement[] buttons)
    {
        var r = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        foreach (var b in buttons) r.Children.Add(b);
        return r;
    }

    /// <summary>Message d'information ou d'avertissement (un seul bouton OK).</summary>
    public static void Info(Window? owner, string title, string text, string ok = "OK")
    {
        var (w, body) = Create(owner, title);
        body.Children.Add(Theme.Label(text, 14));
        var okBtn = Theme.Btn(ok, () => w.Close(), primary: true);
        okBtn.IsDefault = true;
        okBtn.Margin = new Thickness(0);
        body.Children.Add(ButtonRow(okBtn));
        w.Loaded += (_, _) => okBtn.Focus();
        w.ShowDialog();
    }

    /// <summary>Question à deux réponses (OK/Annuler ou Oui/Non) : renvoie vrai pour la réponse positive.</summary>
    public static bool Ask(Window? owner, string title, string text, string? yes = null, string? no = null)
    {
        var result = false;
        var (w, body) = Create(owner, title);
        body.Children.Add(Theme.Label(text, 14));
        var noBtn = Theme.Btn(no ?? L.Get("Dialog_No"), () => w.Close());
        noBtn.IsCancel = true;
        var yesBtn = Theme.Btn(yes ?? L.Get("Dialog_Yes"), () => { result = true; w.Close(); }, primary: true);
        yesBtn.IsDefault = true;
        yesBtn.Margin = new Thickness(0);
        body.Children.Add(ButtonRow(noBtn, yesBtn));
        w.Loaded += (_, _) => yesBtn.Focus();
        w.ShowDialog();
        return result;
    }

    /// <summary>Réponse à « des réglages ne sont pas enregistrés » à la fermeture.</summary>
    public enum UnsavedChoice { Save, Discard, Cancel }

    /// <summary>Changements non enregistrés : Enregistrer / Ne pas enregistrer / Annuler (Échap = Annuler).</summary>
    public static UnsavedChoice AskUnsaved(Window? owner, string title, string text)
    {
        var result = UnsavedChoice.Cancel;
        var (w, body) = Create(owner, title);
        body.Children.Add(Theme.Label(text, 14));
        var cancelBtn = Theme.Btn(L.Get("Dialog_Cancel"), () => w.Close());
        cancelBtn.IsCancel = true;
        var discardBtn = Theme.Btn(L.Get("Dialog_DontSave"), () => { result = UnsavedChoice.Discard; w.Close(); });
        var saveBtn = Theme.Btn(L.Get("Dialog_Save"), () => { result = UnsavedChoice.Save; w.Close(); }, primary: true);
        saveBtn.IsDefault = true;
        saveBtn.Margin = new Thickness(0);
        body.Children.Add(ButtonRow(cancelBtn, discardBtn, saveBtn));
        w.Loaded += (_, _) => saveBtn.Focus();
        w.ShowDialog();
        return result;
    }
}
