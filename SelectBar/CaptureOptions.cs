using System;
using System.Collections.Generic;
using System.Globalization;

namespace SelectBar;

/// <summary>Onglets de la fenêtre de réglages ouvrables en ligne de commande.</summary>
public enum SettingsTabArg { General, Actions, Ai, About }

/// <summary>
/// Options de ligne de commande réservées aux captures d'écran des développeurs (logique pure, testée) :
/// <c>--capture-mode</c> (ou SELECTO_CAPTURE_MODE=1), <c>--open-settings=&lt;general|actions|ai|about&gt;</c> (anciennes valeurs « général », « ia », « à propos » acceptées),
/// <c>--settings-ia-step=&lt;1|2|3&gt;</c>, <c>--window-pos=&lt;x&gt;,&lt;y&gt;</c>, <c>--open-welcome[=1|2|3]</c>, <c>--open-flyout</c>, <c>--fake-update=&lt;version&gt;</c>, <c>--theme=light|dark</c>,
/// <c>--show-bar=&lt;texte&gt;</c> (+ <c>--show-bar-editable</c>), <c>--open-bar-menu=&lt;id&gt;</c>.
/// Une valeur invalide est ignorée (l'option vaut alors « absente »), jamais une erreur.
/// </summary>
public sealed class CaptureOptions
{
    public const string EnvVariable = "SELECTO_CAPTURE_MODE";

    /// <summary>Options du processus en cours (renseignées par Program.Main ; mode normal par défaut).</summary>
    public static CaptureOptions Current { get; set; } = new();

    public bool CaptureMode { get; init; }
    public SettingsTabArg? OpenSettings { get; init; }
    public int? SettingsIaStep { get; init; }
    public (int X, int Y)? WindowPos { get; init; }
    /// <summary>Écran de la fenêtre Bienvenue à ouvrir (1 à 3), actif seulement avec --capture-mode.</summary>
    public int? OpenWelcome { get; init; }
    /// <summary>Ouvrir le flyout du tray (actif seulement avec --capture-mode).</summary>
    public bool OpenFlyout { get; init; }
    /// <summary>Mise à jour factice exposée au lieu de latest.json (ex. « 9.9.9 ») : badge et bande du flyout pour les captures (actif seulement avec --capture-mode).</summary>
    public string? FakeUpdateVersion { get; init; }

    /// <summary>Adresse de remplacement du latest.json (scénario de mise à jour en VM), http(s) seulement ; actif seulement avec --capture-mode.</summary>
    public string? UpdateUrl { get; init; }

    /// <summary>Version que l'application prétend avoir (scénario de mise à jour en VM : une 3.1.0 fraîchement compilée joue une 3.0.0) ; capture seulement.</summary>
    public string? PretendVersion { get; init; }
    /// <summary>Thème forcé pour la session : true = sombre, false = clair, null = celui de Windows (actif seulement avec --capture-mode).</summary>
    public bool? ForceDark { get; init; }
    /// <summary>Texte lu tel quel pour afficher la barre comme après une sélection (--show-bar, actif seulement avec --capture-mode).</summary>
    public string? ShowBarText { get; init; }
    /// <summary>Le texte de --show-bar est traité comme éditable (actif seulement avec --capture-mode).</summary>
    public bool ShowBarEditable { get; init; }
    /// <summary>Identifiant de l'action dont le sous-menu s'ouvre après la barre (--open-bar-menu, actif seulement avec --capture-mode).</summary>
    public string? OpenBarMenu { get; init; }

