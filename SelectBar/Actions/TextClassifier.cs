using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SelectBar.Actions;

/// <summary>
/// Détection d'URL, d'e-mails et de chemins Windows dans un texte sélectionné.
/// Le texte vient de l'utilisateur ou d'une page web : c'est une donnée non fiable, rien n'est exécuté ici.
/// </summary>
public static class TextClassifier
{
    private static readonly HashSet<string> Tlds = new(StringComparer.OrdinalIgnoreCase)
    {
        "com", "org", "net", "io", "fr", "dev", "app", "co", "me", "info", "edu", "gov",
        "ma", "uk", "de", "es", "it", "ca", "us", "eu", "ai", "tv", "xyz", "page", "be", "ch",
        "biz", "pro", "shop", "site", "online", "store", "tech", "blog", "cloud", "nl", "pt", "se",
        "dk", "pl", "ru", "jp", "cn", "br", "au", "nz", "ie", "at", "lu", "tn", "dz", "sn", "ci"
    };

    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(150);
    private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant;

    private static readonly Regex Scheme = new(@"^https?://[A-Za-z0-9\[]\S*$", Opts, Timeout);
    private static readonly Regex MailUrl = new(@"^mailto:[^\s@]+@[^\s@]+$", Opts, Timeout);
    private static readonly Regex FileUrl = new(@"^file://\S+$", Opts, Timeout);
    private static readonly Regex Bare = new(
        @"^(?:www\.)?(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+(?<tld>[a-z]{2,})(?::\d+)?(?:[/?#]\S*)?$", Opts, Timeout);
    private static readonly Regex Inline = new(
        @"(?<![\w@/])(?:https?://|mailto:|www\.)[^\s<>""«»“”]+", Opts, Timeout);
    private static readonly Regex EmailExact = new(
        @"^[A-Za-z0-9._%+-]+@(?:[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?\.)+[A-Za-z]{2,}$", RegexOptions.Compiled, Timeout);
    private static readonly Regex EmailInline = new(
        @"(?<![\w.%+-])[A-Za-z0-9._%+-]+@(?:[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?\.)+[A-Za-z]{2,}(?![A-Za-z0-9@_-])", RegexOptions.Compiled, Timeout);
    private static readonly Regex EnvVar = new(@"%([^%\r\n\\/:*?""<>|]{1,64})%", RegexOptions.Compiled, Timeout);
    private static readonly Regex Drive = new(@"^[A-Za-z]:\\", RegexOptions.Compiled, Timeout);
    private static readonly Regex Unc = new(@"^\\\\([^\\]+)\\([^\\]+)", RegexOptions.Compiled, Timeout);
    private static readonly Regex HasScheme = new(@"^[a-z][a-z0-9+.-]*:", Opts, Timeout);
    private static readonly Regex HostPort = new(@"^[^/]+:\d+", RegexOptions.Compiled, Timeout);

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    // ---------------------------------------------------------------- nettoyage

    private static char Closer(char c) => c switch
    {
        '"' => '"', '\'' => '\'', '<' => '>', '«' => '»', '“' => '”', '‘' => '’', '(' => ')', '[' => ']', '{' => '}', _ => '\0'
    };

    /// <summary>
    /// Retire espaces, guillemets/chevrons/parenthèses englobants et ponctuation finale (.,;:!?), y compris
    /// une parenthèse fermante en trop (les URL du type /Foo_(bar) gardent leur parenthèse équilibrée).
    /// </summary>
    public static string CleanToken(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var t = text.Trim();
        for (var guard = 0; guard < 24 && t.Length > 0; guard++)
        {
            t = t.Trim();
            if (t.Length == 0) break;
            var open = Closer(t[0]);
            if (t.Length >= 2 && open != '\0' && t[^1] == open) { t = t.Substring(1, t.Length - 2); continue; }
            if (t.Length >= 2 && "\"'<«“‘".IndexOf(t[0]) >= 0) { t = t.Substring(1); continue; }
            if (t.Length >= 2 && "([{".IndexOf(t[0]) >= 0 && t.IndexOf(open, 1) < 0) { t = t.Substring(1); continue; }
            var last = t[^1];
            if (".,;:!?\"'>»”’".IndexOf(last) >= 0) { t = t.Substring(0, t.Length - 1); continue; }
            if ((last == ')' || last == ']' || last == '}') && Unbalanced(t, last)) { t = t.Substring(0, t.Length - 1); continue; }
            break;
        }
        return t.Trim();
    }

    private static bool Unbalanced(string t, char close)
    {
        var open = close switch { ')' => '(', ']' => '[', _ => '{' };
        return t.Count(c => c == close) > t.Count(c => c == open);
    }

    private static bool HasControl(string s) => s.Any(char.IsControl);

    // ---------------------------------------------------------------- URL

    /// <summary>Tout le texte (hors ponctuation/guillemets autour) est une URL, un mailto: ou un domaine plausible.</summary>
    public static bool IsUrl(string? text) => IsUrlToken(CleanToken(text));

