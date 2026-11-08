using System;
using System.Collections.Generic;

namespace SelectBar.Setup.Logic;

public sealed record RegValue(string Name, string? Text, int? DWord);

/// <summary>Valeurs de l'entrée « Applications installées » (HKCU\...\Uninstall\SelectoAI).</summary>
public static class UninstallEntry
{
    public static string KeyPath => InstallPaths.UninstallKeyRoot + "\\" + InstallPaths.UninstallKeyName;

    public static string UninstallString(string dir) => "\"" + InstallPaths.UninstallerPath(dir) + "\" --uninstall";
    public static string QuietUninstallString(string dir) => "\"" + InstallPaths.UninstallerPath(dir) + "\" --uninstall --silent";

    public static IReadOnlyList<RegValue> Build(string displayName, string version, string dir, long installedBytes)
    {
        return new List<RegValue>
        {
            new("DisplayName", displayName, null),
            new("DisplayVersion", version, null),
            new("Publisher", InstallPaths.Publisher, null),
            new("DisplayIcon", InstallPaths.IconPath(dir), null),
            new("InstallLocation", dir, null),
            new("URLInfoAbout", InstallPaths.Website, null),
            new("HelpLink", InstallPaths.Website, null),
            new("UninstallString", UninstallString(dir), null),
            new("QuietUninstallString", QuietUninstallString(dir), null),
            new("EstimatedSize", null, SizeFormat.EstimatedSizeKb(installedBytes)),
            new("NoModify", null, 1),
            new("NoRepair", null, 1),
        };
    }
}
