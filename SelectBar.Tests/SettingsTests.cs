using System;
using System.IO;
using SelectBar.Settings;
using Xunit;

namespace SelectBar.Tests;

public class SettingsTests
{
    [Fact]
    public void Excluded_apps_normalize()
    {
        var e = new ExcludedApps(new[] { "Chrome", @"C:\Program Files\App\Code.EXE", "  " });
        Assert.True(e.Contains("chrome.exe"));
        Assert.True(e.Contains("CODE.exe"));
        Assert.False(e.Contains("firefox.exe"));
        Assert.False(e.Contains(null));
        Assert.Equal(2, e.Items.Count);
        Assert.True(e.Remove("chrome"));
        Assert.False(e.Contains("chrome.exe"));
    }

    [Theory]
    [InlineData("Ctrl+Alt+P", true, HotkeyParser.Control | HotkeyParser.Alt | HotkeyParser.NoRepeat, (uint)'P')]
    [InlineData("shift+f5", true, HotkeyParser.Shift | HotkeyParser.NoRepeat, 0x74u)]
    [InlineData("Win+1", true, HotkeyParser.Win | HotkeyParser.NoRepeat, (uint)'1')]
    [InlineData("P", false, 0u, 0u)]
    [InlineData("Ctrl+", false, 0u, 0u)]
    [InlineData("Ctrl+Alt+PP", false, 0u, 0u)]
    [InlineData("Ctrl+F13", false, 0u, 0u)]
    [InlineData("", false, 0u, 0u)]
    public void Hotkey(string text, bool ok, uint mods, uint vk)
    {
        Assert.Equal(ok, HotkeyParser.TryParse(text, out var m, out var k));
        if (ok) { Assert.Equal(mods, m); Assert.Equal(vk, k); }
    }

    [Fact]
    public void Save_after_exit_lock_is_ignored()
    {
        var dir = Path.Combine(Path.GetTempPath(), "selectbar-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(dir);
            store.Save(new AppSettings { ConfirmQuit = false });
            Assert.False(store.Locked);

            store.Lock();
            Assert.True(store.Locked);
            store.Save(new AppSettings { ConfirmQuit = true });   // copie périmée après la sortie
            Assert.False(new SettingsStore(dir).Load().ConfirmQuit);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Store_round_trip_and_corruption()
    {
        var dir = Path.Combine(Path.GetTempPath(), "selectbar-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(dir);
            Assert.True(store.Load().Enabled);
            Assert.True(store.LoadExcluded().Contains("cmd.exe"));

            var s = new AppSettings { SearchEngineId = "kagi", Enabled = false };
            s.DisabledActions.Add("x");
            store.Save(s);
            var back = store.Load();
            Assert.Equal("kagi", back.SearchEngineId);
            Assert.False(back.Enabled);
            Assert.Equal(new[] { "x" }, back.DisabledActions);

            store.SaveExcluded(new ExcludedApps(new[] { "foo" }));
            Assert.True(store.LoadExcluded().Contains("foo.exe"));
            Assert.False(store.LoadExcluded().Contains("cmd.exe"));

            File.WriteAllText(store.SettingsPath, "{ not json");
            Assert.Equal("google", store.Load().SearchEngineId);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
