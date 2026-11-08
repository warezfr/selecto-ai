using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SelectBar.Actions;
using SelectBar.Extensions;
using SelectBar.Localization;
using Xunit;

namespace SelectBar.Tests;

/// <summary>Exécute les extensions intégrées (BuiltInExtensions/*.js) dans Jint avec un faux hôte.</summary>
[Collection("Localization")]
public class TextTransformsTests
{
    private const string TextParent = "ai.selecto.builtin.text";

    private sealed class FakeApi : IExtensionApi
    {
        public readonly List<string> Copied = new();
        public readonly List<string> Pasted = new();
        public readonly List<string> Shown = new();
        public readonly List<string> Results = new();
        public void CopyText(string text) => Copied.Add(text);
        public void PasteText(string text) => Pasted.Add(text);
        public void OpenUrl(string url) { }
        public void ShowText(string text) => Shown.Add(text);
        public void ShowResult(string text, bool isEditable) => Results.Add(text);
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SelectBar.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string ReadScript(string fileName) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "SelectBar", "BuiltInExtensions", fileName));

    private static FakeApi RunNormal(string fileName, string text, bool editable)
    {
        var api = new FakeApi();
        ExtensionHost.Run(ReadScript(fileName), new ActionContext(text, editable, null), api);
        return api;
    }

    [Fact]
    public void Uppercase_replaces_an_editable_selection_and_says_so_briefly()
    {
        L.Culture = UiCulture.Make("fr");
        var api = RunNormal("text-upper.js", "hello world", editable: true);
        Assert.Equal(new[] { "HELLO WORLD" }, api.Pasted);
        Assert.Equal(new[] { "Remplacé" }, api.Shown);
    }

    [Fact]
    public void Title_case_copies_when_the_selection_is_read_only()
    {
        L.Culture = UiCulture.Make("fr");
        var api = RunNormal("text-title.js", "hello world", editable: false);
        Assert.Equal(new[] { "Hello World" }, api.Copied);
        Assert.Equal(new[] { "Copié" }, api.Shown);
    }

    [Fact]
    public void Sentence_case_capitalises_each_sentence()
    {
        L.Culture = UiCulture.Make("fr");
        var api = RunNormal("text-sentence.js", "bonjour. ça va", editable: true);
        Assert.Equal(new[] { "Bonjour. Ça va" }, api.Pasted);
    }

    [Fact]
    public void Lowercase_uses_the_selection_text()
    {
        L.Culture = UiCulture.Make("en");
        var api = RunNormal("text-lower.js", "HeLLo", editable: false);
        Assert.Equal(new[] { "hello" }, api.Copied);
        Assert.Equal(new[] { "Copied" }, api.Shown);
    }

    [Fact]
    public void Double_spaces_are_collapsed_but_line_breaks_are_kept()
    {
        L.Culture = UiCulture.Make("fr");
        var api = RunNormal("text-spaces.js", "a  b\n\nc", editable: true);
        Assert.Equal(new[] { "a b\n\nc" }, api.Pasted);
    }

    [Fact]
    public void Join_lines_puts_each_non_empty_line_on_one_line()
    {
        L.Culture = UiCulture.Make("fr");
        var api = RunNormal("text-join-lines.js", "a\nb", editable: true);
        Assert.Equal(new[] { "a b" }, api.Pasted);
    }

    [Fact]
    public void Sort_lines_orders_them_alphabetically()
    {
        L.Culture = UiCulture.Make("fr");
        var api = RunNormal("text-sort-lines.js", "b\na", editable: true);
        Assert.Equal(new[] { "a\nb" }, api.Pasted);
    }

    [Fact]
    public void Count_is_shown_inline_in_french()
    {
        L.Culture = UiCulture.Make("fr");
        var text = ExtensionHost.RunInline(ReadScript("count.js"), new ActionContext("un deux trois", false, null), false, TimeSpan.FromSeconds(5));
        Assert.Equal("3 mots · 13 caractères · < 1 min", text);
    }

    [Fact]
    public void Calc_is_shown_inline_and_nothing_is_shown_for_a_date()
    {
        L.Culture = UiCulture.Make("fr");
        var calc = ReadScript("calc.js");
        var timeout = TimeSpan.FromSeconds(5);   // 50 ms en production ; le premier appel de Jint peut dépasser 50 ms sous test
        Assert.Equal("= 1 500", ExtensionHost.RunInline(calc, new ActionContext("1250*1,2", false, null), false, timeout));
        Assert.Null(ExtensionHost.RunInline(calc, new ActionContext("2024-10-24", false, null), false, timeout));
    }

    [Fact]
    public void Calc_click_shows_the_bare_result_in_the_popup()
    {
        L.Culture = UiCulture.Make("fr");
        var api = new FakeApi();
        ExtensionHost.Run(ReadScript("calc.js"), new ActionContext("1250*1,2", true, null), api);
        Assert.Equal(new[] { "1 500" }, api.Results);
    }

    [Fact]
    public void Every_text_action_is_a_disabled_child_of_the_text_submenu()
    {
        var root = RepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "SelectBar", "BuiltInExtensions"), "text-*.js");
        Assert.Equal(7, files.Length);
        foreach (var file in files)
        {
            Assert.True(ExtensionHeaderParser.TryParse(File.ReadAllText(file), out var meta, out var err), err);
            Assert.Equal(TextParent, meta!.Parent);
            Assert.False(meta.EnabledByDefault);
            Assert.Contains("text", meta.Requirements);
            Assert.False(meta.Inline);
        }
    }

    [Fact]
    public void The_text_submenu_has_a_header_and_no_code()
    {
        Assert.True(ExtensionHeaderParser.TryParse(ReadScript("text.js"), out var meta, out var err), err);
        Assert.Equal(TextParent, meta!.Identifier);
        Assert.Null(meta.Parent);
        Assert.Equal("نص", meta.Names["ar"]);
        var code = ReadScript("text.js").Split('\n').Where(l => l.Trim().Length > 0 && !l.Trim().StartsWith("//", StringComparison.Ordinal));
        Assert.Empty(code);
    }

    [Fact]
    public void Count_and_calc_are_inline_actions()
    {
        Assert.True(ExtensionHeaderParser.TryParse(ReadScript("count.js"), out var count, out _));
        Assert.True(count!.Inline);
        Assert.True(ExtensionHeaderParser.TryParse(ReadScript("calc.js"), out var calc, out _));
        Assert.True(calc!.Inline);
        Assert.False(calc.EnabledByDefault);
        Assert.NotNull(calc.Regex);
        Assert.True(new Regex(calc.Regex!).IsMatch("1250*1,2"));
        Assert.False(new Regex(calc.Regex!).IsMatch("hello"));
    }
}
