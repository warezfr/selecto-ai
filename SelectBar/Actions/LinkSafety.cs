using System;
using System.Linq;

namespace SelectBar.Actions;

public enum LinkKind { Rejected, Web, LocalPath }

/// <summary>
/// Garde-fous avant d'ouvrir quoi que ce soit venant d'un texte sélectionné ou d'une extension :
/// schémas http, https, mailto, file uniquement ; jamais de lancement de fichier.
/// </summary>
public static class LinkSafety
{
    private static readonly string[] Allowed = { "http", "https", "mailto", "file" };

    /// <summary>
    /// Web : target est une URI absolue à passer au navigateur/client mail par défaut.
    /// LocalPath : target est un chemin à afficher dans l'Explorateur (jamais à lancer).
    /// </summary>
    public static LinkKind Check(string? url, out string target, Func<string, string?>? getEnv = null)
    {
        target = "";
        if (string.IsNullOrWhiteSpace(url) || url.Length > 8192) return LinkKind.Rejected;
        var u = url.Trim();
        if (u.Any(char.IsControl)) return LinkKind.Rejected;

        var colon = u.IndexOf(':');
        var scheme = colon > 0 ? u.Substring(0, colon).ToLowerInvariant() : "";
        if (Array.IndexOf(Allowed, scheme) < 0)
        {
            // « exemple.com » ou « exemple.com:8080/x » sans schéma : domaine plausible uniquement.
            if (u.Contains("://") || !TextClassifier.IsUrl(u) || TextClassifier.IsMailto(u)) return LinkKind.Rejected;
            u = "https://" + TextClassifier.CleanToken(u);
            scheme = "https";
        }

        if (scheme == "file")
        {
            var path = TextClassifier.ResolvePath(u, getEnv);
            if (path == null) return LinkKind.Rejected;
            target = path;
            return LinkKind.LocalPath;
        }
        if (!Uri.TryCreate(u, UriKind.Absolute, out var uri)) return LinkKind.Rejected;
        if (!string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase)) return LinkKind.Rejected;
        if (scheme == "mailto")
        {
            if (uri.AbsoluteUri.IndexOf('@') < 0) return LinkKind.Rejected;
        }
        else if (string.IsNullOrEmpty(uri.Host)) return LinkKind.Rejected;
        target = uri.AbsoluteUri;
        return LinkKind.Web;
    }

    /// <summary>
    /// Arguments pour explorer.exe : fichier -> /select,"chemin" (sélection seule, jamais d'ouverture) ;
    /// dossier -> ouverture du dossier ; sinon dossier parent s'il existe ; sinon null.
    /// </summary>
    public static string? ExplorerArguments(string? resolvedPath, Func<string, bool> fileExists, Func<string, bool> dirExists)
    {
        var p = TextClassifier.ValidateResolved(resolvedPath);
        if (p == null) return null;
        if (fileExists(p)) return "/select,\"" + p + "\"";
        if (dirExists(p)) return "\"" + p + "\"";
        var parent = TextClassifier.ParentOf(p);
        return parent != null && dirExists(parent) ? "\"" + parent + "\"" : null;
    }
}
