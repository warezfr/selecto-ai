namespace SelectBar.Settings;

/// <summary>
/// Migration des réglages au chargement (schéma 3 : actions en ligne activées par défaut ; schéma 4 : aucune conversion ; schéma 5 : raccourci par défaut pour qui n'en a pas).
/// Logique pure, sans fichier ni interface : testée dans SettingsMigrationTests.
/// </summary>
public static class SettingsMigration
{
    /// <param name="loaded">Réglages lus (ou par défaut).</param>
    /// <param name="fileExisted">Le fichier settings.json existait avant le chargement.</param>
    /// <returns>Réglages à utiliser, et vrai si les actions en ligne viennent d'être activées par la migration.</returns>
    public static (AppSettings Settings, bool JustEnabled) Upgrade(AppSettings loaded, bool fileExisted)
    {
        // nouvelle installation : actions en ligne activées sans annonce, raccourci par défaut
        if (!fileExisted)
        {
            loaded.OnlineActionsEnabled = true;
            EnsureShortcut(loaded);
            loaded.SettingsSchema = AppSettings.CurrentSchema;
            return (loaded, false);
        }

        // déjà au schéma courant : les choix de l'utilisateur (y compris « Aucun » raccourci) sont respectés
        if (loaded.SettingsSchema >= AppSettings.CurrentSchema) return (loaded, false);

        // schéma 3 ou 4 -> 5 : seul le raccourci est posé, et seulement s'il manque
        if (loaded.SettingsSchema >= 3)
        {
            EnsureShortcut(loaded);
            loaded.SettingsSchema = AppSettings.CurrentSchema;
            return (loaded, false);
        }

        // v2 ou plus ancien : actions en ligne activées une seule fois, avec une annonce
        loaded.OnlineActionsEnabled = true;
        EnsureShortcut(loaded);
        loaded.SettingsSchema = AppSettings.CurrentSchema;
        return (loaded, true);
    }

    /// <summary>Pose le raccourci par défaut quand il est absent (désactivé, « Aucun ») ou vide ; jamais sur un raccourci déjà choisi.</summary>
    private static void EnsureShortcut(AppSettings s)
    {
        if (s.HotkeyEnabled && !string.IsNullOrWhiteSpace(s.Hotkey)) return;
        s.Hotkey = HotkeyDefaults.Shortcut;
        s.HotkeyEnabled = true;
    }
}
