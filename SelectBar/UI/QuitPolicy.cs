namespace SelectBar.UI;

/// <summary>
/// Faut-il demander confirmation avant de quitter (logique pure, testée). Pas de confirmation quand Windows s'arrête
/// (SessionEnding : une boîte bloquerait l'arrêt), en mode capture (scripts de captures d'écran), ni pour une fermeture externe.
/// </summary>
public static class QuitPolicy
{
    /// <param name="confirmQuitSetting">Réglage ConfirmQuit (vrai par défaut).</param>
    /// <param name="sessionEnding">Windows s'arrête ou ferme la session.</param>
    /// <param name="captureMode">Lancement avec --capture-mode.</param>
    /// <param name="externalCloseRequest">Fermeture demandée de l'extérieur (WM_CLOSE). Théorique aujourd'hui : la fenêtre
    /// principale est une fenêtre de message cachée (HWND_MESSAGE), qui ne reçoit pas WM_CLOSE de taskkill sans /F ;
    /// l'installateur passe donc directement à taskkill /F. Gardé pour une future fenêtre visible fermable de l'extérieur.</param>
    public static bool ShouldAsk(bool confirmQuitSetting, bool sessionEnding, bool captureMode, bool externalCloseRequest)
        => confirmQuitSetting && !sessionEnding && !captureMode && !externalCloseRequest;
}
