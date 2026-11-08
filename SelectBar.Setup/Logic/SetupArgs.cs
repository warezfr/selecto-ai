using System;
using System.Collections.Generic;

namespace SelectBar.Setup.Logic;

public enum SetupMode { Install, Uninstall }

/// <summary>Écrans de l'installateur (et du désinstallateur).</summary>
public enum SetupScreen { Welcome, Progress, Done, Error, UninstallConfirm, UninstallProgress, UninstallDone }

/// <summary>Options de la ligne de commande (mêmes noms que l'installateur Inno pour le mode silencieux).</summary>
public sealed class SetupOptions
{
    public SetupMode Mode { get; set; } = SetupMode.Install;
    public bool Silent { get; set; }
    public string? Dir { get; set; }
    public bool PurgeData { get; set; }
    public bool Force { get; set; }
    public bool DesktopShortcut { get; set; }
    public bool Startup { get; set; }
    /// <summary>Désinstallateur relancé depuis %TEMP% (le dossier d'installation y est donné par --relocated=).</summary>
    public bool Relocated { get; set; }
    public string? RelocatedDir { get; set; }
    public bool Demo { get; set; }
    public SetupScreen? DemoScreen { get; set; }
    public bool Hold { get; set; }
    /// <summary>« light », « dark » ou null (suit Windows).</summary>
    public string? Theme { get; set; }
    /// <summary>« fr », « en » ou null (suit Windows).</summary>
    public string? Lang { get; set; }
    public List<string> Unknown { get; } = new();
}

public static class SetupArgs
{
    public static SetupOptions Parse(IEnumerable<string> args)
    {
        var o = new SetupOptions();
        foreach (var raw in args)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var token = raw.Trim();
            if (token[0] != '/' && token[0] != '-') { o.Unknown.Add(raw); continue; }
            var body = token.TrimStart('/', '-');
            var eq = body.IndexOf('=');
            var key = (eq >= 0 ? body.Substring(0, eq) : body).Trim().ToLowerInvariant();
            var value = eq >= 0 ? Unquote(body.Substring(eq + 1)) : null;
            switch (key)
            {
                case "s": case "silent": case "verysilent": o.Silent = true; break;
                case "uninstall": o.Mode = SetupMode.Uninstall; break;
                case "dir": o.Dir = string.IsNullOrWhiteSpace(value) ? null : value; break;
                case "purgedata": o.PurgeData = true; break;
                case "force": o.Force = true; break;
                case "norestart": case "suppressmsgboxes": case "currentuser": break; // acceptés pour la compatibilité avec Inno
                case "desktopicon": o.DesktopShortcut = true; break;
                case "startup": o.Startup = true; break;
                case "tasks": ApplyTasks(o, value); break;
                case "relocated": o.Relocated = true; o.RelocatedDir = string.IsNullOrWhiteSpace(value) ? null : value; break;
                case "demo": o.Demo = true; break;
                case "screen": o.DemoScreen = ParseScreen(value); if (o.DemoScreen == null) o.Unknown.Add(raw); break;
                case "hold": o.Hold = true; break;
                case "theme": o.Theme = (value is "light" or "dark") ? value : null; break;
                case "lang": o.Lang = Strings.NormalizeLang(value); break;
                default: o.Unknown.Add(raw); break;
            }
        }
        return o;
    }

    public static SetupScreen? ParseScreen(string? name) => (name ?? "").Trim().ToLowerInvariant() switch
    {
        "welcome" => SetupScreen.Welcome,
        "progress" => SetupScreen.Progress,
        "done" => SetupScreen.Done,
        "error" => SetupScreen.Error,
        "uninstall" => SetupScreen.UninstallConfirm,
        "uninstall-progress" => SetupScreen.UninstallProgress,
        "uninstall-done" => SetupScreen.UninstallDone,
        _ => null,
    };

    private static void ApplyTasks(SetupOptions o, string? list)
    {
        if (string.IsNullOrWhiteSpace(list)) return;
        foreach (var t in list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (t.Equals("desktopicon", StringComparison.OrdinalIgnoreCase)) o.DesktopShortcut = true;
            else if (t.Equals("startup", StringComparison.OrdinalIgnoreCase)) o.Startup = true;
        }
    }

    private static string Unquote(string s)
    {
        s = s.Trim();
        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"') s = s.Substring(1, s.Length - 2);
        return s;
    }
}