    public static CaptureOptions Parse(IEnumerable<string> args, Func<string, string?>? getEnv = null)
    {
        var capture = IsTrue(getEnv?.Invoke(EnvVariable));
        SettingsTabArg? tab = null;
        int? step = null;
        (int, int)? pos = null;
        int? welcome = null;
        var flyout = false;
        bool? dark = null;
        string? fakeUpdate = null;
        string? updateUrl = null;
        string? pretendVersion = null;
        string? showBar = null;
        var showBarEditable = false;
        string? openBarMenu = null;
        string v;
        foreach (var raw in args)
        {
            var a = (raw ?? "").Trim();
            // le texte de --show-bar n'est pas découpé : espaces et guillemets internes conservés
            if (RawValue(raw ?? "", "--show-bar") is { } bar) { showBar = bar; continue; }
            if (a.Equals("--capture-mode", StringComparison.OrdinalIgnoreCase)) { capture = true; continue; }
            if (a.Equals("--show-bar-editable", StringComparison.OrdinalIgnoreCase)) { showBarEditable = true; continue; }
            if (TryValue(a, "--open-bar-menu", out v)) { openBarMenu = string.IsNullOrWhiteSpace(v) ? null : v; continue; }
            if (TryValue(a, "--open-settings", out v)) { tab = ParseTab(v); continue; }
            if (TryValue(a, "--settings-ia-step", out v)) { step = v is "1" or "2" or "3" ? int.Parse(v, CultureInfo.InvariantCulture) : null; continue; }
            if (TryValue(a, "--window-pos", out v)) { pos = ParsePos(v); continue; }
            if (a.Equals("--open-welcome", StringComparison.OrdinalIgnoreCase)) { welcome = 1; continue; }
            if (TryValue(a, "--open-welcome", out v)) { welcome = ParseWelcome(v); continue; }
            if (a.Equals("--open-flyout", StringComparison.OrdinalIgnoreCase)) { flyout = true; continue; }
            if (TryValue(a, "--theme", out v)) { dark = ParseTheme(v); continue; }
            if (TryValue(a, "--fake-update", out v)) { fakeUpdate = ParseFakeUpdate(v); continue; }
            if (TryValue(a, "--pretend-version", out v)) { pretendVersion = Version.TryParse(v, out _) ? v : null; continue; }
            if (TryValue(a, "--update-url", out v)) { updateUrl = Uri.TryCreate(v, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https") ? v : null; continue; }
        }
        // ces options n'ont d'effet qu'en mode capture : sans lui, elles restent « absentes »
        if (!capture) { welcome = null; flyout = false; dark = null; fakeUpdate = null; showBar = null; showBarEditable = false; openBarMenu = null; }
        return new CaptureOptions { CaptureMode = capture, OpenSettings = tab, SettingsIaStep = step, WindowPos = pos, OpenWelcome = welcome, OpenFlyout = flyout, ForceDark = dark, FakeUpdateVersion = fakeUpdate, UpdateUrl = capture ? updateUrl : null, PretendVersion = capture ? pretendVersion : null,
            ShowBarText = showBar, ShowBarEditable = showBarEditable, OpenBarMenu = openBarMenu };
    }

    public static SettingsTabArg? ParseTab(string? value) => (value ?? "").Trim().ToLowerInvariant() switch
    {
        // identifiants techniques (3.1) puis anciennes valeurs françaises, toujours acceptées pour les scripts de la VM
        "general" or "général" => SettingsTabArg.General,
        "actions" => SettingsTabArg.Actions,
        "ai" or "ia" => SettingsTabArg.Ai,
        "about" or "à propos" or "a propos" or "apropos" => SettingsTabArg.About,
        _ => null
    };

    /// <summary>« 1 », « 2 » ou « 3 » (valeur vide : premier écran) ; toute autre valeur est ignorée.</summary>
    public static int? ParseWelcome(string? value) => (value ?? "").Trim() switch { "" or "1" => 1, "2" => 2, "3" => 3, _ => null };

    /// <summary>« dark »/« sombre » = true, « light »/« clair » = false, sinon null.</summary>
    public static bool? ParseTheme(string? value) => (value ?? "").Trim().ToLowerInvariant() switch
    {
        "dark" or "sombre" => true,
        "light" or "clair" => false,
        _ => null
    };

    /// <summary>Numéro de version valide (« 9.9.9 ») ou null : une valeur invalide est ignorée.</summary>
    public static string? ParseFakeUpdate(string? value)
    {
        var v = (value ?? "").Trim();
        return Version.TryParse(v, out _) ? v : null;
    }

    public static (int X, int Y)? ParsePos(string? value)
    {
        var parts = (value ?? "").Split(',');
        if (parts.Length != 2) return null;
        if (!int.TryParse(parts[0].Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var x)) return null;
        if (!int.TryParse(parts[1].Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var y)) return null;
        return (x, y);
    }

    private static bool TryValue(string arg, string name, out string value)
    {
        value = "";
        if (!arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase)) return false;
        value = arg.Substring(name.Length + 1).Trim().Trim('"');
        return true;
    }

    /// <summary>Valeur brute de « name=valeur » (sans découpage ni trim, un seul couple de guillemets extérieurs retiré) ; null si absente ou vide.</summary>
    private static string? RawValue(string raw, string name)
    {
        if (!raw.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase)) return null;
        var v = raw.Substring(name.Length + 1);
        if (v.Length >= 2 && v[0] == '"' && v[^1] == '"') v = v.Substring(1, v.Length - 2);
        return v.Trim().Length == 0 ? null : v;
    }

    private static bool IsTrue(string? v) => v != null && (v.Trim() == "1" || v.Trim().Equals("true", StringComparison.OrdinalIgnoreCase));
}
