using SelectBar.Settings;
using Xunit;

namespace SelectBar.Tests;

public class ColorContrastTests
{
    [Theory]
    [InlineData(0.82, 255, 255, 255)] // pire cas : fond blanc
    [InlineData(0.82, 0, 0, 0)]
    [InlineData(0.85, 255, 255, 255)]
    public void Texte_blanc_sur_indigo_translucide_depasse_7_pour_1(double alpha, double br, double bg, double bb)
    {
        var (r, g, b) = ColorContrast.Over(0x1E, 0x14, 0x50, alpha, br, bg, bb);
        var ratio = ColorContrast.Ratio(1.0, ColorContrast.Luminance(r, g, b));
        Assert.True(ratio > 7, $"contraste {ratio:F2}");
    }

    [Fact]
    public void Blanc_sur_blanc_vaut_1() => Assert.Equal(1.0, ColorContrast.Ratio(1.0, 1.0), 3);
}
