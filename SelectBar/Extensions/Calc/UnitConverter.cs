using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace SelectBar.Extensions;

/// <summary>
/// Conversions d'une sélection : unités (« 30 miles », « 100 °F », « 1 GiB ») et devises (« 100 USD », « 50 € en dollars »).
/// Sens : impérial vers métrique (en-US : l'inverse), binaire (GiB) vers décimal (Go). Une unité déjà dans le système de la langue : rien.
/// Pur : les taux de change sont fournis par l'appelant (voir <see cref="CurrencyRates"/>).
/// </summary>
public static class UnitConverter
{
    private sealed record Unit(string Label, decimal ToBase, bool Imperial, string Pair, string Group);

    // Clés en minuscules. ToBase : mètre, gramme ou litre ; Pair : clé de l'unité du système opposé.
    private static readonly Dictionary<string, Unit> Units = new(StringComparer.Ordinal)
    {
        ["km"] = new("km", 1000m, false, "mi", "length"),
        ["m"] = new("m", 1m, false, "ft", "length"),
        ["cm"] = new("cm", 0.01m, false, "in", "length"),
        ["mm"] = new("mm", 0.001m, false, "in", "length"),
        ["mi"] = new("mi", 1609.344m, true, "km", "length"),
        ["miles"] = new("mi", 1609.344m, true, "km", "length"),
        ["ft"] = new("ft", 0.3048m, true, "m", "length"),
        ["in"] = new("in", 0.0254m, true, "cm", "length"),
        ["pouce"] = new("in", 0.0254m, true, "cm", "length"),
        ["pouces"] = new("in", 0.0254m, true, "cm", "length"),
        ["kg"] = new("kg", 1000m, false, "lb", "mass"),
        ["g"] = new("g", 1m, false, "oz", "mass"),
        ["lb"] = new("lb", 453.59237m, true, "kg", "mass"),
        ["lbs"] = new("lb", 453.59237m, true, "kg", "mass"),
        ["oz"] = new("oz", 28.349523125m, true, "g", "mass"),
        ["l"] = new("l", 1m, false, "gal", "volume"),
        ["ml"] = new("ml", 0.001m, false, "oz fl", "volume"),
        ["gal"] = new("gal", 3.785411784m, true, "l", "volume"),
        ["oz fl"] = new("fl oz", 0.0295735295625m, true, "ml", "volume"),
        ["°c"] = new("°C", 0m, false, "°f", "temp"),
        ["°f"] = new("°F", 0m, true, "°c", "temp"),
        ["k"] = new("K", 0m, false, "°c", "temp"),
    };

    private static readonly string[] BinaryUnits = { "kib", "mib", "gib", "tib" };
    private static readonly string[] Prefixes = { "K", "M", "G", "T" };

    private const string UnitChars = @"[^\d\s.,;:!?()\[\]]+";

    private static readonly Regex Pattern = new(
        @"^(?<n>[-−]?\d+(?:[   ]\d{3})*(?:[.,]\d+)?)\s*(?<u>" + UnitChars + @"(?:\s+fl)?)"
        + @"(?:\s+(?:en|to|in|->|→)\s+(?<t>" + UnitChars + @"))?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Conversion de la sélection, ou null si elle n'est pas une grandeur convertible.
    /// <paramref name="rates"/> n'est appelé que pour une devise (taux : 1 EUR = x CCY) ; il renvoie null si les taux sont indisponibles.
    /// </summary>
    public static string? TryConvert(string text, CultureInfo culture, Func<IReadOnlyDictionary<string, decimal>?>? rates = null)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 100) return null;
        var m = Pattern.Match(text.Trim());
        if (!m.Success || !TryAmount(m.Groups["n"].Value, out var amount)) return null;

        var unit = Normalize(m.Groups["u"].Value);
        var target = m.Groups["t"].Success ? m.Groups["t"].Value.Trim() : null;

        if (Units.TryGetValue(unit, out var known))
            return target == null ? ConvertUnit(amount, unit, known, culture) : null;

        var binary = Array.IndexOf(BinaryUnits, unit);
        if (binary >= 0)
            return target == null ? ConvertBinary(amount, binary, culture) : null;

