using System.Threading.Tasks;
using SelectBar.Actions;
using SelectBar.Extensions;
using Xunit;

namespace SelectBar.Tests;

public class ExtensionHeaderParserTests
{
    private const string Good = @"// ==SelectBarExtension==
// @name MAJUSCULES
// @identifier com.selectbar.sample.uppercase
// @icon AA
// @regex /[a-z]/
// @requirements text, paste
// @apps Chrome.exe, code.exe
// ==/SelectBarExtension==
selecto.pasteText(selecto.text.toUpperCase());";

    [Fact]
    public void Parses_header()
    {
        Assert.True(ExtensionHeaderParser.TryParse(Good, out var m, out var err), err);
        Assert.Equal("MAJUSCULES", m!.Name);
        Assert.Equal("com.selectbar.sample.uppercase", m.Identifier);
        Assert.Equal("AA", m.Icon);
        Assert.Equal("[a-z]", m.Regex);
        Assert.Equal(new[] { "text", "paste" }, m.Requirements);
        Assert.Equal(new[] { "chrome.exe", "code.exe" }, m.Apps);
    }

    [Fact]
    public void Accepts_the_new_selecto_marker_and_the_legacy_one()
    {
        var selecto = Good.Replace("SelectBarExtension", "SelectoExtension");
        Assert.True(ExtensionHeaderParser.TryParse(selecto, out var m, out var err), err);
        Assert.Equal("MAJUSCULES", m!.Name);
        // marqueurs mélangés (début ancien, fin nouvelle) : tolérés aussi
        Assert.True(ExtensionHeaderParser.TryParse(Good.Replace("==/SelectBarExtension==", "==/SelectoExtension=="), out _, out _));
        Assert.False(ExtensionHeaderParser.TryParse(Good.Replace("SelectBarExtension", "AutreExtension"), out _, out _));
    }

    [Fact]
    public void Reads_translated_names_parent_default_and_inline()
    {
        // pas de @name nu : @name:fr sert de nom par défaut
        const string src = "// ==SelectoExtension==\n// @name:fr MAJUSCULES\n// @name:en UPPERCASE\n// @name:es MAYÚSCULAS\n// @name:ar أحرف كبيرة\n// @identifier ai.selecto.builtin.text.upper\n// @parent ai.selecto.builtin.text\n// @enabled-by-default false\n// @inline true\n// ==/SelectoExtension==\nselecto.text;";
        Assert.True(ExtensionHeaderParser.TryParse(src, out var m, out var err), err);
        Assert.Equal("MAJUSCULES", m!.Name);
        Assert.Equal("UPPERCASE", m.Names["en"]);
        Assert.Equal("MAYÚSCULAS", m.Names["es"]);
        Assert.Equal("أحرف كبيرة", m.Names["ar"]);
        Assert.False(m.Names.ContainsKey("name"));
        Assert.Equal("ai.selecto.builtin.text", m.Parent);
        Assert.False(m.EnabledByDefault);
        Assert.True(m.Inline);
    }

    [Fact]
    public void Defaults_are_enabled_and_not_inline_when_absent()
    {
        Assert.True(ExtensionHeaderParser.TryParse(Good, out var m, out _));
        Assert.True(m!.EnabledByDefault);
        Assert.False(m.Inline);
        Assert.Null(m.Parent);
        Assert.Empty(m.Names);
    }

    [Fact]
    public void Handles_crlf() => Assert.True(ExtensionHeaderParser.TryParse(Good.Replace("\n", "\r\n"), out _, out _));

    [Theory]
    [InlineData("selecto.showText('x');")]
    [InlineData("// ==SelectBarExtension==\n// @name A\n// @identifier a")]
    [InlineData("// ==SelectBarExtension==\n// @identifier a\n// ==/SelectBarExtension==")]
    [InlineData("// ==SelectBarExtension==\n// @name A\n// ==/SelectBarExtension==")]
    public void Rejects_invalid(string src)
    {
        Assert.False(ExtensionHeaderParser.TryParse(src, out var m, out var err));
        Assert.Null(m);
        Assert.False(string.IsNullOrEmpty(err));
    }

