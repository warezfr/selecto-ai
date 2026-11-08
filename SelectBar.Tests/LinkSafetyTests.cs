using SelectBar.Actions;
using Xunit;

namespace SelectBar.Tests;

public class LinkSafetyTests
{
    [Theory]
    [InlineData("https://example.com", "https://example.com/")]
    [InlineData("http://a.fr/x?y=1", "http://a.fr/x?y=1")]
    [InlineData("example.com", "https://example.com/")]
    [InlineData("www.exemple.com/x", "https://www.exemple.com/x")]
    [InlineData("mailto:a@b.fr", "mailto:a@b.fr")]
    public void Web_targets(string url, string expected)
    {
        Assert.Equal(LinkKind.Web, LinkSafety.Check(url, out var target));
        Assert.Equal(expected, target);
    }

    [Fact]
    public void Bare_domain_with_port_is_web() =>
        Assert.Equal(LinkKind.Web, LinkSafety.Check("exemple.com:8080/x", out _));

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hi")]
    [InlineData("vbscript:x")]
    [InlineData("ftp://x.fr")]
    [InlineData("ms-copilot:")]
    [InlineData("ms-settings:")]
    [InlineData("shell:startup")]
    [InlineData("search-ms:query=x")]
    [InlineData(@"C:\Windows\System32\calc.exe")]
    [InlineData("calc.exe")]
    [InlineData("cmd /c calc")]
    [InlineData(@"\\server\share\x.exe")]
    [InlineData("http://")]
    [InlineData("https://\n")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("mailto:nope")]
    [InlineData("file:///C:/a<b")]
    public void Rejected(string url)
    {
        Assert.Equal(LinkKind.Rejected, LinkSafety.Check(url, out var target));
        Assert.Equal("", target);
    }

    [Fact]
    public void Control_characters_are_rejected() =>
        Assert.Equal(LinkKind.Rejected, LinkSafety.Check("https://a.fr/\u0000x", out _));

    [Fact]
    public void File_urls_become_local_paths_to_reveal_only()
    {
        Assert.Equal(LinkKind.LocalPath, LinkSafety.Check("file:///C:/Temp/run.exe", out var t));
        Assert.Equal(@"C:\Temp\run.exe", t);
        Assert.Equal(LinkKind.LocalPath, LinkSafety.Check("file://srv/share/a.txt", out var u));
        Assert.Equal(@"\\srv\share\a.txt", u);
    }

    [Fact]
    public void Explorer_arguments_select_files_never_open_them()
    {
        // même un exécutable est seulement sélectionné
        Assert.Equal("/select,\"C:\\Temp\\a.exe\"", LinkSafety.ExplorerArguments(@"C:\Temp\a.exe", p => p == @"C:\Temp\a.exe", _ => false));
        Assert.Equal("/select,\"C:\\Temp\\s.ps1\"", LinkSafety.ExplorerArguments(@"C:\Temp\s.ps1", _ => true, _ => true));
    }

    [Fact]
    public void Explorer_arguments_folder_parent_or_nothing()
    {
        Assert.Equal("\"C:\\Temp\"", LinkSafety.ExplorerArguments(@"C:\Temp", _ => false, p => p == @"C:\Temp"));
        Assert.Equal("\"C:\\Temp\"", LinkSafety.ExplorerArguments(@"C:\Temp\gone.txt", _ => false, p => p == @"C:\Temp"));
        Assert.Null(LinkSafety.ExplorerArguments(@"C:\Nope\gone.txt", _ => false, _ => false));
    }

    [Theory]
    [InlineData("C:\\a\" /e,calc")]
    [InlineData(@"C:\a|b")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("calc.exe")]
    public void Explorer_arguments_reject_unsafe_paths(string? path) =>
        Assert.Null(LinkSafety.ExplorerArguments(path, _ => true, _ => true));
}
