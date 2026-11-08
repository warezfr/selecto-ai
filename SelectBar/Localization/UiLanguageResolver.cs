using System;

namespace SelectBar.Localization;

/// <summary>
/// Choix de la langue de l'interface (PUR, sans WPF). Réglage « auto » : la langue d'affichage Windows si elle est prise en charge, sinon l'anglais.
/// </summary>
public static class UiLanguageResolver
{
    public static readonly string[] Supported = { "fr", "en", "es", "ar" };

    /// <param name="setting">Réglage UiLanguage : une langue prise en charge, ou « auto ».</param>
    /// <param name="windowsUiCulture">Nom de la culture d'affichage Windows (ex. fr-FR), peut être vide.</param>
    /// <returns>Code à 2 lettres parmi <see cref="Supported"/>.</returns>
    public static string Resolve(string? setting, string? windowsUiCulture)
    {
        var chosen = (setting ?? string.Empty).Trim().ToLowerInvariant();
        if (chosen.Length == 0) chosen = "auto";
        if (Array.IndexOf(Supported, chosen) >= 0) return chosen;
        // Réglage inconnu (fichier modifié à la main, ancienne valeur) : anglais, jamais une langue devinée.
        if (chosen != "auto") return "en";

        var windows = (windowsUiCulture ?? string.Empty).Trim().ToLowerInvariant();
        if (windows.Length < 2) return "en";
        var twoLetters = windows.Substring(0, 2);
        return Array.IndexOf(Supported, twoLetters) >= 0 ? twoLetters : "en";
    }
}
