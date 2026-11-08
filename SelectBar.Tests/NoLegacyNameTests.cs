using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SelectBar.Extensions;
using Xunit;

namespace SelectBar.Tests;

/// <summary>Le nom de l'ancien objet JavaScript ne doit figurer nulle part dans le dépôt (hors Git, binaires et spécifications).</summary>
public class NoLegacyNameTests
{
    // .claude : les missions des agents citent le chemin du dossier local du dépôt, qui n'est pas du code.
    private static readonly string[] SkippedSegments = { ".git", "bin", "obj", "dist", "node_modules", ".claude" }; // dist : artefacts de build (DLL du framework)
    private const string SkippedPrefix = "docs/superpowers/";

    [Fact]
    public void The_legacy_object_name_appears_nowhere_in_the_repository()
    {
        var word = Encoding.UTF8.GetString(Convert.FromBase64String(ExtensionHost.LegacyGlobalNameBase64));
        var root = FindRepositoryRoot();
        var hits = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (rel.StartsWith(SkippedPrefix, StringComparison.Ordinal)) continue;
            if (rel.Split('/').Any(s => Array.IndexOf(SkippedSegments, s) >= 0)) continue;

            string[] lines;
            try { lines = File.ReadAllLines(file); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            for (var i = 0; i < lines.Length; i++)
                if (lines[i].Contains(word, StringComparison.OrdinalIgnoreCase))
                    hits.Add($"{rel}:{i + 1}");
        }

        Assert.True(hits.Count == 0, "Ancien nom trouvé : " + string.Join(", ", hits));
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SelectBar.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
