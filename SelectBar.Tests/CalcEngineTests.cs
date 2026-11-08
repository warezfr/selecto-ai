using SelectBar.Extensions;
using SelectBar.Localization;
using Xunit;

namespace SelectBar.Tests;

[Collection("Localization")]
public class CalcEngineTests
{
    private static readonly System.Globalization.CultureInfo Fr = UiCulture.Make("fr");
    private static readonly System.Globalization.CultureInfo En = UiCulture.Make("en");

    [Fact]
    public void Decimal_comma_and_grouped_result_in_french()
    {
        Assert.Equal("1 500", CalcEngine.Evaluate("1250*1,2", Fr));
    }

    [Fact]
    public void Percent_of_a_number()
    {
        Assert.Equal("16", CalcEngine.Evaluate("20 % de 80", Fr));
    }

    [Fact]
    public void Power_is_formatted_with_english_separators()
    {
        Assert.Equal("1,024", CalcEngine.Evaluate("2^10", En));
    }

    [Fact]
    public void Times_as_x_and_as_sign()
    {
        Assert.Equal("6", CalcEngine.Evaluate("2 x 3", En));
        Assert.Equal("6", CalcEngine.Evaluate("2 × 3", En));
    }

    [Fact]
    public void Precedence_parentheses_and_unary_minus()
    {
        Assert.Equal("12", CalcEngine.Evaluate("2 + 3 * (5 - 1) - 2", En));
        Assert.Equal("20", CalcEngine.Evaluate("(2+3)*4", En));
        Assert.Equal("-4", CalcEngine.Evaluate("-2^2", En));
    }

    [Fact]
    public void Thousands_are_read_when_grouped_by_spaces()
    {
        Assert.Equal("1 001", CalcEngine.Evaluate("1 000 + 1", Fr));
    }

    [Fact]
    public void Dates_are_not_calculated_but_subtractions_with_decimals_are()
    {
        Assert.Null(CalcEngine.Evaluate("10/24/2024", En));
        Assert.Equal("7.25", CalcEngine.Evaluate("10.5-3.25", En));
    }

    [Theory]
    [InlineData("2024-10-24")]
    [InlineData("06 12 34 56 78")]
    [InlineData("1/2 tasse")]
    [InlineData("1 500 €")]
    [InlineData("hello world")]
    [InlineData("42")]
    [InlineData("-5")]
    [InlineData("5/0")]
    [InlineData("9999999999999999 * 9999999999999999")]
    public void Null_when_the_selection_is_not_a_calculable_expression(string selection)
    {
        Assert.Null(CalcEngine.Evaluate(selection, Fr));
    }

    [Fact]
    public void Format_number_uses_culture_separators()
    {
        Assert.Equal("1,234.50", CalcEngine.FormatNumber(1234.5m, 2, En));
        Assert.Equal("1 234,50", CalcEngine.FormatNumber(1234.5m, 2, Fr));
    }
}
