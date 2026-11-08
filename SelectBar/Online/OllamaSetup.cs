using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using SelectBar.Localization;

namespace SelectBar.Online;

// Logique pure de l'installation d'Ollama dans l'application (aucun réseau, aucune interface) : testée sans Windows.

/// <summary>Un fichier d'une release GitHub : nom, adresse de téléchargement, taille et empreinte SHA-256 (hexadécimale minuscule, vide si absente).</summary>
public sealed record OllamaAsset(string Name, string Url, long Size, string Sha256);

public enum DigestCheck { Match, Mismatch, Missing }

public static class OllamaRelease
{
    /// <summary>Source UNIQUE de l'installation : propriétaire et dépôt figés.</summary>
    public const string ApiUrl = "https://api.github.com/repos/ollama/ollama/releases/latest";
    public const string DownloadPrefix = "https://github.com/ollama/ollama/releases/download/";
    public const string AmdName = "ollama-windows-amd64.zip";
    public const string ArmName = "ollama-windows-arm64.zip";
    public static string CorruptMessage => L.Get("Ollama_ErrCorrupt");
    public static string NoDigestMessage => L.Get("Ollama_ErrNoDigest");

    /// <summary>Tous les fichiers de la release (JSON de l'API GitHub), avec empreinte lue dans le champ « digest ».</summary>
    public static IReadOnlyList<OllamaAsset> ParseAssets(string json)
    {
        var list = new List<OllamaAsset>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                return list;
            foreach (var a in assets.EnumerateArray())
            {
                var name = Str(a, "name");
                var url = Str(a, "browser_download_url");
                if (name == null || url == null) continue;
                long size = a.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number && s.TryGetInt64(out var sz) ? sz : 0;
                list.Add(new OllamaAsset(name, url, size, ParseDigest(Str(a, "digest")) ?? ""));
            }
        }
        catch (JsonException) { }
        return list;
    }

    /// <summary>« sha256:ABC... » devient « abc... » ; null si le format n'est pas un SHA-256 de 64 caractères hexadécimaux.</summary>
    public static string? ParseDigest(string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest)) return null;
        var d = digest.Trim();
        const string prefix = "sha256:";
        if (!d.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var hex = d.Substring(prefix.Length).ToLowerInvariant();
        return hex.Length == 64 && hex.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')) ? hex : null;
    }

    public static string AssetNameFor(bool arm64) => arm64 ? ArmName : AmdName;

    /// <summary>Choix par nom exact : jamais les versions -mlx ou -rocm, ni OllamaSetup.exe.</summary>
    public static OllamaAsset? Choose(IReadOnlyList<OllamaAsset> assets, bool arm64)
    {
        var wanted = AssetNameFor(arm64);
        return assets.FirstOrDefault(a => string.Equals(a.Name, wanted, StringComparison.Ordinal));
    }

    /// <summary>Le contenu n'est téléchargé (puis exécuté) que s'il vient du dépôt GitHub figé, en https.</summary>
    public static bool IsTrustedUrl(string? url) =>
        url != null && url.StartsWith(DownloadPrefix, StringComparison.Ordinal) && !url.Contains("..") && Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps
        && u.Host == "github.com" && u.UserInfo.Length == 0;

    /// <summary>Plan complet depuis le JSON de l'API : le fichier à télécharger, ou un message d'erreur en français.</summary>
    public static bool TryPlan(string json, bool arm64, out OllamaAsset? asset, out string? error)
    {
        asset = Choose(ParseAssets(json), arm64);
        error = null;
        if (asset == null) { error = L.Get("Ollama_ErrNoAsset"); return false; }
        if (!IsTrustedUrl(asset.Url)) { asset = null; error = L.Get("Ollama_ErrUntrustedUrl"); return false; }
        if (asset.Sha256.Length == 0) { asset = null; error = NoDigestMessage; return false; }
        if (asset.Size <= 0) { asset = null; error = L.Get("Ollama_ErrNoSize"); return false; }
        return true;
    }

    /// <summary>Compare l'empreinte annoncée (hex) à l'empreinte calculée (hex), sans tenir compte de la casse.</summary>
    public static DigestCheck CheckDigest(string? expectedHex, string? actualHex)
    {
        if (string.IsNullOrWhiteSpace(expectedHex)) return DigestCheck.Missing;
        return string.Equals(expectedHex.Trim(), (actualHex ?? "").Trim(), StringComparison.OrdinalIgnoreCase) ? DigestCheck.Match : DigestCheck.Mismatch;
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

/// <summary>Protection « zip-slip » : une entrée de l'archive ne peut jamais sortir du dossier cible.</summary>
public static class ZipGuard
{
    public static bool IsSafeEntryName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        var n = name.Replace('\\', '/');
        if (n.StartsWith("/", StringComparison.Ordinal)) return false;       // chemin absolu (ou réseau \\serveur)
        if (n.IndexOf(':') >= 0) return false;                               // lecteur (C:), flux alternatif
        if (n.IndexOf('\0') >= 0) return false;
        return n.Split('/').All(seg => seg != "..");
    }

    /// <summary>Chemin complet de l'entrée, ou false si elle sort de <paramref name="root"/>.</summary>
    public static bool TryResolve(string root, string? entryName, out string fullPath)
    {
        fullPath = "";
        if (!IsSafeEntryName(entryName)) return false;
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var combined = Path.GetFullPath(Path.Combine(rootFull, entryName!.Replace('\\', '/')));
        var cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!combined.StartsWith(rootFull, cmp) && !string.Equals(combined + Path.DirectorySeparatorChar, rootFull, cmp)) return false;
        fullPath = combined;
        return true;
    }
}

