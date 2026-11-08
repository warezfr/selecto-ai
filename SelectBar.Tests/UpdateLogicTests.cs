using System;
using System.Collections.Generic;
using System.Linq;
using SelectBar.Update;
using Xunit;

namespace SelectBar.Tests;

[Collection("Localization")]   // série : CaptureOrigin est un état statique partagé avec UpdateRedirectTests
public class UpdateLogicTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static string Manifest(string version = "4.0.0", string? notes = "\"Corrections\"", string installers = "")
    {
        if (installers.Length == 0)
        {
            installers =
                "{\"edition\":\"Site\",\"arch\":\"x64\",\"file\":\"SelectoAI-Setup-x64-4.0.0.exe\",\"size\":1000,\"sha256\":\"" + Sha +
                "\",\"url\":\"https://selecto.span.observer/downloads/SelectoAI-Setup-x64-4.0.0.exe\"}," +
                "{\"edition\":\"Demo\",\"arch\":\"x64\",\"file\":\"SelectoAI-Setup-x64-4.0.0.exe\",\"size\":1000,\"sha256\":\"" + Sha +   // entrée de compatibilité pour les clients 3.x
                "\",\"url\":\"https://selecto.span.observer/downloads/SelectoAI-Setup-x64-4.0.0.exe\"}," +
                "{\"edition\":\"Site\",\"arch\":\"arm64\",\"file\":\"SelectoAI-Setup-arm64-4.0.0.exe\",\"size\":3000,\"sha256\":\"" + Sha +
                "\",\"url\":\"https://selecto.span.observer/downloads/SelectoAI-Setup-arm64-4.0.0.exe\"}";
        }
        return "{\"version\":\"" + version + "\",\"notes\":" + (notes ?? "null") + ",\"installers\":[" + installers + "]}";
    }

    private static UpdateEntry Entry(string edition, string arch) =>
        new(edition, arch, "f.exe", 1, Sha, "https://selecto.span.observer/downloads/f.exe");

    // ---------------------------------------------------------------- lecture de latest.json

    [Fact]
    public void Parse_reads_version_entries_and_notes()
    {
        var parsed = UpdateLogic.Parse(Manifest());
        Assert.NotNull(parsed);
        Assert.Equal("4.0.0", parsed!.Value.Version);
        Assert.Equal("Corrections", parsed.Value.Notes);
        Assert.Equal(3, parsed.Value.Entries.Count);
        Assert.Equal("Demo", parsed.Value.Entries[1].Edition);
        Assert.Equal("x64", parsed.Value.Entries[1].Arch);
        Assert.Equal(1000, parsed.Value.Entries[1].Size);
        Assert.Equal(Sha, parsed.Value.Entries[1].Sha256);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{")]
    [InlineData("pas du json")]
    [InlineData("[]")]
    [InlineData("\"texte\"")]
    public void Parse_returns_null_for_invalid_json(string json)
    {
        Assert.Null(UpdateLogic.Parse(json));
    }

    [Fact]
    public void Parse_returns_null_when_null_input()
    {
        Assert.Null(UpdateLogic.Parse(null));
    }

    [Theory]
    [InlineData("{\"notes\":null,\"installers\":[]}")]                 // version absente
    [InlineData("{\"version\":42,\"installers\":[]}")]                  // version non textuelle
    [InlineData("{\"version\":\"abc\",\"installers\":[]}")]             // version non numérique
    [InlineData("{\"version\":\"3.1.0-beta\",\"installers\":[]}")]      // préversion refusée
    [InlineData("{\"version\":\"\",\"installers\":[]}")]                 // version vide
    [InlineData("{\"version\":\"3.1.0\"}")]                              // installers absent
    [InlineData("{\"version\":\"3.1.0\",\"installers\":{}}")]            // installers pas un tableau
    public void Parse_returns_null_when_version_or_installers_are_missing_or_wrong(string json)
    {
        Assert.Null(UpdateLogic.Parse(json));
    }

    [Fact]
    public void Parse_skips_incomplete_entries_and_keeps_valid_ones()
    {
        var installers =
            "{\"edition\":\"Site\",\"arch\":\"x64\",\"size\":1,\"sha256\":\"" + Sha + "\",\"url\":\"https://selecto.span.observer/a.exe\"}," + // file absent
            "{\"edition\":\"Site\",\"arch\":\"x64\",\"file\":\"a.exe\",\"size\":1,\"url\":\"https://selecto.span.observer/a.exe\"}," +        // sha256 absent
            "{\"edition\":\"Site\",\"arch\":\"x64\",\"file\":\"a.exe\",\"size\":0,\"sha256\":\"" + Sha + "\",\"url\":\"https://selecto.span.observer/a.exe\"}," + // taille nulle
            "{\"edition\":\"Site\",\"arch\":\"x64\",\"file\":\"a.exe\",\"size\":\"12\",\"sha256\":\"" + Sha + "\",\"url\":\"https://selecto.span.observer/a.exe\"}," + // taille texte
            "{\"edition\":\"Site\",\"arch\":\"x64\",\"file\":\"a.exe\",\"size\":1,\"sha256\":\"trop court\",\"url\":\"https://selecto.span.observer/a.exe\"}," +
            "42," +
            "{\"edition\":\"Site\",\"arch\":\"x64\",\"file\":\"a.exe\",\"size\":1,\"sha256\":\"" + Sha + "\",\"url\":\"https://selecto.span.observer/a.exe\"}";
        var parsed = UpdateLogic.Parse(Manifest(installers: installers));
        Assert.NotNull(parsed);
        Assert.Single(parsed!.Value.Entries);
    }

    [Fact]
    public void Parse_drops_entries_with_refused_urls()
    {
        var installers =
            "{\"edition\":\"Site\",\"arch\":\"x64\",\"file\":\"a.exe\",\"size\":1,\"sha256\":\"" + Sha + "\",\"url\":\"http://selecto.span.observer/a.exe\"}," +
            "{\"edition\":\"Site\",\"arch\":\"x64\",\"file\":\"a.exe\",\"size\":1,\"sha256\":\"" + Sha + "\",\"url\":\"https://exemple.com/a.exe\"}," +
            "{\"edition\":\"Site\",\"arch\":\"x64\",\"file\":\"a.exe\",\"size\":1,\"sha256\":\"" + Sha + "\",\"url\":\"https://selecto.span.observer/downloads/a.exe\"}";
        var parsed = UpdateLogic.Parse(Manifest(installers: installers));
        Assert.NotNull(parsed);
        Assert.Single(parsed!.Value.Entries);
        Assert.Equal("Site", parsed.Value.Entries[0].Edition);
    }

    [Fact]
    public void Parse_notes_are_optional()
    {
        Assert.Null(UpdateLogic.Parse(Manifest(notes: null))!.Value.Notes);
        Assert.Null(UpdateLogic.Parse("{\"version\":\"3.1.0\",\"installers\":[]}")!.Value.Notes);
    }

    // ---------------------------------------------------------------- comparaison de versions

    [Theory]
    [InlineData("3.0.1", true)]
    [InlineData("3.1", true)]
    [InlineData("3.1.0", true)]
    [InlineData("4.0", true)]
    [InlineData("v3.0.1", true)]
    [InlineData("3.0.0", false)]
    [InlineData("3.0", false)]
    [InlineData("3.0.0.0", false)]
    [InlineData("2.9", false)]
    [InlineData("2.9.9", false)]
    [InlineData("3.0.0-beta", false)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    public void IsNewer_compares_against_installed_3_0_0(string candidate, bool expected)
    {
        Assert.Equal(expected, UpdateLogic.IsNewer(candidate, new Version(3, 0, 0, 0)));
    }

    [Fact]
    public void IsNewer_handles_two_digit_components()
    {
        Assert.True(UpdateLogic.IsNewer("3.10", new Version(3, 9, 0)));
        Assert.False(UpdateLogic.IsNewer("3.9", new Version(3, 10, 0)));
    }

    // ---------------------------------------------------------------- choix de l'installateur

    [Fact]
    public void Pick_prefers_the_site_entry_for_the_architecture()
    {
        var entries = new[] { Entry("Demo", "x64"), Entry("Site", "x64"), Entry("Site", "arm64") };
        Assert.Equal("Site", UpdateLogic.Pick(entries, "x64")!.Edition);
        Assert.Equal("arm64", UpdateLogic.Pick(entries, "arm64")!.Arch);
    }

    [Fact]
    public void Pick_falls_back_to_any_entry_of_the_same_architecture()
    {
        var entries = new[] { Entry("Demo", "x64"), Entry("Demo", "arm64") };
        var pick = UpdateLogic.Pick(entries, "arm64");
        Assert.NotNull(pick);
        Assert.Equal("arm64", pick!.Arch);
    }

    [Fact]
    public void Pick_never_crosses_architectures()
    {
        var entries = new[] { Entry("Site", "x64") };
        Assert.Null(UpdateLogic.Pick(entries, "arm64"));
        Assert.Null(UpdateLogic.Pick(entries, ""));
    }

    [Fact]
    public void Pick_returns_null_without_any_installer()
    {
        Assert.Null(UpdateLogic.Pick(Array.Empty<UpdateEntry>(), "x64"));
    }

    [Fact]
    public void Pick_ignores_case()
    {
        var entries = new[] { Entry("site", "x64") };
        Assert.NotNull(UpdateLogic.Pick(entries, "X64"));
    }

    // ---------------------------------------------------------------- validation d'URL

    [Theory]
    [InlineData("https://selecto.span.observer/downloads/latest.json", true)]
    [InlineData("https://SELECTO.span.observer/downloads/a.exe", true)]
    [InlineData("https://selecto.span.observer:443/downloads/a.exe", true)]
    [InlineData("http://selecto.span.observer/downloads/a.exe", false)]
    [InlineData("ftp://selecto.span.observer/downloads/a.exe", false)]
    [InlineData("file:///C:/a.exe", false)]
    [InlineData("https://exemple.com/a.exe", false)]
    [InlineData("https://github.com/warezfr/selecto-ai/releases/download/v4.0.0/SelectoAI-Setup-x64-4.0.0.exe", true)]
    [InlineData("https://objects.githubusercontent.com/github-production-release-asset/abc/x.exe", true)]
    [InlineData("https://github.com:443/warezfr/selecto-ai/releases/download/v4.0.0/a.exe", true)]
    [InlineData("http://github.com/warezfr/selecto-ai/releases/download/v4.0.0/a.exe", false)]
    [InlineData("https://github.com:8443/a.exe", false)]
    [InlineData("https://user@github.com/a.exe", false)]
    [InlineData("https://github.com.evil.com/a.exe", false)]
    [InlineData("https://raw.githubusercontent.com/warezfr/selecto-ai/main/a.exe", false)]
    [InlineData("https://gist.github.com/a.exe", false)]
    [InlineData("https://evil.selecto.span.observer/a.exe", false)]
    [InlineData("https://selecto.span.observer.evil.com/a.exe", false)]
    [InlineData("https://selecto.span.observer:8443/a.exe", false)]
    [InlineData("https://user@selecto.span.observer/a.exe", false)]
    [InlineData("/downloads/a.exe", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAllowedUrl_accepts_only_https_on_the_allowed_hosts(string? url, bool expected)
    {
        Assert.Equal(expected, UpdateLogic.IsAllowedUrl(url));
    }

    [Fact]
    public void FileNameFor_uses_version_and_hash_prefix_only()
    {
        var info = new UpdateInfo("3.1.0", "https://selecto.span.observer/downloads/x.exe", Sha, 1, null);
        Assert.Equal("SelectoAI-Setup-3.1.0-0123456789ab.exe", UpdateLogic.FileNameFor(info));
    }

    // ---------------------------------------------------------------- planification

    [Fact]
    public void ShouldCheck_is_true_when_never_checked()
    {
        Assert.True(UpdateLogic.ShouldCheck(null, new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void ShouldCheck_waits_for_24_hours()
    {
        var now = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(UpdateLogic.ShouldCheck(now.AddHours(-23), now));
        Assert.False(UpdateLogic.ShouldCheck(now.AddHours(-23).AddMinutes(-59), now));
        Assert.True(UpdateLogic.ShouldCheck(now.AddHours(-24), now));
        Assert.True(UpdateLogic.ShouldCheck(now.AddDays(-3), now));
    }

    [Fact]
    public void ShouldCheck_is_true_when_clock_went_backwards()
    {
        var now = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(UpdateLogic.ShouldCheck(now.AddHours(2), now));
    }

    [Fact]
    public void ShouldNotify_is_true_for_a_version_never_dismissed()
    {
        var now = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(UpdateLogic.ShouldNotify("3.1.0", null, null, now));
        Assert.True(UpdateLogic.ShouldNotify("3.1.0", "3.0.5", now.AddHours(-1), now));
    }

    [Fact]
    public void ShouldNotify_stays_quiet_for_24_hours_after_later_then_returns()
    {
        var now = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(UpdateLogic.ShouldNotify("3.1.0", "3.1.0", now.AddHours(-1), now));
        Assert.False(UpdateLogic.ShouldNotify("3.1.0", "3.1.0", now.AddHours(-23), now));
        Assert.True(UpdateLogic.ShouldNotify("3.1.0", "3.1.0", now.AddHours(-24), now));
    }

    [Fact]
    public void ShouldNotify_is_true_when_dismissal_date_is_missing_or_in_the_future()
    {
        var now = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(UpdateLogic.ShouldNotify("3.1.0", "3.1.0", null, now));
        Assert.True(UpdateLogic.ShouldNotify("3.1.0", "3.1.0", now.AddHours(2), now));
    }

    [Fact]
    public void Capture_origin_allows_only_that_origin()
    {
        try
        {
            UpdateLogic.CaptureOrigin = new Uri("http://127.0.0.1:8765/latest.json");
            Assert.True(UpdateLogic.IsAllowedUrl("http://127.0.0.1:8765/SelectoAI-Setup-arm64-4.0.0.exe"));
            Assert.False(UpdateLogic.IsAllowedUrl("http://127.0.0.1:9999/x.exe"));
            Assert.False(UpdateLogic.IsAllowedUrl("http://evil.example/x.exe"));
            Assert.True(UpdateLogic.IsAllowedUrl("https://selecto.span.observer/downloads/x.exe"));
        }
        finally { UpdateLogic.CaptureOrigin = null; }
        Assert.False(UpdateLogic.IsAllowedUrl("http://127.0.0.1:8765/x.exe"));
    }
}
