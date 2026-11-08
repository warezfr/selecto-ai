using System.Text.Json;
using SelectBar.Settings;
using Xunit;

namespace SelectBar.Tests;

/// <summary>RC-001 : raccourci par défaut Ctrl+Maj+Espace (affiche la barre après une sélection au clavier), posé par la migration du schéma 5.</summary>
public class HotkeyDefaultTests
{
    private const string Expected = "Ctrl+Shift+Space";

    private static AppSettings Parse(string json) => JsonSerializer.Deserialize<AppSettings>(json)!;

    [Fact]
    public void Schema_goes_up_by_one() => Assert.Equal(5, AppSettings.CurrentSchema);

    [Fact]
    public void New_install_gets_the_default_shortcut()
    {
        var (s, _) = SettingsMigration.Upgrade(new AppSettings(), fileExisted: false);
        Assert.True(s.HotkeyEnabled);
        Assert.Equal(Expected, s.Hotkey);
    }

    [Fact]
    public void Default_shortcut_is_ctrl_shift_space_and_parses()
    {
        Assert.True(HotkeyParser.TryParse(Expected, out var mods, out var vk));
        Assert.Equal(HotkeyParser.Control | HotkeyParser.Shift | HotkeyParser.NoRepeat, mods);
        Assert.Equal(0x20u, vk);
    }

    [Theory]
    [InlineData("Ctrl+Shift+Enter", 0x0Du)]
    [InlineData("ctrl+shift+space", 0x20u)]
    public void Space_and_enter_are_valid_keys(string text, uint expectedVk)
    {
        Assert.True(HotkeyParser.TryParse(text, out _, out var vk));
        Assert.Equal(expectedVk, vk);
    }

    [Fact]
    public void Format_accepts_space_and_enter()
    {
        Assert.Equal("Ctrl+Shift+Space", HotkeyParser.Format(true, false, true, false, "Space"));
        Assert.Equal("Ctrl+Shift+Enter", HotkeyParser.Format(true, false, true, false, "Enter"));
    }

    [Theory]
    [InlineData("{\"Enabled\":true}")]                                                                   // fichier de la 2.x, sans schéma
    [InlineData("{\"SettingsSchema\":3,\"HotkeyEnabled\":false,\"Hotkey\":\"Ctrl+Alt+P\"}")]
    [InlineData("{\"SettingsSchema\":4,\"HotkeyEnabled\":false,\"Hotkey\":\"Ctrl+Alt+P\"}")]            // 3.2.0 : « Aucun » (la touche mémorisée n'est pas active)
    [InlineData("{\"SettingsSchema\":4,\"HotkeyEnabled\":true,\"Hotkey\":\"\"}")]                       // activé mais vide
    [InlineData("{\"SettingsSchema\":4,\"HotkeyEnabled\":true,\"Hotkey\":\"   \"}")]
    public void Settings_without_a_shortcut_receive_the_default(string json)
    {
        var (s, _) = SettingsMigration.Upgrade(Parse(json), fileExisted: true);
        Assert.True(s.HotkeyEnabled);
        Assert.Equal(Expected, s.Hotkey);
        Assert.Equal(AppSettings.CurrentSchema, s.SettingsSchema);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void Existing_shortcut_is_kept(int schema)
    {
        var (s, _) = SettingsMigration.Upgrade(Parse("{\"SettingsSchema\":" + schema + ",\"HotkeyEnabled\":true,\"Hotkey\":\"Ctrl+Alt+P\"}"), fileExisted: true);
        Assert.True(s.HotkeyEnabled);
        Assert.Equal("Ctrl+Alt+P", s.Hotkey);
        Assert.Equal(AppSettings.CurrentSchema, s.SettingsSchema);
    }

    [Fact]
    public void Shortcut_cleared_by_the_user_at_the_current_schema_stays_off()
    {
        var (s, _) = SettingsMigration.Upgrade(Parse("{\"SettingsSchema\":5,\"HotkeyEnabled\":false,\"Hotkey\":\"Ctrl+Alt+P\"}"), fileExisted: true);
        Assert.False(s.HotkeyEnabled);
    }

    [Fact]
    public void Migration_does_not_touch_the_online_actions_choice_of_schema_4()
    {
        var (s, justEnabled) = SettingsMigration.Upgrade(Parse("{\"SettingsSchema\":4,\"OnlineActionsEnabled\":false}"), fileExisted: true);
        Assert.False(s.OnlineActionsEnabled);
        Assert.False(justEnabled);
        Assert.True(s.HotkeyEnabled);
    }
}
