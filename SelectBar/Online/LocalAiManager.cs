using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SelectBar.Localization;

namespace SelectBar.Online;

public enum LocalAiPhase { Idle, FetchingInfo, Downloading, Verifying, Extracting, Starting, PullingModel, Deleting }

/// <summary>Photographie de la situation locale : un Ollama répond-il, celui de SelectBar est-il installé, quels modèles sont là.</summary>
public sealed record LocalAiStatus(bool EngineRunning, bool EngineInstalled, bool OwnedBySelectBar, IReadOnlyList<OllamaModel> Models);

/// <summary>
/// Installation et pilotage d'Ollama dans l'application, sans administrateur : téléchargement de la version portable officielle
/// (GitHub figé, SHA-256 vérifié), extraction sûre, démarrage caché à la demande, téléchargement des modèles, désinstallation.
/// Les tâches vivent ici (pas dans la fenêtre de réglages) : elles continuent si la fenêtre est fermée. Une seule à la fois.
/// Tout est exécuté hors du thread d'interface ; <see cref="Changed"/> peut être levé depuis n'importe quel thread.
/// </summary>
public sealed class LocalAiManager
{
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);

    private readonly HttpClient _http;
    private readonly OllamaClient _client;
    private readonly object _lock = new();
    private readonly SemaphoreSlim _engineLock = new(1, 1);
    private Process? _process;
    private CancellationTokenSource? _cts;

    public LocalAiManager(HttpClient http, OllamaClient client, OllamaPaths? paths = null)
    {
        _http = http;
        _client = client;
        Paths = paths ?? OllamaPaths.Default();
    }

    public OllamaPaths Paths { get; }

    // -- état visible (lu par l'interface)

    public LocalAiPhase Phase { get; private set; } = LocalAiPhase.Idle;
    public bool Busy => Phase != LocalAiPhase.Idle;
    /// <summary>Ce qui se passe, en une phrase (« Téléchargement du moteur IA… »).</summary>
    public string Activity { get; private set; } = "";
    public TransferStatus? Progress { get; private set; }
    /// <summary>Dernier résultat à afficher (succès ou erreur), jusqu'à la prochaine action.</summary>
    public string? Message { get; private set; }
    public bool MessageIsError { get; private set; }
    public string? PullingModel { get; private set; }

    public event Action? Changed;

    /// <summary>Appelé quand un modèle vient d'être téléchargé entièrement (thread d'arrière-plan) : l'application le choisit comme modèle actif.</summary>
    public Action<string>? ModelReady { get; set; }
    /// <summary>Appelé quand le modèle actif vient d'être supprimé (thread d'arrière-plan).</summary>
    public Action<string>? ModelDeleted { get; set; }

    public bool IsEngineInstalled => File.Exists(Paths.Exe);

    public static bool IsArm64 => RuntimeInformation.OSArchitecture == Architecture.Arm64;

    /// <summary>Taille annoncée sur le bouton, connue d'avance (la vraie taille est relue sur GitHub au clic).</summary>
    public static long ApproxEngineBytes(bool arm64) => arm64 ? 210_000_000 : 1_470_000_000;

    /// <summary>Mémoire physique totale (GlobalMemoryStatusEx) ; repli sur le GC si l'appel échoue.</summary>
    public static long TotalMemoryBytes()
    {
        try
        {
            var status = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (GlobalMemoryStatusEx(ref status) && status.ullTotalPhys > 0) return (long)status.ullTotalPhys;
        }
        catch { }
        try { return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes; } catch { return 0; }
    }

    /// <summary>Nombre de cœurs logiques du PC (1 au minimum).</summary>
    public static int LogicalCores() => Math.Max(1, Environment.ProcessorCount);

    /// <summary>AVX2 présent (ou processeur non Intel/AMD, où la question ne se pose pas). Sans AVX2, Ollama est très lent.</summary>
    public static bool HasAvx2()
    {
        try
        {
            var x86 = RuntimeInformation.ProcessArchitecture is Architecture.X86 or Architecture.X64;
            return !x86 || System.Runtime.Intrinsics.X86.Avx2.IsSupported;
        }
        catch { return false; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    /// <summary>
    /// Préchauffage : démarre le moteur si besoin, puis charge le modèle en mémoire (keep_alive 30 min).
    /// Tâche de fond : rien ne remonte à l'interface, les erreurs sont seulement journalisées.
    /// </summary>
    public async Task WarmUpAsync(string model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model)) return;
        var sw = Stopwatch.StartNew();
        try
        {
            var ensure = await EnsureRunningAsync(ct).ConfigureAwait(false);
            if (ensure != EngineEnsure.Ready) { OnlineLog.Write("Préchauffage de l'IA locale ignoré (moteur : " + ensure + ")"); return; }
            if (!await WaitForApiAsync(ct).ConfigureAwait(false))
            {
                OnlineLog.Write("Préchauffage de l'IA locale ignoré : le moteur ne répond pas après " + LocalAiTiming.FormatDuration(WarmupRetryPolicy.ReadyDeadline));
                return;
            }
            var json = JsonSerializer.Serialize(new { model, prompt = "", stream = false, keep_alive = LocalAiTiming.KeepAlive });
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, "http://" + OllamaEngineInfo.Host + "/api/generate")
                    { Content = new StringContent(json, Encoding.UTF8, "application/json") };
                    LocalAiTiming.Warmth.BeginWarming(model);
                    var (status, _) = await OnlineHttp.SendAsync(_http, req, LocalAiTiming.ColdChatTimeout, ct).ConfigureAwait(false);
                    if ((int)status >= 400)
                    {
                        LocalAiTiming.Warmth.Failed(model);
                        OnlineLog.Write("Préchauffage : réponse HTTP " + (int)status + " (modèle " + model + ")");
                        return;
                    }
                    LocalAiTiming.Warmth.MarkWarm(model, DateTime.UtcNow);
                    OnlineLog.Write("préchauffage terminé en " + LocalAiTiming.FormatDuration(sw.Elapsed) + " (modèle " + model + ", essai " + attempt + ")");
                    return;
                }
                catch (HttpRequestException ex) when (WarmupRetryPolicy.ShouldRetry(attempt))
                {
                    LocalAiTiming.Warmth.Failed(model);
                    var delay = WarmupRetryPolicy.DelayAfterFailure(attempt);
                    OnlineLog.Write("Préchauffage : essai " + attempt + " échoué (" + ex.Message + "), nouvel essai dans " + LocalAiTiming.FormatDuration(delay));
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            LocalAiTiming.Warmth.Failed(model);
        }
        catch (Exception ex)
        {
            LocalAiTiming.Warmth.Failed(model);
            OnlineLog.Write("Préchauffage de l'IA locale : " + ex.Message);
        }
    }

    /// <summary>Sonde GET /api/tags jusqu'à une réponse HTTP (moteur prêt) ou l'échéance de 20 s ; annulable.</summary>
    private async Task<bool> WaitForApiAsync(CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + WarmupRetryPolicy.ReadyDeadline;
        while (true)
        {
            try
            {
                using var probe = new HttpRequestMessage(HttpMethod.Get, "http://" + OllamaEngineInfo.Host + "/api/tags");
                var (status, _) = await OnlineHttp.SendAsync(_http, probe, WarmupRetryPolicy.ProbeTimeout, ct).ConfigureAwait(false);
                if ((int)status < 500) return true;
            }
            catch (Exception) when (!ct.IsCancellationRequested) { /* pas encore prêt : on réessaie */ }
            if (DateTime.UtcNow >= deadline) return false;
            await Task.Delay(WarmupRetryPolicy.ReadyPollInterval, ct).ConfigureAwait(false);
        }
    }

    private void Raise() { try { Changed?.Invoke(); } catch (Exception ex) { OnlineLog.Write("LocalAi.Changed : " + ex.Message); } }

    private void Set(LocalAiPhase phase, string activity, TransferStatus? progress = null)
    {
        Phase = phase; Activity = activity; Progress = progress;
        Raise();
    }

    private void Finish(string? message, bool isError)
    {
        lock (_lock) { Phase = LocalAiPhase.Idle; Activity = ""; Progress = null; PullingModel = null; _cts = null; }
        Message = message; MessageIsError = isError;
        Raise();
    }

    public void ClearMessage() { Message = null; MessageIsError = false; }

    /// <summary>Réserve la tâche unique ; false si une autre est en cours.</summary>
    private bool TryBegin(out CancellationToken ct)
    {
        lock (_lock)
        {
            if (Phase != LocalAiPhase.Idle) { ct = CancellationToken.None; return false; }
            Phase = LocalAiPhase.FetchingInfo;
            _cts = new CancellationTokenSource();
            ct = _cts.Token;
            Message = null; MessageIsError = false;
            return true;
        }
    }

    public void Cancel()
    {
        CancellationTokenSource? c;
        lock (_lock) c = _cts;
        try { c?.Cancel(); } catch (ObjectDisposedException) { }
    }

    // -- état du moteur

    public async Task<LocalAiStatus> RefreshAsync(CancellationToken ct)
    {
        var st = await _client.DetectAsync(ct).ConfigureAwait(false);
        var running = st.Status != OllamaStatus.Absent;
        return new LocalAiStatus(running, IsEngineInstalled, OwnedAndAlive, st.Models);
    }

    private bool OwnedAndAlive { get { try { return _process != null && !_process.HasExited; } catch { return false; } } }

    /// <summary>
    /// Garantit qu'un Ollama répond : réutilise celui de l'utilisateur, sinon démarre celui de SelectBar (caché) s'il est installé.
    /// Appelé à la première action IA et à l'ouverture de l'assistant.
    /// </summary>
    public async Task<EngineEnsure> EnsureRunningAsync(CancellationToken ct)
    {
        await _engineLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var responds = (await _client.DetectAsync(ct).ConfigureAwait(false)).Status != OllamaStatus.Absent;
            switch (EngineDecision.Decide(responds, IsEngineInstalled))
            {
                case EnginePlan.UseExisting: return EngineEnsure.Ready;
                case EnginePlan.NeedInstall: return EngineEnsure.NotInstalled;
            }
            var wasIdle = Phase == LocalAiPhase.Idle;
            // « Annuler » (Cancel) doit aussi arrêter ce démarrage : il passe donc par _cts, comme les autres tâches
            CancellationTokenSource? owned = null;
            var engineCt = ct;
            if (wasIdle)
            {
                owned = CancellationTokenSource.CreateLinkedTokenSource(ct);
                lock (_lock) _cts = owned;
                engineCt = owned.Token;
                Set(LocalAiPhase.Starting, L.Get("Online_LocalStarting"));
            }
            try { return await StartEngineAsync(engineCt).ConfigureAwait(false) ? EngineEnsure.Ready : EngineEnsure.Failed; }
            finally
            {
                if (wasIdle && Phase == LocalAiPhase.Starting) Finish(Message, MessageIsError);
                owned?.Dispose();
            }
        }
        finally { _engineLock.Release(); }
    }

    private async Task<bool> StartEngineAsync(CancellationToken ct)
    {
        try
        {
            KillOwned();
            Directory.CreateDirectory(Paths.ModelsDir);
            var psi = new ProcessStartInfo(Paths.Exe, "serve")
            {
                WorkingDirectory = Paths.AppDir, CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden
            };
            foreach (var kv in Paths.ProcessEnvironment()) psi.Environment[kv.Key] = kv.Value;
            _process = Process.Start(psi);
            if (_process == null) return false;
            var sw = Stopwatch.StartNew();
            var deadline = DateTime.UtcNow.AddSeconds(OllamaEngineInfo.StartTimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                if (_process.HasExited) { OnlineLog.Write("ollama serve s'est arrêté (code " + _process.ExitCode + ")"); return false; }
                if ((await _client.DetectAsync(ct).ConfigureAwait(false)).Status != OllamaStatus.Absent)
                {
                    OnlineLog.Write("moteur démarré en " + LocalAiTiming.FormatDuration(sw.Elapsed));
                    return true;
                }
                await Task.Delay(400, ct).ConfigureAwait(false);
            }
            OnlineLog.Write("ollama serve n'a pas répondu en " + OllamaEngineInfo.StartTimeoutSeconds + " s");
            return false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            OnlineLog.Write("Démarrage du moteur IA : " + ex.Message);
            Message = SetupErrors.Friendly(ex) == L.Get("Ollama_ErrGeneric")
                ? L.Get("Online_EngineNotStarted")
                : SetupErrors.Friendly(ex);
            MessageIsError = true;
            return false;
        }
    }

    /// <summary>Arrête le moteur lancé par SelectBar (jamais celui de l'utilisateur) ; appelé à la fermeture et avant une désinstallation.</summary>
    public void StopOwned() => KillOwned();

    private void KillOwned()
    {
        try
        {
            if (_process != null && !_process.HasExited) _process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) { OnlineLog.Write("Arrêt du moteur IA : " + ex.Message); }
        _process = null;
        try
        {
            // reste d'une session précédente : tout ollama.exe qui tourne depuis NOTRE dossier est le nôtre
            var appDir = Path.GetFullPath(Paths.AppDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var p in Process.GetProcessesByName("ollama"))
            {
                try
                {
                    var file = p.MainModule?.FileName;
                    if (file != null && Path.GetFullPath(file).StartsWith(appDir, StringComparison.OrdinalIgnoreCase)) p.Kill(entireProcessTree: true);
                }
                catch { /* processus d'un autre compte ou déjà fini : ce n'est pas le nôtre */ }
                finally { p.Dispose(); }
            }
        }
        catch (Exception ex) { OnlineLog.Write("Recherche d'un ancien moteur IA : " + ex.Message); }
    }

    // -- installation du moteur

    /// <summary>Installe le moteur (sur clic explicite), puis le démarre. <paramref name="modelBytes"/> sert au contrôle d'espace disque.</summary>
    public bool StartInstall(long modelBytes)
    {
        if (!TryBegin(out var ct)) return false;
        _ = Task.Run(() => RunInstallAsync(modelBytes, ct));
        return true;
    }

    private async Task RunInstallAsync(long modelBytes, CancellationToken ct)
    {
        try
        {
            Set(LocalAiPhase.FetchingInfo, L.Get("Online_FetchingVersion"));
            var asset = await FetchAssetAsync(ct).ConfigureAwait(false);
            if (asset == null) return;

            Directory.CreateDirectory(Paths.DownloadDir);
            var free = FreeBytes(Paths.Root);
            var tight = DiskSpace.Check(free, asset.Size, modelBytes);
            if (tight != null) { Finish(tight, true); return; }

            await DownloadAsync(asset, ct).ConfigureAwait(false);

            Set(LocalAiPhase.Verifying, L.Get("Online_Verifying"));
            var actual = await Task.Run(() => Sha256Of(Paths.Zip, ct), ct).ConfigureAwait(false);
            if (OllamaRelease.CheckDigest(asset.Sha256, actual) != DigestCheck.Match)
            {
                TryDelete(Paths.Zip);
                Finish(OllamaRelease.CorruptMessage, true);
                return;
            }

            Set(LocalAiPhase.Extracting, "Installation du moteur IA…");
            await Task.Run(() => Extract(Paths.Zip, Paths.AppDir, ct), ct).ConfigureAwait(false);
            TryDelete(Paths.Zip);
            if (!File.Exists(Paths.Exe))
            {
                TryDeleteDir(Paths.AppDir);
                Finish(L.Get("Online_ArchiveNoEngine"), true);
                return;
            }

            Set(LocalAiPhase.Starting, L.Get("Online_LocalStarting"));
            await _engineLock.WaitAsync(ct).ConfigureAwait(false);
            bool started;
            try { started = await StartEngineAsync(ct).ConfigureAwait(false); }
            finally { _engineLock.Release(); }
            Finish(started ? L.Get("Online_EngineInstalled") : (Message ?? L.Get("Online_EngineStartBlocked")), !started);
        }
        catch (OperationCanceledException)
        {
            TryDelete(Paths.ZipPart);
            Finish(L.Get("Online_InstallCancelled"), false);
        }
        catch (Exception ex)
        {
            OnlineLog.Write("Installation du moteur IA : " + ex);
            Finish(SetupErrors.Friendly(ex), true);
        }
    }

    private async Task<OllamaAsset?> FetchAssetAsync(CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, OllamaRelease.ApiUrl);
        req.Headers.Accept.ParseAdd("application/vnd.github+json");
        var (status, body) = await OnlineHttp.SendAsync(_http, req, OnlineHttp.ShortTimeout, ct).ConfigureAwait(false);
        if (status == HttpStatusCode.Forbidden || (int)status == 429)
        { Finish(L.Get("Online_GitHubLimit"), true); return null; }
        if ((int)status >= 400) { Finish(L.Get("Online_GitHubBad"), true); return null; }
        if (!OllamaRelease.TryPlan(body, IsArm64, out var asset, out var error)) { Finish(error, true); return null; }
        return asset;
    }

    private async Task DownloadAsync(OllamaAsset asset, CancellationToken ct)
    {
        var part = Paths.ZipPart;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            long existing = File.Exists(part) ? new FileInfo(part).Length : 0;
            if (existing > asset.Size) { TryDelete(part); existing = 0; }
            if (existing == asset.Size) break;                      // déjà complet (reprise après fermeture)

            using var req = new HttpRequestMessage(HttpMethod.Get, asset.Url);
            if (existing > 0) req.Headers.Range = new RangeHeaderValue(existing, null);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable) { TryDelete(part); continue; }
            resp.EnsureSuccessStatusCode();
            var append = existing > 0 && resp.StatusCode == HttpStatusCode.PartialContent;
            if (!append) existing = 0;

            var meter = new TransferMeter();
            var sw = Stopwatch.StartNew();
            long done = existing, lastReport = -1000;
            Set(LocalAiPhase.Downloading, L.Get("Online_DownloadingEngine"), new TransferStatus(done, asset.Size, 0, null));
            using (var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            using (var dst = new FileStream(part, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1 << 17, useAsync: true))
            {
                var buffer = new byte[1 << 17];
                using var stall = CancellationTokenSource.CreateLinkedTokenSource(ct);
                while (true)
                {
                    stall.CancelAfter(StallTimeout);                // pas d'octet pendant 60 s : connexion morte
                    int n;
                    try { n = await src.ReadAsync(buffer.AsMemory(), stall.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new TimeoutException("Téléchargement bloqué"); }
                    if (n == 0) break;
                    await dst.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                    done += n;
                    if (sw.ElapsedMilliseconds - lastReport >= 250)
                    {
                        lastReport = sw.ElapsedMilliseconds;
                        Set(LocalAiPhase.Downloading, L.Get("Online_DownloadingEngine"), meter.Update(done, asset.Size, sw.ElapsedMilliseconds));
                    }
                }
            }
            if (done < asset.Size) throw new IOException("Téléchargement interrompu");   // le .part reste : reprise au prochain clic
            break;
        }
        if (!File.Exists(part) || new FileInfo(part).Length != asset.Size)
        {
            TryDelete(part);
            throw new InvalidDataException("Taille inattendue");
        }
        File.Move(part, Paths.Zip, overwrite: true);
    }

    private static string Sha256Of(string path, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 17);
        var buf = new byte[1 << 17];
        int n;
        while ((n = fs.Read(buf, 0, buf.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            sha.TransformBlock(buf, 0, n, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    private void Extract(string zipPath, string targetDir, CancellationToken ct)
    {
        KillOwned();
        TryDeleteDir(targetDir);
        Directory.CreateDirectory(targetDir);
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            // première passe : on refuse toute l'archive si une seule entrée sort du dossier
            foreach (var e in zip.Entries)
                if (!ZipGuard.TryResolve(targetDir, e.FullName, out _))
                    throw new InvalidDataException("Entrée dangereuse : " + e.FullName);
            long total = zip.Entries.Sum(e => e.Length), done = 0;
            var sw = Stopwatch.StartNew();
            long last = -1000;
            foreach (var e in zip.Entries)
            {
                ct.ThrowIfCancellationRequested();
                ZipGuard.TryResolve(targetDir, e.FullName, out var full);
                if (e.FullName.EndsWith("/", StringComparison.Ordinal) || e.FullName.EndsWith("\\", StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(full);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                e.ExtractToFile(full, overwrite: true);
                done += e.Length;
                if (sw.ElapsedMilliseconds - last >= 250)
                {
                    last = sw.ElapsedMilliseconds;
                    Set(LocalAiPhase.Extracting, "Installation du moteur IA…", new TransferStatus(done, total, 0, null));
                }
            }
        }
        catch
        {
            TryDeleteDir(targetDir);                                  // pas d'installation à moitié faite
            throw;
        }
    }

    // -- modèles

    /// <summary>Télécharge un modèle (sur clic explicite). Démarre le moteur au besoin. <paramref name="expectedBytes"/> : taille connue du catalogue.</summary>
    public bool StartPull(string model, long expectedBytes)
    {
        if (!TryBegin(out var ct)) return false;
        PullingModel = model;
        _ = Task.Run(() => RunPullAsync(model, expectedBytes, ct));
        return true;
    }

    private async Task RunPullAsync(string model, long expectedBytes, CancellationToken ct)
    {
        try
        {
            Set(LocalAiPhase.Starting, L.Get("Online_LocalStarting"));
            var ensure = await EnsureRunningAsync(ct).ConfigureAwait(false);
            if (ensure == EngineEnsure.NotInstalled) { Finish(L.Get("Online_InstallFirst"), true); return; }
            if (ensure == EngineEnsure.Failed)
            { Finish(Message ?? L.Get("Online_EngineNoResponse"), true); return; }

            var tight = DiskSpace.Check(FreeBytes(Paths.Root), 0, expectedBytes);
            if (tight != null) { Finish(tight, true); return; }

            var meter = new TransferMeter();
            var sw = Stopwatch.StartNew();
            var layers = new Dictionary<string, (long Done, long Total)>();
            long last = -1000;
            Set(LocalAiPhase.PullingModel, L.Get("Online_DownloadingModel"), new TransferStatus(0, expectedBytes, 0, null));
            var error = await _client.PullAsync(model, p =>
            {
                if (p.Total > 0) layers[p.Status] = (p.Completed, p.Total);
                if (sw.ElapsedMilliseconds - last < 250) return;
                last = sw.ElapsedMilliseconds;
                var done = layers.Values.Sum(v => v.Done);
                var total = Math.Max(expectedBytes, layers.Values.Sum(v => v.Total));
                Set(LocalAiPhase.PullingModel, L.Get("Online_DownloadingModel"), meter.Update(done, total, sw.ElapsedMilliseconds));
            }, ct).ConfigureAwait(false);
            if (error != null) { Finish(error, true); return; }
            try { ModelReady?.Invoke(model); } catch (Exception ex) { OnlineLog.Write("ModelReady : " + ex.Message); }
            Finish(L.Get("Online_ModelReady"), false);
        }
        catch (OperationCanceledException) { Finish(L.Get("Online_DownloadCancelled"), false); }
        catch (Exception ex)
        {
            OnlineLog.Write("Téléchargement du modèle : " + ex);
            Finish(SetupErrors.Friendly(ex), true);
        }
    }

    public bool StartDeleteModel(string model, bool wasActive)
    {
        if (!TryBegin(out var ct)) return false;
        _ = Task.Run(async () =>
        {
            try
            {
                Set(LocalAiPhase.Deleting, L.Get("Online_DeletingModel"));
                var err = await _client.DeleteModelAsync(model, ct).ConfigureAwait(false);
                if (err == null && wasActive) { try { ModelDeleted?.Invoke(model); } catch (Exception ex) { OnlineLog.Write("ModelDeleted : " + ex.Message); } }
                Finish(err ?? L.Get("Online_ModelDeleted"), err != null);
            }
            catch (OperationCanceledException) { Finish(null, false); }
            catch (Exception ex) { OnlineLog.Write("Suppression : " + ex.Message); Finish(SetupErrors.Friendly(ex), true); }
        });
        return true;
    }

    // -- désinstallation

    /// <summary>Taille occupée par l'installation privée de SelectBar (moteur + modèles), pour la confirmation.</summary>
    public long InstalledBytes()
    {
        try
        {
            return Directory.Exists(Paths.Root)
                ? new DirectoryInfo(Paths.Root).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length)
                : 0;
        }
        catch { return 0; }
    }

    public bool HasPrivateInstall => Directory.Exists(Paths.Root);

    /// <summary>Arrête le moteur de SelectBar et supprime son dossier. Ne touche jamais à un Ollama installé par l'utilisateur.</summary>
    public bool StartUninstall()
    {
        if (!TryBegin(out _)) return false;
        _ = Task.Run(async () =>
        {
            try
            {
                Set(LocalAiPhase.Deleting, L.Get("Online_UninstallingEngine"));
                KillOwned();
                for (var i = 0; i < 5 && Directory.Exists(Paths.Root); i++)
                {
                    TryDeleteDir(Paths.Root);
                    if (Directory.Exists(Paths.Root)) await Task.Delay(500).ConfigureAwait(false);
                }
                var gone = !Directory.Exists(Paths.Root);
                Finish(gone ? L.Get("Online_EngineUninstalled") : L.Get("Online_UninstallLeftovers"), !gone);
            }
            catch (Exception ex) { OnlineLog.Write("Désinstallation : " + ex); Finish(SetupErrors.Friendly(ex), true); }
        });
        return true;
    }

    // -- outils

    private static long FreeBytes(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            return string.IsNullOrEmpty(root) ? long.MaxValue : new DriveInfo(root).AvailableFreeSpace;
        }
        catch { return long.MaxValue; }                               // lecteur réseau ou illisible : on laisse le système refuser
    }

    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (Exception ex) { OnlineLog.Write("Suppression de " + path + " : " + ex.Message); } }
    private static void TryDeleteDir(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch (Exception ex) { OnlineLog.Write("Suppression de " + path + " : " + ex.Message); } }
}
