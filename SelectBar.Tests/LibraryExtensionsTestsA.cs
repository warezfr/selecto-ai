using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SelectBar.Actions;
using SelectBar.Extensions;
using SelectBar.Localization;
using Xunit;

namespace SelectBar.Tests;

/// <summary>Lot A de la bibliothèque (extensions-library/) : exécute chaque .js avec Jint et un faux hôte.</summary>
[Collection("Localization")]
public class LibraryExtensionsTestsA
{
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

    private static FakeApi Run(string relativePath, string text, bool editable = true, string locale = "fr")
    {
        L.Culture = UiCulture.Make(locale);
        var script = File.ReadAllText(Path.Combine(RepositoryRoot(), "extensions-library", relativePath));
        var api = new FakeApi();
        ExtensionHost.Run(script, new ActionContext(text, editable, null), api);
        return api;
    }

    // ---- Zigzag -------------------------------------------------------------

    [Theory]
    [InlineData("alterne la casse", "AlTeRnE lA cAsSe")]
    [InlineData("", "")]
    [InlineData("a", "A")]
    [InlineData("Été 2026 😀 مرحبا بالعالم", "ÉtÉ 2026 😀 مرحبا بالعالم")]
    [InlineData("Hello, World!", "HeLlO, wOrLd!")]
    public void Zigzag_alternates_case_over_letters_only(string input, string expected)
    {
        Assert.Equal(new[] { expected }, Run("zigzag/zigzag.js", input).Pasted);
    }

    [Fact]
    public void Zigzag_copies_when_the_selection_is_read_only_and_says_so_in_english()
    {
        var api = Run("zigzag/zigzag.js", "ab", editable: false, locale: "en");
        Assert.Equal(new[] { "Ab" }, api.Copied);
        Assert.Empty(api.Pasted);
        Assert.Equal(new[] { "Copied" }, api.Shown);
    }

    // ---- Encadrer (5 enfants) ------------------------------------------------

    [Theory]
    [InlineData("texte", "(texte)")]
    [InlineData("", "()")]
    [InlineData("ligne1\nligne2", "(ligne1\nligne2)")]
    [InlineData("😀 مرحبا بالعالم", "(😀 مرحبا بالعالم)")]
    public void Encadrer_parentheses_wraps_the_whole_selection(string input, string expected)
    {
        Assert.Equal(new[] { expected }, Run("encadrer/encadrer-parentheses.js", input).Pasted);
    }

    [Theory]
    [InlineData("x", "[x]")]
    [InlineData("", "[]")]
    [InlineData("مرحبا", "[مرحبا]")]
    public void Encadrer_crochets_wraps_in_square_brackets(string input, string expected)
    {
        Assert.Equal(new[] { expected }, Run("encadrer/encadrer-crochets.js", input).Pasted);
    }

    [Theory]
    [InlineData("x", "{x}")]
    [InlineData("", "{}")]
    [InlineData("a\nb", "{a\nb}")]
    public void Encadrer_accolades_wraps_in_curly_braces(string input, string expected)
    {
        Assert.Equal(new[] { expected }, Run("encadrer/encadrer-accolades.js", input).Pasted);
    }

    [Theory]
    [InlineData("é", "« é »")]
    [InlineData("", "«  »")]
    [InlineData("😀 مرحبا", "« 😀 مرحبا »")]
    public void Encadrer_chevrons_uses_narrow_no_break_spaces(string input, string expected)
    {
        Assert.Equal(new[] { expected }, Run("encadrer/encadrer-chevrons.js", input).Pasted);
    }

    [Theory]
    [InlineData("x", "\"x\"")]
    [InlineData("", "\"\"")]
    [InlineData("un deux", "\"un deux\"")]
    public void Encadrer_guillemets_wraps_in_straight_quotes(string input, string expected)
    {
        Assert.Equal(new[] { expected }, Run("encadrer/encadrer-guillemets.js", input).Pasted);
    }

    [Fact]
    public void Encadrer_child_copies_when_read_only()
    {
        var api = Run("encadrer/encadrer-crochets.js", "x", editable: false, locale: "es");
        Assert.Equal(new[] { "[x]" }, api.Copied);
        Assert.Equal(new[] { "Copiado" }, api.Shown);
    }

    // ---- Nom de code (4 enfants) ----------------------------------------------

