using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SelectBar.Update;

/// <summary>Mise à jour proposée : version, installateur choisi pour ce PC, notes facultatives (FR).</summary>
public sealed record UpdateInfo(string Version, string Url, string Sha256, long Size, string? Notes);

/// <summary>Une entrée « installers » du latest.json : une édition, une architecture, un installateur.</summary>
public sealed record UpdateEntry(string Edition, string Arch, string File, long Size, string Sha256, string Url);

/// <summary>
/// Logique pure de la vérification des mises à jour (sans réseau, sans WPF) : lecture de latest.json, comparaison de versions,
/// choix de l'installateur, validation d'URL, planification. Compilée aussi dans SelectBar.Tests.
/// </summary>
public static class UpdateLogic
{
    /// <summary>Hôte du site : manifeste latest.json et installateurs des clients 3.x (HTTPS, port 443 seulement).</summary>
    public const string AllowedHost = "selecto.span.observer";

    /// <summary>
    /// Hôtes autorisés pour les installateurs (HTTPS, port 443, sans identifiants) : le site, GitHub Releases et le stockage des
    /// fichiers de GitHub (cible des redirections). Le SHA-256 de latest.json reste vérifié après le téléchargement.
    /// </summary>
    public static readonly string[] AllowedHosts = { AllowedHost, "github.com", "objects.githubusercontent.com" };

    public const string ManifestUrl = "https://selecto.span.observer/downloads/latest.json";

    /// <summary>Intervalle minimal entre deux vérifications automatiques.</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    /// <summary>Après « Plus tard », la même version ne revient pas avant ce délai.</summary>
    public static readonly TimeSpan Snooze = TimeSpan.FromHours(24);

    /// <summary>
    /// Lit latest.json. Renvoie null si le JSON est invalide, si la version manque ou n'est pas une version, ou si « installers »
    /// n'est pas un tableau. Une entrée incomplète, à l'URL non autorisée ou au SHA-256 mal formé est ignorée (jamais d'exception).
    /// </summary>
    public static (string Version, IReadOnlyList<UpdateEntry> Entries, string? Notes)? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var version = Str(root, "version");
            if (version is null || !TryParseVersion(version, out _)) return null;

            if (!root.TryGetProperty("installers", out var list) || list.ValueKind != JsonValueKind.Array) return null;
            var entries = new List<UpdateEntry>();
            foreach (var item in list.EnumerateArray())
            {
                var entry = ReadEntry(item);
                if (entry is not null) entries.Add(entry);
            }
            return (version, entries, Str(root, "notes"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Vrai si <paramref name="candidate"/> est strictement plus récente que <paramref name="current"/> (3.0.1 &gt; 3.0.0, 3.1 &gt; 3.0.0, 2.9 &lt; 3.0.0).</summary>
    public static bool IsNewer(string candidate, Version current)
    {
        if (!TryParseVersion(candidate, out var v) || current is null) return false;
        return Normalize(v).CompareTo(Normalize(current)) > 0;
    }

    /// <summary>
    /// Choisit l'installateur : même architecture, entrée « Site » de préférence, sinon la première de cette architecture.
    /// Null si aucun installateur ne correspond à l'architecture.
    /// </summary>
    public static UpdateEntry? Pick(IEnumerable<UpdateEntry> entries, string arch)
    {
        var sameArch = entries.Where(e => string.Equals(e.Arch, arch, StringComparison.OrdinalIgnoreCase)).ToList();
        return sameArch.FirstOrDefault(e => string.Equals(e.Edition, "Site", StringComparison.OrdinalIgnoreCase))
               ?? sameArch.FirstOrDefault();
    }

    /// <summary>
    /// Origine supplémentaire acceptée (schéma, hôte, port), posée UNIQUEMENT par le mode capture à partir de --update-url
    /// pour rejouer une mise à jour depuis un serveur local en VM. Null en fonctionnement normal.
    /// </summary>
    public static Uri? CaptureOrigin { get; set; }

    /// <summary>HTTPS, hôte exactement l'un de <see cref="AllowedHosts"/>, port 443, sans identifiants (ou l'origine de capture, si posée).</summary>
    public static bool IsAllowedUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (CaptureOrigin is { } o && uri.Scheme == o.Scheme && string.Equals(uri.Host, o.Host, StringComparison.OrdinalIgnoreCase) && uri.Port == o.Port && uri.UserInfo.Length == 0)
            return true;
        return uri.Scheme == Uri.UriSchemeHttps
               && AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase)
               && uri.Port == 443
               && uri.UserInfo.Length == 0;
    }

