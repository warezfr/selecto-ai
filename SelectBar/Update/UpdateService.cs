using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SelectBar.Packaging;
using SelectBar.Localization;
using SelectBar.Online;

namespace SelectBar.Update;

public enum UpdateState { Idle, Checking, UpToDate, Available, Downloading, Failed }

/// <summary>
/// Vérification et installation des mises à jour du site (édition téléchargée). Désactivé en édition Store et en mode capture.
/// Aucune exception ne remonte à l'interface : les échecs passent par <see cref="State"/> et <see cref="LastError"/>, et sont journalisés.
/// </summary>
public sealed class UpdateService
{
    private readonly HttpClient _http;
    private readonly Version _current;
    private readonly bool _enabled;

    /// <param name="http">Client partagé de l'application (User-Agent « SelectoAI/x.y.z »).</param>
    /// <param name="current">Version installée (assembly exécutable).</param>
    /// <param name="allowed">Faux en mode capture ; l'édition Store est toujours exclue.</param>
    public UpdateService(HttpClient http, Version current, bool allowed)
    {
        _http = http;
        _current = current;
        _enabled = allowed && EditionInfo.Current != AppEdition.Store;
    }

    /// <summary>La fonction est active (ni Store, ni mode capture).</summary>
    public bool Enabled => _enabled;

    public UpdateState State { get; private set; } = UpdateState.Idle;

    /// <summary>Dernière mise à jour trouvée, tant que la version n'a pas été installée (null si à jour ou inconnue).</summary>
    public UpdateInfo? Available { get; private set; }

    /// <summary>Raison courte du dernier échec de téléchargement ou de lancement (null sinon). Déclenche <see cref="Changed"/>.</summary>
    public string? LastError { get; private set; }

    /// <summary>Progression du téléchargement, de 0 à 1 (NaN tant qu'elle est inconnue).</summary>
    public double Progress { get; private set; } = double.NaN;

    /// <summary>Levé à chaque changement d'état, depuis un thread quelconque : l'interface doit se resynchroniser via son Dispatcher.</summary>
    public event Action? Changed;

    /// <summary>Version installée, « 3.0.0 ».</summary>
    public string CurrentVersionText => $"{_current.Major}.{_current.Minor}.{Math.Max(0, _current.Build)}";