    [Theory]
    [InlineData("Mon titre de page", "mon_titre_de_page")]
    [InlineData("", "")]
    [InlineData("une", "une")]
    [InlineData("ÉcoleNormaleSupérieure", "ecole_normale_superieure")]
    [InlineData("HTMLParser", "html_parser")]
    [InlineData("😀 test", "test")]
    [InlineData("مرحبا بالعالم", "مرحبا_بالعالم")]
    [InlineData("a-b_c.d", "a_b_c_d")]
    public void Nom_de_code_snake_splits_words_and_drops_accents(string input, string expected)
    {
        Assert.Equal(new[] { expected }, Run("nom-de-code/nom-de-code-snake.js", input).Pasted);
    }

    [Theory]
    [InlineData("Mon titre de page", "mon-titre-de-page")]
    [InlineData("ÉcoleNormale", "ecole-normale")]
    [InlineData("", "")]
    public void Nom_de_code_kebab_joins_with_dashes(string input, string expected)
    {
        Assert.Equal(new[] { expected }, Run("nom-de-code/nom-de-code-kebab.js", input).Pasted);
    }

    [Theory]
    [InlineData("Mon titre de page", "monTitreDePage")]
    [InlineData("", "")]
    [InlineData("ÉcoleNormale", "ecoleNormale")]
    [InlineData("HTMLParser", "htmlParser")]
    public void Nom_de_code_camel_keeps_the_first_word_lower(string input, string expected)
    {
        Assert.Equal(new[] { expected }, Run("nom-de-code/nom-de-code-camel.js", input).Pasted);
    }

    [Theory]
    [InlineData("mon titre", "MonTitre")]
    [InlineData("", "")]
    [InlineData("HTMLParser", "HtmlParser")]
    public void Nom_de_code_pascal_capitalises_every_word(string input, string expected)
    {
        Assert.Equal(new[] { expected }, Run("nom-de-code/nom-de-code-pascal.js", input).Pasted);
    }

    // ---- Slug ---------------------------------------------------------------

    [Theory]
    [InlineData("Café & Crème : l'été 2026!", "cafe-creme-l-ete-2026")]
    [InlineData("", "")]
    [InlineData("un", "un")]
    [InlineData("😀", "")]
    [InlineData("مرحبا بالعالم", "")]
    [InlineData("  --Hello__World--  ", "hello-world")]
    public void Slug_keeps_only_lowercase_letters_and_digits_joined_by_dashes(string input, string expected)
    {
        Assert.Equal(new[] { expected }, Run("slug/slug.js", input).Pasted);
    }

    // ---- Renverser -----------------------------------------------------------

    [Theory]
    [InlineData("un\ndeux\ntrois", "trois\ndeux\nun")]
    [InlineData("", "")]
    [InlineData("seul", "seul")]
    [InlineData("a\r\nb", "b\r\na")]
    [InlineData("é\n😀\nمرحبا بالعالم", "مرحبا بالعالم\n😀\né")]
    public void Renverser_reverses_the_lines(string input, string expected)
    {
        Assert.Equal(new[] { expected }, Run("renverser/renverser.js", input).Pasted);
    }

    // ---- Mélanger ------------------------------------------------------------

    [Fact]
    public void Melanger_keeps_the_same_lines_in_some_order()
    {
        var output = Run("melanger/melanger.js", "un\ndeux\ntrois\nquatre").Pasted.Single();
        Assert.Equal(new[] { "un", "deux", "trois", "quatre" }.OrderBy(s => s), output.Split('\n').OrderBy(s => s));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("seul", "seul")]
    public void Melanger_leaves_empty_and_single_line_text_unchanged(string input, string expected)
    {
        Assert.Equal(new[] { expected }, Run("melanger/melanger.js", input).Pasted);
    }

    // ---- Sans doublons -------------------------------------------------------

    [Theory]
    [InlineData("a\nb\na\nc\nb", "a\nb\nc")]
    [InlineData("", "")]
    [InlineData("a\nA\na", "a\nA")]
    [InlineData("x\n\nx\n\n", "x\n")]
    public void Sans_doublons_keeps_first_occurrence_in_order_with_exact_comparison(string input, string expected)
    {
        Assert.Equal(new[] { expected }, Run("sans-doublons/sans-doublons.js", input).Pasted);
    }

    // ---- Numéroter -----------------------------------------------------------

    [Theory]
    [InlineData("a\n\nb", "1. a\n\n2. b")]
    [InlineData("", "")]
    [InlineData("seul", "1. seul")]
    [InlineData("é\n😀\nمرحبا بالعالم", "1. é\n2. 😀\n3. مرحبا بالعالم")]
    [InlineData("   \nb", "   \n1. b")]
    public void Numeroter_numbers_only_non_empty_lines(string input, string expected)
    {
        Assert.Equal(new[] { expected }, Run("numeroter/numeroter.js", input).Pasted);
    }

