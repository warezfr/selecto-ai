using System.Collections.Generic;
using SelectBar.Actions;
using Xunit;

namespace SelectBar.Tests;

public class CaptureOptionsTests
{
    private static CaptureOptions P(params string[] args) => CaptureOptions.Parse(args, _ => null);

    [Fact]
    public void Default_is_normal_mode()
    {
        var o = P();
        Assert.False(o.CaptureMode);
        Assert.Null(o.OpenSettings);
        Assert.Null(o.SettingsIaStep);
        Assert.Null(o.WindowPos);
    }

    [Fact]
    public void Capture_mode_from_argument()
    {
        Assert.True(P("--capture-mode").CaptureMode);
        Assert.True(P("--CAPTURE-MODE").CaptureMode);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("0", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Capture_mode_from_environment(string? value, bool expected)
    {
        var o = CaptureOptions.Parse(new string[0], n => n == CaptureOptions.EnvVariable ? value : null);
        Assert.Equal(expected, o.CaptureMode);
    }

    [Theory]
    [InlineData("général", SettingsTabArg.General)]
    [InlineData("general", SettingsTabArg.General)]
    [InlineData("Actions", SettingsTabArg.Actions)]
    [InlineData("ia", SettingsTabArg.Ai)]
    [InlineData("about", SettingsTabArg.About)]
    [InlineData("AI", SettingsTabArg.Ai)]
    [InlineData("général", SettingsTabArg.General)]
    [InlineData("ia", SettingsTabArg.Ai)]
    [InlineData("à propos", SettingsTabArg.About)]
    [InlineData("a propos", SettingsTabArg.About)]
    public void Open_settings_valid(string value, SettingsTabArg expected) =>
        Assert.Equal(expected, P("--open-settings=" + value).OpenSettings);

    [Theory]
    [InlineData("--open-settings=")]
    [InlineData("--open-settings=xyz")]
    [InlineData("--open-settings")]
    public void Open_settings_invalid_is_ignored(string arg) => Assert.Null(P(arg).OpenSettings);

    [Fact]
    public void Ia_step_only_1_to_3()
    {
        Assert.Equal(2, P("--settings-ia-step=2").SettingsIaStep);
        Assert.Null(P("--settings-ia-step=4").SettingsIaStep);
        Assert.Null(P("--settings-ia-step=x").SettingsIaStep);
    }

    [Theory]
    [InlineData("100,200", 100, 200)]
    [InlineData(" 10 , 20 ", 10, 20)]
    [InlineData("-1920,40", -1920, 40)]
    public void Window_pos_valid(string value, int x, int y) =>
        Assert.Equal((x, y), P("--window-pos=" + value).WindowPos);

    [Theory]
    [InlineData("")]
    [InlineData("100")]
    [InlineData("1,2,3")]
    [InlineData("a,b")]
    [InlineData("1,")]
    public void Window_pos_invalid_is_ignored(string value) => Assert.Null(P("--window-pos=" + value).WindowPos);

    [Theory]
    [InlineData("--open-welcome", 1)]
    [InlineData("--open-welcome=", 1)]
    [InlineData("--open-welcome=2", 2)]
    [InlineData("--open-welcome=3", 3)]
    public void Open_welcome_valid(string arg, int expected) => Assert.Equal(expected, P("--capture-mode", arg).OpenWelcome);

    [Theory]
    [InlineData("--open-welcome=0")]
    [InlineData("--open-welcome=4")]
    [InlineData("--open-welcome=x")]
    public void Open_welcome_invalid_is_ignored(string arg) => Assert.Null(P("--capture-mode", arg).OpenWelcome);

    [Fact]
    public void Open_flyout_and_theme()
    {
        var o = P("--capture-mode", "--open-flyout", "--theme=dark");
        Assert.True(o.OpenFlyout);
        Assert.True(o.ForceDark);
        Assert.False(P("--capture-mode", "--theme=LIGHT").ForceDark);
        Assert.Null(P("--capture-mode", "--theme=rouge").ForceDark);
        Assert.Null(P("--capture-mode").ForceDark);
        Assert.False(P("--capture-mode").OpenFlyout);
    }

    [Fact]
    public void Fake_update_needs_capture_mode_and_a_valid_version()
    {
        Assert.Equal("9.9.9", P("--capture-mode", "--fake-update=9.9.9").FakeUpdateVersion);
        Assert.Null(P("--fake-update=9.9.9").FakeUpdateVersion);
        Assert.Null(P("--capture-mode", "--fake-update=abc").FakeUpdateVersion);
        Assert.Equal("http://127.0.0.1:8765/latest.json", P("--capture-mode", "--update-url=http://127.0.0.1:8765/latest.json").UpdateUrl);
        Assert.Null(P("--update-url=http://127.0.0.1:8765/latest.json").UpdateUrl);
        Assert.Null(P("--capture-mode", "--update-url=file:///c:/x.json").UpdateUrl);
        Assert.Equal("3.0.0", P("--capture-mode", "--pretend-version=3.0.0").PretendVersion);
        Assert.Null(P("--pretend-version=3.0.0").PretendVersion);
        Assert.Null(P("--capture-mode", "--pretend-version=abc").PretendVersion);
        Assert.Null(P("--capture-mode").FakeUpdateVersion);
    }

    [Fact]
    public void New_options_need_capture_mode()
    {
        var o = P("--open-welcome=2", "--open-flyout", "--theme=dark", "--fake-update=9.9.9");
        Assert.Null(o.OpenWelcome);
        Assert.False(o.OpenFlyout);
        Assert.Null(o.ForceDark);
        Assert.Null(o.FakeUpdateVersion);
    }

    [Fact]
    public void Show_bar_keeps_text_as_is_in_capture_mode()
    {
        Assert.Equal("1250*1,2", P("--capture-mode", "--show-bar=1250*1,2").ShowBarText);
        Assert.Equal("= 1 500", P("--capture-mode", "--show-bar=\"= 1 500\"").ShowBarText);
        Assert.Equal("  = 1 500  ", P("--capture-mode", "--show-bar=  = 1 500  ").ShowBarText);
        Assert.Equal("=", P("--capture-mode", "--show-bar==").ShowBarText);
    }

    [Theory]
    [InlineData("--show-bar=")]
    [InlineData("--show-bar=   ")]
    [InlineData("--show-bar=\"\"")]
    public void Show_bar_empty_is_ignored(string arg) => Assert.Null(P("--capture-mode", arg).ShowBarText);

    [Fact]
    public void Show_bar_needs_capture_mode()
    {
        Assert.Null(P("--show-bar=abc").ShowBarText);
        Assert.False(P("--show-bar=abc", "--show-bar-editable").ShowBarEditable);
    }

    [Fact]
    public void Show_bar_editable_is_a_flag_for_capture_mode()
    {
        Assert.True(P("--capture-mode", "--show-bar=x", "--show-bar-editable").ShowBarEditable);
        Assert.False(P("--capture-mode", "--show-bar=x").ShowBarEditable);
        Assert.False(P("--show-bar=x", "--show-bar-editable").ShowBarEditable);
    }

    [Fact]
    public void Open_bar_menu_needs_capture_mode_and_an_id()
    {
        Assert.Equal("ai.selecto.builtin.text", P("--capture-mode", "--open-bar-menu=ai.selecto.builtin.text").OpenBarMenu);
        Assert.Null(P("--open-bar-menu=ai.selecto.builtin.text").OpenBarMenu);
        Assert.Null(P("--capture-mode", "--open-bar-menu=").OpenBarMenu);
        Assert.Null(P("--capture-mode", "--open-bar-menu=   ").OpenBarMenu);
    }

    [Fact]
    public void Options_combine_and_unknown_args_are_ignored()
    {
        var o = P("--foo", "--capture-mode", "--open-settings=actions", "--window-pos=5,6");
        Assert.True(o.CaptureMode);
        Assert.Equal(SettingsTabArg.Actions, o.OpenSettings);
        Assert.Equal((5, 6), o.WindowPos);
    }

    [Fact]
    public void Simulated_backend_logs_and_never_launches()
    {
        var lines = new List<string>();
        new SimulatedLaunchBackend(lines.Add).Start(LaunchKind.Browser, "https://exemple.com");
        Assert.Equal(new[] { "[capture] lancement simulé : https://exemple.com" }, lines);
    }

    [Fact]
    public void Read_only_store_writes_nothing()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "selecto-ro-" + System.Guid.NewGuid());
        var store = new SelectBar.Settings.SettingsStore(dir) { ReadOnly = true };
        store.Save(new SelectBar.Settings.AppSettings { OnlineActionsEnabled = true });
        Assert.False(System.IO.File.Exists(store.SettingsPath));
    }
}