public static class DiskSpace
{
    /// <summary>Au moins 2 × la taille du fichier du moteur (zip + extraction) plus la taille du modèle.</summary>
    public static long Required(long engineAssetSize, long modelSize) => 2 * Math.Max(0, engineAssetSize) + Math.Max(0, modelSize);

    public static long Missing(long freeBytes, long requiredBytes) => Math.Max(0, requiredBytes - Math.Max(0, freeBytes));

    /// <summary>Null si la place suffit, sinon un message qui dit combien il en manque. <paramref name="engineAssetSize"/> = 0 si le moteur est déjà là.</summary>
    public static string? Check(long freeBytes, long engineAssetSize, long modelSize)
    {
        var missing = Missing(freeBytes, Required(engineAssetSize, modelSize));
        return missing == 0 ? null : L.Format("Ollama_ErrDiskSpace", OllamaParsers.FormatSize(missing));
    }
}

public sealed record ModelChoice(string Id, string Label, string Badge, string Description, long SizeBytes)
{
    public string SizeText => "≈ " + (SizeBytes / 1_000_000_000.0).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + " Go";
}

/// <summary>Catalogue des modèles proposés (tailles réelles du registre Ollama) et recommandation selon la mémoire du PC.</summary>
public static class ModelCatalog
{
    public static ModelChoice Light => new("qwen2.5:1.5b", L.Get("Model_Light"), L.Get("Model_Fast"), L.Get("Model_LightDesc"), 990_000_000);
    public static ModelChoice Balanced => new("qwen2.5:3b", L.Get("Model_Balanced"), L.Get("Model_Versatile"), L.Get("Model_BalancedDesc"), 1_930_000_000);
    public static ModelChoice Performant => new("gemma3:4b", L.Get("Model_Powerful"), L.Get("Model_MostAccurate"), L.Get("Model_PerformantDesc"), 3_350_000_000);
    /// <summary>Option avancée repliée, réservée aux PC de 16 Go de mémoire ou plus.</summary>
    public static ModelChoice Powerful => new("qwen2.5:7b", L.Get("Model_MorePowerful"), L.Get("Model_Advanced"), L.Get("Model_PowerfulDesc"), 4_680_000_000);

    public static IReadOnlyList<ModelChoice> Main => new[] { Light, Balanced, Performant };

    public static string RecommendedBadge => L.Get("Model_RecommendedBadge");
    private const double GiB = 1024.0 * 1024 * 1024;

    public static double ToGib(long ramBytes) => ramBytes / GiB;

    public static ModelChoice Recommend(long ramBytes)
    {
        var gb = ToGib(ramBytes);
        return gb < 6 ? Light : gb < 12 ? Balanced : Performant;
    }

    /// <summary>
    /// Recommandation selon la mémoire ET le processeur : un PC avec moins de 8 Go, 4 cœurs ou moins, ou sans AVX2
    /// reçoit le plus petit modèle. La mémoire est arrondie comme pour « Plus puissant » (un PC « 8 Go » annonce souvent 7,7 Gio).
    /// </summary>
    public static ModelChoice Recommend(long ramBytes, int logicalCores, bool avx2)
    {
        var gb = Math.Round(ToGib(ramBytes));
        if (gb < 8 || logicalCores <= 4 || !avx2) return Light;
        return gb < 12 ? Balanced : Performant;
    }

    /// <summary>Phrase affichée sous le modèle recommandé : vitesse attendue de la première réponse.</summary>
    public static string SpeedHint(ModelChoice choice) => choice.Id switch
    {
        "qwen2.5:1.5b" => L.Get("Model_Speed1"),
        "qwen2.5:3b" => L.Get("Model_Speed2"),
        "gemma3:4b" => L.Get("Model_Speed3"),
        _ => L.Get("Model_SpeedMany")
    };

