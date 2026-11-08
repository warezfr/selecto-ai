using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using SelectBar.Extensions;
using Xunit;

namespace SelectBar.Tests.Journeys;

/// <summary>Parcours « j'importe codec.zip depuis le site » : seul le script est pris, le reste est ignoré sans bruit.</summary>
public class ExtensionImportJourneyTests
{
    private static readonly byte[] Script = Encoding.UTF8.GetBytes(
        "// ==SelectoExtension==\n// @name Codec\n// @identifier com.example.codec\n// ==/SelectoExtension==\nselecto.result('ok');\n");

    private static readonly byte[] FakeExe = { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };   // « MZ » : en-tête d'un exécutable Windows

    private static MemoryStream Zip(params (string Name, byte[] Content)[] entries)
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, content) in entries)
            {
                using var s = archive.CreateEntry(name).Open();
                s.Write(content, 0, content.Length);
            }
        ms.Position = 0;
        return ms;
    }

    // Choix documenté d'après ExtensionImporter : ImportZip ne retient que les entrées « .js » ; un .exe n'est même pas candidat,
    // il n'apparaît donc ni dans Scripts ni dans Skipped (aucun message pour le client).
    [Fact]
    public void Zip_with_js_and_exe_imports_only_js()
    {
        using var zip = Zip(("codec.js", Script), ("setup.exe", FakeExe));

        var result = ExtensionImporter.ImportZip("codec.zip", zip);

        var imported = Assert.Single(result.Scripts);
        Assert.Equal("codec.js", imported.FileName);
        Assert.Equal(Script, imported.Content);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void Zip_with_only_an_exe_is_refused_as_having_no_script()
    {
        using var zip = Zip(("setup.exe", FakeExe));

        var result = ExtensionImporter.ImportZip("codec.zip", zip);

        Assert.Empty(result.Scripts);
        Assert.Equal(ImportIssue.NoScript, Assert.Single(result.Skipped).Issue);
    }

    [Fact]
    public void A_lone_exe_chosen_in_the_file_dialog_is_refused_as_not_a_script()
    {
        var path = Path.Combine(Path.GetTempPath(), "selecto-journey-" + System.Guid.NewGuid().ToString("N") + ".exe");
        File.WriteAllBytes(path, FakeExe);
        try
        {
            var result = ExtensionImporter.ImportFile(path);
            Assert.Empty(result.Scripts);
            Assert.Equal(ImportIssue.NotScript, Assert.Single(result.Skipped).Issue);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Exe_never_reaches_the_extension_folder_whatever_the_zip_contains()
    {
        using var zip = Zip(("a.js", Script), ("b.exe", FakeExe), ("c.dll", FakeExe), ("d.js.exe", FakeExe));

        var result = ExtensionImporter.ImportZip("pack.zip", zip);

        Assert.All(result.Scripts, s => Assert.EndsWith(".js", s.FileName));
        Assert.Single(result.Scripts);
    }
}
