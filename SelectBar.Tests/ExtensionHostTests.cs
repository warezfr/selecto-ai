using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SelectBar.Actions;
using SelectBar.Extensions;
using SelectBar.Localization;
using Xunit;

namespace SelectBar.Tests;

[Collection("Localization")]
public class ExtensionHostTests
{
    private sealed class FakeApi : IExtensionApi
    {
        public readonly List<string> Shown = new();
        public readonly List<string> Results = new();
        public bool? ResultEditable;
        public void CopyText(string text) { }
        public void PasteText(string text) { }
        public void OpenUrl(string url) { }
        public void ShowText(string text) => Shown.Add(text);
        public void ShowResult(string text, bool isEditable) { Results.Add(text); ResultEditable = isEditable; }
    }

    private static string DecodedLegacyName => Encoding.UTF8.GetString(Convert.FromBase64String(ExtensionHost.LegacyGlobalNameBase64));

    [Fact]
    public void Script_reads_selecto_text_editable_and_locale()
    {
        L.Culture = UiCulture.Make("fr");
        var api = new FakeApi();
        ExtensionHost.Run("selecto.showText(selecto.text + '|' + selecto.editable + '|' + selecto.locale);",
            new ActionContext("abc", true, null), api);
        Assert.Equal(new[] { "abc|true|fr" }, api.Shown);
    }

    [Fact]
    public void Compatibility_alias_is_the_same_object()
    {
        var api = new FakeApi();
        var script = "selecto.showText(String(globalThis['NAME'] === selecto && globalThis['NAME'].text === 'abc'));".Replace("NAME", DecodedLegacyName);
        ExtensionHost.Run(script, new ActionContext("abc", false, null), api);
        Assert.Equal(new[] { "true" }, api.Shown);
    }

    [Fact]
    public void Show_result_calls_the_host_with_editability()
    {
        var api = new FakeApi();
        ExtensionHost.Run("selecto.showResult('résultat');", new ActionContext("abc", true, null), api);
        Assert.Equal(new[] { "résultat" }, api.Results);
        Assert.True(api.ResultEditable);
    }
}
