using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SelectBar.Extensions;
using Xunit;

namespace SelectBar.Tests;

/// <summary>Inventaire des extensions utilisateur (Réglages) et présence des descriptifs dans les en-têtes livrés.</summary>
public class ExtensionInventoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "selecto-inventory-" + Guid.NewGuid().ToString("N"));

    public ExtensionInventoryTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Write(string file, string identifier, string name, string? parent = null, string? description = null, bool code = true, bool enabled = true)
    {
        var lines = new List<string>
        {
            "// ==SelectoExtension==",
            "// @identifier " + identifier,
            "// @name " + name,
            "// @name:fr " + name + " (fr)",
            "// @name:en " + name + " (en)",
        };
        if (description != null) lines.Add("// @description:fr " + description + " (fr)");
        if (description != null) lines.Add("// @description:en " + description + " (en)");
        if (parent != null) lines.Add("// @parent " + parent);
        lines.Add("// @enabled-by-default " + (enabled ? "true" : "false"));
        lines.Add("// ==/SelectoExtension==");
        if (code) lines.Add("selecto.copyText(selecto.text);");
        File.WriteAllText(Path.Combine(_dir, file), string.Join("\n", lines) + "\n");
    }

    [Fact]
    public void Empty_or_missing_folder_gives_an_empty_list()
    {
        Assert.Empty(ExtensionInventory.List(Path.Combine(_dir, "absent"), "fr"));
        Assert.Empty(ExtensionInventory.List(_dir, "fr"));
    }

    [Fact]
    public void Lists_top_level_extensions_with_localized_title_and_description()
    {
        Write("zig.js", "ai.test.zig", "Zig", description: "Décrit Zig.");
        var list = ExtensionInventory.List(_dir, "en");
        var zig = Assert.Single(list);
        Assert.Equal("ai.test.zig", zig.Id);
        Assert.Equal("Zig (en)", zig.Title);
        Assert.Equal("Décrit Zig. (en)", zig.Description);
        Assert.True(zig.EnabledByDefault);
        Assert.Equal(0, zig.ActionCount);
        Assert.False(zig.IsMenu);
    }

    [Fact]
    public void Description_falls_back_to_french_then_null()
    {
        Write("zig.js", "ai.test.zig", "Zig", description: "Décrit Zig.");
        Write("nu.js", "ai.test.nu", "Nu");
        var list = ExtensionInventory.List(_dir, "es");
        Assert.Equal("Décrit Zig. (fr)", list.Single(e => e.Id == "ai.test.zig").Description);
        Assert.Null(list.Single(e => e.Id == "ai.test.nu").Description);
    }

    [Fact]
    public void Sub_actions_are_counted_not_listed_and_menus_without_children_are_hidden()
    {
        Write("menu.js", "ai.test.menu", "Menu", code: false, description: "Menu.");
        Write("a.js", "ai.test.menu.a", "A", parent: "ai.test.menu");
        Write("b.js", "ai.test.menu.b", "B", parent: "ai.test.menu");
        Write("vide.js", "ai.test.vide", "Vide", code: false);
        Write("orphelin.js", "ai.test.orphelin", "Orphelin", parent: "ai.test.inconnu");
        var list = ExtensionInventory.List(_dir, "fr");

        var menu = Assert.Single(list);
        Assert.Equal("ai.test.menu", menu.Id);
        Assert.True(menu.IsMenu);
        Assert.Equal(2, menu.ActionCount);
        Assert.Equal("Menu. (fr)", menu.Description);
    }

    [Fact]
    public void Same_identifier_in_two_files_keeps_the_last_file_like_the_loader()
    {
        Write("a-first.js", "ai.test.same", "Premier");
        Write("b-second.js", "ai.test.same", "Second");
        var only = Assert.Single(ExtensionInventory.List(_dir, "fr"));
        Assert.Equal("Second (fr)", only.Title);
    }

    [Fact]
    public void Disabled_and_invalid_files_are_reported_consistently()
    {
        Write("off.js", "ai.test.off", "Off", enabled: false);
        File.WriteAllText(Path.Combine(_dir, "bad.js"), "selecto.copyText(1);");
        File.WriteAllText(Path.Combine(_dir, "notes.txt"), "// ==SelectoExtension==");
        var list = ExtensionInventory.List(_dir, "fr");
        var off = Assert.Single(list);
        Assert.False(off.EnabledByDefault);
    }

    [Fact]
    public void Every_built_in_and_library_file_has_a_description_in_each_language()
    {
        var root = RepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "SelectBar", "BuiltInExtensions"), "*.js")
            .Concat(Directory.GetDirectories(Path.Combine(root, "extensions-library"))
                .SelectMany(d => Directory.GetFiles(d, "*.js")))
            .ToList();
        Assert.True(files.Count >= 49, $"fichiers trouvés : {files.Count}");
        foreach (var file in files)
        {
            Assert.True(ExtensionHeaderParser.TryParse(File.ReadAllText(file), out var meta, out var err), $"{Path.GetFileName(file)} : {err}");
            foreach (var lang in new[] { "fr", "en", "es", "ar" })
            {
                var text = meta!.DescriptionFor(lang);
                Assert.True(!string.IsNullOrEmpty(text) && meta.Descriptions!.ContainsKey(lang), $"{Path.GetFileName(file)} : @description:{lang} manquant");
                Assert.True(text!.Length <= ExtensionHeaderParser.MaxDescriptionLength, $"{Path.GetFileName(file)} : descriptif trop long");
            }
        }
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SelectBar.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
