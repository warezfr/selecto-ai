using System;
using System.IO;
using System.Text.Json;
using SelectBar.Settings;
using Xunit;

namespace SelectBar.Tests;

public class SettingsMigrationTests
{
    private static AppSettings Parse(string json) => JsonSerializer.Deserialize<AppSettings>(json)!;

    [Fact]
    public void New_install_gets_online_actions_on_without_announcement()
    {
        var (s, justEnabled) = SettingsMigration.Upgrade(new AppSettings(), fileExisted: false);
        Assert.True(s.OnlineActionsEnabled);
        Assert.Equal(AppSettings.CurrentSchema, s.SettingsSchema);
        Assert.False(justEnabled);
    }

    [Fact]
    public void Old_file_without_schema_is_migrated_once_and_announced()
    {
        var (s, justEnabled) = SettingsMigration.Upgrade(Parse("{\"Enabled\":true}"), fileExisted: true);
        Assert.True(s.OnlineActionsEnabled);
        Assert.Equal(AppSettings.CurrentSchema, s.SettingsSchema);
        Assert.True(justEnabled);
    }

    [Fact]
    public void Schema_3_keeps_the_users_choice()
    {
        var (s, justEnabled) = SettingsMigration.Upgrade(Parse("{\"OnlineActionsEnabled\":false,\"SettingsSchema\":3}"), fileExisted: true);
        Assert.False(s.OnlineActionsEnabled);
        Assert.False(justEnabled);
    }

    [Fact]
    public void Migrated_file_is_not_migrated_again()
    {
        var (first, _) = SettingsMigration.Upgrade(Parse("{\"OnlineActionsEnabled\":false}"), fileExisted: true);
        first.OnlineActionsEnabled = false;   // l'utilisateur la désactive ensuite, puis le fichier est enregistré au schéma 3
        var json = JsonSerializer.Serialize(first);
        var (again, justEnabled) = SettingsMigration.Upgrade(Parse(json), fileExisted: true);
        Assert.False(again.OnlineActionsEnabled);
        Assert.False(justEnabled);
    }

    [Fact]
    public void Store_load_corrupt_file_gives_defaults_without_announcement()
    {
        var dir = Path.Combine(Path.GetTempPath(), "selecto-mig-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "settings.json"), "{ ceci n'est pas du json");
            var s = new SettingsStore(dir).Load();
            Assert.True(s.OnlineActionsEnabled);
            Assert.False(s.OnlineActionsJustEnabled);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch (IOException) { } }
    }

    [Fact]
    public void Store_load_old_file_sets_announcement_flag_and_save_persists_schema()
    {
        var dir = Path.Combine(Path.GetTempPath(), "selecto-mig-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "settings.json"), "{\"Enabled\":true,\"OnlineActionsEnabled\":false}");
            var store = new SettingsStore(dir);
            var s = store.Load();
            Assert.True(s.OnlineActionsEnabled);
            Assert.True(s.OnlineActionsJustEnabled);
            store.Save(s);
            var again = store.Load();
            Assert.Equal(AppSettings.CurrentSchema, again.SettingsSchema);
            Assert.False(again.OnlineActionsJustEnabled);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch (IOException) { } }
    }
}
