using System;
using System.Linq;

namespace SelectBar.Localization;

/// <summary>Valeurs du sélecteur « Langue de l'interface » (PUR, sans WPF) : « auto » puis les langues prises en charge.</summary>
public static class UiLanguageChoices
{
    /// <summary>Ordre du sélecteur : automatique d'abord, puis fr, en, es, ar.</summary>
    public static readonly string[] Codes = new[] { "auto" }.Concat(UiLanguageResolver.Supported).ToArray();

    /// <summary>Code affiché par le sélecteur pour un réglage donné : « auto » si la valeur n'est pas dans la liste.</summary>
    public static string Normalize(string? setting)
    {
        var code = (setting ?? string.Empty).Trim().ToLowerInvariant();
        return Array.IndexOf(Codes, code) >= 0 ? code : "auto";
    }
}
