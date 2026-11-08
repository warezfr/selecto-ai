using System;
using SelectBar.Localization;

namespace SelectBar.Settings;

/// <summary>Raccourci posé par défaut (schéma 5) : affiche la barre sur la sélection en cours, par exemple après une sélection au clavier.</summary>
public static class HotkeyDefaults
{
    public const string Shortcut = "Ctrl+Shift+Space";
}

/// <summary>Analyse "Ctrl+Alt+P" en modificateurs RegisterHotKey + code de touche virtuelle.</summary>
public static class HotkeyParser
{
    public const uint Alt = 1, Control = 2, Shift = 4, Win = 8, NoRepeat = 0x4000;
    private const uint VkSpace = 0x20, VkEnter = 0x0D;

    public static bool TryParse(string? text, out uint modifiers, out uint vk)
    {
        modifiers = 0; vk = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var key = 0u;
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl": case "control": modifiers |= Control; continue;
                case "alt": modifiers |= Alt; continue;
                case "shift": modifiers |= Shift; continue;
                case "win": case "windows": modifiers |= Win; continue;
            }
            if (key != 0) return false;
            if (raw.Equals("space", StringComparison.OrdinalIgnoreCase)) key = VkSpace;
            else if (raw.Equals("enter", StringComparison.OrdinalIgnoreCase) || raw.Equals("return", StringComparison.OrdinalIgnoreCase)) key = VkEnter;
            else if (raw.Length == 1 && char.IsAsciiLetter(raw[0])) key = char.ToUpperInvariant(raw[0]);
            else if (raw.Length == 1 && char.IsAsciiDigit(raw[0])) key = raw[0];
            else if (raw.Length >= 2 && (raw[0] == 'F' || raw[0] == 'f') && int.TryParse(raw.AsSpan(1), out var n) && n is >= 1 and <= 12)
                key = (uint)(0x70 + n - 1);
            else return false;
        }
        if (key == 0 || modifiers == 0) return false;
        vk = key;
        modifiers |= NoRepeat;
        return true;
    }

    /// <summary>
    /// Nom de touche WPF (Key.ToString()) -> jeton du raccourci : "A".."Z", "D0".."D9" -> "0".."9", "F1".."F12", "Space", "Return" -> "Enter".
    /// Null pour toute autre touche.
    /// </summary>
    public static string? KeyToToken(string? wpfKeyName)
    {
        if (string.IsNullOrEmpty(wpfKeyName)) return null;
        if (wpfKeyName == "Space") return "Space";
        if (wpfKeyName is "Return" or "Enter") return "Enter";
        if (wpfKeyName.Length == 1 && char.IsAsciiLetter(wpfKeyName[0])) return wpfKeyName.ToUpperInvariant();
        if (wpfKeyName.Length == 2 && wpfKeyName[0] == 'D' && char.IsAsciiDigit(wpfKeyName[1])) return wpfKeyName.Substring(1);
        if (wpfKeyName.Length is 2 or 3 && wpfKeyName[0] == 'F' && int.TryParse(wpfKeyName.AsSpan(1), out var n) && n is >= 1 and <= 12)
            return "F" + n;
        return null;
    }

    /// <summary>"Ctrl+Alt+Maj+Win+Touche" dans l'ordre canonique ; null si aucune touche principale ou aucun modificateur.</summary>
    public static string? Format(bool ctrl, bool alt, bool shift, bool win, string? keyToken)
    {
        if (string.IsNullOrEmpty(keyToken) || !(ctrl || alt || shift || win)) return null;
        var parts = new System.Collections.Generic.List<string>();
        if (ctrl) parts.Add("Ctrl");
        if (alt) parts.Add("Alt");
        if (shift) parts.Add("Shift");
        if (win) parts.Add("Win");
        parts.Add(keyToken);
        var text = string.Join("+", parts);
        return TryParse(text, out _, out _) ? text : null;
    }

    /// <summary>Raccourci écrit dans la langue de l'interface : "Ctrl+Shift+Space" devient « Ctrl+Maj+Espace » en français. Texte vide : chaîne vide.</summary>
    public static string Display(string? shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut)) return "";
        var parts = shortcut.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
            parts[i] = parts[i].ToLowerInvariant() switch
            {
                "shift" => L.Get("Key_Shift"),
                "space" => L.Get("Key_Space"),
                "enter" or "return" => L.Get("Key_Enter"),
                _ => parts[i]
            };
        return string.Join("+", parts);
    }
}
