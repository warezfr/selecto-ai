using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SelectBar.Actions;
using SelectBar.Settings;
using Xunit;

namespace SelectBar.Tests;

public class ActionFilterTests
{
    private sealed class FakeServices : IActionServices
    {
        public List<string> Calls { get; } = new();
        public void CopyToClipboard(string text) => Calls.Add("copy:" + text);
        public Task SendCutAsync() { Calls.Add("cut"); return Task.CompletedTask; }
        public Task SendPasteAsync() { Calls.Add("paste"); return Task.CompletedTask; }
        public void OpenUrl(string url) => Calls.Add("open:" + url);
        public void RevealInExplorer(string path) => Calls.Add("reveal:" + path);
    }

    private static ActionContext Ctx(string text, bool editable = false, string? exe = "notepad.exe", bool clip = true, bool path = false)
    {
        var c = TextClassifier.BuildContext(text, editable, exe, _ => path, clip);
        return c;
    }

    private static List<string> Ids(string text, bool editable = false, string? exe = "notepad.exe", bool clip = true, bool path = false, AppSettings? s = null)
    {
        var all = BuiltInActions.Create(new FakeServices(), () => s ?? new AppSettings());
        return ActionFilter.Select(all, Ctx(text, editable, exe, clip, path), s ?? new AppSettings()).Select(a => a.Id).ToList();
    }

    [Fact]
    public void ReadOnly_text_has_no_cut_or_paste()
    {
        var ids = Ids("hello world");
        Assert.Contains(BuiltInActions.Copy, ids);
        Assert.Contains(BuiltInActions.Search, ids);
        Assert.DoesNotContain(BuiltInActions.Cut, ids);
        Assert.DoesNotContain(BuiltInActions.Paste, ids);
    }

    [Fact]
    public void Editable_text_has_cut_and_paste()
    {
        var ids = Ids("hello", editable: true);
        Assert.Contains(BuiltInActions.Cut, ids);
        Assert.Contains(BuiltInActions.Paste, ids);
    }

    [Fact]
    public void Paste_hidden_when_clipboard_has_no_text() =>
        Assert.DoesNotContain(BuiltInActions.Paste, Ids("hello", editable: true, clip: false));

    [Fact]
    public void OpenLink_only_for_urls()
    {
        Assert.Contains(BuiltInActions.OpenLink, Ids("https://example.com"));
        Assert.DoesNotContain(BuiltInActions.OpenLink, Ids("hello"));
    }

    [Fact]
    public void Reveal_only_for_existing_paths()
    {
        Assert.Contains(BuiltInActions.Reveal, Ids(@"C:\Temp\a.txt", path: true));
        Assert.DoesNotContain(BuiltInActions.Reveal, Ids(@"C:\Temp\a.txt", path: false));
    }

    [Fact]
    public void Dictionary_only_for_short_non_url_text()
    {
        var online = new AppSettings { OnlineActionsEnabled = true };
        Assert.Contains(BuiltInActions.Define, Ids("serendipity", s: online));
        Assert.DoesNotContain(BuiltInActions.Define, Ids("a long sentence with many words inside", s: online));
        Assert.DoesNotContain(BuiltInActions.Define, Ids("https://example.com", s: online));
        Assert.DoesNotContain(BuiltInActions.Define, Ids("serendipity", s: new AppSettings { OnlineActionsEnabled = false })); // actions en ligne désactivées par le choix de l'utilisateur
    }

    [Fact]
    public void Disabled_actions_are_removed()
    {
        var s = new AppSettings { DisabledActions = { BuiltInActions.Copy } };
        Assert.DoesNotContain(BuiltInActions.Copy, Ids("hello", s: s));
    }

    [Fact]
    public void Order_follows_settings_then_registration()
    {
        var s = new AppSettings { ActionOrder = { BuiltInActions.Search, BuiltInActions.Copy } };
        var ids = Ids("hello", s: s);
        Assert.Equal(BuiltInActions.Search, ids[0]);
        Assert.Equal(BuiltInActions.Copy, ids[1]);
    }

    private static ActionDefinition Custom(Regex? rx = null, string[]? include = null, string[]? exclude = null) => new()
    {
        Id = "x", Title = "x", TextPattern = rx,
        IncludeApps = include ?? new string[0], ExcludeApps = exclude ?? new string[0],
        Execute = _ => Task.CompletedTask
    };

    [Fact]
    public void Regex_filter()
    {
        var a = Custom(new Regex(@"^\d+$"));
        Assert.True(ActionFilter.Matches(a, Ctx("123")));
        Assert.False(ActionFilter.Matches(a, Ctx("12a")));
    }

    [Fact]
    public void App_include_and_exclude_are_case_insensitive_and_exe_optional()
    {
        Assert.True(ActionFilter.Matches(Custom(include: new[] { "Chrome" }), Ctx("t", exe: "chrome.exe")));
        Assert.False(ActionFilter.Matches(Custom(include: new[] { "chrome.exe" }), Ctx("t", exe: "notepad.exe")));
        Assert.False(ActionFilter.Matches(Custom(include: new[] { "chrome.exe" }), Ctx("t", exe: null)));
        Assert.False(ActionFilter.Matches(Custom(exclude: new[] { "NOTEPAD.EXE" }), Ctx("t", exe: "notepad.exe")));
    }

    [Fact]
    public async Task Builtin_execution_goes_through_services()
    {
        var svc = new FakeServices();
        var all = BuiltInActions.Create(svc, () => new AppSettings { SearchEngineId = "bing" });
        var ctx = Ctx("hi there");
        await all.First(a => a.Id == BuiltInActions.Search).Execute(ctx);
        await all.First(a => a.Id == BuiltInActions.Copy).Execute(ctx);
        Assert.Equal(new[] { "open:https://www.bing.com/search?q=hi%20there", "copy:hi there" }, svc.Calls);
    }
}
