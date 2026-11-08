using System.Threading.Tasks;
using SelectBar.Extensions;
using SelectBar.Localization;
using Xunit;

namespace SelectBar.Tests;

[Collection("Localization")]
public class ExtensionMapperTests
{
    private const string Source = @"// ==SelectoExtension==
// @name MAJUSCULES
// @name:fr MAJUSCULES
// @name:en UPPERCASE
// @name:es MAYÚSCULAS
// @identifier ai.selecto.builtin.text.upper
// @parent ai.selecto.builtin.text
// @enabled-by-default false
// ==/SelectoExtension==
selecto.pasteText(selecto.text.toUpperCase());";

    private static ExtensionMetadata Parse()
    {
        Assert.True(ExtensionHeaderParser.TryParse(Source, out var m, out var err), err);
        return m!;
    }

    [Fact]
    public void Title_follows_the_ui_culture_when_translated()
    {
        L.Culture = UiCulture.Make("es");
        Assert.True(ExtensionMapper.TryMap(Parse(), _ => Task.CompletedTask, out var a, out _));
        Assert.Equal("MAYÚSCULAS", a!.Title);
        L.Culture = UiCulture.Make("en");
        Assert.True(ExtensionMapper.TryMap(Parse(), _ => Task.CompletedTask, out a, out _));
        Assert.Equal("UPPERCASE", a!.Title);
    }

    [Fact]
    public void Arabic_without_name_ar_falls_back_to_french()
    {
        L.Culture = UiCulture.Make("ar");
        Assert.True(ExtensionMapper.TryMap(Parse(), _ => Task.CompletedTask, out var a, out _));
        Assert.Equal("MAJUSCULES", a!.Title);
    }

    [Fact]
    public void Falls_back_to_name_when_no_translation_matches()
    {
        Assert.True(ExtensionHeaderParser.TryParse(Source.Replace("// @name:fr MAJUSCULES\n", "").Replace("// @name:en UPPERCASE\n", "").Replace("// @name:es MAYÚSCULAS\n", ""), out var m, out _));
        L.Culture = UiCulture.Make("en");
        Assert.True(ExtensionMapper.TryMap(m!, _ => Task.CompletedTask, out var a, out _));
        Assert.Equal("MAJUSCULES", a!.Title);
    }

    [Fact]
    public void Parent_and_identifier_reach_the_action()
    {
        L.Culture = UiCulture.Make("fr");
        Assert.True(ExtensionMapper.TryMap(Parse(), _ => Task.CompletedTask, out var a, out _));
        Assert.Equal("ai.selecto.builtin.text", a!.ParentId);
        Assert.Equal("ai.selecto.builtin.text.upper", a.Id);
    }
}
