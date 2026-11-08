using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SelectBar.Actions;
using SelectBar.Localization;
using SelectBar.Settings;
using Xunit;

namespace SelectBar.Tests;

[Collection("Localization")]
public class BuiltInActionsV2Tests
{
    public BuiltInActionsV2Tests() => L.Culture = UiCulture.Make("fr");

    private sealed class Svc : IActionServices
    {
        public List<string> Calls { get; } = new();
        public void CopyToClipboard(string text) => Calls.Add("copy:" + text);
        public Task SendCutAsync() => Task.CompletedTask;
        public Task SendPasteAsync() => Task.CompletedTask;
        public void OpenUrl(string url) => Calls.Add("open:" + url);
        public void RevealInExplorer(string path) => Calls.Add("reveal:" + path);
    }

    private static ActionContext Ctx(string text, bool editable = false, Func<string, bool>? exists = null) =>
        TextClassifier.BuildContext(text, editable, "notepad.exe", exists ?? (_ => false), true);

    private static List<string> Ids(string text, bool editable = false, Func<string, bool>? exists = null,
        Func<string, IReadOnlyList<string>>? spell = null, Func<string, Task>? paste = null)
    {
        var s = new AppSettings();
        var all = BuiltInActions.Create(new Svc(), () => s, spell, paste);
        return ActionFilter.Select(all, Ctx(text, editable, exists), s).Select(a => a.Id).ToList();
    }

    [Fact]
    public void Link_actions_come_right_after_search()
    {
        var url = Ids("https://a.fr");
        Assert.Equal(BuiltInActions.Search, url[0]);
        Assert.Equal(BuiltInActions.OpenLink, url[1]);
        var path = Ids(@"C:\Temp\a.txt", exists: _ => true);
        Assert.Equal(new[] { BuiltInActions.Search, BuiltInActions.Reveal }, path.Take(2).ToArray());
        var mail = Ids("jean@exemple.fr");
        Assert.Equal(new[] { BuiltInActions.Search, BuiltInActions.Mail }, mail.Take(2).ToArray());
    }

    [Fact]
    public void Mail_only_for_emails_and_replaces_open_link()
    {
        var ids = Ids("jean@exemple.fr");
        Assert.Contains(BuiltInActions.Mail, ids);
        Assert.DoesNotContain(BuiltInActions.OpenLink, ids);
        Assert.DoesNotContain(BuiltInActions.Define, ids);
        Assert.DoesNotContain(BuiltInActions.Mail, Ids("https://a.fr"));
        Assert.Contains(BuiltInActions.Mail, Ids("mailto:a@b.fr"));
    }

    [Fact]
    public void Open_link_for_url_inside_a_sentence()
    {
        Assert.Contains(BuiltInActions.OpenLink, Ids("Voir https://exemple.com/page, merci"));
        Assert.DoesNotContain(BuiltInActions.OpenLink, Ids("Voir exemple.com au milieu"));
    }

    [Fact]
    public void Reveal_offered_when_only_the_parent_folder_exists()
    {
        Assert.Contains(BuiltInActions.Reveal, Ids(@"C:\Temp\missing.txt", exists: p => p == @"C:\Temp"));
        Assert.DoesNotContain(BuiltInActions.Reveal, Ids(@"C:\Nope\missing.txt", exists: _ => false));
    }

    [Fact]
    public void Titles_follow_the_spec()
    {
        var all = BuiltInActions.Create(new Svc(), () => new AppSettings());
        string T(string id) => all.First(a => a.Id == id).Title;
        Assert.Equal("Afficher dans l'Explorateur", T(BuiltInActions.Reveal));
        Assert.Equal("Écrire à…", T(BuiltInActions.Mail));
        Assert.Equal("Ouvrir le lien", T(BuiltInActions.OpenLink));
        Assert.Equal("Orthographe", T(BuiltInActions.Spell));
    }

    [Fact]
    public void BuiltInActions_titles_follow_culture()
    {
        try
        {
            L.Culture = UiCulture.Make("en");
            var all = BuiltInActions.Create(new Svc(), () => new AppSettings());
            Assert.Contains(all, a => a.Id == BuiltInActions.Search && a.Title == "Search");
            Assert.Contains(all, a => a.Id == BuiltInActions.Reveal && a.Title == "Show in Explorer");
        }
        finally
        {
            L.Culture = UiCulture.Make("fr");
        }
    }

    [Fact]
    public async Task Executions_use_the_cleaned_targets()
    {
        var svc = new Svc();
        var all = BuiltInActions.Create(svc, () => new AppSettings());
        await all.First(a => a.Id == BuiltInActions.OpenLink).Execute(Ctx("Voir www.exemple.com/x, merci"));
        await all.First(a => a.Id == BuiltInActions.Mail).Execute(Ctx("écris à jean@exemple.fr."));
        await all.First(a => a.Id == BuiltInActions.Reveal).Execute(Ctx("\"C:\\Temp\\a b.txt\"", exists: _ => true));
        Assert.Equal(new[]
        {
            "open:https://www.exemple.com/x", "open:mailto:jean@exemple.fr", @"reveal:C:\Temp\a b.txt"
        }, svc.Calls);
    }

    [Fact]
    public void Spell_only_for_a_misspelled_single_word_in_editable_text()
    {
        IReadOnlyList<string> Spell(string w) => w == "bonjor" ? new[] { "bonjour" } : Array.Empty<string>();
        Task Paste(string _) => Task.CompletedTask;
        Assert.Contains(BuiltInActions.Spell, Ids("bonjor", editable: true, spell: Spell, paste: Paste));
        Assert.DoesNotContain(BuiltInActions.Spell, Ids("bonjour", editable: true, spell: Spell, paste: Paste));
        Assert.DoesNotContain(BuiltInActions.Spell, Ids("bonjor", editable: false, spell: Spell, paste: Paste));
        Assert.DoesNotContain(BuiltInActions.Spell, Ids("bonjor tout", editable: true, spell: Spell, paste: Paste));
        Assert.DoesNotContain(BuiltInActions.Spell, Ids("bonjor", editable: true));
    }

    [Fact]
    public async Task Spell_pastes_first_suggestion()
    {
        var pasted = new List<string>();
        var all = BuiltInActions.Create(new Svc(), () => new AppSettings(),
            w => new[] { "bonjour", "bonjoure" }, t => { pasted.Add(t); return Task.CompletedTask; });
        await all.First(a => a.Id == BuiltInActions.Spell).Execute(Ctx("bonjor", editable: true));
        Assert.Equal(new[] { "bonjour" }, pasted);
    }

    [Fact]
    public void Glyphs()
    {
        Assert.Equal("\uE71B", ActionGlyphs.ForBar(BuiltInActions.OpenLink));
        Assert.Equal("\uED25", ActionGlyphs.ForBar(BuiltInActions.Reveal));
        Assert.Equal("\uE715", ActionGlyphs.ForBar(BuiltInActions.Mail));
        Assert.Equal("\uE721", ActionGlyphs.ForBar(BuiltInActions.Search));
        Assert.Null(ActionGlyphs.ForBar(BuiltInActions.Copy));
        Assert.NotNull(ActionGlyphs.ForList(BuiltInActions.Copy));
    }
}
