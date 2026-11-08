using System.Linq;
using SelectBar.UI;
using Xunit;

namespace SelectBar.Tests;

public class AboutLinksTests
{
    [Fact]
    public void Three_https_links_with_their_label_keys()
    {
        var links = AboutLinks.For("fr");
        Assert.Equal(3, links.Count);
        Assert.Equal(new[] { "About_LinkSite", "About_LinkPrivacy", "About_LinkSource" }, links.Select(l => l.LabelKey));
        Assert.All(links, l => Assert.StartsWith("https://", l.Url));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("en-US")]
    public void English_links_use_the_english_pages(string lang)
    {
        var links = AboutLinks.For(lang);
        Assert.Equal(new[]
        {
            "https://selecto.span.observer/en/",
            "https://selecto.span.observer/en/privacy/",
            "https://github.com/warezfr/selecto-ai",
        }, links.Select(l => l.Url));
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("es")]
    [InlineData("ar")]
    public void Other_languages_use_the_french_pages(string lang)
    {
        var links = AboutLinks.For(lang);
        Assert.Equal(new[]
        {
            "https://selecto.span.observer/",
            "https://selecto.span.observer/confidentialite/",
            "https://github.com/warezfr/selecto-ai",
        }, links.Select(l => l.Url));
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("fr-FR")]
    public void Extensions_catalogue_is_french_for_french(string lang)
    {
        Assert.Equal("https://selecto.span.observer/extensions/", AboutLinks.ExtensionsCatalogUrl(lang));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("en-US")]
    [InlineData("es")]
    [InlineData("ar")]
    [InlineData("")]
    [InlineData(null)]
    public void Extensions_catalogue_is_english_otherwise(string? lang)
    {
        Assert.Equal("https://selecto.span.observer/en/extensions/", AboutLinks.ExtensionsCatalogUrl(lang));
    }
}