    /// <summary>« Plus puissant » seulement à partir de 16 Go ; un PC « 16 Go » annonce souvent 15,7 Gio : on arrondit.</summary>
    public static bool AllowsPowerful(long ramBytes) => Math.Round(ToGib(ramBytes)) >= 16;

    public static string FormatRam(long ramBytes) => L.Format("Model_RamInfo", Math.Max(1, (long)Math.Round(ToGib(ramBytes))));

    public static ModelChoice? Find(string? id) =>
        Main.Concat(new[] { Powerful }).FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
}

public sealed record TransferStatus(long Done, long Total, double BytesPerSecond, TimeSpan? Remaining)
{
    public double? Fraction => Total > 0 ? Math.Min(1.0, (double)Done / Total) : null;

    /// <summary>« 35 % — 73 Mo sur 210 Mo — 4,2 Mo/s — 33 s restantes ».</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (Fraction is double f) parts.Add((int)(f * 100) + " %");
        parts.Add(Total > 0 ? L.Format("Ollama_TransferOf", TransferMeter.FormatBytes(Done), TransferMeter.FormatBytes(Total)) : TransferMeter.FormatBytes(Done));
        if (BytesPerSecond > 0) parts.Add(TransferMeter.FormatBytes((long)BytesPerSecond) + "/s");
        if (Remaining is TimeSpan r) parts.Add(L.Format("Ollama_TimeRemaining", TransferMeter.FormatDuration(r)));
        return string.Join(" — ", parts);
    }
}

/// <summary>Calcule vitesse (moyenne sur ~5 s) et temps restant à partir d'échantillons (octets faits, instant en ms). Pur : l'horloge est fournie.</summary>
public sealed class TransferMeter
{
    private const long WindowMs = 5000;
    private readonly Queue<(long T, long Done)> _samples = new();

    public TransferStatus Update(long done, long total, long nowMs)
    {
        _samples.Enqueue((nowMs, done));
        while (_samples.Count > 2 && nowMs - _samples.Peek().T > WindowMs) _samples.Dequeue();
        var first = _samples.Peek();
        var dt = nowMs - first.T;
        var speed = dt > 0 && done >= first.Done ? (done - first.Done) * 1000.0 / dt : 0;
        TimeSpan? remaining = speed > 0 && total > done ? TimeSpan.FromSeconds((total - done) / speed) : null;
        return new TransferStatus(done, total, speed, remaining);
    }

    public static string FormatBytes(long bytes) => bytes <= 0 ? "0 Mo" : OllamaParsers.FormatSize(bytes);

    public static string FormatDuration(TimeSpan t)
    {
        var s = (long)Math.Ceiling(t.TotalSeconds);
        if (s < 60) return Math.Max(1, s) + " s";
        if (s < 3600) return (s / 60) + " min " + (s % 60).ToString("00", CultureInfo.InvariantCulture) + " s";
        return (s / 3600) + " h " + ((s % 3600) / 60).ToString("00", CultureInfo.InvariantCulture) + " min";
    }
}

/// <summary>Dossiers de l'installation privée : tout sous %LOCALAPPDATA%\SelectoAI\ollama.</summary>
public sealed record OllamaPaths(string Root)
{
    public string AppDir => Path.Combine(Root, "app");
    public string ModelsDir => Path.Combine(Root, "models");
    public string DownloadDir => Path.Combine(Root, "download");
    public string ZipPart => Path.Combine(DownloadDir, "ollama.zip.part");
    public string Zip => Path.Combine(DownloadDir, "ollama.zip");
    public string Exe => Path.Combine(AppDir, "ollama.exe");

    public static OllamaPaths Default() =>
        Resolve(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Directory.Exists);

    /// <summary>
    /// %LOCALAPPDATA%\SelectoAI\ollama ; si ce dossier n'existe pas mais que l'ancien (%LOCALAPPDATA%\SelectBar\ollama) existe encore
    /// (déplacement impossible lors de la migration : moteur en cours d'exécution, antivirus), l'ancien chemin est réutilisé.
    /// </summary>
    public static OllamaPaths Resolve(string localAppData, Func<string, bool> directoryExists)
    {
        var current = Path.Combine(localAppData, AppPaths.FolderName, "ollama");
        if (directoryExists(current)) return new(current);
        var legacy = Path.Combine(localAppData, AppPaths.LegacyFolderName, "ollama");
        return new(directoryExists(legacy) ? legacy : current);
    }

