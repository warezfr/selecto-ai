using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using SelectBar.Extensions;
using Xunit;

namespace SelectBar.Tests;

public class ExtensionImporterTests
{
    private static string Header(string id) =>
        "// ==SelectoExtension==\n// @name Démo " + id + "\n// @identifier com.example." + id + "\n// ==/SelectoExtension==\nselecto.result('ok');\n";

    private static byte[] Js(string id) => Encoding.UTF8.GetBytes(Header(id));

    private static MemoryStream Zip(params (string Name, byte[] Content)[] entries)
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var entry = archive.CreateEntry(name).Open();
                entry.Write(content, 0, content.Length);
            }
        }
        ms.Position = 0;
        return ms;
    }

    private static ImportResult FromZip(params (string Name, byte[] Content)[] entries)
    {
        using var zip = Zip(entries);
        return ExtensionImporter.ImportZip("pack.zip", zip);
    }

    [Fact]
    public void Valid_zip_yields_each_root_js_file()
    {
        var result = FromZip(("alpha.js", Js("alpha")), ("beta.js", Js("beta")), ("readme.txt", Encoding.UTF8.GetBytes("hi")));

        Assert.Empty(result.Skipped);
        Assert.Equal(new[] { "alpha.js", "beta.js" }, result.Scripts.Select(s => s.FileName));
        Assert.Equal(Js("alpha"), result.Scripts[0].Content);
    }

    [Fact]
    public void Js_extension_is_case_insensitive_in_zip()
    {
        var result = FromZip(("Gamma.JS", Js("gamma")));

        Assert.Single(result.Scripts);
        Assert.Empty(result.Skipped);
    }

    [Theory]
    [InlineData("sub/inner.js")]
    [InlineData("sub\\inner.js")]
    [InlineData("../escape.js")]
    [InlineData("a..b.js")]
    public void Entry_outside_root_is_refused(string name)
    {
        var result = FromZip(("ok.js", Js("ok")), (name, Js("bad")));

        Assert.Equal(new[] { "ok.js" }, result.Scripts.Select(s => s.FileName));
        var skip = Assert.Single(result.Skipped);
        Assert.Equal(name, skip.Name);
        Assert.Equal(ImportIssue.OutsideRoot, skip.Issue);
    }

    [Theory]
    [InlineData("bad:stream.js")]
    [InlineData("bad*.js")]
    [InlineData(".js")]
    public void Unsafe_file_name_is_refused(string name)
    {
        var result = FromZip((name, Js("x")));

        Assert.Empty(result.Scripts);
        Assert.Equal(ImportIssue.BadName, Assert.Single(result.Skipped).Issue);
    }

    [Fact]
    public void Entry_over_256_kilobytes_is_refused()
    {
        var big = new byte[ExtensionImporter.MaxScriptBytes + 1];
        Js("big").CopyTo(big, 0);
        for (var i = 200; i < big.Length; i++) big[i] = (byte)'x';

        var result = FromZip(("big.js", big), ("small.js", Js("small")));

        Assert.Equal(new[] { "small.js" }, result.Scripts.Select(s => s.FileName));
        var skip = Assert.Single(result.Skipped);
        Assert.Equal(ImportIssue.TooLarge, skip.Issue);
        Assert.Equal("big.js", skip.Name);
    }

    [Fact]
    public void Entry_of_exactly_256_kilobytes_is_accepted()
    {
        var exact = new byte[ExtensionImporter.MaxScriptBytes];
        Js("exact").CopyTo(exact, 0);
        for (var i = Js("exact").Length; i < exact.Length; i++) exact[i] = (byte)' ';

        var result = FromZip(("exact.js", exact));

        Assert.Single(result.Scripts);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void Invalid_header_is_refused_with_detail()
    {
        var result = FromZip(("nohead.js", Encoding.UTF8.GetBytes("selecto.result('x');")), ("ok.js", Js("ok")));

        Assert.Equal(new[] { "ok.js" }, result.Scripts.Select(s => s.FileName));
        var skip = Assert.Single(result.Skipped);
        Assert.Equal(ImportIssue.InvalidHeader, skip.Issue);
        Assert.Equal("nohead.js", skip.Name);
        Assert.False(string.IsNullOrEmpty(skip.Detail));
    }

    [Fact]
    public void More_than_fifty_js_files_refuses_the_whole_archive()
    {
        var entries = Enumerable.Range(0, ExtensionImporter.MaxZipScripts + 1)
            .Select(i => ($"f{i}.js", Js("f" + i))).ToArray();

        var result = FromZip(entries);

        Assert.Empty(result.Scripts);
        var skip = Assert.Single(result.Skipped);
        Assert.Equal(ImportIssue.TooManyEntries, skip.Issue);
        Assert.Equal("pack.zip", skip.Name);
    }

    [Fact]
    public void Exactly_fifty_js_files_are_accepted()
    {
        var entries = Enumerable.Range(0, ExtensionImporter.MaxZipScripts)
            .Select(i => ($"f{i}.js", Js("f" + i))).ToArray();

        var result = FromZip(entries);

        Assert.Equal(ExtensionImporter.MaxZipScripts, result.Scripts.Count);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void Zip_without_root_js_is_refused()
    {
        var result = FromZip(("docs/readme.txt", Encoding.UTF8.GetBytes("x")), ("sub/a.js", Js("a")));

        Assert.Empty(result.Scripts);
        Assert.Equal(ImportIssue.NoScript, Assert.Single(result.Skipped).Issue);
    }

    [Fact]
    public void Corrupt_archive_is_refused()
    {
        using var broken = new MemoryStream(Encoding.ASCII.GetBytes("ceci n'est pas un zip du tout, vraiment pas"));

        var result = ExtensionImporter.ImportZip("broken.zip", broken);

        Assert.Empty(result.Scripts);
        Assert.Equal(ImportIssue.NotArchive, Assert.Single(result.Skipped).Issue);
    }

    [Fact]
    public void Several_js_files_are_each_checked_on_their_own()
    {
        var one = ExtensionImporter.ImportScript("one.js", Js("one"));
        var two = ExtensionImporter.ImportScript("two.js", Encoding.UTF8.GetBytes("no header here"));
        var three = ExtensionImporter.ImportScript("three.js", Js("three"));

        Assert.Equal("one.js", one.Scripts.Single().FileName);
        Assert.Equal(ImportIssue.InvalidHeader, two.Skipped.Single().Issue);
        Assert.Equal("three.js", three.Scripts.Single().FileName);
    }

    [Fact]
    public void Utf8_byte_order_mark_is_accepted()
    {
        var withBom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Js("bom")).ToArray();

        var result = ExtensionImporter.ImportScript("bom.js", withBom);

        Assert.Single(result.Scripts);
    }

    [Fact]
    public void Import_file_refuses_other_extensions()
    {
        var path = Path.Combine(Path.GetTempPath(), "selecto-import-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllBytes(path, Js("txt"));
        try
        {
            var result = ExtensionImporter.ImportFile(path);
            Assert.Empty(result.Scripts);
            Assert.Equal(ImportIssue.NotScript, Assert.Single(result.Skipped).Issue);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Import_file_reads_a_zip_from_disk()
    {
        var path = Path.Combine(Path.GetTempPath(), "selecto-import-" + Guid.NewGuid().ToString("N") + ".zip");
        using (var fs = File.Create(path)) Zip(("disk.js", Js("disk"))).CopyTo(fs);
        try
        {
            var result = ExtensionImporter.ImportFile(path);
            Assert.Equal("disk.js", Assert.Single(result.Scripts).FileName);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Unique_file_name_adds_a_counter_when_taken()
    {
        var taken = new System.Collections.Generic.HashSet<string> { "pack.js", "pack-2.js" };

        Assert.Equal("pack-3.js", ExtensionImporter.UniqueFileName("pack", taken.Contains));
        Assert.Equal("fresh.js", ExtensionImporter.UniqueFileName("fresh", taken.Contains));
    }

    [Theory]
    [InlineData("ok", true)]
    [InlineData("ok name", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("a:b", false)]
    [InlineData("a\\b", false)]
    [InlineData("a\u0001b", false)]
    public void Safe_file_name_rules(string stem, bool expected)
    {
        Assert.Equal(expected, ExtensionImporter.IsSafeFileName(stem));
    }
}
