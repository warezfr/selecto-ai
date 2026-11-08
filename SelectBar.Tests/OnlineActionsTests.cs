using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SelectBar.Actions;
using SelectBar.Online;
using SelectBar.Localization;
using SelectBar.Settings;
using Xunit;

namespace SelectBar.Tests;

[Collection("Localization")]
public class OnlineActionsTests
{
    public OnlineActionsTests() => L.Culture = UiCulture.Make("fr");

    private sealed class Svc : IActionServices
    {
        public List<ResultRequest> Requests { get; } = new();
        public void CopyToClipboard(string text) { }
        public Task SendCutAsync() => Task.CompletedTask;
        public Task SendPasteAsync() => Task.CompletedTask;
        public void OpenUrl(string url) { }
        public void RevealInExplorer(string path) { }
        public Task ShowResultAsync(ResultRequest request) { Requests.Add(request); return Task.CompletedTask; }
    }

    private static readonly AppSettings On = new() { OnlineActionsEnabled = true };

    private static List<ActionDefinition> All(Svc svc, AppSettings s) => BuiltInActions.Create(svc, () => s);

    private static List<string> Ids(AppSettings s, string text, bool editable = false) =>
        ActionFilter.Select(All(new Svc(), s), TextClassifier.BuildContext(text, editable, "notepad.exe", _ => false, true), s).Select(a => a.Id).ToList();

    [Fact]
    public void Online_actions_are_hidden_until_the_switch_is_on()
    {
        var ids = Ids(new AppSettings { OnlineActionsEnabled = false }, "bonjour");
        Assert.DoesNotContain(BuiltInActions.Define, ids);
        Assert.DoesNotContain(BuiltInActions.Translate, ids);
        Assert.DoesNotContain(BuiltInActions.Ai, ids);
        Assert.DoesNotContain(BuiltInActions.AiSummarize, ids);
        Assert.Contains(BuiltInActions.Copy, ids);
    }

    [Fact]
    public void Bar_order_search_cut_copy_paste_then_dictionary_translate_ai()
    {
        var ids = Ids(On, "bonjour", editable: true);
        var top = ids.Where(i => !i.StartsWith("builtin.ai.")).ToList();
        Assert.Equal(BuiltInActions.Search, top[0]);
        Assert.True(top.IndexOf(BuiltInActions.Cut) < top.IndexOf(BuiltInActions.Copy));
        Assert.True(top.IndexOf(BuiltInActions.Copy) < top.IndexOf(BuiltInActions.Paste));
        Assert.True(top.IndexOf(BuiltInActions.Paste) < top.IndexOf(BuiltInActions.Define));
        Assert.True(top.IndexOf(BuiltInActions.Define) < top.IndexOf(BuiltInActions.Translate));
        Assert.True(top.IndexOf(BuiltInActions.Translate) < top.IndexOf(BuiltInActions.Ai));
    }

    [Fact]
    public void Ai_menu_has_four_children_without_translate_or_copilot()
    {
        var ids = Ids(On, "Un texte assez long pour être résumé");
        var i = ids.IndexOf(BuiltInActions.Ai);
        Assert.True(i >= 0);
        Assert.Equal(new[] { BuiltInActions.AiSummarize, BuiltInActions.AiExplain, BuiltInActions.AiRephrase, BuiltInActions.AiCorrect },
            ids.Skip(i + 1).Take(4).ToArray());
        Assert.DoesNotContain(BuiltInActions.CopilotTranslate, ids);
        Assert.DoesNotContain(BuiltInActions.Copilot, ids);
        var all = All(new Svc(), On);
        Assert.Equal(new[] { "Résumer", "Expliquer", "Reformuler", "Corriger" },
            all.Where(a => a.ParentId == BuiltInActions.Ai).Select(a => a.Title).ToArray());
    }

    [Fact]
    public void Ai_submenu_disappears_when_the_parent_is_disabled()
    {
        var s = new AppSettings { OnlineActionsEnabled = true, DisabledActions = { BuiltInActions.Ai } };
        var ids = Ids(s, "Un texte");
        Assert.DoesNotContain(BuiltInActions.Ai, ids);
        Assert.DoesNotContain(BuiltInActions.AiCorrect, ids);
    }

    [Fact]
    public void Translate_not_offered_for_links_and_emails()
    {
        Assert.DoesNotContain(BuiltInActions.Translate, Ids(On, "https://exemple.com"));
        Assert.DoesNotContain(BuiltInActions.Translate, Ids(On, "jean@exemple.fr"));
        Assert.DoesNotContain(BuiltInActions.Translate, Ids(On, "12345"));
    }

    [Fact]
    public async Task Actions_build_requests_and_never_open_the_browser()
    {
        var svc = new Svc();
        var s = new AppSettings { OnlineActionsEnabled = true, TargetLanguage = "es" };
        var all = All(svc, s);
        ActionContext Ctx(string t, bool editable) => TextClassifier.BuildContext(t, editable, "notepad.exe", _ => false, true);
        await all.First(a => a.Id == BuiltInActions.Define).Execute(Ctx("  maison ", false));
        await all.First(a => a.Id == BuiltInActions.Translate).Execute(Ctx("Hello", true));
        await all.First(a => a.Id == BuiltInActions.AiSummarize).Execute(Ctx("Long texte", false));
        await all.First(a => a.Id == BuiltInActions.AiCorrect).Execute(Ctx("teste", true));
        Assert.Equal(new[] { ResultKind.Define, ResultKind.Translate, ResultKind.Summarize, ResultKind.Correct }, svc.Requests.Select(r => r.Kind).ToArray());
        Assert.Equal("maison", svc.Requests[0].Text);
        Assert.Equal(new[] { false, true, false, true }, svc.Requests.Select(r => r.IsEditable).ToArray());
        Assert.All(svc.Requests, r => Assert.Equal("es", r.TargetLanguage));
    }

    [Fact]
    public async Task Ai_button_explains_read_only_text_and_corrects_editable_text()
    {
        var svc = new Svc();
        var ai = All(svc, On).First(a => a.Id == BuiltInActions.Ai);
        await ai.Execute(TextClassifier.BuildContext("texte", false, "notepad.exe", _ => false, true));
        await ai.Execute(TextClassifier.BuildContext("texte", true, "notepad.exe", _ => false, true));
        Assert.Equal(new[] { ResultKind.Explain, ResultKind.Correct }, svc.Requests.Select(r => r.Kind).ToArray());
    }

    [Fact]
    public void Glyphs_for_new_buttons()
    {
        Assert.Equal("", ActionGlyphs.ForBar(BuiltInActions.Translate));
        Assert.Equal("", ActionGlyphs.ForBar(BuiltInActions.Define));
        Assert.Null(ActionGlyphs.ForBar(BuiltInActions.Ai)); // l'IA s'affiche avec son icône texte ✦
        Assert.Equal("✦", All(new Svc(), On).First(a => a.Id == BuiltInActions.Ai).Icon);
    }

    [Fact]
    public void Online_defaults_are_on_for_new_installs()
    {
        var s = new AppSettings();
        Assert.True(s.OnlineActionsEnabled);
        Assert.Equal("fr", s.TargetLanguage);
        Assert.Equal("en", s.FallbackLanguage);
        Assert.Null(s.MyMemoryEmail);
        Assert.Equal(LlmPresets.None, s.LlmProvider);
        Assert.Null(s.LlmApiKeyProtected);
    }
}