    /// <summary>Variables d'environnement du moteur lancé par Selecto AI (en plus de celles du processus).</summary>
    public IReadOnlyDictionary<string, string> ProcessEnvironment() => new Dictionary<string, string>
    {
        ["OLLAMA_MODELS"] = ModelsDir,
        ["OLLAMA_HOST"] = OllamaEngineInfo.Host,
        ["OLLAMA_KEEP_ALIVE"] = LocalAiTiming.KeepAlive
    };
}

public static class OllamaEngineInfo
{
    public const string Host = "127.0.0.1:11434";
    public static int StartTimeoutSeconds => (int)LocalAiTiming.EngineStartTimeout.TotalSeconds;
}

public enum EngineEnsure { Ready, NotInstalled, Failed }

public enum EnginePlan { UseExisting, StartInstalled, NeedInstall }

public static class EngineDecision
{
    /// <summary>Un Ollama répond déjà : on le réutilise, sans rien télécharger. Sinon on démarre le nôtre s'il est installé, sinon il faut l'installer.</summary>
    public static EnginePlan Decide(bool respondsOnPort, bool installedBySelectBar) =>
        respondsOnPort ? EnginePlan.UseExisting : installedBySelectBar ? EnginePlan.StartInstalled : EnginePlan.NeedInstall;
}

public enum AssistantStage { InstallEngine, StartEngine, ChooseModel, Ready }

/// <summary>Étape à afficher et message unique et cohérent ; <see cref="ClearActiveModel"/> : les réglages doivent oublier le modèle (jamais le fournisseur).</summary>
public sealed record AssistantState(AssistantStage Stage, string? Message, bool ClearActiveModel);

public static class AssistantLogic
{
    /// <summary>Déduit l'étape de l'assistant de la détection RÉELLE (jamais des seuls réglages enregistrés).</summary>
    public static AssistantState ComputeAssistantState(bool engineReachable, IReadOnlyCollection<string>? modelsInstalled, string? configuredModel,
        bool installedBySelectBar, bool engineProcessRunning)
    {
        var configured = (configuredModel ?? "").Trim();
        var models = modelsInstalled ?? Array.Empty<string>();
        if (engineReachable)
        {
            if (configured.Length > 0 && models.Contains(configured, StringComparer.OrdinalIgnoreCase))
                return new AssistantState(AssistantStage.Ready, null, false);
            return configured.Length > 0
                ? new AssistantState(AssistantStage.ChooseModel, L.Format("Ollama_ModelNotInstalled", configured), true)
                : new AssistantState(AssistantStage.ChooseModel, null, false);
        }
        if (installedBySelectBar || engineProcessRunning)
            return new AssistantState(AssistantStage.StartEngine, L.Get("Ollama_Starting"), false);
        return new AssistantState(AssistantStage.InstallEngine,
            configured.Length > 0 ? L.Get("Ollama_EngineNotInstalled") : null, configured.Length > 0);
    }
}

public enum StepState { Todo, Active, Done }

/// <summary>Les trois étapes visibles de l'assistant : ① moteur, ② modèle, ③ prêt.</summary>
public static class AssistantSteps
{
    /// <param name="engineReady">Un Ollama répond, ou celui de SelectBar est installé.</param>
    /// <param name="modelReady">Le modèle actif de SelectBar est installé dans Ollama.</param>
    public static StepState[] Compute(bool engineReady, bool modelReady, bool changingModel = false)
    {
        if (!engineReady) return new[] { StepState.Active, StepState.Todo, StepState.Todo };
        if (!modelReady || changingModel) return new[] { StepState.Done, StepState.Active, StepState.Todo };
        return new[] { StepState.Done, StepState.Done, StepState.Done };
    }

    public static string Glyph(StepState s) => s switch { StepState.Done => "✓", StepState.Active => "●", _ => "○" };
}

/// <summary>Messages lisibles pour les échecs d'installation (la solution est dans le message).</summary>
public static class SetupErrors
{
    public static string Friendly(Exception ex) => ex switch
    {
        System.Net.Http.HttpRequestException => L.Get("Ollama_ErrNetwork"),
        TimeoutException => L.Get("Ollama_ErrTimeout"),
        UnauthorizedAccessException => L.Get("Ollama_ErrBlockedFile"),
        System.ComponentModel.Win32Exception => L.Get("Ollama_ErrLaunchBlocked"),
        IOException io when IsDiskFull(io) => L.Get("Ollama_ErrDiskFull"),
        InvalidDataException => L.Get("Ollama_ErrBadArchive"),
        _ => L.Get("Ollama_ErrGeneric")
    };

    private static bool IsDiskFull(IOException io)
    {
        var code = io.HResult & 0xFFFF;
        return code == 112 || code == 39;                     // ERROR_DISK_FULL, ERROR_HANDLE_DISK_FULL
    }
}
