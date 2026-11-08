using SelectBar.Localization;
using SelectBar.UI;
using Xunit;

namespace SelectBar.Tests;

public class RtlLayoutTests
{
    [Fact]
    public void MirrorX_inverse_en_droite_a_gauche()
    {
        Assert.Equal(260, RtlLayout.MirrorX(10, 50, 320, true));
        Assert.Equal(10, RtlLayout.MirrorX(10, 50, 320, false));
    }

    [Fact]
    public void Glyph_inverse_les_fleches_directionnelles_en_rtl()
    {
        Assert.Equal("", RtlLayout.Glyph("", true));
        Assert.Equal("", RtlLayout.Glyph("", true));
        Assert.Equal("", RtlLayout.Glyph("", true));
        Assert.Equal("", RtlLayout.Glyph("", true));
        Assert.Equal("", RtlLayout.Glyph("", true));
        Assert.Equal("", RtlLayout.Glyph("", true));
    }

    [Fact]
    public void Glyph_inchange_en_ltr_et_pour_les_autres_glyphes()
    {
        Assert.Equal("", RtlLayout.Glyph("", false));
        Assert.Equal("", RtlLayout.Glyph("", true));   // chevron bas : non directionnel
        Assert.Equal("", RtlLayout.Glyph("", true));   // chevron haut : non directionnel
        Assert.Equal("", RtlLayout.Glyph("", true));
        Assert.Equal("", RtlLayout.Glyph("", true));
    }

    [Fact]
    public void IsRtl_arabe_vrai_francais_faux()
    {
        Assert.True(RtlLayout.IsRtl(UiCulture.Make("ar")));
        Assert.False(RtlLayout.IsRtl(UiCulture.Make("fr")));
    }
}
