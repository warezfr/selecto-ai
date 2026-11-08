using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SelectBar.Actions;
using SelectBar.Settings;
using Xunit;

namespace SelectBar.Tests;

public class CopilotTests
{
    [Theory]
    [InlineData(CopilotKind.Ask, "Bonjour")]
    [InlineData(CopilotKind.Summarize, "Résume ce texte : Bonjour")]
    [InlineData(CopilotKind.Explain, "Explique ce texte : Bonjour")]
    [InlineData(CopilotKind.Translate, "Traduis ce texte en français : Bonjour")]
    [InlineData(CopilotKind.Rephrase, "Reformule ce texte : Bonjour")]
    public void Prompts(CopilotKind kind, string expected) =>
        Assert.Equal(expected, CopilotPrompts.BuildPrompt(kind, "  Bonjour \n"));

    [Fact]
    public void Url_is_encoded()
    {
        var p = CopilotPrompts.Plan(CopilotKind.Summarize, "Bonjour le monde");
        Assert.False(p.NeedsClipboard);
        Assert.Equal("https://copilot.microsoft.com/?q=R%C3%A9sume%20ce%20texte%20%3A%20Bonjour%20le%20monde", p.Url);
    }

    [Fact]
    public void Selected_text_cannot_inject_query_parameters_or_fragments()
    {
        var p = CopilotPrompts.Plan(CopilotKind.Ask, "a&b=c#d?e https://evil.example/x");
        Assert.StartsWith("https://copilot.microsoft.com/?q=", p.Url);
        var query = p.Url.Substring("https://copilot.microsoft.com/?q=".Length);
        Assert.DoesNotContain('&', query);
        Assert.DoesNotContain('#', query);
        Assert.DoesNotContain('?', query);
        Assert.DoesNotContain('/', query);
        Assert.Equal("a&b=c#d?e https://evil.example/x", Uri.UnescapeDataString(query));
    }

    [Fact]
    public void Long_text_goes_through_the_clipboard()
    {
        var ok = CopilotPrompts.Plan(CopilotKind.Ask, new string('a', 1500) + "   ");
        Assert.False(ok.NeedsClipboard);
        var text = new string('b', 1501);
        var p = CopilotPrompts.Plan(CopilotKind.Explain, text);
        Assert.True(p.NeedsClipboard);
        Assert.Equal(CopilotPrompts.BaseUrl, p.Url);
        Assert.Equal("Explique ce texte : " + text, p.Prompt);
    }

    [Fact]
    public void Correct_prompt() =>
        Assert.Equal("Corrige ce texte : Bonjour", CopilotPrompts.BuildPrompt(CopilotKind.Correct, "Bonjour"));
}
