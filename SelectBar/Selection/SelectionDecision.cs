using System;
using System.Collections.Generic;

namespace SelectBar.Selection;

public enum UiaOutcome { Text, EmptySelection, NoTextPattern, Timeout, Error }

public enum SelectionSource { Uia, Clipboard, None }

/// <summary>Décision UIA puis repli presse-papiers (logique pure, testable).</summary>
public static class SelectionDecision
{
    /// <summary>Applis (navigateurs, Electron) dont UIA ne reflète pas toujours la sélection : on retombe sur Ctrl+C.</summary>
    public static readonly IReadOnlyCollection<string> AlwaysFallbackExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "chrome.exe", "msedge.exe", "firefox.exe", "brave.exe", "opera.exe", "vivaldi.exe",
        "code.exe", "slack.exe", "discord.exe", "teams.exe", "ms-teams.exe", "notion.exe",
        "obsidian.exe", "spotify.exe", "whatsapp.exe", "signal.exe"
    };

    public static SelectionSource Decide(UiaOutcome outcome, string? exe, IReadOnlyCollection<string>? alwaysFallback = null)
    {
        alwaysFallback ??= AlwaysFallbackExes;
        switch (outcome)
        {
            case UiaOutcome.Text:
                return SelectionSource.Uia;
            case UiaOutcome.EmptySelection:
                // Un vrai contrôle texte natif dit "rien de sélectionné" : on le croit, sauf navigateur/Electron.
                return exe != null && Contains(alwaysFallback, exe) ? SelectionSource.Clipboard : SelectionSource.None;
            default:
                // Pas de TextPattern, timeout ou erreur : toujours retomber sur Ctrl+C.
                return SelectionSource.Clipboard;
        }
    }

    private static bool Contains(IReadOnlyCollection<string> set, string exe)
    {
        foreach (var s in set)
            if (string.Equals(s, exe, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
