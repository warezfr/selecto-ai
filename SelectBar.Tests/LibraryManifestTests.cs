using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SelectBar.Extensions;
using Xunit;

namespace SelectBar.Tests;

/// <summary>Contrôle la bibliothèque extensions-library : manifestes meta.json, fichiers listés et en-têtes.</summary>
public class LibraryManifestTests
{
    private const int ExpectedExtensions = 22;
    private const int ExpectedActions = 35;
    private const string IdentifierPrefix = "ai.selecto.library.";
    private static readonly string[] Families = { "forme", "lignes", "discretion", "redaction", "ailleurs", "lecture" };
    private static readonly string[] Languages = { "fr", "en", "es", "ar" };

    private sealed record LibraryFile(string Folder, string FilePath, ExtensionMetadata Metadata);

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SelectBar.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string LibraryRoot() => Path.Combine(RepositoryRoot(), "extensions-library");

    private static List<string> FolderPaths() =>
        Directory.GetDirectories(LibraryRoot()).OrderBy(d => d, StringComparer.Ordinal).ToList();

    private static JsonElement ReadManifest(string folder)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "meta.json")));
        return doc.RootElement.Clone();
    }

    private static List<string> ListedFiles(JsonElement manifest) =>
        manifest.GetProperty("files").EnumerateArray().Select(e => e.GetString()!).ToList();

    private static ExtensionMetadata ParseHeader(string path)
    {
        Assert.True(ExtensionHeaderParser.TryParse(File.ReadAllText(path), out var metadata, out var error),
            $"{Path.GetFileName(path)} : {error}");
        return metadata!;
    }

    /// <summary>Tous les fichiers listés dans les manifestes, avec leur en-tête analysé.</summary>
    private static List<LibraryFile> AllFiles()
    {
        var result = new List<LibraryFile>();
        foreach (var folder in FolderPaths())
        {
            if (!File.Exists(Path.Combine(folder, "meta.json"))) continue;
            foreach (var name in ListedFiles(ReadManifest(folder)))
            {
                var path = Path.Combine(folder, name);
                if (!File.Exists(path)) continue;
                result.Add(new LibraryFile(folder, path, ParseHeader(path)));
            }
        }
        return result;
    }

    private static void AssertAllLanguages(JsonElement manifest, string key)
    {
        var node = manifest.GetProperty(key);
        foreach (var lang in Languages)
        {
            Assert.True(node.TryGetProperty(lang, out var value) && !string.IsNullOrWhiteSpace(value.GetString()),
                $"« {key} » : langue {lang} manquante");
        }
    }

    [Fact]
    public void The_library_has_22_extensions()
    {
        var manifests = FolderPaths().Where(f => File.Exists(Path.Combine(f, "meta.json"))).ToList();
        Assert.Equal(ExpectedExtensions, manifests.Count);
    }

    [Fact]
    public void Each_manifest_is_complete()
    {
        foreach (var folder in FolderPaths())
        {
            var manifest = ReadManifest(folder);
            var slug = Path.GetFileName(folder);
            Assert.Equal(slug, manifest.GetProperty("slug").GetString());
            Assert.Contains(manifest.GetProperty("family").GetString(), Families);
            Assert.False(string.IsNullOrWhiteSpace(manifest.GetProperty("icon").GetString()), slug + " : icône manquante");
            Assert.Contains(slug + ".js", ListedFiles(manifest));
            AssertAllLanguages(manifest, "name");
            AssertAllLanguages(manifest, "tagline");
            AssertAllLanguages(manifest, "description");
            var example = manifest.GetProperty("example");
            Assert.False(string.IsNullOrWhiteSpace(example.GetProperty("before").GetString()), slug + " : exemple avant manquant");
            Assert.False(string.IsNullOrWhiteSpace(example.GetProperty("after").GetString()), slug + " : exemple après manquant");
        }
    }

    [Fact]
    public void Every_listed_file_exists_and_has_a_valid_header()
    {
        foreach (var folder in FolderPaths())
        {
            foreach (var name in ListedFiles(ReadManifest(folder)))
            {
                var path = Path.Combine(folder, name);
                Assert.True(File.Exists(path), $"Fichier listé introuvable : {path}");
                var meta = ParseHeader(path);
                Assert.StartsWith(IdentifierPrefix, meta.Identifier);
                Assert.True(meta.EnabledByDefault, $"{name} : @enabled-by-default doit valoir true");
                Assert.Contains("text", meta.Requirements);
                Assert.NotNull(meta.Names);
                foreach (var lang in Languages)
                    Assert.True(meta.Names!.ContainsKey(lang), $"{name} : @name:{lang} manquant");
            }
        }
    }

    [Fact]
    public void Every_js_file_in_a_folder_is_listed_in_its_manifest()
    {
        foreach (var folder in FolderPaths())
        {
            var listed = ListedFiles(ReadManifest(folder)).OrderBy(n => n, StringComparer.Ordinal).ToList();
            var onDisk = Directory.GetFiles(folder, "*.js").Select(p => Path.GetFileName(p))
                .OrderBy(n => n, StringComparer.Ordinal).ToList();
            Assert.Equal(listed, onDisk);
        }
    }

    [Fact]
    public void Identifiers_are_unique()
    {
        var identifiers = AllFiles().Select(f => f.Metadata.Identifier).ToList();
        Assert.Equal(identifiers.Count, identifiers.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Every_parent_refers_to_an_existing_identifier()
    {
        var files = AllFiles();
        var identifiers = files.Select(f => f.Metadata.Identifier).ToHashSet(StringComparer.Ordinal);
        foreach (var file in files.Where(f => f.Metadata.Parent != null))
        {
            Assert.True(identifiers.Contains(file.Metadata.Parent!),
                $"{Path.GetFileName(file.FilePath)} : @parent {file.Metadata.Parent} introuvable");
        }
    }

    [Fact]
    public void There_are_35_actions_and_parents_are_not_counted()
    {
        var files = AllFiles();
        var parents = files.Where(f => f.Metadata.Parent != null).Select(f => f.Metadata.Parent).ToHashSet(StringComparer.Ordinal);
        var actions = files.Count(f => !parents.Contains(f.Metadata.Identifier));
        Assert.Equal(ExpectedActions, actions);
    }
}
