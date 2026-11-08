namespace SelectBar.Setup.Logic;

public enum SetupEvent
{
    StartInstall, InstallSucceeded, InstallFailed, Cancel, Retry, Finish,
    StartUninstall, UninstallSucceeded, UninstallFailed,
}

/// <summary>Machine à états des écrans : une transition inconnue laisse l'écran inchangé.</summary>
public static class SetupFlow
{
    public static SetupScreen Initial(SetupMode mode) => mode == SetupMode.Uninstall ? SetupScreen.UninstallConfirm : SetupScreen.Welcome;

    public static SetupScreen Next(SetupScreen current, SetupEvent e, SetupMode mode)
    {
        switch (current)
        {
            case SetupScreen.Welcome:
                return e == SetupEvent.StartInstall ? SetupScreen.Progress : current;
            case SetupScreen.Progress:
                return e switch
                {
                    SetupEvent.InstallSucceeded => SetupScreen.Done,
                    SetupEvent.InstallFailed => SetupScreen.Error,
                    SetupEvent.Cancel => SetupScreen.Welcome,
                    _ => current,
                };
            case SetupScreen.Error:
                if (e == SetupEvent.Retry) return mode == SetupMode.Uninstall ? SetupScreen.UninstallProgress : SetupScreen.Progress;
                if (e == SetupEvent.Cancel) return Initial(mode);
                return current;
            case SetupScreen.UninstallConfirm:
                return e == SetupEvent.StartUninstall ? SetupScreen.UninstallProgress : current;
            case SetupScreen.UninstallProgress:
                return e switch
                {
                    SetupEvent.UninstallSucceeded => SetupScreen.UninstallDone,
                    SetupEvent.UninstallFailed => SetupScreen.Error,
                    _ => current,
                };
            default:
                return current; // Done, UninstallDone : terminaux
        }
    }

    /// <summary>Écrans après lesquels la seule action restante est de quitter.</summary>
    public static bool IsTerminal(SetupScreen s) => s is SetupScreen.Done or SetupScreen.UninstallDone;

    /// <summary>L'annulation (avec retour en arrière des fichiers) n'existe que pendant l'installation.</summary>
    public static bool CanCancel(SetupScreen s) => s == SetupScreen.Progress;

    /// <summary>Fermer la fenêtre demande confirmation pendant une opération en cours.</summary>
    public static bool IsBusy(SetupScreen s) => s is SetupScreen.Progress or SetupScreen.UninstallProgress;
}