        // Les taux ne sont demandés (et donc téléchargés au besoin) que pour un mot qui ressemble à une devise.
        var rawUnit = m.Groups["u"].Value.Trim();
        if (AliasCode(rawUnit) == null && !(rawUnit.Length == 3 && rawUnit.All(char.IsLetter))) return null;
        return ConvertCurrency(amount, rawUnit, target, culture, rates?.Invoke());
    }

    /// <summary>Système impérial : anglais américain (et « en » seul).</summary>
    private static bool IsImperial(CultureInfo culture) => culture.Name is "en-US" or "en";

    private static bool TryAmount(string raw, out decimal amount)
    {
        var clean = raw.Replace("−", "-").Replace(" ", "").Replace(" ", "").Replace(" ", "").Replace(',', '.');
        return decimal.TryParse(clean, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount);
    }

    private static string Normalize(string unit) => unit.Replace('º', '°').Trim().ToLowerInvariant();

    private static string? ConvertUnit(decimal amount, string key, Unit from, CultureInfo culture)
    {
        if (from.Group == "temp") return ConvertTemperature(amount, key, culture);
        if (from.Imperial == IsImperial(culture)) return null;   // déjà dans le système de la langue
        var to = Units[from.Pair];
        var value = amount * from.ToBase / to.ToBase;
        return $"{CalcEngine.FormatNumber(value, 2, culture)} {to.Label}";
    }

    private static string? ConvertTemperature(decimal amount, string key, CultureInfo culture)
    {
        var celsius = key == "°f" ? (amount - 32m) * 5m / 9m : key == "k" ? amount - 273.15m : amount;
        if (IsImperial(culture))
        {
            if (key == "°f") return null;
            return $"{CalcEngine.FormatNumber(celsius * 9m / 5m + 32m, 2, culture)} °F";
        }
        if (key == "°c") return null;
        return $"{CalcEngine.FormatNumber(celsius, 2, culture)} °C";
    }

    /// <summary>Préfixes binaires : 1 GiB = 1,07 Go (fr) ou 1.07 GB (autres langues).</summary>
    private static string ConvertBinary(decimal amount, int index, CultureInfo culture)
    {
        var bytes = amount;
        var decimalPerUnit = 1m;
        for (var i = 0; i <= index; i++)
        {
            bytes *= 1024m;
            decimalPerUnit *= 1000m;
        }
        var label = culture.TwoLetterISOLanguageName == "fr" ? Prefixes[index] + "o" : Prefixes[index] + "B";
        return $"{CalcEngine.FormatNumber(bytes / decimalPerUnit, 2, culture)} {label}";
    }

    private static string? ConvertCurrency(decimal amount, string rawFrom, string? rawTo, CultureInfo culture,
        IReadOnlyDictionary<string, decimal>? rates)
    {
        if (rates == null) return null;
        var from = CurrencyCode(rawFrom, rates);
        if (from == null) return null;
        var to = rawTo == null ? DefaultCurrency(culture) : CurrencyCode(rawTo, rates);
        if (to == null || to == from) return null;
        var fromRate = RateOf(from, rates);
        var toRate = RateOf(to, rates);
        if (fromRate == null || toRate == null) return null;
        var result = amount / fromRate.Value * toRate.Value;
        return $"{CalcEngine.FormatNumber(result, 2, culture)} {Symbol(to)}";
    }

    private static string? CurrencyCode(string raw, IReadOnlyDictionary<string, decimal> rates)
    {
        var alias = AliasCode(raw);
        if (alias != null) return alias;
        var upper = raw.Trim().ToUpperInvariant();
        return upper.Length == 3 && upper.All(char.IsLetter) && RateOf(upper, rates) != null ? upper : null;
    }

    /// <summary>Symboles et noms courants : « € », « dollars », « livres »… ; null si inconnu.</summary>
    private static string? AliasCode(string raw)
    {
        switch (raw.Trim().ToLowerInvariant())
        {
            case "€": case "eur": case "euro": case "euros": return "EUR";
            case "$": case "usd": case "dollar": case "dollars": return "USD";
            case "£": case "gbp": case "livre": case "livres": case "pound": case "pounds": return "GBP";
            case "¥": case "jpy": case "yen": return "JPY";
        }
        return null;
    }

    /// <summary>Taux : 1 EUR = x CCY (EUR = 1, même si absent du flux).</summary>
    private static decimal? RateOf(string code, IReadOnlyDictionary<string, decimal> rates)
    {
        if (code == "EUR") return 1m;
        if (rates.TryGetValue(code, out var rate) && rate > 0) return rate;
        return null;
    }

    private static string DefaultCurrency(CultureInfo culture) => culture.Name switch
    {
        "en-GB" => "GBP",
        "en-US" or "en" => "USD",
        _ => "EUR",
    };

    private static string Symbol(string code) => code switch
    {
        "EUR" => "€",
        "USD" => "$",
        "GBP" => "£",
        "JPY" => "¥",
        _ => code,
    };
}
