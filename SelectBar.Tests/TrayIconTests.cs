using System;
using System.IO;
using System.Linq;
using SelectBar.UI;
using Xunit;

namespace SelectBar.Tests;

/// <summary>Choix de l'icône de la zone de notification et présence des ressources déclarées dans le csproj.</summary>
public class TrayIconTests
{
    [Theory]
    [InlineData(1, false, "tray-light.ico")]
    [InlineData(1, true, "tray-light-paused.ico")]
    [InlineData(0, false, "tray-dark.ico")]
    [InlineData(0, true, "tray-dark-paused.ico")]
    [InlineData(null, false, "tray-dark.ico")] // valeur absente : sombre
    [InlineData(null, true, "tray-dark-paused.ico")]
    [InlineData(2, false, "tray-dark.ico")]    // valeur inattendue : sombre
    [InlineData(-1, true, "tray-dark-paused.ico")]
    public void Chooses_icon_from_theme_and_state(int? lightTheme, bool paused, string expected)
        => Assert.Equal(expected, TrayIconChoice.FileName(lightTheme, paused));

    [Fact]
    public void Every_choice_is_a_known_resource()
    {
        foreach (int? v in new int?[] { null, 0, 1, 2 })
            foreach (var paused in new[] { false, true })
                Assert.Contains(TrayIconChoice.FileName(v, paused), TrayIconChoice.AllFiles);
        Assert.Equal(5, TrayIconChoice.AllFiles.Distinct().Count());
    }

    [Fact]
    public void Pack_uri_points_to_assets_folder()
        => Assert.Equal("pack://application:,,,/Assets/tray-dark.ico", TrayIconChoice.PackUri(TrayIconChoice.DarkFile));

    private static string ProjectDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SelectBar", "SelectBar.csproj"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "SelectBar");
    }

    [Fact]
    public void Csproj_declares_icons_as_resource_and_application_icon()
    {
        var csproj = File.ReadAllText(Path.Combine(ProjectDir(), "SelectBar.csproj"));
        Assert.Contains("<ApplicationIcon>Assets\\SelectoAI.ico</ApplicationIcon>", csproj);
        Assert.Contains("<Resource Include=\"Assets\\*.ico\"", csproj);
    }

    [Fact]
    public void Referenced_icon_files_exist_and_are_valid_ico()
    {
        foreach (var name in TrayIconChoice.AllFiles)
        {
            var path = Path.Combine(ProjectDir(), "Assets", name);
            Assert.True(File.Exists(path), "Fichier manquant : " + path);
            var b = File.ReadAllBytes(path);
            Assert.True(b.Length > 22);
            Assert.Equal(0, BitConverter.ToUInt16(b, 0));
            Assert.Equal(1, BitConverter.ToUInt16(b, 2)); // type ICO
            var count = BitConverter.ToUInt16(b, 4);
            Assert.True(count >= 4);
            for (var i = 0; i < count; i++)
            {
                var offset = BitConverter.ToInt32(b, 6 + 16 * i + 12);
                var size = BitConverter.ToInt32(b, 6 + 16 * i + 8);
                Assert.True(offset + size <= b.Length);
                Assert.Equal(0x89, b[offset]); // image PNG intégrée
            }
        }
    }

    [Fact]
    public void Tray_ico_files_have_notification_sizes()
    {
        foreach (var name in TrayIconChoice.AllFiles.Where(n => n.StartsWith("tray-")))
        {
            var b = File.ReadAllBytes(Path.Combine(ProjectDir(), "Assets", name));
            var sizes = Enumerable.Range(0, BitConverter.ToUInt16(b, 4)).Select(i => (int)b[6 + 16 * i]).ToArray();
            Assert.Equal(new[] { 16, 20, 24, 32 }, sizes);
        }
    }
}
