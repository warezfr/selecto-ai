using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SelectBar.Extensions;
using SelectBar.Localization;
using Xunit;

namespace SelectBar.Tests;

[Collection("Localization")]
public class UnitConverterTests
{
    private static readonly System.Globalization.CultureInfo Fr = UiCulture.Make("fr");
    private static readonly System.Globalization.CultureInfo En = UiCulture.Make("en");

    private static readonly Dictionary<string, decimal> Rates = new()
    {
        ["EUR"] = 1m,
        ["USD"] = 1.25m,
        ["GBP"] = 0.8m,
    };

    [Fact]
    public void Fahrenheit_to_celsius_in_french()
    {
        Assert.Equal("37,78 °C", UnitConverter.TryConvert("100 °F", Fr));
    }

    [Fact]
    public void Miles_to_kilometres_in_french()
    {
        Assert.Equal("48,28 km", UnitConverter.TryConvert("30 miles", Fr));
    }

    [Fact]
    public void Pounds_to_kilograms_in_french()
    {
        Assert.Equal("0,91 kg", UnitConverter.TryConvert("2 lb", Fr));
    }

    [Fact]
    public void Gibibytes_become_gigaoctets_in_french_and_gigabytes_in_english()
    {
        Assert.Equal("1,07 Go", UnitConverter.TryConvert("1 GiB", Fr));
        Assert.Equal("1.07 GB", UnitConverter.TryConvert("1 GiB", En));
    }

    [Fact]
    public void Celsius_to_fahrenheit_for_english_us()
    {
        Assert.Equal("212.00 °F", UnitConverter.TryConvert("100 °C", En));
    }

    [Fact]
    public void Units_already_in_the_language_system_give_null()
    {
        Assert.Null(UnitConverter.TryConvert("10 km", Fr));
        Assert.Null(UnitConverter.TryConvert("30 miles", En));
        Assert.Null(UnitConverter.TryConvert("100 °F", En));
    }

    [Fact]
    public void Currency_converts_to_the_culture_currency()
    {
        Assert.Equal("80,00 €", UnitConverter.TryConvert("100 USD", Fr, () => Rates));
    }

    [Fact]
    public void Currency_with_explicit_target_word()
    {
        Assert.Equal("62,50 $", UnitConverter.TryConvert("50 € en dollars", Fr, () => Rates));
    }

    [Fact]
    public void Pounds_sterling_to_euros()
    {
        Assert.Equal("37,50 €", UnitConverter.TryConvert("30 GBP", Fr, () => Rates));
    }

    [Fact]
    public void Currency_for_english_us_defaults_to_dollars()
    {
        Assert.Equal("125.00 $", UnitConverter.TryConvert("100 EUR", En, () => Rates));
    }

    [Fact]
    public void Currency_needs_rates()
    {
        Assert.Null(UnitConverter.TryConvert("100 USD", Fr));
        Assert.Null(UnitConverter.TryConvert("100 USD", Fr, () => null));
    }

    [Fact]
    public void Rates_are_not_requested_for_words_that_are_not_currencies()
    {
        var asked = false;
        Assert.Null(UnitConverter.TryConvert("12 mots", Fr, () => { asked = true; return Rates; }));
        Assert.False(asked);
    }

    [Fact]
    public void Parses_the_ecb_feed_with_eur_as_reference()
    {
        const string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<gesmes:Envelope xmlns:gesmes=""http://www.gesmes.org/xml/2002-08-01"" xmlns=""http://www.ecb.int/vocabulary/2002-08-01/eurofxref"">
<Cube><Cube time=""2026-10-23""><Cube currency=""USD"" rate=""1.25""/><Cube currency=""GBP"" rate=""0.8""/></Cube></Cube>
</gesmes:Envelope>";
        var rates = CurrencyRates.ParseEcb(xml);
        Assert.NotNull(rates);
        Assert.Equal(1.25m, rates!["USD"]);
        Assert.Equal(0.8m, rates["GBP"]);
        Assert.Equal(1m, rates["EUR"]);
        Assert.Null(CurrencyRates.ParseEcb("<pas-du-xml"));
    }

    [Fact]
    public void Refresh_writes_the_cache_and_online_rates_are_only_given_when_allowed()
    {
        var dir = Path.Combine(Path.GetTempPath(), "selecto-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            const string xml = @"<Envelope xmlns=""http://www.ecb.int/vocabulary/2002-08-01/eurofxref""><Cube><Cube><Cube currency=""USD"" rate=""1.25""/></Cube></Cube></Envelope>";
            var rates = new CurrencyRates(Path.Combine(dir, "rates.xml"), _ => Task.FromResult(xml));

            Assert.True(rates.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult());
            Assert.True(File.Exists(Path.Combine(dir, "rates.xml")));
            Assert.Null(rates.Current(onlineAllowed: false));
            Assert.Equal(1.25m, rates.Current(onlineAllowed: true)!["USD"]);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void A_failed_download_is_reported_without_throwing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "selecto-tests-" + Guid.NewGuid().ToString("N"));
        var rates = new CurrencyRates(Path.Combine(dir, "rates.xml"), _ => Task.FromException<string>(new InvalidOperationException("hors ligne")));
        Assert.False(rates.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult());
        Assert.False(Directory.Exists(dir));
    }
}