    [Fact]
    public void Mapper_builds_filters()
    {
        ExtensionHeaderParser.TryParse(Good, out var m, out _);
        Assert.True(ExtensionMapper.TryMap(m!, _ => Task.CompletedTask, out var a, out var err), err);
        Assert.Equal(ContextRequirement.EditableOnly, a!.Context);
        Assert.True(ActionFilter.Matches(a, TextClassifier.BuildContext("abc", true, "chrome.exe", _ => false)));
        Assert.False(ActionFilter.Matches(a, TextClassifier.BuildContext("abc", false, "chrome.exe", _ => false)));
        Assert.False(ActionFilter.Matches(a, TextClassifier.BuildContext("ABC", true, "chrome.exe", _ => false)));
        Assert.False(ActionFilter.Matches(a, TextClassifier.BuildContext("abc", true, "notepad.exe", _ => false)));
    }

    [Fact]
    public void Mapper_rejects_unknown_requirement_and_bad_regex()
    {
        var bad1 = new ExtensionMetadata("n", "i", null, null, new[] { "bogus" }, new string[0]);
        Assert.False(ExtensionMapper.TryMap(bad1, _ => Task.CompletedTask, out _, out var e1));
        Assert.Contains("bogus", e1);
        var bad2 = new ExtensionMetadata("n", "i", null, "([", new string[0], new string[0]);
        Assert.False(ExtensionMapper.TryMap(bad2, _ => Task.CompletedTask, out _, out _));
    }

    private static string WithHeader(params string[] fields) =>
        "// ==SelectoExtension==\n" + string.Join("\n", fields) + "\n// @identifier ai.test.desc\n// ==/SelectoExtension==\nselecto.text;";

    [Fact]
    public void Description_is_optional_and_null_when_absent()
    {
        Assert.True(ExtensionHeaderParser.TryParse(Good, out var m, out _));
        Assert.Null(m!.Description);
        Assert.Null(m.DescriptionFor("fr"));
    }

    [Fact]
    public void Description_falls_back_like_the_name()
    {
        // pas de @description nu : le français sert de défaut
        Assert.True(ExtensionHeaderParser.TryParse(WithHeader("// @name A", "// @description:fr Ma phrase.", "// @description:en My sentence."), out var m, out var err), err);
        Assert.Equal("Ma phrase.", m!.Description);
        Assert.Equal("My sentence.", m.DescriptionFor("en"));
        Assert.Equal("Ma phrase.", m.DescriptionFor("es"));  // langue absente : français
        Assert.Equal("Ma phrase.", m.DescriptionFor("ar"));

        // un @description nu l'emporte sur le repli
        Assert.True(ExtensionHeaderParser.TryParse(WithHeader("// @name A", "// @description Nu.", "// @description:fr Français."), out m, out _));
        Assert.Equal("Nu.", m!.Description);
        Assert.Equal("Français.", m.DescriptionFor("fr"));
        Assert.Equal("Français.", m.DescriptionFor("es"));  // langue absente : français, avant le descriptif nu

        // sans @description:fr, le premier descriptif localisé sert de défaut
        Assert.True(ExtensionHeaderParser.TryParse(WithHeader("// @name A", "// @description:en Only English."), out m, out _));
        Assert.Equal("Only English.", m!.Description);
        Assert.Equal("Only English.", m.DescriptionFor("ar"));
    }

    [Fact]
    public void Description_is_truncated_to_200_characters()
    {
        var long1 = new string('a', 300);
        Assert.True(ExtensionHeaderParser.TryParse(WithHeader("// @name A", "// @description:fr " + long1), out var m, out _));
        var shown = m!.DescriptionFor("fr")!;
        Assert.Equal(ExtensionHeaderParser.MaxDescriptionLength, shown.Length);
        Assert.EndsWith("…", shown);
        Assert.Equal(200, ExtensionHeaderParser.Truncate(new string('b', 200)).Length);
        Assert.Equal(new string('b', 200), ExtensionHeaderParser.Truncate(new string('b', 200)));
    }

    [Fact]
    public void Description_is_read_from_crlf_headers()
    {
        var src = WithHeader("// @name A", "// @description:fr Texte.").Replace("\n", "\r\n");
        Assert.True(ExtensionHeaderParser.TryParse(src, out var m, out _));
        Assert.Equal("Texte.", m!.DescriptionFor("fr"));
    }
}
