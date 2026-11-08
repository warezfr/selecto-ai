using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SelectBar.Localization;
using SelectBar.Packaging;
using SelectBar.Settings;
using SelectBar.Support;
using SelectBar.UI;
using Xunit;

namespace SelectBar.Tests;

/// <summary>Version 4.0.0 : logiciel libre (MIT) et gratuit. Ni essai, ni clé, ni prix, ni achat nulle part.</summary>
[Collection("Localization")]
public class FreeSoftwareTests
{
    public FreeSoftwareTests() => L.Culture = UiCulture.Make("fr");

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SelectBar.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n").Trim();

    private static readonly string[] Resx = { "Strings.resx", "Strings.en.resx", "Strings.es.resx", "Strings.ar.resx" };

    private static Dictionary<string, string> ReadResx(string file)
    {
        var path = Path.Combine(RepoRoot(), "SelectBar", "Localization", file);
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => d.Attribute("name")!.Value, d => d.Element("value")?.Value ?? "", StringComparer.Ordinal);
    }

    [Fact]
    public void AboutShowsMitAndGitHub()
    {
        // ligne « Logiciel libre (MIT) » dans chaque langue
        foreach (var file in Resx) Assert.Contains("MIT", ReadResx(file)["About_FreeSoftware"]);
        Assert.Equal("Logiciel libre (MIT)", L.Get("About_FreeSoftware"));

        // lien GitHub dans la liste des liens de À propos (français et anglais), et sur l'écran de licence
        Assert.Equal("https://github.com/warezfr/selecto-ai", EditionInfo.GitHubUrl);
        foreach (var lang in new[] { "fr", "en", "es", "ar" })
            Assert.Contains(AboutLinks.For(lang), l => l.LabelKey == "About_LinkSource" && l.Url == EditionInfo.GitHubUrl);

        // texte MIT affiché dans l'application : identique au fichier LICENSE du dépôt
        var license = File.ReadAllText(Path.Combine(RepoRoot(), "LICENSE"));
        Assert.Equal(Normalize(license), Normalize(MitLicense.Text));
        Assert.StartsWith("MIT License", MitLicense.Text);
        Assert.Contains("Copyright (c) 2026 EL MAHDI ZINEDDINE", MitLicense.Text);
        Assert.Contains("Permission is hereby granted, free of charge", MitLicense.Text);
        Assert.Contains("THE SOFTWARE IS PROVIDED \"AS IS\"", MitLicense.Text);
    }

    [Fact]
    public void AboutHasNoUpdateRowInTheStoreAndKeepsTheCheckOtherwise()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "SelectBar", "UI", "SettingsWindow.cs"));
        var start = source.IndexOf("private UIElement[] BuildUpdateRows()", StringComparison.Ordinal);
        Assert.True(start > 0);
        var body = source.Substring(start, 700);
        Assert.Contains("#if EDITION_STORE", body);
        Assert.Contains("Array.Empty<UIElement>()", body);
        Assert.Equal("Rechercher une mise à jour", L.Get("About_CheckUpdate"));
    }

    [Fact]
    public void WelcomeHasNoTrialStep()
    {
        // trois écrans : présentation, niveau d'IA, logiciel libre et gratuit
        Assert.Equal(3, FirstRunFlow.StepCount);
        Assert.Equal(new[] { "Intro", "Ai", "Free" }, Enum.GetNames<WelcomeStep>());
        Assert.Equal("C'est parti", FirstRunFlow.PrimaryLabel(WelcomeStep.Free));

        foreach (var file in Resx)
        {
            var values = ReadResx(file);
            Assert.True(values.ContainsKey("Welcome_FreeTitle"));
            Assert.Contains("GitHub", values["Welcome_FreeGitHub"]);
            Assert.DoesNotContain(values.Keys, k => k.StartsWith("Welcome_Trial", StringComparison.Ordinal) || k.StartsWith("Welcome_HaveKey", StringComparison.Ordinal));
        }
        Assert.Equal("Logiciel libre et gratuit", L.Get("Welcome_FreeTitle"));

        var window = File.ReadAllText(Path.Combine(RepoRoot(), "SelectBar", "UI", "WelcomeWindow.cs"));
        Assert.Contains("EditionInfo.GitHubUrl", window);
        Assert.DoesNotContain("Trial", window);
        Assert.DoesNotContain("Licen", window);
    }

    // Seules les clés « Mit_* » (page et texte de la licence MIT) peuvent employer ces mots.
    private static readonly Regex ForbiddenWords = new(
        @"\b(licen[cs]e|licence|licencia|essai|trial|acheter|buy|comprar|prueba gratuita)|€|\bpro\b|abonnement|subscription",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [Fact]
    public void NoPriceOrLicenseWording()
    {
        foreach (var file in Resx)
        {
            foreach (var (key, value) in ReadResx(file))
            {
                if (key.StartsWith("Mit_", StringComparison.Ordinal)) continue;
                Assert.False(ForbiddenWords.IsMatch(key), file + " : clé " + key);
                Assert.False(ForbiddenWords.IsMatch(value), file + " : " + key + " = " + value);
            }
        }
        // la page licence MIT existe dans chaque langue
        foreach (var file in Resx)
        {
            var values = ReadResx(file);
            foreach (var key in new[] { "Mit_Title", "Mit_Name", "Mit_Help", "Mit_Read", "Mit_Intro", "Mit_OpenGitHub" })
                Assert.False(string.IsNullOrWhiteSpace(values[key]), file + " : " + key);
        }
    }

    [Fact]
    public void NoLicenseOrTrialCodeRemains()
    {
        var root = RepoRoot();
        Assert.False(Directory.Exists(Path.Combine(root, "SelectBar.LicenseTool")));
        var files = Directory.EnumerateFiles(Path.Combine(root, "SelectBar", "Edition"), "*.cs", SearchOption.AllDirectories)
            .Select(Path.GetFileName).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "EditionInfo.cs", "MitLicense.cs", "StoreStartup.cs" }, files);
    }

    [Fact]
    public void SettingsSchemaMigratesFrom3()
    {
        // réglages 3.2.x : schéma 5, champ d'essai et champs de licence qui n'existent plus
        const string json = """
            {
              "SettingsSchema": 5,
              "Enabled": true,
              "StartWithWindows": true,
              "UiLanguage": "en",
              "SearchEngineId": "bing",
              "ConfirmQuit": false,
              "LastTrialToastDay": "2026-10-20",
              "LicenseKey": "SB1-secret",
              "Edition": "Demo",
              "DeviceId": "abcdef"
            }
            """;
        var dir = Path.Combine(Path.GetTempPath(), "selecto-v3-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "settings.json"), json);
            var store = new SettingsStore(dir);

            var loaded = store.Load();   // aucune exception

            Assert.True(loaded.Enabled);
            Assert.True(loaded.StartWithWindows);
            Assert.Equal("en", loaded.UiLanguage);
            Assert.Equal("bing", loaded.SearchEngineId);
            Assert.False(loaded.ConfirmQuit);
            Assert.Equal(AppSettings.CurrentSchema, loaded.SettingsSchema);

            // réécrits en 4.0.0, les champs de licence ont disparu
            store.Save(loaded);
            var saved = File.ReadAllText(Path.Combine(dir, "settings.json"));
            Assert.DoesNotContain("LastTrialToastDay", saved);
            Assert.DoesNotContain("SB1-secret", saved);
            Assert.DoesNotContain("DeviceId", saved);
            using var doc = JsonDocument.Parse(saved);
            Assert.False(doc.RootElement.TryGetProperty("LicenseKey", out _));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ConfirmQuitAndFeedbackServerAreKept()
    {
        Assert.True(new AppSettings().ConfirmQuit);
        Assert.Equal("https://selecto-api.span.observer", FeedbackServer.ResolveBaseUrl());
        Assert.Equal("http://127.0.0.1:8080", FeedbackServer.ResolveBaseUrl(n => n == FeedbackServer.EnvironmentVariable ? "http://127.0.0.1:8080/" : null));
        Assert.Equal(FeedbackServer.BaseUrl, FeedbackServer.ResolveBaseUrl(_ => "http://example.com"));
        Assert.Equal("/v1/feedback", FeedbackServer.FeedbackPath);
    }

    [Fact]
    public void ProjectVersionIs400AndEditionsAreSiteAndStore()
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "SelectBar", "SelectBar.csproj"));
        Assert.Contains("<Version>4.0.0</Version>", csproj);
        Assert.Contains("<AssemblyVersion>4.0.0.0</AssemblyVersion>", csproj);
        Assert.Contains("'$(Edition)' == ''\">Site<", csproj);
        Assert.DoesNotContain("EDITION_PRO", csproj);
        Assert.DoesNotContain("EDITION_DEMO", csproj);
        Assert.Equal(new[] { "Site", "Store" }, Enum.GetNames<AppEdition>());
    }
}
