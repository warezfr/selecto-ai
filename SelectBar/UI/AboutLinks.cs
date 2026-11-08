using System;
using System.Collections.Generic;
using SelectBar.Packaging;

namespace SelectBar.UI;

/// <summary>Un lien de la page À propos : libellé (clé de ressource) et adresse.</summary>
public sealed record AboutLink(string LabelKey, string Url);

// Liens de la page À propos, selon la langue (logique pure, testée).
public static class AboutLinks
{
    private const string SiteFr = "https://selecto.span.observer/";
    private const string SiteEn = "https://selecto.span.observer/en/";
    private const string PrivacyFr = "https://selecto.span.observer/confidentialite/";
    private const string PrivacyEn = "https://selecto.span.observer/en/privacy/";

    private const string ExtensionsFr = "https://selecto.span.observer/extensions/";
    private const string ExtensionsEn = "https://selecto.span.observer/en/extensions/";

    /// <summary>Catalogue des extensions du site : français si la langue est fr, anglais sinon.</summary>
    public static string ExtensionsCatalogUrl(string? lang) => IsFrench(lang) ? ExtensionsFr : ExtensionsEn;

    /// <summary>Liens Site, Confidentialité, Code source (GitHub). Langue « en » : pages anglaises ; autre langue : pages françaises. La licence MIT s'affiche dans l'application.</summary>
    public static IReadOnlyList<AboutLink> For(string lang = "fr")
    {
        var en = IsEnglish(lang);
        return new[]
        {
            new AboutLink("About_LinkSite", en ? SiteEn : SiteFr),
            new AboutLink("About_LinkPrivacy", en ? PrivacyEn : PrivacyFr),
            new AboutLink("About_LinkSource", EditionInfo.GitHubUrl),
        };
    }

    private static bool IsFrench(string? lang) =>
        string.Equals((lang ?? "").Split('-')[0], "fr", StringComparison.OrdinalIgnoreCase);

    private static bool IsEnglish(string? lang) =>
        string.Equals((lang ?? "").Split('-')[0], "en", StringComparison.OrdinalIgnoreCase);
}
