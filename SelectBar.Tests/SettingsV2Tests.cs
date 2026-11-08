using System;
using System.IO;
using SelectBar.Settings;
using Xunit;

namespace SelectBar.Tests;

public class SettingsV2Tests
{
    [Theory]
    [InlineData("https://www.Exemple.com:8080/x?y", "exemple.com")]
    [InlineData("*.exemple.com", "exemple.com")]
    [InlineData("www.exemple.com", "exemple.com")]
    [InlineData("sub.exemple.com", "sub.exemple.com")]
    [InlineData("exemple.com.", "exemple.com")]
    [InlineData("localhost", "localhost")]
    public void NormalizeDomain_valid(string input, string expected) => Assert.Equal(expected, ExcludedSites.NormalizeDomain(input));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("exemple")]
    [InlineData("ex emple.com")]
    [InlineData("-bad.com")]
    [InlineData("a..b.com")]
    [InlineData("http://")]
    public void NormalizeDomain_invalid(string input) => Assert.Null(ExcludedSites.NormalizeDomain(input));

    [Fact]
    public void Excluded_sites_add_remove_dedupe()
    {
        var s = new ExcludedSites(new[] { "https://www.exemple.com/a", "EXEMPLE.com", "nope" });
        Assert.Equal(1, s.Count);
        Assert.False(s.Add("exemple.com"));
        Assert.True(s.Add("autre.fr"));
        Assert.True(s.Remove("https://autre.fr/x"));
        Assert.False(s.Remove("autre.fr"));
    }

    [Theory]
    [InlineData("https://exemple.com/a", null, true)]
    [InlineData("https://www.exemple.com", null, true)]
    [InlineData("https://sub.exemple.com/x", null, true)]
    [InlineData("https://notexemple.com", null, false)]
    [InlineData("exemple.com/page", null, true)]
    [InlineData(null, "Mon blog - exemple.com - Chrome", true)]
    [InlineData(null, "autreexemple.com", false)]
    [InlineData(null, "exemple.community", false)]
    [InlineData("https://other.fr", "exemple.com", false)]
    [InlineData(null, null, false)]
    [InlineData("pas une url", null, false)]
    public void IsExcluded(string? url, string? title, bool expected) =>
        Assert.Equal(expected, new ExcludedSites(new[] { "exemple.com" }).IsExcluded(url, title));

    [Fact]
    public void Empty_site_list_excludes_nothing() => Assert.False(new ExcludedSites().IsExcluded("https://exemple.com", "exemple.com"));

    [Fact]
    public void Appearance_helpers()
    {
        Assert.Equal(80, Appearance.ClampScale(50));
        Assert.Equal(140, Appearance.ClampScale(200));
        Assert.Equal(100, Appearance.ClampScale(100));
        Assert.Equal(Appearance.Dark, Appearance.NormalizeStyle("DARK"));
        Assert.Equal(Appearance.Auto, Appearance.NormalizeStyle("x"));
        Assert.Equal(Appearance.Below, Appearance.NormalizePosition("Below"));
        Assert.Equal(Appearance.Above, Appearance.NormalizePosition(null));
        Assert.False(Appearance.IsDark("auto", true));
        Assert.True(Appearance.IsDark("auto", false));
        Assert.True(Appearance.IsDark("dark", true));
        Assert.False(Appearance.IsDark("light", false));
    }

    [Fact]
    public void PlaceBar_above_below_flip_and_clamp()
    {
        (int, int) P(int cx, int cy, string pos) => Appearance.PlaceBar(cx, cy, 200, 34, 10, 24, 0, 0, 1920, 1080, pos);
        Assert.Equal((400, 456), P(500, 500, "above"));
        Assert.Equal((400, 524), P(500, 500, "below"));
        Assert.Equal((400, 44), P(500, 20, "above"));     // pas de place au-dessus : retourné
        Assert.Equal((400, 1026), P(500, 1070, "below")); // pas de place en dessous : retourné
        Assert.Equal((0, 456), P(10, 500, "above"));
        Assert.Equal((1720, 456), P(1900, 500, "above"));
    }

    [Fact]
    public void Defaults_of_v2_settings()
    {
        var s = new AppSettings();
        Assert.True(s.ShowTrayIcon);
        Assert.Equal(100, s.BarScalePercent);
        Assert.Equal(Appearance.Above, s.BarPosition);
        Assert.False(s.CopilotEnabled);
        Assert.True(s.CopilotUseApp);
    }

    [Fact]
    public void Store_round_trip_sites_and_new_settings()
    {
        var dir = Path.Combine(Path.GetTempPath(), "selectbar-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(dir);
            Assert.Equal(0, store.LoadExcludedSites().Count);
            store.SaveExcludedSites(new ExcludedSites(new[] { "exemple.com", "autre.fr" }));
            Assert.True(File.Exists(store.ExcludedSitesPath));
            Assert.Equal("excluded-sites.json", Path.GetFileName(store.ExcludedSitesPath));
            Assert.Equal(new[] { "autre.fr", "exemple.com" }, store.LoadExcludedSites().Items);

            store.Save(new AppSettings { BarScalePercent = 120, BarPosition = "below", CopilotEnabled = true, ShowTrayIcon = false });
            var back = store.Load();
            Assert.Equal(120, back.BarScalePercent);
            Assert.Equal("below", back.BarPosition);
            Assert.True(back.CopilotEnabled);
            Assert.False(back.ShowTrayIcon);

            File.WriteAllText(store.ExcludedSitesPath, "{ not json");
            Assert.Equal(0, store.LoadExcludedSites().Count);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Theory]
    [InlineData("A", "A")]
    [InlineData("z", "Z")]
    [InlineData("D5", "5")]
    [InlineData("F1", "F1")]
    [InlineData("F12", "F12")]
    [InlineData("Space", "Space")]
    [InlineData("Return", "Enter")]
    [InlineData("Enter", "Enter")]
    public void KeyToToken_valid(string key, string expected) => Assert.Equal(expected, HotkeyParser.KeyToToken(key));

    [Theory]
    [InlineData("F13")]
    [InlineData("F0")]
    [InlineData("Escape")]
    [InlineData("LeftCtrl")]
    [InlineData("")]
    [InlineData(null)]
    public void KeyToToken_invalid(string? key) => Assert.Null(HotkeyParser.KeyToToken(key));

    [Fact]
    public void Format_hotkey()
    {
        Assert.Equal("Ctrl+Alt+P", HotkeyParser.Format(true, true, false, false, "P"));
        Assert.Equal("Ctrl+Shift+F5", HotkeyParser.Format(true, false, true, false, "F5"));
        Assert.Equal("Win+1", HotkeyParser.Format(false, false, false, true, "1"));
        Assert.Null(HotkeyParser.Format(false, false, false, false, "P"));
        Assert.Null(HotkeyParser.Format(true, false, false, false, null));
        Assert.True(HotkeyParser.TryParse(HotkeyParser.Format(true, true, true, true, "Q"), out _, out _));
    }

    [Fact]
    public void Daily_recommendation()
    {
        var none = Array.Empty<string>();
        Assert.Contains("pause", Recommendation.Daily(new AppSettings { Enabled = false }, 0, none));
        Assert.Contains("en ligne", Recommendation.Daily(new AppSettings { OnlineActionsEnabled = false }, 0, none));
        var s = new AppSettings { OnlineActionsEnabled = true };
        Assert.Contains("Copier", Recommendation.Daily(s, 0, new[] { "Copier" }));
        Assert.Contains("extension", Recommendation.Daily(s, 0, none));
        Assert.Contains("ordre", Recommendation.Daily(s, 2, none));
    }
}