    // ---- Éclater -------------------------------------------------------------

    [Theory]
    [InlineData("a, b; c", "a\nb\nc")]
    [InlineData("", "")]
    [InlineData("seul", "seul")]
    [InlineData("élève, 😀 ; مرحبا بالعالم", "élève\n😀\nمرحبا بالعالم")]
    [InlineData("a\nb,c", "a\nb\nc")]
    [InlineData(" ,;, ", "")]
    public void Eclater_puts_each_item_on_its_own_line(string input, string expected)
    {
        Assert.Equal(new[] { expected }, Run("eclater/eclater.js", input).Pasted);
    }

    // ---- Rassembler ----------------------------------------------------------

    [Theory]
    [InlineData("a\nb\nc", "a, b, c")]
    [InlineData("", "")]
    [InlineData("seul", "seul")]
    [InlineData("é\n😀\nمرحبا بالعالم", "é, 😀, مرحبا بالعالم")]
    [InlineData("a\n\n  b  \n", "a, b")]
    public void Rassembler_joins_non_empty_lines_with_commas(string input, string expected)
    {
        Assert.Equal(new[] { expected }, Run("rassembler/rassembler.js", input).Pasted);
    }

    // ---- Déplier -------------------------------------------------------------

    [Theory]
    [InlineData("Bonjour à\ntous, ce matin\n\nMerci", "Bonjour à tous, ce matin\n\nMerci")]
    [InlineData("", "")]
    [InlineData("une\n   ligne", "une ligne")]
    [InlineData("é  😀\nمرحبا بالعالم", "é 😀 مرحبا بالعالم")]
    [InlineData("a\n\n\n\nb", "a\n\nb")]
    public void Deplier_joins_lines_inside_paragraphs_and_keeps_paragraph_breaks(string input, string expected)
    {
        Assert.Equal(new[] { expected }, Run("deplier/deplier.js", input).Pasted);
    }

    // ---- Caviarder -----------------------------------------------------------

    [Theory]
    [InlineData("Nom : Dupont", "••• • ••••••")]
    [InlineData("", "")]
    [InlineData("un\ndeux", "••\n••••")]
    [InlineData("a\tb", "•\t•")]
    [InlineData("😀éمرحبا بالعالم", "••••••• •••••••")]
    public void Caviarder_replaces_each_character_but_keeps_whitespace(string input, string expected)
    {
        Assert.Equal(new[] { expected }, Run("caviarder/caviarder.js", input).Pasted);
    }

    // ---- Brouiller (ROT13) ---------------------------------------------------

    [Theory]
    [InlineData("Bonjour", "Obawbhe")]
    [InlineData("", "")]
    [InlineData("abc XYZ", "nop KLM")]
    [InlineData("éà 😀 مرحبا بالعالم", "éà 😀 مرحبا بالعالم")]
    [InlineData("Zz aA", "Mm nN")]
    public void Brouiller_rotates_ascii_letters_by_thirteen(string input, string expected)
    {
        Assert.Equal(new[] { expected }, Run("brouiller/brouiller.js", input).Pasted);
    }

    [Theory]
    [InlineData("Bonjour le Monde, 😀 مرحبا")]
    [InlineData("")]
    [InlineData("abcdefghijklmnopqrstuvwxyz ABCDEFGHIJKLMNOPQRSTUVWXYZ")]
    public void Brouiller_applied_twice_gives_back_the_original_text(string input)
    {
        var once = Run("brouiller/brouiller.js", input).Pasted.Single();
        var twice = Run("brouiller/brouiller.js", once).Pasted.Single();
        Assert.Equal(input, twice);
    }

    [Fact]
    public void Brouiller_copies_when_read_only_and_says_so_in_arabic()
    {
        var api = Run("brouiller/brouiller.js", "abc", editable: false, locale: "ar");
        Assert.Equal(new[] { "nop" }, api.Copied);
        Assert.Equal(new[] { "تم النسخ" }, api.Shown);
    }

    [Fact]
    public void Editable_selection_is_replaced_and_the_message_is_in_english()
    {
        var api = Run("caviarder/caviarder.js", "ab", editable: true, locale: "en");
        Assert.Equal(new[] { "••" }, api.Pasted);
        Assert.Equal(new[] { "Replaced" }, api.Shown);
    }
}
