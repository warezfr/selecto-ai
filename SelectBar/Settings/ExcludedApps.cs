using System;
using System.Collections.Generic;
using System.Linq;

namespace SelectBar.Settings;

/// <summary>Liste d'exclusion par nom d'exe (insensible à la casse, ".exe" optionnel).</summary>
public sealed class ExcludedApps
{
    private readonly HashSet<string> _set = new(StringComparer.OrdinalIgnoreCase);

    public static readonly string[] Defaults =
        { "cmd.exe", "powershell.exe", "pwsh.exe", "WindowsTerminal.exe", "conhost.exe", "mintty.exe" };

    public ExcludedApps(IEnumerable<string>? items = null)
    {
        if (items != null) foreach (var i in items) Add(i);
    }

    public static string Normalize(string name)
    {
        var n = name.Trim().Trim('"');
        var cut = n.LastIndexOfAny(new[] { '\\', '/' });
        if (cut >= 0) n = n.Substring(cut + 1);
        n = n.Trim().ToLowerInvariant();
        if (n.Length > 0 && !n.EndsWith(".exe", StringComparison.Ordinal)) n += ".exe";
        return n;
    }

    public bool Add(string name)
    {
        var n = Normalize(name);
        return n.Length > 4 && _set.Add(n);
    }

    public bool Remove(string name) => _set.Remove(Normalize(name));
    public bool Contains(string? exe) => !string.IsNullOrWhiteSpace(exe) && _set.Contains(Normalize(exe));
    public IReadOnlyList<string> Items => _set.OrderBy(x => x, StringComparer.Ordinal).ToList();
}
