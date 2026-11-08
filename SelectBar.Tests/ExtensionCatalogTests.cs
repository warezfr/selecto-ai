using System.Linq;
using SelectBar.Extensions;
using SelectBar.Settings;
using Xunit;

namespace SelectBar.Tests;

public class ExtensionCatalogTests
{
    private const string Count = "ai.selecto.builtin.count";

    [Fact]
    public void Only_count_is_enabled_by_default()
    {
        var enabled = ExtensionCatalog.BuiltIns.Where(b => b.EnabledByDefault).Select(b => b.Id).ToList();
        Assert.Equal(new[] { Count }, enabled);
    }

    [Fact]
    public void Without_a_setting_the_default_applies()
    {
        var settings = new AppSettings();
        Assert.True(ExtensionCatalog.IsEnabled(settings, Count));
        Assert.True(ExtensionCatalog.IsEnabled(settings, "ai.selecto.builtin.inexistante"));
        Assert.False(ExtensionCatalog.IsEnabled(settings, Count, enabledByDefault: false));
    }

    [Fact]
    public void A_stored_true_forces_on_and_a_stored_false_forces_off()
    {
        var settings = new AppSettings();
        settings.ExtensionEnabled[Count] = false;
        Assert.False(ExtensionCatalog.IsEnabled(settings, Count));
        Assert.False(ExtensionCatalog.IsEnabled(settings, Count, enabledByDefault: true));
        settings.ExtensionEnabled[Count] = true;
        Assert.True(ExtensionCatalog.IsEnabled(settings, Count, enabledByDefault: false));
    }

    [Fact]
    public void Sub_actions_follow_their_menu_unless_set_explicitly()
    {
        const string Menu = ExtensionCatalog.TextMenu;
        const string Upper = Menu + ".upper";
        var settings = new AppSettings();
        Assert.False(ExtensionCatalog.IsActive(settings, Upper, false, Menu, parentActive: false));
        Assert.True(ExtensionCatalog.IsActive(settings, Upper, false, Menu, parentActive: true));
        settings.ExtensionEnabled[Upper] = false;
        Assert.False(ExtensionCatalog.IsActive(settings, Upper, false, Menu, parentActive: true));
        Assert.True(ExtensionCatalog.IsActive(settings, Count, true, null, parentActive: false));
    }
}
