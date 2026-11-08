using System;
using System.Collections.Generic;
using System.IO;
using SelectBar.Actions;
using SelectBar.Extensions;
using SelectBar.Localization;
using Xunit;

namespace SelectBar.Tests;

/// <summary>Exécute les extensions de la bibliothèque (extensions-library/*) dans Jint avec un faux hôte.</summary>
[Collection("Localization")]
public class LibraryExtensionsTestsB
{
    private sealed class RecordingApi : IExtensionApi
    {
        public readonly List<string> Copied = new();
        public readonly List<string> Pasted = new();
        public readonly List<string> Shown = new();
        public readonly List<string> Results = new();
        public readonly List<string> Opened = new();
        public void CopyText(string text) => Copied.Add(text);
        public void PasteText(string text) => Pasted.Add(text);
        public void OpenUrl(string url) => Opened.Add(url);
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

    private static string ReadScript(string folder, string fileName) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "extensions-library", folder, fileName));

    private static RecordingApi Run(string folder, string fileName, string text, bool editable = false, string culture = "fr")
    {
        L.Culture = UiCulture.Make(culture);
        var api = new RecordingApi();
        ExtensionHost.Run(ReadScript(folder, fileName), new ActionContext(text, editable, null), api);
        return api;
    }

    private static string? RunInline(string folder, string fileName, string text, string culture)
    {
        L.Culture = UiCulture.Make(culture);
        return ExtensionHost.RunInline(ReadScript(folder, fileName), new ActionContext(text, false, null), false, TimeSpan.FromSeconds(5));
    }

    /// <summary>Le texte produit : collé si le champ est modifiable, sinon copié. Une seule sortie doit exister.</summary>
    private static string Output(RecordingApi api)
    {
        Assert.Equal(1, api.Pasted.Count + api.Copied.Count);
        return api.Pasted.Count == 1 ? api.Pasted[0] : api.Copied[0];
    }

    private static void AssertNothingProduced(RecordingApi api)
    {
        Assert.Empty(api.Pasted);
        Assert.Empty(api.Copied);
    }

    // ---- Citer ----

    [Fact]
    public void Quote_prefixes_each_line_and_pastes_when_editable()
    {
        var api = Run("citer", "citer.js", "une\ndeux", editable: true);
        Assert.Equal("> une\n> deux", api.Pasted[0]);
        Assert.Equal(new[] { "Remplacé" }, api.Shown);
    }

    [Fact]
    public void Quote_copies_when_read_only()
    {
        var api = Run("citer", "citer.js", "Bonjour");
        Assert.Equal(new[] { "> Bonjour" }, api.Copied);
        Assert.Equal(new[] { "Copié" }, api.Shown);
        Assert.Empty(api.Pasted);
    }

    [Fact]
    public void Quote_removes_existing_markers_when_every_line_is_quoted()
    {
        Assert.Equal("a\nb", Output(Run("citer", "citer.js", "> a\n> b")));
    }

    [Fact]
    public void Quote_then_unquote_gives_back_the_original_text_with_an_empty_line()
    {
        var quoted = Output(Run("citer", "citer.js", "a\n\nb"));
        Assert.Equal("> a\n>\n> b", quoted);
        Assert.Equal("a\n\nb", Output(Run("citer", "citer.js", quoted)));
    }

    [Fact]
    public void Quote_of_empty_text_is_a_bare_marker()
    {
        Assert.Equal(">", Output(Run("citer", "citer.js", "")));
    }

    [Fact]
    public void Quote_keeps_accents_and_emoji()
    {
        Assert.Equal("> é 😀", Output(Run("citer", "citer.js", "é 😀")));
    }

    [Fact]
    public void Quote_handles_arabic_and_arabic_messages()
    {
        var api = Run("citer", "citer.js", "مرحبا", editable: true, culture: "ar");
        Assert.Equal("> مرحبا", api.Pasted[0]);
        Assert.Equal(new[] { "تم الاستبدال" }, api.Shown);
    }

    [Fact]
    public void Quote_accepts_windows_line_endings()
    {
        Assert.Equal("> a\n> b", Output(Run("citer", "citer.js", "a\r\nb")));
    }

    // ---- Puces ----

    [Fact]
    public void Bullets_prefix_each_line_and_paste_when_editable()
    {
        var api = Run("puces", "puces.js", "eggs\nmilk", editable: true);
        Assert.Equal("- eggs\n- milk", api.Pasted[0]);
        Assert.Equal(new[] { "Remplacé" }, api.Shown);
    }

    [Fact]
    public void Bullets_copy_when_read_only()
    {
        var api = Run("puces", "puces.js", "x");
        Assert.Equal(new[] { "- x" }, api.Copied);
        Assert.Equal(new[] { "Copié" }, api.Shown);
    }

    [Fact]
    public void Bullets_are_removed_when_the_list_is_already_bulleted()
    {
        Assert.Equal("a\nb", Output(Run("puces", "puces.js", "- a\n- b")));
    }

    [Fact]
    public void Bullets_leave_blank_lines_alone()
    {
        Assert.Equal("- a\n\n- b", Output(Run("puces", "puces.js", "a\n\nb")));
    }

    [Fact]
    public void Bullets_mixed_list_gets_bullets_on_every_line()
    {
        Assert.Equal("- - a\n- b", Output(Run("puces", "puces.js", "- a\nb")));
    }

    [Fact]
    public void Bullets_of_empty_text_is_empty()
    {
        Assert.Equal("", Output(Run("puces", "puces.js", "")));
    }

    [Fact]
    public void Bullets_keep_accents_and_emoji()
    {
        Assert.Equal("- ça 🚀", Output(Run("puces", "puces.js", "ça 🚀")));
    }

    // ---- Monospace ----

    [Fact]
    public void Inline_code_wraps_the_text_in_backticks()
    {
        Assert.Equal("`x`", Output(Run("monospace", "monospace-ligne.js", "x")));
    }

    [Fact]
    public void Inline_code_uses_a_double_fence_when_the_text_has_a_backtick()
    {
        Assert.Equal("`` a`b ``", Output(Run("monospace", "monospace-ligne.js", "a`b")));
    }

    [Fact]
    public void Inline_code_turns_line_breaks_into_spaces()
    {
        Assert.Equal("`a b`", Output(Run("monospace", "monospace-ligne.js", "a\nb")));
    }

    [Fact]
    public void Inline_code_pastes_when_editable()
    {
        var api = Run("monospace", "monospace-ligne.js", "x", editable: true);
        Assert.Equal(new[] { "Remplacé" }, api.Shown);
        Assert.Empty(api.Copied);
    }

    [Fact]
    public void Code_block_is_fenced_with_three_backticks()
    {
        Assert.Equal("```\nx\n```", Output(Run("monospace", "monospace-bloc.js", "x")));
    }

    [Fact]
    public void Code_block_keeps_every_line()
    {
        Assert.Equal("```\na\nb\n```", Output(Run("monospace", "monospace-bloc.js", "a\nb")));
    }

    [Fact]
    public void Code_block_grows_its_fence_when_the_text_contains_three_backticks()
    {
        Assert.Equal("````\nx ``` y\n````", Output(Run("monospace", "monospace-bloc.js", "x ``` y")));
    }

    [Fact]
    public void Code_block_copies_with_the_english_message()
    {
        var api = Run("monospace", "monospace-bloc.js", "x", culture: "en");
        Assert.Equal(new[] { "Copied" }, api.Shown);
    }

    // ---- Codec : Base64 ----

    [Fact]
    public void Base64_encodes_ascii()
    {
        Assert.Equal("SGVsbG8=", Output(Run("codec", "codec-b64-enc.js", "Hello")));
    }

    [Fact]
    public void Base64_encodes_accents_and_emoji_as_utf8()
    {
        // é = C3 A9, 😀 = F0 9F 98 80
        Assert.Equal("w6nwn5iA", Output(Run("codec", "codec-b64-enc.js", "é😀")));
    }

    [Fact]
    public void Base64_encodes_empty_text_to_empty_text()
    {
        Assert.Equal("", Output(Run("codec", "codec-b64-enc.js", "")));
    }

    [Fact]
    public void Base64_decodes_ascii_and_emoji()
    {
        Assert.Equal("Hello", Output(Run("codec", "codec-b64-dec.js", "SGVsbG8=")));
        Assert.Equal("é😀", Output(Run("codec", "codec-b64-dec.js", "w6nwn5iA")));
    }

    [Fact]
    public void Base64_decode_ignores_line_breaks_in_the_input()
    {
        Assert.Equal("Hello", Output(Run("codec", "codec-b64-dec.js", "SGVs\nbG8=")));
    }

    [Fact]
    public void Base64_decode_accepts_missing_padding()
    {
        Assert.Equal("Hello", Output(Run("codec", "codec-b64-dec.js", "SGVsbG8")));
    }

    [Theory]
    [InlineData("@@@")]
    [InlineData("A")]
    [InlineData("SGVsbG8===")]
    public void Base64_invalid_input_shows_a_message_and_pastes_nothing(string input)
    {
        var api = Run("codec", "codec-b64-dec.js", input, editable: true);
        Assert.Equal(new[] { "Base64 invalide" }, api.Shown);
        AssertNothingProduced(api);
    }

    [Fact]
    public void Base64_decode_rejects_bytes_that_are_not_utf8()
    {
        var encoded = Convert.ToBase64String(new byte[] { 0xFF, 0x41 });
        var api = Run("codec", "codec-b64-dec.js", encoded);
        Assert.Equal(new[] { "Base64 invalide" }, api.Shown);
        AssertNothingProduced(api);
    }

    [Fact]
    public void Base64_decode_rejects_a_truncated_utf8_sequence()
    {
        var encoded = Convert.ToBase64String(new byte[] { 0xC3 });
        var api = Run("codec", "codec-b64-dec.js", encoded);
        Assert.Equal(new[] { "Base64 invalide" }, api.Shown);
        AssertNothingProduced(api);
    }

    [Fact]
    public void Base64_invalid_message_is_translated()
    {
        var api = Run("codec", "codec-b64-dec.js", "@@@", culture: "en");
        Assert.Equal(new[] { "Invalid Base64" }, api.Shown);
    }

    [Theory]
    [InlineData("مرحبا بكم 🙂 é")]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("abc")]
    [InlineData("€")]
    public void Base64_round_trip_keeps_the_text(string text)
    {
        var encoded = Output(Run("codec", "codec-b64-enc.js", text));
        Assert.Equal(text, Output(Run("codec", "codec-b64-dec.js", encoded)));
    }

    // ---- Codec : URL ----

    [Fact]
    public void Url_encoding_escapes_reserved_characters_and_utf8_bytes()
    {
        Assert.Equal("a%20b%26c%2F%C3%A9", Output(Run("codec", "codec-url-enc.js", "a b&c/é")));
    }

    [Fact]
    public void Url_encoding_keeps_unreserved_characters()
    {
        Assert.Equal("Az09-_.~", Output(Run("codec", "codec-url-enc.js", "Az09-_.~")));
    }

    [Fact]
    public void Url_decoding_restores_the_text()
    {
        Assert.Equal("a bé", Output(Run("codec", "codec-url-dec.js", "a%20b%C3%A9")));
    }

    [Fact]
    public void Url_decoding_keeps_plus_signs()
    {
        Assert.Equal("a+b", Output(Run("codec", "codec-url-dec.js", "a+b")));
    }

    [Fact]
    public void Url_decoding_invalid_encoding_shows_a_message_and_pastes_nothing()
    {
        var api = Run("codec", "codec-url-dec.js", "%E9", editable: true);
        Assert.Equal(new[] { "Encodage URL invalide" }, api.Shown);
        AssertNothingProduced(api);
    }

    [Fact]
    public void Url_round_trip_keeps_the_text()
    {
        const string text = "Crème brûlée & 😀 / ?#";
        var encoded = Output(Run("codec", "codec-url-enc.js", text));
        Assert.Equal(text, Output(Run("codec", "codec-url-dec.js", encoded)));
    }

    // ---- Codec : HTML ----

    [Fact]
    public void Html_encoding_escapes_the_five_special_characters()
    {
        Assert.Equal("&lt;a href=&quot;x&quot;&gt;&#39;&amp;&#39;&lt;/a&gt;",
            Output(Run("codec", "codec-html-enc.js", "<a href=\"x\">'&'</a>")));
    }

    [Fact]
    public void Html_encoding_keeps_accents()
    {
        Assert.Equal("é", Output(Run("codec", "codec-html-enc.js", "é")));
    }

    [Fact]
    public void Html_decoding_handles_named_and_numeric_entities_in_one_pass()
    {
        Assert.Equal("<b> é é 😀 &lt;",
            Output(Run("codec", "codec-html-dec.js", "&lt;b&gt; &#233; &#xE9; &#x1F600; &amp;lt;")));
    }

    [Fact]
    public void Html_decoding_decodes_quotes_apostrophes_and_nbsp()
    {
        Assert.Equal("\"' ", Output(Run("codec", "codec-html-dec.js", "&quot;&apos;&nbsp;")));
    }

    [Fact]
    public void Html_decoding_leaves_unknown_or_invalid_references_alone()
    {
        const string text = "&foo; &#0; &#xD800; &#1114112;";
        Assert.Equal(text, Output(Run("codec", "codec-html-dec.js", text)));
    }

    [Fact]
    public void Html_round_trip_keeps_the_text()
    {
        const string text = "<p class=\"x\">Tom & 'Jerry' é 😀</p>";
        var encoded = Output(Run("codec", "codec-html-enc.js", text));
        Assert.Equal(text, Output(Run("codec", "codec-html-dec.js", encoded)));
    }

    [Fact]
    public void Html_decoding_of_empty_text_is_empty()
    {
        Assert.Equal("", Output(Run("codec", "codec-html-dec.js", "")));
    }

    // ---- JSON net ----

    [Fact]
    public void Clean_json_indents_with_two_spaces()
    {
        Assert.Equal("{\n  \"a\": 1,\n  \"b\": [\n    1,\n    2\n  ]\n}",
            Output(Run("json-net", "json-net.js", "{\"a\":1,\"b\":[1,2]}")));
    }

    [Fact]
    public void Clean_json_pastes_when_editable()
    {
        var api = Run("json-net", "json-net.js", "{\"a\":1}", editable: true);
        Assert.Equal("{\n  \"a\": 1\n}", api.Pasted[0]);
        Assert.Equal(new[] { "Remplacé" }, api.Shown);
    }

    [Fact]
    public void Clean_json_keeps_accents_emoji_and_arabic()
    {
        Assert.Equal("{\n  \"é\": \"😀\"\n}", Output(Run("json-net", "json-net.js", "{\"é\":\"😀\"}")));
        Assert.Equal("[\n  \"مرحبا\"\n]", Output(Run("json-net", "json-net.js", "[\"مرحبا\"]")));
    }

    [Fact]
    public void Clean_json_invalid_input_shows_a_message_and_pastes_nothing()
    {
        var api = Run("json-net", "json-net.js", "{a:1}", editable: true);
        Assert.Equal(new[] { "JSON invalide" }, api.Shown);
        AssertNothingProduced(api);
    }

    [Fact]
    public void Clean_json_empty_input_is_invalid_in_english()
    {
        var api = Run("json-net", "json-net.js", "", culture: "en");
        Assert.Equal(new[] { "Invalid JSON" }, api.Shown);
        AssertNothingProduced(api);
    }

    // ---- Web : Encyclopédie ----

    [Fact]
    public void Encyclopedia_opens_the_french_wikipedia_search()
    {
        var api = Run("encyclopedie", "encyclopedie.js", "Marie Curie", culture: "fr");
        Assert.Equal(new[] { "https://fr.wikipedia.org/w/index.php?search=Marie%20Curie" }, api.Opened);
        AssertNothingProduced(api);
    }

    [Fact]
    public void Encyclopedia_uses_the_ui_language_when_supported()
    {
        Assert.Equal("https://es.wikipedia.org/w/index.php?search=Paris",
            Run("encyclopedie", "encyclopedie.js", "Paris", culture: "es").Opened[0]);
        Assert.Equal("https://ar.wikipedia.org/w/index.php?search=x",
            Run("encyclopedie", "encyclopedie.js", "x", culture: "ar").Opened[0]);
    }

    [Fact]
    public void Encyclopedia_sends_at_most_200_characters()
    {
        var api = Run("encyclopedie", "encyclopedie.js", new string('a', 250));
        Assert.Equal("https://fr.wikipedia.org/w/index.php?search=" + new string('a', 200), api.Opened[0]);
    }

    [Fact]
    public void Encyclopedia_counts_characters_not_utf16_units()
    {
        var text = string.Concat(System.Linq.Enumerable.Repeat("😀", 250));
        var api = Run("encyclopedie", "encyclopedie.js", text);
        Assert.Equal(Uri.EscapeDataString(string.Concat(System.Linq.Enumerable.Repeat("😀", 200))),
            api.Opened[0].Substring("https://fr.wikipedia.org/w/index.php?search=".Length));
    }

    [Fact]
    public void Encyclopedia_collapses_whitespace_before_searching()
    {
        Assert.Equal("https://fr.wikipedia.org/w/index.php?search=Jean%20Paul",
            Run("encyclopedie", "encyclopedie.js", "  Jean\n\tPaul ").Opened[0]);
    }

    [Fact]
    public void Encyclopedia_blank_selection_opens_nothing_and_says_so()
    {
        var api = Run("encyclopedie", "encyclopedie.js", "   \n ", editable: true);
        Assert.Empty(api.Opened);
        Assert.Equal(new[] { "Aucun texte sélectionné" }, api.Shown);
        AssertNothingProduced(api);
    }

    // ---- Web : Itinéraire ----

    [Fact]
    public void Directions_open_google_maps_with_the_encoded_address()
    {
        var api = Run("itineraire", "itineraire.js", "10 rue de la Paix, Paris");
        Assert.Equal(new[] { "https://www.google.com/maps/search/?api=1&query=10%20rue%20de%20la%20Paix%2C%20Paris" }, api.Opened);
        AssertNothingProduced(api);
    }

    [Fact]
    public void Directions_encode_accents_and_emoji_as_utf8()
    {
        Assert.Equal("https://www.google.com/maps/search/?api=1&query=Zo%C3%A9%20%F0%9F%98%80",
            Run("itineraire", "itineraire.js", "Zoé 😀").Opened[0]);
    }

    [Fact]
    public void Directions_blank_selection_opens_nothing()
    {
        var api = Run("itineraire", "itineraire.js", "");
        Assert.Empty(api.Opened);
        Assert.Single(api.Shown);
    }

    // ---- Web : Vidéo ----

    [Fact]
    public void Video_opens_youtube_search_with_the_encoded_text()
    {
        var api = Run("video", "video.js", "tuto café");
        Assert.Equal(new[] { "https://www.youtube.com/results?search_query=tuto%20caf%C3%A9" }, api.Opened);
        AssertNothingProduced(api);
    }

    [Fact]
    public void Video_encodes_arabic_text_as_utf8()
    {
        Assert.Equal("https://www.youtube.com/results?search_query=%D9%85%D8%B1%D8%AD%D8%A8%D8%A7",
            Run("video", "video.js", "مرحبا").Opened[0]);
    }

    [Fact]
    public void Video_blank_selection_opens_nothing()
    {
        var api = Run("video", "video.js", "\n ");
        Assert.Empty(api.Opened);
        Assert.Single(api.Shown);
    }

    // ---- Chrono lecture (inline) ----

    private static string Words(int count, string word = "mot") => string.Join(' ', System.Linq.Enumerable.Repeat(word, count));

    [Fact]
    public void Reading_time_counts_words_in_french()
    {
        Assert.Equal("3 mots · < 1 min", RunInline("chrono-lecture", "chrono-lecture.js", "un deux trois", "fr"));
    }

    [Fact]
    public void Reading_time_uses_singular_for_one_word()
    {
        Assert.Equal("1 mot · < 1 min", RunInline("chrono-lecture", "chrono-lecture.js", "bonjour", "fr"));
    }

    [Fact]
    public void Reading_time_of_blank_text_is_zero_words()
    {
        Assert.Equal("0 mots · < 1 min", RunInline("chrono-lecture", "chrono-lecture.js", "   ", "fr"));
    }

    [Fact]
    public void Reading_time_rounds_up_to_the_next_minute_at_230_words_a_minute()
    {
        Assert.Equal("230 mots · 1 min de lecture", RunInline("chrono-lecture", "chrono-lecture.js", Words(230), "fr"));
        Assert.Equal("231 mots · 2 min de lecture", RunInline("chrono-lecture", "chrono-lecture.js", Words(231), "fr"));
    }

    [Fact]
    public void Reading_time_groups_thousands_with_a_narrow_no_break_space_in_french()
    {
        Assert.Equal("1 240 mots · 6 min de lecture", RunInline("chrono-lecture", "chrono-lecture.js", Words(1240), "fr"));
    }

    [Fact]
    public void Reading_time_uses_english_and_spanish_formats()
    {
        Assert.Equal("1,240 words · 6 min read", RunInline("chrono-lecture", "chrono-lecture.js", Words(1240, "w"), "en"));
        Assert.Equal("1.240 palabras · 6 min de lectura", RunInline("chrono-lecture", "chrono-lecture.js", Words(1240, "w"), "es"));
    }

    [Fact]
    public void Reading_time_in_arabic_counts_words_and_uses_the_arabic_unit()
    {
        Assert.Equal("2 كلمات · < 1 د", RunInline("chrono-lecture", "chrono-lecture.js", "مرحبا بكم", "ar"));
        Assert.Equal("1 كلمة · < 1 د", RunInline("chrono-lecture", "chrono-lecture.js", "مرحبا", "ar"));
    }

    [Fact]
    public void Reading_time_outside_the_bar_shows_a_popup_result()
    {
        var api = Run("chrono-lecture", "chrono-lecture.js", "un deux", culture: "fr");
        Assert.Equal(new[] { "2 mots · < 1 min" }, api.Results);
        Assert.Empty(api.Shown);
    }
}
