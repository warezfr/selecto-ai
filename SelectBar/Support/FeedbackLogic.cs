using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SelectBar.Localization;

namespace SelectBar.Support;

/// <summary>Type de problème proposé dans le formulaire : code du contrat (envoyé au serveur) et libellé français affiché.</summary>
public sealed record FeedbackType(string Code, string Label);

/// <summary>Résultat de l'envoi : numéro de ticket si le serveur l'a accepté, sinon message d'erreur en français.</summary>
public sealed record FeedbackOutcome(bool Ok, string Ticket, string Error);

/// <summary>
/// Logique pure du formulaire « Signaler un problème » (V3-09, sans WPF ni réseau) : types, validation, nettoyage du journal, lecture de la
/// réponse du serveur. Compilée aussi dans SelectBar.Tests. Le nettoyage du journal est la barrière contre les données personnelles : il
/// ne doit jamais laisser passer un chemin utilisateur, un nom d'utilisateur ou de machine, une adresse e-mail, une clé de licence ou un jeton.
/// </summary>
public static class FeedbackLogic
{
    public const int MinMessageLength = 10;
    public const int MaxMessageLength = 5000;
    public const int MaxEmailLength = 254;
    public const int MaxImages = 3;
    public const int MaxLogLines = 2000;
    public const int MaxLogLineLength = 500;
    public const int MaxImageBytes = 5 * 1024 * 1024;
    public const int MaxLogBytes = 1024 * 1024;
    public const int MaxTotalBytes = 12 * 1024 * 1024;

    /// <summary>Marque ajoutée à la fin d'une ligne tronquée.</summary>
    public const string TruncationMarker = " […]";


    public static string NetworkMessage => L.Get("Feedback_NetworkError");
    public static string TimeoutMessage => L.Get("Feedback_TimeoutError");
    public static string MalformedMessage => L.Get("Feedback_MalformedError");

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    /// <summary>Types de problème, dans l'ordre d'affichage. Les codes doivent rester ceux du contrat POST /v1/feedback.</summary>
    /// <summary>Codes des types de problème (contrat POST /v1/feedback : ne pas les traduire).</summary>
    private static readonly string[] TypeCodes =
    {
        "bug", "installation", "antivirus", "ia-locale", "actions-en-ligne", "suggestion", "autre",
    };

    /// <summary>Types de problème, dans l'ordre d'affichage ; libellés dans la langue courante.</summary>
    public static IReadOnlyList<FeedbackType> Types => TypeCodes.Select(c => new FeedbackType(c, TypeLabel(c))).ToArray();

    /// <summary>Libellé affiché d'un code de type (Feedback_Type_&lt;code&gt;).</summary>
    public static string TypeLabel(string code) => L.Get("Feedback_Type_" + code);

    public static bool IsKnownType(string? code) => code is not null && Types.Any(t => t.Code == code);

    /// <summary>Premier problème trouvé dans le formulaire, en français ; null si le formulaire peut être envoyé.</summary>
    /// <param name="imageBytes">Taille de chaque capture jointe.</param>
    /// <param name="logBytes">Taille du journal joint (0 si aucun).</param>
    public static string? ValidateDraft(string? type, string? message, string? email, bool consent, IReadOnlyList<long> imageBytes, long logBytes)
    {
        if (!IsKnownType(type)) return L.Get("Feedback_Err_Type");
        var text = (message ?? "").Trim();
        if (text.Length < MinMessageLength) return L.Get("Feedback_Err_TooShort");
        if (text.Length > MaxMessageLength) return L.Get("Feedback_Err_TooLong");
        var mail = (email ?? "").Trim();
        if (mail.Length > 0 && !IsValidEmail(mail)) return L.Get("Feedback_Err_Email");
        if (!consent) return L.Get("Feedback_Err_Consent");
        return ValidateAttachments(imageBytes, logBytes);
    }

    /// <summary>Limites des pièces jointes : 3 captures de 5 Mo, un journal de 1 Mo, 12 Mo au total (mêmes limites que le serveur).</summary>
    public static string? ValidateAttachments(IReadOnlyList<long> imageBytes, long logBytes)
    {
        if (imageBytes.Count > MaxImages) return L.Get("Feedback_Err_Shots");
        if (imageBytes.Any(b => b > MaxImageBytes)) return L.Get("Feedback_Err_ShotSize");
        if (logBytes > MaxLogBytes) return L.Get("Feedback_Err_LogSize");
        if (imageBytes.Sum() + logBytes > MaxTotalBytes) return L.Get("Feedback_Err_TotalSize");
        return null;
    }

    private static readonly Regex EmailPattern = new(
        @"\A[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]{1,64}@[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)+\z",
        Options, RegexTimeout);

    /// <summary>Adresse e-mail simple et valide (pas de « .. », domaine avec au moins un point). Longueur ≤ 254.</summary>
    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrEmpty(email) && email.Length <= MaxEmailLength && !email.Contains("..", StringComparison.Ordinal) && EmailPattern.IsMatch(email);


    /// <summary>Lit la réponse du serveur : 201 avec un numéro de ticket = succès ; sinon message d'erreur selon le code.</summary>
    public static FeedbackOutcome Interpret(int status, string? body)
    {
        if (status == 201)
        {
            var ticket = ParseTicket(body);
            return ticket is null ? Fail(MalformedMessage) : new FeedbackOutcome(true, ticket, "");
        }
        return Fail(ErrorMessage(status));
    }