    private static bool IsUrlToken(string t)
    {
        if (t.Length == 0 || t.Length > 2048) return false;
        if (t.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))) return false;
        try
        {
            if (Scheme.IsMatch(t) || MailUrl.IsMatch(t) || FileUrl.IsMatch(t)) return true;
            var m = Bare.Match(t);
            if (!m.Success) return false;
            if (t.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) return true;
            return Tlds.Contains(m.Groups["tld"].Value);
        }
        catch (RegexMatchTimeoutException) { return false; }
    }

    /// <summary>
    /// Première URL du texte, telle qu'écrite mais nettoyée : le texte entier s'il en est une (domaine nu compris),
    /// sinon la première occurrence http(s)://, www. ou mailto: au milieu d'une phrase.
    /// </summary>
    public static string? FindUrl(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var whole = CleanToken(text);
        if (IsUrlToken(whole)) return whole;
        try
        {
            foreach (Match m in Inline.Matches(text))
            {
                var c = CleanToken(m.Value);
                if (IsUrlToken(c)) return c;
            }
        }
        catch (RegexMatchTimeoutException) { }
        return null;
    }

    /// <summary>Forme ouvrable : ajoute https:// aux domaines nus et aux www.</summary>
    public static string NormalizeUrl(string? text)
    {
        var t = FindUrl(text) ?? CleanToken(text);
        if (HasScheme.IsMatch(t) && !HostPort.IsMatch(t)) return t;
        return "https://" + t;
    }

    // ---------------------------------------------------------------- e-mail

    public static bool IsMailto(string? s) => s != null && s.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

    /// <summary>Adresse e-mail : le texte entier (ou mailto:), sinon la première du texte si aucun lien web n'y figure.</summary>
    public static string? FindEmail(string? text, bool webLinkPresent = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            var whole = CleanToken(text);
            if (IsMailto(whole)) whole = whole.Substring(7);
            var q = whole.IndexOf('?');
            if (q >= 0) whole = whole.Substring(0, q);
            if (whole.Length <= 254 && EmailExact.IsMatch(whole)) return whole;
            if (webLinkPresent) return null;
            var m = EmailInline.Match(text);
            return m.Success && m.Value.Length <= 254 ? m.Value : null;
        }
        catch (RegexMatchTimeoutException) { return null; }
    }

    // ---------------------------------------------------------------- chemins

    public static string CleanPath(string? text) => CleanToken(text);

    /// <summary>
    /// Chemin Windows absolu extrait du texte (lecteur, UNC, \\?\, file:///, %VAR%, ~), ou null s'il est invalide.
    /// Ne touche pas au disque. getEnv/home sont injectables pour les tests.
    /// </summary>
    public static string? ResolvePath(string? text, Func<string, string?>? getEnv = null, string? home = null)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 33000) return null;
        var t = CleanToken(text);
        if (t.Length == 0 || HasControl(t)) return null;

        if (t.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            var f = FromFileUrl(t);
            if (f == null) return null;
            t = f;
        }
        if (t.IndexOf('%') >= 0)
        {
            var e = ExpandEnv(t, getEnv ?? (n => Environment.GetEnvironmentVariable(n)));
            if (e == null) return null;
            t = e;
        }
        if (t == "~" || t.StartsWith("~\\", StringComparison.Ordinal) || t.StartsWith("~/", StringComparison.Ordinal))
        {
            home ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(home)) return null;
            t = home.TrimEnd('\\', '/') + t.Substring(1);
        }
        return ValidateResolved(t);
    }

    private static string? ExpandEnv(string t, Func<string, string?> getEnv)
    {
        var failed = false;
        string result;
        try
        {
            result = EnvVar.Replace(t, m =>
            {
                var v = getEnv(m.Groups[1].Value);
                if (string.IsNullOrEmpty(v)) { failed = true; return m.Value; }
                return v;
            });
        }
        catch (RegexMatchTimeoutException) { return null; }
        return failed ? null : result;
    }

    private static string? FromFileUrl(string u)
    {
        var rest = u.Substring(5);
        if (rest.StartsWith("///", StringComparison.Ordinal)) rest = rest.Substring(3);
        else if (rest.StartsWith("//", StringComparison.Ordinal))
        {
            rest = rest.Substring(2);
            if (rest.StartsWith("localhost/", StringComparison.OrdinalIgnoreCase)) rest = rest.Substring(10);
            else
            {
                var slash = rest.IndexOf('/');
                if (slash <= 0) return null;
                rest = "\\\\" + rest; // file://serveur/partage/x -> \\serveur\partage\x
            }
        }
        else return null;
        try { rest = Uri.UnescapeDataString(rest); } catch (UriFormatException) { return null; }
        if (rest.Length >= 3 && rest[0] == '/' && char.IsAsciiLetter(rest[1]) && rest[2] == ':') rest = rest.Substring(1);
        return rest;
    }

    /// <summary>
    /// Valide un chemin déjà développé : lecteur ou UNC, pas de caractères interdits, pas de noms réservés,
    /// longueur (260 sans préfixe \\?\, 32000 avec). Retourne la forme canonique (barres inverses) ou null.
    /// </summary>
    public static string? ValidateResolved(string? path)
    {
        if (string.IsNullOrEmpty(path) || HasControl(path)) return null;
        var p = path.Replace('/', '\\');
        string body = p;
        var longForm = false;
        var longUnc = false;
        if (p.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            longForm = true;
            body = p.Substring(4);
            if (body.StartsWith(@"UNC\", StringComparison.OrdinalIgnoreCase)) { longUnc = true; body = @"\\" + body.Substring(4); }
        }
        else if (p.StartsWith(@"\\.\", StringComparison.Ordinal)) return null;

        var drive = Drive.IsMatch(body);
        var unc = Unc.Match(body);
        if (!drive && !unc.Success) return null;
        if (!drive && (unc.Groups[1].Value is "." or "?")) return null;
        if (body.Length > (longForm ? 32000 : 260)) return null;

        var rest = body.Substring(2);
        foreach (var c in rest)
            if ("<>\"|?*:".IndexOf(c) >= 0) return null;

        foreach (var seg in rest.Split('\\'))
        {
            if (seg.Length == 0) continue;
            if (seg.All(c => c == '.')) continue;                    // "." et ".."
            if (seg.EndsWith(' ') || seg.EndsWith('.')) return null; // Windows les supprimerait en silence
            var dot = seg.IndexOf('.');
            var stem = (dot >= 0 ? seg.Substring(0, dot) : seg).TrimEnd();
            if (ReservedNames.Contains(stem)) return null;
        }
        if (!longForm) return body;
        return longUnc ? @"\\?\UNC\" + body.Substring(2) : @"\\?\" + body;
    }

    /// <summary>Dossier parent d'un chemin canonique (null à la racine d'un lecteur ou d'un partage).</summary>
    public static string? ParentOf(string? path)
    {
        var p = ValidateResolved(path);
        if (p == null) return null;
        var prefix = "";
        var body = p;
        var longUnc = false;
        if (p.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) { prefix = @"\\?\UNC\"; body = @"\\" + p.Substring(8); longUnc = true; }
        else if (p.StartsWith(@"\\?\", StringComparison.Ordinal)) { prefix = @"\\?\"; body = p.Substring(4); }

        var isDrive = Drive.IsMatch(body);
        if (body.Length > 3) body = body.TrimEnd('\\');
        string? parent;
        if (isDrive)
        {
            if (body.Length <= 3) return null;
            var idx = body.LastIndexOf('\\');
            parent = idx <= 2 ? body.Substring(0, 3) : body.Substring(0, idx);
        }
        else
        {
            var idx = body.LastIndexOf('\\');
            var m = Unc.Match(body);
            if (!m.Success || idx < 2 || body.Length <= m.Length) return null; // \\hote\partage : racine
            parent = body.Substring(0, idx);
        }
        if (prefix.Length == 0) return parent;
        return longUnc ? prefix + parent.Substring(2) : prefix + parent;
    }

    /// <summary>Le texte désigne-t-il un chemin existant (ou dont le dossier parent existe) ?</summary>
    public static bool IsPath(string? text, Func<string, bool> exists) => ResolveExisting(text, exists) != null;

    /// <summary>Chemin résolu si lui ou son dossier parent existe, sinon null. Les erreurs d'E/S comptent comme « n'existe pas ».</summary>
    public static string? ResolveExisting(string? text, Func<string, bool> exists, Func<string, string?>? getEnv = null, string? home = null)
    {
        var p = ResolvePath(text, getEnv, home);
        if (p == null) return null;
        try
        {
            if (exists(p)) return p;
            var parent = ParentOf(p);
            if (parent != null && exists(parent)) return p;
        }
        catch { }
        return null;
    }

    // ---------------------------------------------------------------- contexte

    public static ActionContext BuildContext(string text, bool isEditable, string? appExe, Func<string, bool> pathExists,
        bool clipboardHasText = false, string? html = null, string? rtf = null)
    {
        var ctx = new ActionContext(text, isEditable, appExe, clipboardHasText, html, rtf);
        var p = ctx.ProcessText;

        var link = FindUrl(p);
        var mail = FindEmail(p, webLinkPresent: link != null && !IsMailto(link));
        if (mail != null && (link == null || IsMailto(link))) link = null;
        if (link != null && link.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) link = null; // géré comme chemin
        ctx.LinkTarget = link;
        ctx.IsUrl = link != null;
        ctx.MailTarget = mail == null ? null : "mailto:" + mail;
        ctx.IsEmail = mail != null;

        var path = ResolveExisting(p, pathExists);
        ctx.PathTarget = path;
        ctx.IsPath = path != null;
        return ctx;
    }
}
