using System.Windows;
using System.Windows.Controls;
using SelectBar.Localization;

namespace SelectBar.UI;

/// <summary>Confirmation avant de quitter : Quitter (accentué), Annuler (Échap), case « Ne plus demander ».</summary>
public static class QuitDialog
{
    public static (bool quit, bool dontAskAgain) Show(Window? owner)
    {
        var quit = false;
        var (w, body) = Dialogs.Create(owner, L.Get("Quit_Title"), 380);
        body.Children.Add(Theme.Label(L.Get("Quit_Body"), 14));
        var dontAsk = Theme.Check(L.Get("Quit_DontAsk"), false);
        dontAsk.Margin = new Thickness(0, 12, 0, 0);
        body.Children.Add(dontAsk);
        var cancelBtn = Theme.Btn(L.Get("Quit_Cancel"), () => w.Close());
        cancelBtn.IsCancel = true;
        var quitBtn = Theme.Btn(L.Get("Quit_Confirm"), () => { quit = true; w.Close(); }, primary: true);
        quitBtn.IsDefault = true;
        quitBtn.Margin = new Thickness(0);
        body.Children.Add(Dialogs.ButtonRow(cancelBtn, quitBtn));
        w.Loaded += (_, _) => cancelBtn.Focus();
        w.ShowDialog();
        return (quit, dontAsk.IsChecked == true);
    }
}
