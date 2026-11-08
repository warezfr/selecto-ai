using System;
using System.Globalization;

namespace SelectBar.Online;

// Logique pure de l'IA locale adaptée aux PC lents : délais, état de chauffe du modèle, préchauffage, texte du journal.
// Aucun réseau, aucune interface : l'horloge est fournie par l'appelant, donc testable sans Windows.

public static class LocalAiTiming
{
    /// <summary>Délai de démarrage de « ollama serve » : un PC modeste met souvent plus de 20 s.</summary>
    public static readonly TimeSpan EngineStartTimeout = TimeSpan.FromSeconds(90);

    /// <summary>Premier appel (le modèle se charge en mémoire, sans GPU cela peut durer 1 à 2 minutes) et préchauffage.</summary>
    public static readonly TimeSpan ColdChatTimeout = TimeSpan.FromSeconds(180);

    /// <summary>Appel quand le modèle est déjà chargé en mémoire.</summary>
    public static readonly TimeSpan WarmChatTimeout = TimeSpan.FromSeconds(60);

    /// <summary>Durée pendant laquelle Ollama garde le modèle en mémoire (OLLAMA_KEEP_ALIVE et préchauffage).</summary>
    public const string KeepAlive = "30m";

    /// <summary>Au-delà de ce délai sans appel, le modèle est considéré comme déchargé (état Cold).</summary>
    public static readonly TimeSpan IdleLimit = TimeSpan.FromMinutes(30);

    public static TimeSpan ChatTimeout(bool warm) => warm ? WarmChatTimeout : ColdChatTimeout;

    /// <summary>Durée lisible en français : « 12,4 s ».</summary>
    public static string FormatDuration(TimeSpan t)
    {
        var seconds = Math.Max(0, t.TotalSeconds);
        return seconds.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + " s";
    }

    /// <summary>Ligne du journal au démarrage : « IA locale : RAM 8,0 Go, 4 cœurs logiques, AVX2 oui, modèle qwen2.5:1.5b ».</summary>
    public static string StartupSummary(double ramGib, int logicalCores, bool avx2, string model) =>
        "IA locale : RAM " + ramGib.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + " Go, " + logicalCores
        + " cœurs logiques, AVX2 " + (avx2 ? "oui" : "non") + ", modèle " + (string.IsNullOrWhiteSpace(model) ? "aucun" : model);

    /// <summary>État de chauffe partagé par toute l'application (un seul moteur Ollama).</summary>
    public static ModelWarmTracker Warmth { get; } = new();
}

/// <summary>Où en est le modèle actif : pas chargé, en cours de chargement, prêt en mémoire.</summary>
public enum ModelWarmState { Cold, Warming, Warm }

/// <summary>
/// Machine d'états du modèle : Cold -> Warming (premier appel ou préchauffage) -> Warm ; retour à Cold après
/// <see cref="LocalAiTiming.IdleLimit"/> sans appel, ou si un chargement échoue. L'horloge est passée en paramètre.
/// </summary>
public sealed class ModelWarmTracker
{
    private readonly object _gate = new();
    private ModelWarmState _state = ModelWarmState.Cold;
    private string _model = "";
    private DateTime _lastUseUtc = DateTime.MinValue;

    /// <summary>État pour ce modèle à cet instant. Un autre modèle que celui chargé est toujours Cold.</summary>
    public ModelWarmState State(string model, DateTime nowUtc)
    {
        lock (_gate)
        {
            if (!string.Equals(_model, model ?? "", StringComparison.OrdinalIgnoreCase)) return ModelWarmState.Cold;
            if (_state == ModelWarmState.Warm && nowUtc - _lastUseUtc > LocalAiTiming.IdleLimit) _state = ModelWarmState.Cold;
            return _state;
        }
    }

    public void BeginWarming(string model)
    {
        lock (_gate) { _model = model ?? ""; _state = ModelWarmState.Warming; }
    }

    /// <summary>Un appel a réussi : le modèle est en mémoire, la minuterie d'inactivité repart.</summary>
    public void MarkWarm(string model, DateTime nowUtc)
    {
        lock (_gate) { _model = model ?? ""; _state = ModelWarmState.Warm; _lastUseUtc = nowUtc; }
    }

    /// <summary>Le chargement a échoué (délai, erreur) : on repart de Cold, sans toucher à un modèle déjà prêt.</summary>
    public void Failed(string model)
    {
        lock (_gate)
        {
            if (_state == ModelWarmState.Warming && string.Equals(_model, model ?? "", StringComparison.OrdinalIgnoreCase)) _state = ModelWarmState.Cold;
        }
    }
}

/// <summary>Décide si le préchauffage au démarrage doit avoir lieu.</summary>
public static class WarmupPlan
{
    /// <param name="preferenceOn">Réglage « Préparer l'IA au démarrage ».</param>
    /// <param name="providerIsLocal">Le fournisseur choisi est l'IA locale (Ollama).</param>
    /// <param name="engineInstalled">Le moteur installé par Selecto AI est présent.</param>
    public static bool ShouldWarm(bool preferenceOn, bool providerIsLocal, bool engineInstalled) =>
        preferenceOn && providerIsLocal && engineInstalled;
}

/// <summary>
/// Politique du préchauffage au démarrage : attendre que le moteur réponde (sondage de GET /api/tags), puis jusqu'à
/// 3 essais espacés de 1 s et 2 s quand la connexion échoue (Ollama peut ouvrir son port après SelectBar).
/// </summary>
public static class WarmupRetryPolicy
{
    /// <summary>Délai maximal d'attente que GET /api/tags réponde avant d'abandonner le préchauffage.</summary>
    public static readonly TimeSpan ReadyDeadline = TimeSpan.FromSeconds(20);

    /// <summary>Intervalle entre deux sondages de GET /api/tags.</summary>
    public static readonly TimeSpan ReadyPollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>Délai d'un sondage isolé.</summary>
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Nombre total d'essais du préchauffage (le premier compris).</summary>
    public const int MaxAttempts = 3;

    /// <summary>Après l'essai <paramref name="failedAttempt"/> (numéro à partir de 1), faut-il recommencer ?</summary>
    public static bool ShouldRetry(int failedAttempt) => failedAttempt >= 1 && failedAttempt < MaxAttempts;

    /// <summary>Attente avant le nouvel essai : 1 s après le 1er échec, 2 s après le 2e, 4 s au-delà (plafonnée).</summary>
    public static TimeSpan DelayAfterFailure(int failedAttempt)
    {
        var n = Math.Clamp(failedAttempt, 1, 3);
        return TimeSpan.FromSeconds(1 << (n - 1));
    }
}