    /// <summary>Nom local de l'installateur téléchargé : version + 12 premiers caractères du SHA-256 (jamais de chemin venu du serveur).</summary>
    public static string FileNameFor(UpdateInfo info) =>
        "SelectoAI-Setup-" + info.Version + "-" + info.Sha256[..12] + ".exe";

    /// <summary>Vérifie maintenant si l'intervalle de 24 h est écoulé (jamais vérifié, ou horloge reculée).</summary>
    public static bool ShouldCheck(DateTime? lastCheckUtc, DateTime nowUtc)
    {
        if (lastCheckUtc is null) return true;
        var last = lastCheckUtc.Value;
        if (last > nowUtc) return true;
        return nowUtc - last >= CheckInterval;
    }

    /// <summary>
    /// Faut-il afficher la notification pour cette version ? Oui si elle n'a jamais été refusée ; après « Plus tard », non avant 24 h.
    /// </summary>
    public static bool ShouldNotify(string version, string? lastDismissedVersion, DateTime? dismissedAtUtc, DateTime nowUtc)
    {
        if (!string.Equals(version, lastDismissedVersion, StringComparison.OrdinalIgnoreCase)) return true;
        if (dismissedAtUtc is null) return true;
        var since = nowUtc - dismissedAtUtc.Value;
        return since < TimeSpan.Zero || since >= Snooze;
    }

    // ------------------------------------------------------------------ outils

    private static UpdateEntry? ReadEntry(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object) return null;
        var edition = Str(e, "edition");
        var arch = Str(e, "arch");
        var file = Str(e, "file");
        var url = Str(e, "url");
        var sha = Str(e, "sha256")?.ToLowerInvariant();
        if (edition is null || arch is null || file is null || url is null || sha is null) return null;
        if (!IsSha256(sha)) return null;
        if (!e.TryGetProperty("size", out var size) || size.ValueKind != JsonValueKind.Number || !size.TryGetInt64(out var bytes) || bytes <= 0) return null;
        if (!IsAllowedUrl(url)) return null;
        return new UpdateEntry(edition, arch.ToLowerInvariant(), file, bytes, sha, url);
    }

    /// <summary>Texte d'une propriété ; null si absente, pas une chaîne, ou vide.</summary>
    private static string? Str(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.String) return null;
        var s = p.GetString();
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    private static bool IsSha256(string s) =>
        s.Length == 64 && s.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));

    /// <summary>« 3.0.1 », « v3.1 », « 3.0.0.0 » : versions numériques seulement (une version préliminaire « -beta » est refusée).</summary>
    private static bool TryParseVersion(string text, out Version version)
    {
        version = new Version(0, 0);
        var s = text.Trim();
        if (s.StartsWith('v') || s.StartsWith('V')) s = s[1..];
        if (s.Length == 0 || !s.All(c => char.IsDigit(c) || c == '.')) return false;
        if (!Version.TryParse(s, out var parsed)) return false;
        version = parsed;
        return true;
    }

    /// <summary>Trois composantes (majeure, mineure, build) ; les composantes manquantes valent 0.</summary>
    private static Version Normalize(Version v) => new(v.Major, Math.Max(0, v.Minor), Math.Max(0, v.Build));
}