    /// <summary>Numéro de ticket « SEL-AAAA-NNNN » du corps JSON { "ticket": … } ; null si absent ou mal formé.</summary>
    public static string? ParseTicket(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!doc.RootElement.TryGetProperty("ticket", out var t) || t.ValueKind != JsonValueKind.String) return null;
            var s = t.GetString();
            return s is not null && TicketPattern.IsMatch(s) ? s : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly Regex TicketPattern = new(@"\ASEL-[0-9]{4}-[0-9]{3,}\z", Options, RegexTimeout);

    /// <summary>Message français pour un code HTTP du contrat (400, 403, 413, 429, 503) ou inattendu.</summary>
    public static string ErrorMessage(int status) => status switch
    {
        400 => L.Get("Feedback_Err_400"),
        403 => L.Get("Feedback_Err_403"),
        413 => L.Get("Feedback_Err_413"),
        429 => L.Get("Feedback_Err_429"),
        >= 500 => L.Get("Feedback_Err_5xx"),
        _ => L.Format("Feedback_Err_Other", L.Ltr(status.ToString())),
    };

    /// <summary>Texte affiché après un envoi accepté.</summary>
    public static string ThankYou(string ticket) => L.Format("Feedback_ThankYou", ticket);

    private static FeedbackOutcome Fail(string message) => new(false, "", message);

    // ------------------------------------------------------------------ journal

    private static readonly Regex ProfilePathPattern = new(@"[A-Za-z]:\\Users\\[^\\\s""'<>|]+", Options, RegexTimeout);
    private static readonly Regex LogEmailPattern = new(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", Options, RegexTimeout);
    private static readonly Regex BearerPattern = new(@"\bBearer\s+[A-Za-z0-9._~+/=\-]{8,}", Options, RegexTimeout);
    private static readonly Regex SecretAssignmentPattern = new(
        @"\b(api[_-]?key|apikey|token|secret|password|mot de passe|authorization)(\s*[:=]\s*)\S+", Options, RegexTimeout);
    private static readonly Regex VendorKeyPattern = new(
        @"\b(?:sk-[A-Za-z0-9_\-]{16,}|AIza[0-9A-Za-z_\-]{30,}|gh[pousr]_[A-Za-z0-9]{20,}|xox[abprs]-[A-Za-z0-9\-]{10,}|hf_[A-Za-z0-9]{20,}|gsk_[A-Za-z0-9]{20,})",
        Options, RegexTimeout);

    /// <summary>
    /// Nettoie le texte du journal : garde les <see cref="MaxLogLines"/> dernières lignes ; remplace le dossier du profil par
    /// %USERPROFILE% ; masque les e-mails, les jetons Bearer, sk-, clés API et mots de passe affichés,
    /// puis le nom d'utilisateur (<utilisateur>) et le nom de machine (<ordinateur>), insensibles à la casse et en mots entiers ;
    /// tronque chaque ligne à <see cref="MaxLogLineLength"/> caractères.
    /// </summary>
    public static string SanitizeLog(string? raw, string? userProfile, string? userName, string? machineName)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var lines = raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var end = lines.Length;
        if (end > 0 && lines[end - 1].Length == 0) end--;   // saut de ligne final : pas une ligne de plus
        var start = Math.Max(0, end - MaxLogLines);

        var profile = string.IsNullOrWhiteSpace(userProfile) ? null : userProfile.Trim();
        var user = NameMask(userName);
        var machine = NameMask(machineName);

        var sb = new StringBuilder();
        for (var i = start; i < end; i++)
        {
            if (i > start) sb.Append('\n');
            var line = lines[i];
            if (profile is not null)
            {
                line = Regex.Replace(line, Regex.Escape(profile), "%USERPROFILE%", Options);
                line = Regex.Replace(line, Regex.Escape(profile.Replace('\\', '/')), "%USERPROFILE%", Options);
            }
            line = ProfilePathPattern.Replace(line, "%USERPROFILE%");
            line = LogEmailPattern.Replace(line, "<e-mail>");
            line = BearerPattern.Replace(line, "Bearer <jeton masqué>");
            line = SecretAssignmentPattern.Replace(line, "$1$2<masqué>");
            line = VendorKeyPattern.Replace(line, "<clé masquée>");
            if (user is not null) line = user.Replace(line, "<utilisateur>");
            if (machine is not null) line = machine.Replace(line, "<ordinateur>");
            sb.Append(Truncate(line));
        }
        return sb.ToString();
    }

    /// <summary>Retire les lignes les plus anciennes jusqu'à tenir dans <paramref name="maxBytes"/> octets UTF-8.</summary>
    public static string FitToBytes(string text, int maxBytes)
    {
        if (Encoding.UTF8.GetByteCount(text) <= maxBytes) return text;
        var lines = text.Split('\n');
        var first = lines.Length;
        var kept = 0;
        long used = 0;
        for (var k = lines.Length - 1; k >= 0; k--)
        {
            var cost = Encoding.UTF8.GetByteCount(lines[k]) + (kept > 0 ? 1 : 0);
            if (used + cost > maxBytes) break;
            used += cost;
            kept++;
            first = k;
        }
        return string.Join("\n", lines, first, lines.Length - first);
    }

    private static Regex? NameMask(string? name)
    {
        var n = name?.Trim();
        if (n is null || n.Length < 2) return null;
        return new Regex(@"(?<![\p{L}\p{N}_])" + Regex.Escape(n) + @"(?![\p{L}\p{N}_])", Options, RegexTimeout);
    }

    private static string Truncate(string line)
    {
        if (line.Length <= MaxLogLineLength) return line;
        var keep = MaxLogLineLength - TruncationMarker.Length;
        if (char.IsHighSurrogate(line[keep - 1])) keep--;   // ne pas couper une paire de substitution
        return line[..keep] + TruncationMarker;
    }
}
