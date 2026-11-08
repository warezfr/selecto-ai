using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SelectBar.Online;

namespace SelectBar.Support;

/// <summary>Pièce jointe : « screenshot » (PNG) ou « log » (texte UTF-8), selon le contrat POST /v1/feedback.</summary>
public sealed record FeedbackAttachment(string FileName, string ContentType, byte[] Bytes);

/// <summary>Contenu d'un signalement depuis l'application (champs du contrat, source = app).</summary>
public sealed record FeedbackSubmission(
    string Type, string Message, string? Email, string Version, string Edition, string Os, string Arch,
    FeedbackAttachment? Screenshot, FeedbackAttachment? Log);

/// <summary>
/// Envoi d'un signalement à POST /v1/feedback (multipart/form-data). Une seule tentative, sur clic de l'utilisateur : jamais de nouvel essai
/// automatique. Délai de 30 s, annulable. Aucune exception ne sort, sauf l'annulation demandée par l'appelant.
/// </summary>
public sealed class FeedbackClient
{
    public static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _http;
    private readonly string _endpoint;

    /// <param name="endpoint">Adresse complète : base du serveur + FeedbackServer.FeedbackPath (« /v1/feedback »).</param>
    public FeedbackClient(HttpClient http, string endpoint)
    {
        _http = http;
        _endpoint = endpoint;
    }

    public async Task<FeedbackOutcome> SendAsync(FeedbackSubmission submission, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = BuildForm(submission) };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        try
        {
            var (status, body) = await OnlineHttp.SendAsync(_http, request, SendTimeout, ct).ConfigureAwait(false);
            var outcome = FeedbackLogic.Interpret((int)status, body);
            Log.Write(outcome.Ok ? "Signalement : ticket " + outcome.Ticket + " enregistré" : "Signalement : refusé (" + (int)status + ")");
            return outcome;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            Log.Write("Signalement : délai dépassé");
            return new FeedbackOutcome(false, "", FeedbackLogic.TimeoutMessage);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            Log.Write("Signalement : réseau : " + ex.GetType().Name);
            return new FeedbackOutcome(false, "", FeedbackLogic.NetworkMessage);
        }
    }

    /// <summary>
    /// Corps multipart du contrat : champs texte (source, type, message, email facultatif, consent, version, edition, os, arch, includesLogs), puis la capture (« screenshot ») et le journal (« log »), chacun au plus une fois.
    /// </summary>
    public static MultipartFormDataContent BuildForm(FeedbackSubmission s)
    {
        var form = new MultipartFormDataContent();
        AddText(form, "source", "app");
        AddText(form, "type", s.Type);
        AddText(form, "message", s.Message);
        if (!string.IsNullOrEmpty(s.Email)) AddText(form, "email", s.Email);
        AddText(form, "consent", "true");
        AddText(form, "version", s.Version);
        AddText(form, "edition", s.Edition);
        AddText(form, "os", s.Os);
        AddText(form, "arch", s.Arch);
        AddText(form, "includesLogs", s.Log is null ? "false" : "true");
        if (s.Screenshot is { } shot) AddFile(form, "screenshot", shot);
        if (s.Log is { } log) AddFile(form, "log", log);
        return form;
    }

    private static void AddText(MultipartFormDataContent form, string name, string value) =>
        form.Add(new StringContent(value, Encoding.UTF8), name);

    private static void AddFile(MultipartFormDataContent form, string field, FeedbackAttachment file)
    {
        var part = new ByteArrayContent(file.Bytes);
        part.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType); // Parse : accepte « text/plain; charset=utf-8 »
        form.Add(part, field, file.FileName);
    }

    /// <summary>
    /// Journal de l'application, nettoyé et borné (2000 dernières lignes, 1 Mo au plus), lu à partir de la fin du fichier (4 Mo au plus).
    /// Renvoie null si le fichier est absent, illisible ou vide après nettoyage. Ne lève pas d'exception.
    /// </summary>
    public static string? ReadSanitizedLog(string path)
    {
        const long tailBytes = 4L * 1024 * 1024;
        try
        {
            if (!File.Exists(path)) return null;
            string raw;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var length = fs.Length;
                var partial = length > tailBytes;
                if (partial) fs.Seek(length - tailBytes, SeekOrigin.Begin);
                using var reader = new StreamReader(fs, Encoding.UTF8);
                raw = reader.ReadToEnd();
                if (partial)
                {
                    // on repart au début de la première ligne complète
                    var nl = raw.IndexOf('\n');
                    if (nl >= 0) raw = raw[(nl + 1)..];
                }
            }
            var clean = FeedbackLogic.SanitizeLog(raw, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.UserName, Environment.MachineName);
            clean = FeedbackLogic.FitToBytes(clean, FeedbackLogic.MaxLogBytes);
            return clean.Length == 0 ? null : clean;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write("Signalement : journal illisible : " + ex.GetType().Name);
            return null;
        }
    }
}