    /// <summary>Dossier des installateurs téléchargés : %LOCALAPPDATA%\SelectoAI\updates.</summary>
    public static string UpdatesDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppPaths.FolderName, "updates");

    /// <summary>Lit latest.json (délai 10 s). Renvoie l'état final ; ne lève jamais d'exception hors annulation.</summary>
    /// <summary>Adresse du latest.json ; remplacée seulement en mode capture (--update-url) pour rejouer une mise à jour en VM.</summary>
    public string ManifestUrl { get; set; } = UpdateLogic.ManifestUrl;

    public async Task<UpdateState> CheckAsync(CancellationToken ct)
    {
        if (!_enabled || State == UpdateState.Checking || State == UpdateState.Downloading) return State;
        LastError = null;
        Set(UpdateState.Checking, progress: double.NaN);
        try
        {
            var (status, body) = await OnlineHttp.GetAsync(_http, ManifestUrl, TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
            if ((int)status < 200 || (int)status > 299) throw new HttpRequestException("HTTP " + (int)status);
            var parsed = UpdateLogic.Parse(body) ?? throw new FormatException("latest.json invalide");
            var (version, entries, notes) = parsed;

            if (!UpdateLogic.IsNewer(version, _current))
            {
                Available = null;
                Set(UpdateState.UpToDate, progress: double.NaN);
                Log.Write("Mise à jour : à jour (" + CurrentVersionText + ")");
                return State;
            }

            var entry = UpdateLogic.Pick(entries, ArchName)
                        ?? throw new InvalidOperationException(L.Get("Update_NoInstaller"));
            Available = new UpdateInfo(version, entry.Url, entry.Sha256, entry.Size, notes);
            Set(UpdateState.Available, progress: double.NaN);
            Log.Write("Mise à jour : version " + version + " disponible");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Set(UpdateState.Idle, progress: double.NaN);
        }
        catch (Exception ex)
        {
            Log.Write("Mise à jour : vérification impossible : " + ex.Message);
            Set(UpdateState.Failed, progress: double.NaN);
        }
        return State;
    }

    /// <summary>
    /// Télécharge l'installateur dans <see cref="UpdatesDirectory"/>, vérifie sa taille et son SHA-256, puis le renomme.
    /// Renvoie son chemin, ou null (refus, réseau, annulation) avec <see cref="LastError"/> renseigné. Un fichier non conforme est supprimé.
    /// </summary>
    public async Task<string?> DownloadAndVerifyAsync(UpdateInfo info, IProgress<double>? progress, CancellationToken ct)
    {
        if (!_enabled) { LastError = L.Get("Update_Disabled"); Changed?.Invoke(); return null; }
        if (!UpdateLogic.IsAllowedUrl(info.Url))
        {
            Log.Write("Mise à jour : URL refusée : " + info.Url);
            LastError = L.Get("Update_UrlRefused");
            Changed?.Invoke();
            return null;
        }

        var dir = UpdatesDirectory;
        var finalPath = Path.Combine(dir, UpdateLogic.FileNameFor(info));
        var partPath = finalPath + ".part";
        LastError = null;
        Set(UpdateState.Downloading, progress: 0);
        try
        {
            Directory.CreateDirectory(dir);
            using var request = new HttpRequestMessage(HttpMethod.Get, info.Url);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? info.Size;

            await using (var net = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var file = File.Create(partPath))
            {
                var buffer = new byte[81920];
                long read = 0;
                while (true)
                {
                    var n = await net.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
                    if (n == 0) break;
                    await file.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                    read += n;
                    if (total > 0) ReportProgress(progress, Math.Min(1.0, (double)read / total));
                }
            }

            if (info.Size > 0 && new FileInfo(partPath).Length != info.Size)
                throw new InvalidDataException(L.Get("Update_SizeMismatch"));

            string actual;
            using (var check = File.OpenRead(partPath))
                actual = Convert.ToHexString(SHA256.HashData(check));
            if (!string.Equals(actual, info.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(L.Get("Update_HashMismatch"));

            File.Move(partPath, finalPath, overwrite: true);
            Log.Write("Mise à jour : installateur vérifié (" + finalPath + ")");
            Set(UpdateState.Available, progress: double.NaN);
            return finalPath;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            DeleteQuietly(partPath);
            Set(Available is null ? UpdateState.Idle : UpdateState.Available, progress: double.NaN);
            return null;
        }
        catch (Exception ex)
        {
            DeleteQuietly(partPath);
            DeleteQuietly(finalPath);
            var reason = ex is HttpRequestException ? L.Get("Update_Reason_Connection") : ex is InvalidDataException ? ex.Message : L.Get("Update_Reason_Write");
            Log.Write("Mise à jour : téléchargement impossible : " + ex.GetType().Name + " " + ex.Message);
            LastError = L.Format("Update_DownloadFailed", reason);
            Set(Available is null ? UpdateState.Failed : UpdateState.Available, progress: double.NaN);
            return null;
        }
    }

    /// <summary>
    /// Mode capture (--fake-update=) : expose une mise à jour factice, sans réseau, pour les captures du badge et de la bande du flyout.
    /// L'appelant ne doit l'utiliser qu'en mode capture.
    /// </summary>
    public void ForceAvailableForCapture(UpdateInfo info)
    {
        Available = info;
        Set(UpdateState.Available, progress: double.NaN);
    }

    /// <summary>
    /// Lance l'installateur Inno en silencieux (/SILENT /SUPPRESSMSGBOXES /NORESTART) puis ferme l'application par <paramref name="quit"/>
    /// (le même chemin que « Quitter »). Renvoie false, sans rien lancer, si le fichier manque ou si le lancement échoue.
    /// </summary>
    public bool LaunchInstallerAndExit(string path, Action quit)
    {
        if (!File.Exists(path))
        {
            LastError = L.Get("Update_InstallerMissing");
            Changed?.Invoke();
            return false;
        }
        try
        {
            _ = Process.Start(new ProcessStartInfo(path, "/SILENT /SUPPRESSMSGBOXES /NORESTART") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Write("Mise à jour : lancement impossible : " + ex.Message);
            LastError = L.Get("Update_LaunchFailed");
            Changed?.Invoke();
            return false;
        }
        Log.Write("Mise à jour : installation lancée, fermeture de l'application");
        quit();
        return true;
    }

    // ------------------------------------------------------------------ outils

    /// <summary>Architecture du PC : « x64 », « arm64 », ou vide si non prise en charge (aucun installateur ne correspond).</summary>
    private static string ArchName => RuntimeInformation.OSArchitecture switch
    {
        Architecture.X64 => "x64",
        Architecture.Arm64 => "arm64",
        _ => ""
    };

    private void Set(UpdateState state, double progress)
    {
        State = state;
        Progress = progress;
        Changed?.Invoke();
    }

    private void ReportProgress(IProgress<double>? progress, double value)
    {
        Progress = value;
        progress?.Report(value);
    }

    private static void DeleteQuietly(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
