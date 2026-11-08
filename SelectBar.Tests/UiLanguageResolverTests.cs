using System.Linq;
using SelectBar.Localization;
using Xunit;

namespace SelectBar.Tests;

public class UiLanguageResolverTests
{
    [Theory]
    [InlineData("auto", "fr-FR", "fr")]
    [InlineData("auto", "es-MX", "es")]
    [InlineData("auto", "ar-MA", "ar")]
    [InlineData("auto", "he-IL", "en")]
    [InlineData("auto", "", "en")]
    [InlineData("ar", "fr-FR", "ar")]
    [InlineData("xx", "fr-FR", "en")]
    public void Resolve(string setting, string win, string expected) => Assert.Equal(expected, UiLanguageResolver.Resolve(setting, win));

    [Fact]
    public void Null_windows_culture_gives_english()
    {
        Assert.Equal("en", UiLanguageResolver.Resolve("auto", null));
    }

    [Fact]
    public void UiLanguage_choices_are_the_supported_codes()
    {
        Assert.Equal(new[] { "auto" }.Concat(UiLanguageResolver.Supported), UiLanguageChoices.Codes);
    }

    [Theory]
    [InlineData("", "auto")]
    [InlineData("AR", "ar")]
    [InlineData("de", "auto")]
    public void Choice_normalizes_unknown_values_to_auto(string setting, string expected) =>
        Assert.Equal(expected, UiLanguageChoices.Normalize(setting));
}
