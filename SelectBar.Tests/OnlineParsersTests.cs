using System;
using System.IO;
using System.Linq;
using SelectBar.Online;
using Xunit;

namespace SelectBar.Tests;

internal static class Fx
{
    public static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
}

[Collection("Localization")]
public class OnlineParsersTests
{
    [Fact]
    public void Wiktionary_maison_gives_first_noun_with_pronunciation_and_bounded_definitions()
    {
        var e = WiktionaryParser.Parse(Fx.Read("wiktionary-maison.json"));
        Assert.NotNull(e);
        Assert.Equal("maison", e!.Word);
        Assert.Equal("/me.zɔ̃/", e.Phonetic);
        var sense = Assert.Single(e.Senses);
        Assert.Equal("Nom commun · féminin", sense.Label);
        Assert.Contains("Bâtiment servant de logis", sense.Definitions[0]);
        Assert.InRange(sense.Definitions.Count, 1, 5);
        Assert.All(sense.Definitions, d => Assert.DoesNotContain(" — (", d));
    }

    [Fact]
    public void Wiktionary_chat_takes_the_first_of_several_numbered_natures()
    {
        var e = WiktionaryParser.Parse(Fx.Read("wiktionary-chat.json"))!;
        Assert.Equal("/ʃa/", e.Phonetic);
        Assert.Equal("Nom commun · masculin", e.Senses[0].Label);
        Assert.Contains("Mammifère carnivore", e.Senses[0].Definitions[0]);
        Assert.True(e.Senses[0].Definitions.Count <= 5);
    }

    [Fact]
    public void Wiktionary_without_french_section_or_missing_page_gives_null()
    {
        Assert.Null(WiktionaryParser.Parse(Fx.Read("wiktionary-serendipity.json")));
        Assert.Null(WiktionaryParser.Parse(Fx.Read("wiktionary-missing.json")));
        Assert.Null(WiktionaryParser.Parse("pas du json"));
        var en = WiktionaryParser.Parse(Fx.Read("wiktionary-serendipity.json"), "Anglais")!;
        Assert.Equal("Sérendipité.", en.Senses[0].Definitions[0]);
    }

    [Fact]
    public void Dictionary_format_is_compact()
    {
        var e = WiktionaryParser.Parse(Fx.Read("wiktionary-maison.json"))!;
        var body = DictionaryFormat.Body(e);
        Assert.StartsWith("/me.zɔ̃/\n\nNom commun · féminin\n1. (Construction) Bâtiment", body);
        Assert.DoesNotContain("\n\n\n", body);
    }

    [Fact]
    public void DictionaryApi_serendipity()
    {
        var e = DictionaryApiParser.Parse(Fx.Read("dictionaryapi-serendipity.json"))!;
        Assert.Equal("serendipity", e.Word);
        Assert.False(string.IsNullOrEmpty(e.Phonetic));
        Assert.Equal("nom", e.Senses[0].Label);
        Assert.Equal(2, e.Senses[0].Definitions.Count);
        Assert.StartsWith("A combination of events", e.Senses[0].Definitions[0]);
    }

    [Fact]
    public void DictionaryApi_not_found_is_null()
    {
        Assert.Null(DictionaryApiParser.Parse(Fx.Read("dictionaryapi-missing.json")));
        Assert.Null(DictionaryApiParser.Parse("[]"));
    }

    [Fact]
    public void MyMemory_parses_text_and_detected_language()
    {
        var r = MyMemoryParser.Parse(Fx.Read("mymemory-hello.json"));
        Assert.Null(r.Error);
        Assert.Equal("Bonjour le monde", r.Text);
        Assert.Equal("en", r.DetectedLanguage);
        var c = MyMemoryParser.Parse(Fx.Read("mymemory-code.json"));
        Assert.Equal("Le code expire après 10 minutes", c.Text);
    }

    [Fact]
    public void MyMemory_quota_and_errors_are_readable()
    {
        Assert.Equal(MyMemoryParser.QuotaMessage, MyMemoryParser.Parse("{\"quotaFinished\":true,\"responseData\":{\"translatedText\":\"x\"},\"responseStatus\":200}").Error);
        Assert.Equal(MyMemoryParser.QuotaMessage, MyMemoryParser.Parse("{\"responseData\":{\"translatedText\":\"MYMEMORY WARNING: YOU USED ALL AVAILABLE FREE TRANSLATIONS FOR TODAY.\"},\"responseStatus\":\"200\"}").Error);
        Assert.NotNull(MyMemoryParser.Parse("{\"responseData\":{\"translatedText\":\"\"},\"responseStatus\":\"403\"}").Error);
        Assert.NotNull(MyMemoryParser.Parse("<html>").Error);
        Assert.Equal("L'été", MyMemoryParser.Parse("{\"responseData\":{\"translatedText\":\"L&#39;été\"},\"responseStatus\":200}").Text);
    }

    [Fact]
    public void Wikipedia_summary()
    {
        var s = WikipediaParser.Parse(Fx.Read("wikipedia-golden-gate.json"))!;
        Assert.Equal("Pont du Golden Gate", s.Title);
        Assert.StartsWith("Le pont du Golden Gate est un pont suspendu", s.Extract);
        Assert.False(s.IsDisambiguation);
        Assert.Null(WikipediaParser.Parse(Fx.Read("wikipedia-missing.json")));
        Assert.True(WikipediaParser.Shorten(new string('a', 50) + ". " + new string('b', 900), 600).Length <= 601);
    }

    [Fact]
    public void OpenRouter_keeps_only_free_models()
    {
        var all = OpenRouterParsers.ParseFreeModels(Fx.Read("openrouter-models.json"));
        Assert.NotEmpty(all);
        Assert.All(all, m => Assert.EndsWith(":free", m.Id));
        Assert.Equal(6, all.Count);
        Assert.Empty(OpenRouterParsers.ParseFreeModels("{}"));
        Assert.Empty(OpenRouterParsers.ParseFreeModels(
            "{\"data\":[{\"id\":\"a/b:free\",\"pricing\":{\"prompt\":\"0.1\",\"completion\":\"0\"}},{\"id\":\"c/d\",\"pricing\":{\"prompt\":\"0\",\"completion\":\"0\"}}]}"));
        Assert.All(OpenRouterParsers.Fallback, m => Assert.EndsWith(":free", m.Id));
    }
}
