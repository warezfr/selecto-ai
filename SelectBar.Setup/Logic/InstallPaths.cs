using System;
using System.Collections.Generic;

namespace SelectBar.Setup.Logic;

public enum DirCheck { Ok, Empty, NotAbsolute, InvalidChars, DriveRoot, TooLong, TooShallow }

public sealed record Shortcut(string LinkPath, string Target, string? Arguments, string IconPath, string Description);

/// <summary>Chemins et constantes de l'installation. Tout est calculé avec des « \ » pour que les tests tournent aussi hors Windows.</summary>
public static class InstallPaths
{
    public const string AppFolderName = "Selecto AI";
    public const string ExeName = "SelectoAI.exe";
    public const string IconName = "SelectoAI.ico";
    public const string UninstallerName = "Uninstall.exe";
    public const string Publisher = "EL MAHDI ZINEDDINE";
    public const string Website = "https://selecto.span.observer";
    public const string MutexName = @"Local\SelectoAI.SingleInstance";
    public const string SetupMutexName = @"Local\SelectoAI.Setup";
    public const string DataFolderName = "SelectoAI";
    public const string DataRegistryKey = @"Software\SelectoAI";
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string UninstallKeyRoot = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
    /// <summary>Entrée « Applications installées » de ce Setup.</summary>
    public const string UninstallKeyName = "SelectoAI";
    /// <summary>Entrée de l'installateur Inno (même application) : remplacée quand le Setup s'installe par-dessus.</summary>
    public const string InnoUninstallKeyName = "{B6F2A8D4-5C3E-4F71-9A0B-7E1D2C4F8A63}_is1";
    public const string LogFileName = "SelectoAI-Setup.log";

    public const string DisplayName = "Selecto AI";

    public static string Join(string a, string b) => a.TrimEnd('\\', '/') + "\\" + b.TrimStart('\\', '/');

    public static string DefaultInstallDir(string localAppData) => Join(Join(localAppData, "Programs"), AppFolderName);

    public static string ExePath(string dir) => Join(dir, ExeName);
    public static string IconPath(string dir) => Join(dir, IconName);
    public static string UninstallerPath(string dir) => Join(dir, UninstallerName);

    public static string RoamingDataDir(string appData) => Join(appData, DataFolderName);
    public static string LocalDataDir(string localAppData) => Join(localAppData, DataFolderName);

