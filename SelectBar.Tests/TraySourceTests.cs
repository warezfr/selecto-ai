using System;
using System.IO;
using Xunit;

namespace SelectBar.Tests;

/// <summary>RC-007 : garde-fous de source pour la zone de notification (classe WinForms non compilée dans ce projet).</summary>
public class TraySourceTests
{
    private static string Read(params string[] relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SelectBar", "SelectBar.csproj"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, Path.Combine(relative)));
    }

    // RC-007 : le menu de la zone de notification (ContextMenuStrip, construit une fois) suit le changement de langue.
    [Fact]
    public void Tray_menu_listens_to_culture_changes_and_unsubscribes()
    {
        var src = Read("SelectBar", "UI", "TrayIcon.cs");
        Assert.Contains("L.CultureChanged +=", src);
        Assert.Contains("L.CultureChanged -=", src);
        Assert.Contains("ApplyLabels", src);
    }

    // RC-007 : le flyout est recréé à chaque ouverture, donc déjà dans la langue courante ; on le garde ainsi.
    [Fact]
    public void Tray_flyout_is_built_at_each_opening()
    {
        var src = Read("SelectBar", "UI", "AppController.cs");
        Assert.Contains("new TrayFlyout(", src);
    }
}
