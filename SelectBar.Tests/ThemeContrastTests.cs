using SelectBar.Settings;
using Xunit;

namespace SelectBar.Tests;

public class ThemeContrastTests
{
    private static (double R, double G, double B) Hex(string h) =>
        (System.Convert.ToInt32(h.Substring(1, 2), 16), System.Convert.ToInt32(h.Substring(3, 2), 16), System.Convert.ToInt32(h.Substring(5, 2), 16));

    private static double Lum((double R, double G, double B) c) => ColorContrast.Luminance(c.R, c.G, c.B);

    private static double Ratio(string fg, string bg) => ColorContrast.Ratio(Lum(Hex(fg)), Lum(Hex(bg)));

    [Fact]
    public void Chaque_couple_texte_fond_clair_atteint_4_5_pour_1()
    {
        var pairs = new (string Name, string Fg, string Bg)[]
        {
            ("libellé / fenêtre", ThemePalette.Text, ThemePalette.Window),
            ("libellé / carte", ThemePalette.Text, ThemePalette.Card),
            ("navigation inactive", ThemePalette.PivotOff, ThemePalette.Window),
            ("sous-texte / fenêtre", ThemePalette.SubText, ThemePalette.Window),
            ("sous-texte / carte", ThemePalette.SubText, ThemePalette.Card),
            ("accent / fenêtre", ThemePalette.Accent, ThemePalette.Window),
            ("accent / carte", ThemePalette.Accent, ThemePalette.Card),
            ("état actif", ThemePalette.StatusOnHex, ThemePalette.Window),
            ("état pause", ThemePalette.WarnHex, ThemePalette.Window),
            ("erreur / fenêtre", ThemePalette.ErrorHex, ThemePalette.Window),
            ("erreur / carte", ThemePalette.ErrorHex, ThemePalette.Card),
            ("bouton principal", ThemePalette.Card, ThemePalette.Accent),
            ("bouton principal survolé", ThemePalette.Card, ThemePalette.AccentHover),
        };
        foreach (var (name, fg, bg) in pairs)
            Assert.True(Ratio(fg, bg) >= 4.5, $"{name} : {Ratio(fg, bg):F2}:1");
    }

    [Fact]
    public void Chaque_couple_texte_fond_sombre_atteint_4_5_pour_1()
    {
        var pairs = new (string Name, string Fg, string Bg)[]
        {
            ("libellé / fenêtre", ThemePalette.TextDark, ThemePalette.WindowDark),
            ("libellé / carte", ThemePalette.TextDark, ThemePalette.CardDark),
            ("navigation inactive", ThemePalette.PivotOffDark, ThemePalette.WindowDark),
            ("sous-texte / carte", ThemePalette.SubTextDark, ThemePalette.CardDark),
            ("accent / fenêtre", ThemePalette.AccentDark, ThemePalette.WindowDark),
            ("accent / carte", ThemePalette.AccentDark, ThemePalette.CardDark),
            ("état actif", ThemePalette.StatusOnHexDark, ThemePalette.CardDark),
            ("état pause", ThemePalette.WarnHexDark, ThemePalette.CardDark),
            ("erreur / carte", ThemePalette.ErrorHexDark, ThemePalette.CardDark),
            ("erreur / fenêtre", ThemePalette.ErrorHexDark, ThemePalette.WindowDark),
            ("bouton principal (texte noir)", "#000000", ThemePalette.AccentDark),
            ("bouton principal survolé", "#000000", ThemePalette.AccentHoverDark),
        };
        foreach (var (name, fg, bg) in pairs)
            Assert.True(Ratio(fg, bg) >= 4.5, $"{name} : {Ratio(fg, bg):F2}:1");
    }

    [Theory]
    [InlineData(255.0)] // pire cas pour la pilule sombre : bureau blanc derrière
    [InlineData(0.0)]
    [InlineData(128.0)]
    public void Pilule_sombre_lisible_sur_tout_fond(double desktop)
    {
        var back = Hex(ThemePalette.BarBack);
        var pill = ColorContrast.Over(back.R, back.G, back.B, ThemePalette.BarAlpha, desktop, desktop, desktop);
        Assert.True(ColorContrast.Ratio(Lum(Hex(ThemePalette.BarTextDark)), Lum(pill)) >= 4.5);
    }

    [Theory]
    [InlineData(255.0)]
    [InlineData(0.0)] // pire cas pour la pilule claire : bureau noir derrière
    [InlineData(128.0)]
    public void Pilule_claire_lisible_sur_tout_fond(double desktop)
    {
        var back = Hex(ThemePalette.BarBackLight);
        var pill = ColorContrast.Over(back.R, back.G, back.B, ThemePalette.BarAlpha, desktop, desktop, desktop);
        Assert.True(ColorContrast.Ratio(Lum(Hex(ThemePalette.BarTextLight)), Lum(pill)) >= 4.5);
    }

    [Fact]
    public void Accent_du_systeme_utilise_seulement_s_il_est_lisible()
    {
        Assert.Equal("#C42B1C", ColorContrast.PickAccent("#FFC42B1C", ThemePalette.Card, ThemePalette.Accent));   // rouge foncé : lisible sur blanc
        Assert.Equal(ThemePalette.Accent, ColorContrast.PickAccent("#FFFFEB3B", ThemePalette.Card, ThemePalette.Accent)); // jaune : illisible sur blanc
        Assert.Equal(ThemePalette.AccentDark, ColorContrast.PickAccent("#FF1A237E", ThemePalette.CardDark, ThemePalette.AccentDark)); // bleu nuit : illisible sur sombre
        Assert.Equal(ThemePalette.Accent, ColorContrast.PickAccent(null, ThemePalette.Card, ThemePalette.Accent));
        Assert.Equal(ThemePalette.Accent, ColorContrast.PickAccent("pas une couleur", ThemePalette.Card, ThemePalette.Accent));
    }
}
