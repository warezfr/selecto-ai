using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace SelectBar.Tests;

/// <summary>
/// Garde-fou v3.1 : aucun texte français littéral ne reste dans l'interface.
/// Le test de dépôt est actif depuis la fin de T7 : toute nouvelle chaîne française doit passer par les ressources.
/// </summary>
public class NoLiteralFrenchTests
{
    private static readonly string[] ScannedDirs =
    {
        "SelectBar/UI", "SelectBar/Actions", "SelectBar/Update", "SelectBar/Support", "SelectBar/Edition", "SelectBar/Settings", "SelectBar/Online",
    };

    private static readonly string[] Glossary =
    {
        "Réglages", "Quitter", "Rechercher", "Copier", "Remplacer",
        "Activer", "Signaler", "Fermer", "Appliquer", "Annuler", "Barre",
    };

    private const string AccentedLetters = "àâçéèêëîïôùûüœÀÂÇÉÈÊËÎÏÔÙÛÜŒ";

    // Chaîne verbatim @"…" (guillemets doublés) ou chaîne ordinaire "…" (échappements \).
    // Les chaînes interpolées $"…" sont captées par la seconde branche.
    private static readonly Regex StringLiteral = new(
        @"@""((?:[^""]|"""")*)""|""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);

    private static readonly Regex GlossaryWord = new(
        @"\b(?:" + string.Join("|", Glossary.Select(Regex.Escape)) + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Détection pure d'un texte français dans une ligne de source.</summary>
    public static class LiteralScanner
    {
        public static IEnumerable<string> Find(string sourceLine)
        {
            var trimmed = sourceLine.TrimStart();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("/*")) yield break;
            // journaux et diagnostics : jamais affichés à l'utilisateur
            if (IsDiagnosticLine(sourceLine)) yield break;

            foreach (Match m in StringLiteral.Matches(sourceLine))
            {
                string text = m.Groups[1].Success
                    ? m.Groups[1].Value.Replace("\"\"", "\"")
                    : m.Groups[2].Value;
                if (IsFrench(text)) yield return text;
            }
        }

        /// <summary>Ligne d'écriture de journal ou de trace (texte réservé aux développeurs).</summary>
        public static bool IsDiagnosticLine(string line) =>
            line.Contains("Log.Write(") || line.Contains("OnlineLog.Write(") || line.Contains("_log(")
            || line.Contains("LogLayout(") || line.Contains("Trace.") || line.Contains("Debug.");

        public static bool IsFrench(string text) =>
            text.IndexOfAny(AccentedLetters.ToCharArray()) >= 0 || GlossaryWord.IsMatch(text);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SelectBar.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    /// <summary>Lit « chemin/relatif.cs|texte exact » ; « *|texte » vaut pour tous les fichiers.</summary>
    private static HashSet<(string, string)> ReadAllowed(string file)
    {
        var set = new HashSet<(string, string)>();
        foreach (var raw in File.ReadAllLines(file))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            int bar = line.IndexOf('|');
            if (bar < 0) continue;
            set.Add((line.Substring(0, bar).Trim(), line.Substring(bar + 1).Trim()));
        }
        return set;
    }

    [Fact]
    public void No_untranslated_french_literal_in_UI_code()
    {
        var root = RepoRoot();
        var allowed = ReadAllowed(Path.Combine(root, "SelectBar.Tests", "allowed-literals.txt"));
        var hits = new List<string>();

        foreach (var dir in ScannedDirs)
        {
            var full = Path.Combine(root, dir.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(Directory.Exists(full), $"Dossier introuvable : {full}");

            foreach (var file in Directory.EnumerateFiles(full, "*.cs", SearchOption.AllDirectories)
                         .OrderBy(f => f, StringComparer.Ordinal))
            {
                var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (var text in LiteralScanner.Find(lines[i]))
                    {
                        if (allowed.Contains((rel, text)) || allowed.Contains(("*", text)) || allowed.Contains((rel, text.Trim())) || allowed.Contains(("*", text.Trim()))) continue;
                        hits.Add($"{rel}:{i + 1}: \"{text}\"");
                    }
                }
            }
        }

        Assert.True(hits.Count == 0,
            $"{hits.Count} littéral(aux) français non localisé(s) :\n" + string.Join("\n", hits));
    }

    [Fact]
    public void Finder_detects_accented_literal()
    {
        Assert.Equal(new[] { "Réglages" }, LiteralScanner.Find("x = \"Réglages\";"));
    }

    [Fact]
    public void Finder_ignores_comment_lines()
    {
        Assert.Empty(LiteralScanner.Find("// Réglages"));
    }

    [Fact]
    public void Finder_ignores_log_calls()
    {
        Assert.Empty(LiteralScanner.Find("Log.Write(\"Réglé\")"));
    }

    [Theory]
    [InlineData("_log(\"Réglé\");")]
    [InlineData("LogLayout(\"Fenêtre ouverte\");")]
    [InlineData("Trace.WriteLine(\"Réglé\");")]
    [InlineData("Debug.WriteLine(\"Réglé\");")]
    public void Finder_ignores_trace_debug_and_log_helpers(string line) =>
        Assert.Empty(LiteralScanner.Find(line));

    [Fact]
    public void Finder_detects_interpolated_string()
    {
        Assert.Single(LiteralScanner.Find("$\"{n} appareils activés\""));
    }
}
