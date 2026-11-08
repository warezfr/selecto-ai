using SelectBar.Selection;
using Xunit;

namespace SelectBar.Tests;

public class SelectionTests
{
    [Fact]
    public void Uia_text_wins() => Assert.Equal(SelectionSource.Uia, SelectionDecision.Decide(UiaOutcome.Text, "notepad.exe"));

    [Theory]
    [InlineData(UiaOutcome.NoTextPattern)]
    [InlineData(UiaOutcome.Timeout)]
    [InlineData(UiaOutcome.Error)]
    public void Failures_fall_back_to_clipboard(UiaOutcome o) =>
        Assert.Equal(SelectionSource.Clipboard, SelectionDecision.Decide(o, "notepad.exe"));

    [Fact]
    public void Empty_selection_is_trusted_in_native_apps() =>
        Assert.Equal(SelectionSource.None, SelectionDecision.Decide(UiaOutcome.EmptySelection, "notepad.exe"));

    [Theory]
    [InlineData("chrome.exe")]
    [InlineData("Code.exe")]
    public void Empty_selection_falls_back_in_browsers_and_electron(string exe) =>
        Assert.Equal(SelectionSource.Clipboard, SelectionDecision.Decide(UiaOutcome.EmptySelection, exe));

    [Fact]
    public void Unknown_exe_empty_selection_is_none() =>
        Assert.Equal(SelectionSource.None, SelectionDecision.Decide(UiaOutcome.EmptySelection, null));

    [Fact]
    public void Limits()
    {
        Assert.False(TextLimits.IsTooLarge("abc"));
        Assert.True(TextLimits.IsTooLarge(new string('é', TextLimits.MaxTextBytes / 2 + 1)));
        Assert.Equal(TextLimits.MaxProcessChars, TextLimits.Truncate(new string('a', 100_000)).Length);
        var p = TextLimits.Preview(new string('a', 500));
        Assert.Equal(TextLimits.PreviewChars, p.Length);
        Assert.EndsWith("…", p);
        Assert.Equal("a  b", TextLimits.Preview("a\r\nb"));
    }
}
