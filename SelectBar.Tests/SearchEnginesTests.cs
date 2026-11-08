using SelectBar.Actions;
using Xunit;

namespace SelectBar.Tests;

public class SearchEnginesTests
{
    [Fact]
    public void Eight_engines() => Assert.Equal(8, SearchEngines.All.Count);

    [Theory]
    [InlineData("google", "https://www.google.com/search?q=caf%C3%A9%20%26%20th%C3%A9")]
    [InlineData("duckduckgo", "https://duckduckgo.com/?q=caf%C3%A9%20%26%20th%C3%A9")]
    [InlineData("yahoo", "https://search.yahoo.com/search?p=caf%C3%A9%20%26%20th%C3%A9")]
    public void BuildUrl_encodes(string id, string expected) =>
        Assert.Equal(expected, SearchEngines.BuildUrl(id, "café & thé"));

    [Fact]
    public void Unknown_engine_falls_back_to_google() =>
        Assert.StartsWith("https://www.google.com/", SearchEngines.BuildUrl("nope", "x"));

    [Fact]
    public void Custom_template_replaces_all_placeholders() =>
        Assert.Equal("https://x.test/?a=a%20b&b=a%20b", SearchEngines.BuildUrl("custom", "a b", "https://x.test/?a=***&b=***"));

    [Fact]
    public void Custom_without_placeholder_falls_back() =>
        Assert.StartsWith("https://www.google.com/", SearchEngines.BuildUrl("custom", "x", "https://x.test/"));
}
