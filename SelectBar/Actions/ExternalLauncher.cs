using System;
using System.Collections.Generic;
using System.Linq;

namespace SelectBar.Actions;

public enum LaunchKind { Browser, Explorer, Mail, App }

/// <summary>D'où vient la demande de lancement (toujours un clic de l'utilisateur).</summary>
public enum LaunchSource { Button, Extension, Popup, Settings, Tray }

/// <summary>Qui a le droit de lancer quelque chose d'extérieur (navigateur, Explorateur, messagerie, application).</summary>
public static class LaunchPolicy
{
    public const string PopupFallback = "popup.fallback";
    public const string ExtensionPrefix = "extension:";
    public const string UiPrefix = "ui:";

    private static readonly string[] BuiltInAllowed =
        { BuiltInActions.Search, BuiltInActions.OpenLink, BuiltInActions.Reveal, BuiltInActions.Mail };

    /// <summary>
    /// Actions intégrées autorisées : Rechercher, Ouvrir le lien, Afficher dans l'Explorateur, Écrire à… et le bouton de repli du popup.
    /// Les extensions (origine « extension:... ») et les boutons des réglages (« ui:... ») passent aussi, mais seulement dans un clic.
    /// Copier, Couper, Coller, Orthographe, Dictionnaire, Traduire et IA ne lancent jamais rien.
    /// </summary>
    public static bool MayLaunch(string? origin)
    {
        if (string.IsNullOrEmpty(origin)) return false;
        if (BuiltInAllowed.Contains(origin, StringComparer.OrdinalIgnoreCase)) return true;
        if (string.Equals(origin, PopupFallback, StringComparison.OrdinalIgnoreCase)) return true;
        return origin.StartsWith(ExtensionPrefix, StringComparison.OrdinalIgnoreCase)
               || origin.StartsWith(UiPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Origine d'une action de la barre : l'identifiant d'une action intégrée, ou « extension:id ».</summary>
    public static string OriginOf(ActionDefinition action) => action.BuiltIn ? action.Id : ExtensionPrefix + action.Id;
}

/// <summary>Un lancement demandé (accepté ou refusé), tel qu'il est écrit dans le journal.</summary>
public sealed record LaunchRecord(DateTime Time, string Origin, LaunchSource Source, LaunchKind Kind, string Target, int X, int Y, bool Accepted);

/// <summary>Le vrai lancement (Process.Start côté Windows, simulé dans les tests).</summary>
public interface ILaunchBackend
{
    void Start(LaunchKind kind, string target);
}

/// <summary>Mode capture : le lancement est journalisé mais jamais exécuté.</summary>
public sealed class SimulatedLaunchBackend : ILaunchBackend
{
    private readonly Action<string> _log;
    public SimulatedLaunchBackend(Action<string> log) { _log = log; }
    public void Start(LaunchKind kind, string target) => _log("[capture] lancement simulé : " + target);
}

/// <summary>
/// Point d'entrée UNIQUE de tout lancement externe. Chaque demande est journalisée (heure, origine, type, cible, source, position du pointeur)
/// et refusée si elle ne vient pas d'une action autorisée (LaunchPolicy) ou si aucun clic de l'utilisateur n'est en cours (Scope).
/// </summary>
public sealed class ExternalLauncher
{
    private readonly ILaunchBackend _backend;
    private readonly Action<string> _log;
    private readonly Func<(int X, int Y)> _cursor;
    private readonly Func<DateTime> _clock;
    private readonly List<LaunchRecord> _records = new();
    private readonly object _gate = new();
    private string? _origin;
    private LaunchSource _source;

    public ExternalLauncher(ILaunchBackend backend, Action<string>? log = null, Func<(int X, int Y)>? cursor = null, Func<DateTime>? clock = null)
    {
        _backend = backend;
        _log = log ?? (_ => { });
        _cursor = cursor ?? (() => (0, 0));
        _clock = clock ?? (() => DateTime.Now);
    }

    public IReadOnlyList<LaunchRecord> Records
    {
        get { lock (_gate) return _records.ToList(); }
    }

    /// <summary>Vrai pendant l'exécution d'un clic de l'utilisateur (action de la barre, bouton du popup).</summary>
    public bool InUserAction => _origin != null;

    /// <summary>Ouvre la portée d'un clic : les lancements demandés pendant cette portée ont cette origine.</summary>
    public IDisposable Scope(string origin, LaunchSource source)
    {
        var previous = (Origin: _origin, Source: _source);
        _origin = origin;
        _source = source;
        return new Restore(this, previous.Origin, previous.Source);
    }

    /// <summary>Lancement pendant une portée de clic ; refusé s'il n'y en a aucune.</summary>
    public bool Open(LaunchKind kind, string target) => OpenAs(_origin, _source, kind, target);

    /// <summary>Lancement avec une origine explicite (boutons des réglages, du menu de la zone de notification).</summary>
    public bool OpenAs(string? origin, LaunchSource source, LaunchKind kind, string target)
    {
        var (x, y) = SafeCursor();
        var accepted = !string.IsNullOrEmpty(origin) && LaunchPolicy.MayLaunch(origin) && !string.IsNullOrWhiteSpace(target);
        var record = new LaunchRecord(_clock(), origin ?? "(aucune)", source, kind, target ?? "", x, y, accepted);
        lock (_gate)
        {
            _records.Add(record);
            if (_records.Count > 200) _records.RemoveAt(0);
        }
        _log($"Lancement {(accepted ? "accepté" : "REFUSÉ (hors clic ou action non autorisée)")} : origine={record.Origin} source={source} type={kind} "
             + $"cible={Shorten(target)} pointeur=({x},{y})");
        if (!accepted) return false;
        try
        {
            _backend.Start(kind, target!);
            return true;
        }
        catch (Exception ex)
        {
            _log($"Lancement échoué : origine={record.Origin} cible={Shorten(target)} : {ex.Message}");
            return false;
        }
    }

    private (int X, int Y) SafeCursor()
    {
        try { return _cursor(); }
        catch { return (0, 0); }
    }

    private static string Shorten(string? s)
    {
        var t = (s ?? "").Replace('\r', ' ').Replace('\n', ' ');
        return t.Length <= 300 ? t : t.Substring(0, 300) + "…";
    }

    private sealed class Restore : IDisposable
    {
        private readonly ExternalLauncher _owner;
        private readonly string? _origin;
        private readonly LaunchSource _source;
        private bool _done;

        public Restore(ExternalLauncher owner, string? origin, LaunchSource source) { _owner = owner; _origin = origin; _source = source; }

        public void Dispose()
        {
            if (_done) return;
            _done = true;
            _owner._origin = _origin;
            _owner._source = _source;
        }
    }
}
