using System;
using System.IO;
using Xunit;

namespace SelectBar.Tests;

/// <summary>RC-009 : garde-fous de source pour les zones défilantes des Réglages (classes WPF non compilées dans ce projet).</summary>
public class ScrollSourceTests
{
    private static string Read(params string[] relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SelectBar", "SelectBar.csproj"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, Path.Combine(relative)));
    }

    // RC-009 : les pages Réglages gardent une barre de défilement (Fluent, visible au survol).
    [Fact]
    public void Settings_scroll_viewer_has_a_visible_scrollbar()
    {
        var theme = Read("SelectBar", "UI", "Theme.cs");
        var thin = theme.Substring(theme.IndexOf("ThinScroll(", StringComparison.Ordinal));
        thin = thin.Substring(0, Math.Min(thin.Length, 500));
        Assert.Contains("ScrollBarVisibility.Auto", thin);
        Assert.DoesNotContain("VerticalScrollBarVisibility = ScrollBarVisibility.Hidden", thin);

        var fluent = Read("SelectBar", "UI", "FluentTheme.cs");
        Assert.Contains("KeepScrollBar", fluent);   // la règle « aucun ascenseur » laisse cette zone tranquille
    }

    // RC-009 : la fenêtre Réglages ne s'ouvre jamais plus haute que l'écran de travail.
    [Fact]
    public void Settings_window_height_is_clamped_to_the_work_area()
    {
        var src = Read("SelectBar", "UI", "SettingsWindow.cs");
        Assert.Contains("SystemParameters.WorkArea.Height", src);
    }
}
