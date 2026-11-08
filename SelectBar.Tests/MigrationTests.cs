using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SelectBar.Online;
using SelectBar.Settings;
using Xunit;

namespace SelectBar.Tests;

/// <summary>Migration silencieuse « SelectBar » -> « Selecto AI » : dossiers de données, moteur IA, entrée de démarrage Windows.</summary>
public sealed class MigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "selecto-mig-" + Guid.NewGuid().ToString("N"));
    private string Roaming => Path.Combine(_root, "roaming");
    private string Local => Path.Combine(_root, "local");
    private string OldRoaming => Path.Combine(Roaming, "SelectBar");
    private string NewRoaming => Path.Combine(Roaming, "SelectoAI");
    private string OldLocal => Path.Combine(Local, "SelectBar");
    private string NewLocal => Path.Combine(Local, "SelectoAI");

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private void SeedOldRoaming()
    {
        Write(Path.Combine(OldRoaming, "settings.json"), "{\"Enabled\":true}");
        Write(Path.Combine(OldRoaming, "license.key"), "SB1-clé");
        Write(Path.Combine(OldRoaming, "excluded-apps.json"), "[\"a.exe\"]");
        Write(Path.Combine(OldRoaming, "excluded-sites.json"), "[\"x.org\"]");
        Write(Path.Combine(OldRoaming, "Extensions", "mon-action.js"), "// extension");
        Write(Path.Combine(OldRoaming, "selectbar.log"), "journal");
        Write(Path.Combine(OldRoaming, "selftest-report.txt"), "rapport");
    }

    [Fact]
    public void Names_are_the_new_product_folders()
    {
        Assert.Equal("SelectoAI", AppPaths.FolderName);
        Assert.Equal("SelectBar", AppPaths.LegacyFolderName);
    }

    [Fact]
    public void Roaming_data_is_copied_and_the_old_folder_is_left_intact()
    {
        SeedOldRoaming();
        var (migrated, files) = AppDataMigration.MigrateRoaming(Roaming);
        Assert.True(migrated);
        Assert.Equal(5, files);   // réglages, clé, deux exclusions, extension : ni journal ni rapport d'auto-test
        Assert.Equal("{\"Enabled\":true}", File.ReadAllText(Path.Combine(NewRoaming, "settings.json")));
        Assert.Equal("SB1-clé", File.ReadAllText(Path.Combine(NewRoaming, "license.key")));
        Assert.Equal("// extension", File.ReadAllText(Path.Combine(NewRoaming, "Extensions", "mon-action.js")));
        Assert.False(File.Exists(Path.Combine(NewRoaming, "selectbar.log")));
        Assert.False(File.Exists(Path.Combine(NewRoaming, "selftest-report.txt")));
        // l'ancien dossier est intact
        Assert.Equal(7, Directory.EnumerateFiles(OldRoaming, "*", SearchOption.AllDirectories).Count());
        Assert.Equal("SB1-clé", File.ReadAllText(Path.Combine(OldRoaming, "license.key")));
        Assert.Empty(Directory.EnumerateFiles(NewRoaming, "*.migrating", SearchOption.AllDirectories));
    }

    [Fact]
    public void Roaming_migration_runs_once_and_never_overwrites_new_data()
    {
        SeedOldRoaming();
        Assert.True(AppDataMigration.MigrateRoaming(Roaming).Migrated);
        File.WriteAllText(Path.Combine(NewRoaming, "settings.json"), "{\"Enabled\":false}");   // l'utilisateur change un réglage
        File.WriteAllText(Path.Combine(OldRoaming, "settings.json"), "{\"Enabled\":true,\"x\":1}");
        Assert.False(AppDataMigration.MigrateRoaming(Roaming).Migrated);
        Assert.Equal("{\"Enabled\":false}", File.ReadAllText(Path.Combine(NewRoaming, "settings.json")));
    }

    [Fact]
    public void A_new_folder_that_only_holds_a_log_does_not_block_the_migration()
    {
        SeedOldRoaming();
        Write(Path.Combine(NewRoaming, "selecto.log"), "créé par le journal avant la migration");
        Assert.True(AppDataMigration.ShouldMigrate(OldRoaming, NewRoaming));
        Assert.True(AppDataMigration.MigrateRoaming(Roaming).Migrated);
        Assert.True(File.Exists(Path.Combine(NewRoaming, "license.key")));
    }

    [Fact]
    public void A_new_folder_with_real_data_blocks_the_migration()
    {
        SeedOldRoaming();
        Write(Path.Combine(NewRoaming, "settings.json"), "{}");
        Assert.False(AppDataMigration.ShouldMigrate(OldRoaming, NewRoaming));
        Assert.False(File.Exists(Path.Combine(NewRoaming, "license.key")));
    }

    [Fact]
    public void Nothing_happens_on_a_fresh_install()
    {
        var report = AppDataMigration.Run(Roaming, Local);
        Assert.False(report.RoamingMigrated);
        Assert.Equal(OllamaMigration.NothingToMove, report.Ollama);
        Assert.False(Directory.Exists(NewRoaming));
        Assert.False(Directory.Exists(NewLocal));
    }

    [Fact]
    public void Plan_lists_only_missing_non_log_files()
    {
        SeedOldRoaming();
        Write(Path.Combine(NewRoaming, "license.key"), "déjà là");
        var plan = AppDataMigration.PlanCopy(OldRoaming, NewRoaming);
        var names = plan.Select(op => Path.GetRelativePath(NewRoaming, op.Target)).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.DoesNotContain("license.key", names);
        Assert.DoesNotContain("selectbar.log", names);
        Assert.Contains("settings.json", names);
        Assert.Contains(Path.Combine("Extensions", "mon-action.js"), names);
        Assert.Empty(AppDataMigration.PlanCopy(Path.Combine(_root, "absent"), NewRoaming));
    }

    [Theory]
    [InlineData("selectbar.log", true)]
    [InlineData("Selecto.LOG", true)]
    [InlineData("settings.json.migrating", true)]
    [InlineData("selftest-report.txt", true)]
    [InlineData("settings.json", false)]
    [InlineData("license.key", false)]
    public void Skipped_files(string name, bool skipped) => Assert.Equal(skipped, AppDataMigration.IsSkipped(Path.Combine("sub", name)));

    [Fact]
    public void The_engine_folder_is_moved()
    {
        Write(Path.Combine(OldLocal, "ollama", "models", "blobs", "m.bin"), "modèle");
        Write(Path.Combine(OldLocal, "ollama", "app", "ollama.exe"), "exe");
        var report = AppDataMigration.MigrateLocal(Local);
        Assert.Equal(OllamaMigration.Moved, report);
        Assert.Equal("modèle", File.ReadAllText(Path.Combine(NewLocal, "ollama", "models", "blobs", "m.bin")));
        Assert.False(Directory.Exists(Path.Combine(OldLocal, "ollama")));
    }

    [Fact]
    public void An_existing_new_engine_folder_is_kept_and_the_old_one_untouched()
    {
        Write(Path.Combine(OldLocal, "ollama", "app", "ollama.exe"), "ancien");
        Write(Path.Combine(NewLocal, "ollama", "app", "ollama.exe"), "nouveau");
        var report = AppDataMigration.MigrateLocal(Local);
        Assert.Equal(OllamaMigration.AlreadyThere, report);
        Assert.Equal("nouveau", File.ReadAllText(Path.Combine(NewLocal, "ollama", "app", "ollama.exe")));
        Assert.True(File.Exists(Path.Combine(OldLocal, "ollama", "app", "ollama.exe")));
    }

    [Fact]
    public void An_empty_new_engine_folder_does_not_block_the_move()
    {
        Write(Path.Combine(OldLocal, "ollama", "app", "ollama.exe"), "exe");
        Directory.CreateDirectory(Path.Combine(NewLocal, "ollama"));
        Assert.Equal(OllamaMigration.Moved, AppDataMigration.MigrateLocal(Local));
        Assert.True(File.Exists(Path.Combine(NewLocal, "ollama", "app", "ollama.exe")));
    }

    [Fact]
    public void Engine_path_follows_the_new_folder_and_falls_back_on_the_old_one_if_the_move_failed()
    {
        var current = Path.Combine(Local, "SelectoAI", "ollama");
        var legacy = Path.Combine(Local, "SelectBar", "ollama");
        Assert.Equal(current, OllamaPaths.Resolve(Local, _ => false).Root);                       // rien d'installé : nouveau chemin
        Assert.Equal(legacy, OllamaPaths.Resolve(Local, p => p == legacy).Root);                  // déplacement impossible : ancien chemin
        Assert.Equal(current, OllamaPaths.Resolve(Local, p => p == current || p == legacy).Root); // les deux : le nouveau
        Assert.Equal(current, OllamaPaths.Resolve(Local, p => p == current).Root);
        Assert.EndsWith(Path.Combine("SelectoAI", "ollama"), OllamaPaths.Resolve(Local, _ => false).Root);
    }

    [Fact]
    public void Full_run_reports_every_step()
    {
        SeedOldRoaming();
        Write(Path.Combine(OldLocal, "ollama", "x"), "x");
        var notes = new List<string>();
        var report = AppDataMigration.Run(Roaming, Local, notes.Add);
        Assert.True(report.RoamingMigrated);
        Assert.Equal(5, report.FilesCopied);
        Assert.Equal(OllamaMigration.Moved, report.Ollama);
        Assert.NotEmpty(notes);
        // idempotente
        var second = AppDataMigration.Run(Roaming, Local);
        Assert.False(second.RoamingMigrated);
    }

    // ------------------------------------------------------------ entrée de démarrage Windows

    private sealed class FakeRunKey : IRunKey
    {
        public Dictionary<string, string> Values { get; } = new();
        public string? Get(string name) => Values.TryGetValue(name, out var v) ? v : null;
        public void Set(string name, string value) => Values[name] = value;
        public void Delete(string name) => Values.Remove(name);
    }

    [Fact]
    public void Startup_entry_is_renamed_and_the_old_one_removed()
    {
        var key = new FakeRunKey();
        key.Values["SelectBar"] = "\"C:\\Old\\SelectBar.exe\"";
        StartupEntries.Apply(key, true, @"C:\Program Files\Selecto AI\SelectoAI.exe");
        Assert.False(key.Values.ContainsKey("SelectBar"));
        Assert.Equal("\"C:\\Program Files\\Selecto AI\\SelectoAI.exe\"", key.Values["Selecto AI"]);
        Assert.True(StartupEntries.IsEnabled(key));
    }

    [Fact]
    public void Disabled_startup_removes_both_entries()
    {
        var key = new FakeRunKey();
        key.Values["SelectBar"] = "a";
        key.Values["Selecto AI"] = "b";
        StartupEntries.Apply(key, false, @"C:\x.exe");
        Assert.Empty(key.Values);
        Assert.False(StartupEntries.IsEnabled(key));
        StartupEntries.Apply(key, false, @"C:\x.exe");   // idempotent
        StartupEntries.Apply(key, true, null);           // chemin inconnu : rien d'écrit
        Assert.Empty(key.Values);
    }
}
