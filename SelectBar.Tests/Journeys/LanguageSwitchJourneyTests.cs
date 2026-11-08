using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SelectBar.Localization;
using SelectBar.UI;
using Xunit;

namespace SelectBar.Tests.Journeys;

/// <summary>Parcours « je passe l'interface en arabe, puis je reviens en français sans redémarrer » : plus aucun sens RTL résiduel.</summary>
[Collection("Localization")]
public class LanguageSwitchJourneyTests
{
    [Fact]
    public void Switch_ar_then_fr_resets_flow_direction()
    {
        var seen = new List<bool>();
        void OnChanged() => seen.Add(RtlLayout.IsRtl(L.Culture));
        L.CultureChanged += OnChanged;
        try
        {
            L.Culture = UiCulture.Make("ar");
            Assert.True(RtlLayout.IsRtl(L.Culture));
            Assert.Equal(L.Rlm + "x" + L.Rlm, L.Rtl("x"));

            L.Culture = UiCulture.Make("fr");
            Assert.False(RtlLayout.IsRtl(L.Culture));
            Assert.Equal("x", L.Rtl("x"));                        // plus de marque de droite-à-gauche
            Assert.Equal("x", L.Ltr("x"));
            Assert.Equal("fr", L.Culture.TwoLetterISOLanguageName);
            Assert.Equal(new[] { true, false }, seen);            // chaque fenêtre ouverte est prévenue, dans l'ordre
        }
        finally
        {
            L.CultureChanged -= OnChanged;
            L.Culture = UiCulture.Make("fr");
        }
    }

    [Fact]
    public void Back_to_french_gives_french_texts_and_unmirrored_glyphs()
    {
        try
        {
            L.Culture = UiCulture.Make("ar");
            var arabic = L.Get("Welcome_FreeTitle");
            L.Culture = UiCulture.Make("fr");

            Assert.NotEqual(arabic, L.Get("Welcome_FreeTitle"));
            Assert.Equal(RtlLayout.Back, RtlLayout.Glyph(RtlLayout.Back, RtlLayout.IsRtl(L.Culture)));
        }
        finally { L.Culture = UiCulture.Make("fr"); }
    }

    // WindowFactory (WPF) n'est pas compilable dans ce projet : on vérifie son contrat dans le source.
    [Fact]
    public void Window_factory_sets_both_directions_and_follows_culture_changes()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "SelectBar", "UI", "WindowFactory.cs"));

        Assert.Contains("FlowDirection.LeftToRight", text);
        Assert.Contains("FlowDirection.RightToLeft", text);
        Assert.Contains("L.CultureChanged +=", text);
        Assert.Contains("L.CultureChanged -=", text);
    }

    [Fact]
    public void Every_window_class_goes_through_the_window_factory()
    {
        var ui = Path.Combine(RepositoryRoot(), "SelectBar", "UI");
        var missing = new List<string>();
        foreach (var file in Directory.EnumerateFiles(ui, "*.cs"))
        {
            var text = File.ReadAllText(file);
            var isWindow = Regex.IsMatch(text, @"class\s+\w+[^{;]*:\s*(WpfUi\.FluentWindow|Window)\b");
            if (isWindow && !text.Contains("WindowFactory.Apply(this)")) missing.Add(Path.GetFileName(file));
        }
        Assert.True(missing.Count == 0, "Fenêtres sans WindowFactory.Apply : " + string.Join(", ", missing));
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SelectBar.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