    /// <summary>Retire guillemets, espaces et « / », supprime le « \ » final (sauf racine de lecteur).</summary>
    public static string NormalizeDir(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) return "";
        var d = dir.Trim().Trim('"').Trim().Replace('/', '\\');
        while (d.Length > 3 && d.EndsWith("\\", StringComparison.Ordinal)) d = d.Substring(0, d.Length - 1);
        if (d.Length == 2 && d[1] == ':') d += "\\";
        return d;
    }

    public static DirCheck ValidateDir(string? dir)
    {
        var d = NormalizeDir(dir);
        if (d.Length == 0) return DirCheck.Empty;
        if (d.Length > 200) return DirCheck.TooLong;
        if (d.Length < 3 || !((d[0] >= 'A' && d[0] <= 'Z') || (d[0] >= 'a' && d[0] <= 'z')) || d[1] != ':' || d[2] != '\\') return DirCheck.NotAbsolute;
        if (d.Length == 3) return DirCheck.DriveRoot;
        foreach (var c in d.Substring(2))
            if (c < ' ' || c is '<' or '>' or '"' or '|' or '?' or '*' or ':') return DirCheck.InvalidChars;
        foreach (var segment in d.Substring(3).Split('\\'))
            if (segment.Length == 0 || segment == "." || segment == ".." || segment.EndsWith(" ", StringComparison.Ordinal) || segment.EndsWith(".", StringComparison.Ordinal)) return DirCheck.InvalidChars;
        return DirCheck.Ok;
    }

    /// <summary>
    /// Garde-fou avant toute suppression récursive : au moins deux niveaux sous le lecteur et jamais sous le dossier Windows
    /// ni à la racine d'un profil utilisateur (la valeur InstallLocation vient d'une clé HKCU modifiable).
    /// </summary>
    public static bool IsSafeInstallDir(string? dir)
    {
        var d = NormalizeDir(dir);
        if (ValidateDir(d) != DirCheck.Ok) return false;
        var segments = d.Substring(3).Split('\\');
        if (segments.Length < 2) return false;
        var first = segments[0].ToLowerInvariant();
        if (first == "windows") return false;
        if (segments.Length == 2 && first == "users") return false; // un profil utilisateur entier
        return true;
    }

    /// <summary>Choix du dossier : demandé (valide) > installation existante (valide) > défaut.</summary>
    public static string ChooseInstallDir(string? requested, string? existingLocation, string localAppData)
    {
        var r = NormalizeDir(requested);
        if (ValidateDir(r) == DirCheck.Ok) return r;
        var e = NormalizeDir(existingLocation);
        if (ValidateDir(e) == DirCheck.Ok) return e;
        return DefaultInstallDir(localAppData);
    }

    /// <summary>Vrai si <paramref name="path"/> est situé sous <paramref name="dir"/> (insensible à la casse).</summary>
    public static bool IsUnderDir(string? path, string? dir)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(dir)) return false;
        var prefix = NormalizeDir(dir).TrimEnd('\\') + "\\";
        return NormalizeDir(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Entrée de zip → chemin relatif sûr (séparateurs \, pas de « .. », pas de racine), ou null si à ignorer ou dangereuse.</summary>
    public static string? SafeRelativePath(string entryName)
    {
        if (string.IsNullOrEmpty(entryName)) return null;
        var n = entryName.Replace('/', '\\');
        if (n.EndsWith("\\", StringComparison.Ordinal)) return null; // dossier
        if (n.StartsWith("\\", StringComparison.Ordinal) || (n.Length > 1 && n[1] == ':')) return null;
        foreach (var part in n.Split('\\'))
            if (part == ".." || part == "." || part.Length == 0) return null;
        if (n.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)) return null;
        return n;
    }

    /// <summary>Raccourcis : groupe du Menu Démarrer (application + désinstallation) et, si demandé, le Bureau.</summary>
    public static IReadOnlyList<Shortcut> PlanShortcuts(string dir, string startMenuPrograms, string desktop, string displayName, bool desktopShortcut, string uninstallLabel)
    {
        var group = Join(startMenuPrograms, AppFolderName);
        var list = new List<Shortcut>
        {
            new(Join(group, displayName + ".lnk"), ExePath(dir), null, IconPath(dir), displayName),
            new(Join(group, uninstallLabel + ".lnk"), UninstallerPath(dir), "--uninstall", IconPath(dir), uninstallLabel),
        };
        if (desktopShortcut) list.Add(new(Join(desktop, displayName + ".lnk"), ExePath(dir), null, IconPath(dir), displayName));
        return list;
    }

    /// <summary>Tous les emplacements de raccourcis connus (pour la désinstallation) ; « Selecto AI Pro » : anciens raccourcis des versions 3.x.</summary>
    public static IReadOnlyList<string> AllShortcutPaths(string startMenuPrograms, string desktop, string uninstallLabelFr, string uninstallLabelEn)
    {
        var group = Join(startMenuPrograms, AppFolderName);
        return new[]
        {
            Join(group, "Selecto AI.lnk"), Join(group, "Selecto AI Pro.lnk"),
            Join(group, uninstallLabelFr + ".lnk"), Join(group, uninstallLabelEn + ".lnk"),
            Join(desktop, "Selecto AI.lnk"), Join(desktop, "Selecto AI Pro.lnk"),
        };
    }

    public static string StartMenuGroup(string startMenuPrograms) => Join(startMenuPrograms, AppFolderName);
}
