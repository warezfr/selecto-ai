using SelectBar.Actions;
using Xunit;

namespace SelectBar.Tests;

public class TextClassifierTests
{
    [Theory]
    [InlineData("https://example.com/a?b=1", true)]
    [InlineData("http://localhost:3000", true)]
    [InlineData("www.example.org", true)]
    [InlineData("maweb.page/demo", true)]
    [InlineData("example.com", true)]
    [InlineData("mailto:a@b.fr", true)]
    [InlineData("report.pdf", false)]
    [InlineData("hello world.com", false)]
    [InlineData("bonjour", false)]
    [InlineData("", false)]
    [InlineData("v1.2.3", false)]
    public void IsUrl(string text, bool expected) => Assert.Equal(expected, TextClassifier.IsUrl(text));

    [Theory]
    [InlineData("example.com", "https://example.com")]
    [InlineData("https://a.fr", "https://a.fr")]
    [InlineData("mailto:a@b.fr", "mailto:a@b.fr")]
    public void NormalizeUrl(string input, string expected) => Assert.Equal(expected, TextClassifier.NormalizeUrl(input));

    [Fact]
    public void IsPath_requires_existing_file()
    {
        Assert.True(TextClassifier.IsPath(@"C:\Users\me\file.txt", _ => true));
        Assert.False(TextClassifier.IsPath(@"C:\Users\me\file.txt", _ => false));
        Assert.True(TextClassifier.IsPath("\"C:\\Temp\\a b.txt\"", _ => true));
        Assert.True(TextClassifier.IsPath(@"\\server\share\x", _ => true));
        Assert.False(TextClassifier.IsPath("not a path", _ => true));
    }

    [Fact]
    public void BuildContext_sets_flags()
    {
        var c = TextClassifier.BuildContext("https://a.fr", false, "chrome.exe", _ => false);
        Assert.True(c.IsUrl);
        Assert.False(c.IsPath);
    }
}
