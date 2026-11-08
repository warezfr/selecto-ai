using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SelectBar.Localization;
using Xunit;

namespace SelectBar.Tests;

// Les tests modifient l'état statique L.Culture : même collection (exécution séquentielle) et remise à fr dans Dispose.
[Collection("Localization")]
public class LocalizationTests : IDisposable
{
    public void Dispose() => L.Culture = UiCulture.Make("fr");

    [Fact]
    public void Get_returns_french_by_default()
    {
        L.Culture = UiCulture.Make("fr");
        Assert.Equal("Réglages", L.Get("Tray_Settings"));
    }

    [Fact]
    public void Get_falls_back_to_french_when_key_missing_in_en()
    {
        L.Culture = UiCulture.Make("en");
        Assert.Equal("Quit", L.Get("Tray_Quit"));
    }

    [Fact]
    public void Plural_picks_form()
    {
        L.Culture = UiCulture.Make("fr");
        Assert.Equal("1 action", L.Plural(1, "Tray_ActionsOne", "Tray_ActionsOther"));
        Assert.Equal("12 actions", L.Plural(12, "Tray_ActionsOne", "Tray_ActionsOther"));
    }

    [Fact]
    public void Arabic_culture_uses_gregorian_and_western_digits()
    {
        var c = UiCulture.Make("ar");
        Assert.IsType<GregorianCalendar>(c.DateTimeFormat.Calendar);
        Assert.Equal(DigitShapes.None, c.NumberFormat.DigitSubstitution);
        Assert.Equal("1500", 1500.ToString("0", c));
    }

    [Fact]
    public void Setting_culture_raises_event()
    {
        var n = 0;
        Action handler = () => n++;
        L.CultureChanged += handler;
        try
        {
            L.Culture = UiCulture.Make("en");
            Assert.Equal(1, n);
        }
        finally
        {
            L.CultureChanged -= handler;
        }
    }

    [Fact]
    public void Rtl_marks_only_in_right_to_left_text()
    {
        const char rlm = '\u200F';
        Assert.Equal("12 actions", L.Rtl("12 actions", false));
        Assert.Equal("", L.Rtl("", true));
        Assert.Equal(rlm + "12 إجراءات" + rlm, L.Rtl("12 إجراءات", true));
    }

    [Fact]
    public void Rtl_does_not_add_a_second_mark()
    {
        const char rlm = '\u200F';
        var once = L.Rtl("3 من 1", true);
        Assert.Equal(once, L.Rtl(once, true));
        Assert.Equal(rlm, once[0]);
        Assert.Equal(rlm, once[^1]);
    }

    [Fact]
    public void Ltr_isole_la_taille_seulement_en_droite_a_gauche()
    {
        Assert.Equal("≈ 1,0 Go", L.Ltr("≈ 1,0 Go", false));
        Assert.Equal("", L.Ltr("", true));
        Assert.Equal("\u2066≈ 1,0 Go\u2069", L.Ltr("≈ 1,0 Go", true));
        var once = L.Ltr("1,2 Go", true);
        Assert.Equal(once, L.Ltr(once, true));
    }

    [Fact]
    public void Ltr_suit_la_culture_et_protege_la_parenthese_arabe()
    {
        L.Culture = UiCulture.Make("ar");
        Assert.Contains("(\u2066≈ 1,0 Go\u2069)", "(" + L.Ltr("≈ 1,0 Go") + ")");
        Assert.Contains("\u2066≈ 1,0 Go\u2069", L.Format("Welcome_InstallLocal", L.Ltr("≈ 1,0 Go")));
        L.Culture = UiCulture.Make("fr");
        Assert.Equal("≈ 1,0 Go", L.Ltr("≈ 1,0 Go"));
    }

    [Fact]
    public void Rtl_follows_current_culture()
    {
        L.Culture = UiCulture.Make("ar");
        Assert.StartsWith("\u200F", L.Rtl("1 من 3"));
        L.Culture = UiCulture.Make("fr");
        Assert.Equal("1 sur 3", L.Rtl("1 sur 3"));
    }

    [Fact]
    public void Resx_files_have_no_duplicate_keys()
    {
        var dir = Path.Combine(RepoRoot(), "SelectBar", "Localization");
        foreach (var file in new[] { "Strings.resx", "Strings.en.resx", "Strings.es.resx", "Strings.ar.resx" })
        {
            var names = XDocument.Load(Path.Combine(dir, file)).Root!.Elements("data").Select(d => d.Attribute("name")!.Value).ToList();
            Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        }
    }

    // Résolution du dépôt : remonte depuis le dossier de sortie jusqu'à SelectBar/SelectBar.csproj.
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SelectBar", "SelectBar.csproj"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static Dictionary<string, string> ReadResx(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var data in XDocument.Load(path).Root!.Elements("data"))
            values[data.Attribute("name")!.Value] = data.Element("value")?.Value ?? "";
        return values;
    }

    // Libellés identiques au français autorisés : noms propres, sigles, chiffres, symboles, mots identiques en espagnol.
    private static readonly string[] UntranslatedAllowed = { "Pro", "OK", "IA", "Ollama", "Wi-Fi", "Selecto AI", "OpenAI", "Microsoft Store", "disponible" };

    private static bool IsExempt(string value)
    {
        if (value.Contains("://")) return true;                              // URL
        var rest = Regex.Replace(value, @"\{\d+\}", " ");                  // emplacements {0}, {1}…
        foreach (var token in UntranslatedAllowed)
            rest = Regex.Replace(rest, @"\b" + Regex.Escape(token) + @"\b", " ");
        return !rest.Any(char.IsLetter);                                    // plus aucune lettre à traduire
    }

    [Fact]
    public void All_keys_present_in_every_language()
    {
        var dir = Path.Combine(RepoRoot(), "SelectBar", "Localization");
        var reference = ReadResx(Path.Combine(dir, "Strings.resx")).Keys.ToHashSet();
        foreach (var file in new[] { "Strings.en.resx", "Strings.es.resx", "Strings.ar.resx" })
        {
            var keys = ReadResx(Path.Combine(dir, file)).Keys.ToHashSet();
            Assert.True(keys.SetEquals(reference),
                $"{file} : manquantes [{string.Join(", ", reference.Except(keys))}] ; en trop [{string.Join(", ", keys.Except(reference))}]");
        }
    }

    [Fact]
    public void Placeholders_match_in_every_language()
    {
        // Un {0} oublié dans une traduction fait disparaître la donnée (ex. la version dans « Mise à jour {0} disponible »).
        var dir = Path.Combine(RepoRoot(), "SelectBar", "Localization");
        var reference = ReadResx(Path.Combine(dir, "Strings.resx"));
        static HashSet<string> Holes(string v) => System.Text.RegularExpressions.Regex.Matches(v, @"\{\d+\}").Select(m => m.Value).ToHashSet();
        var errors = new List<string>();
        foreach (var file in new[] { "Strings.en.resx", "Strings.es.resx", "Strings.ar.resx" })
        {
            var values = ReadResx(Path.Combine(dir, file));
            foreach (var (key, fr) in reference)
                if (values.TryGetValue(key, out var v) && !Holes(fr).SetEquals(Holes(v))) errors.Add($"{file} : {key}");
        }
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Fact]
    public void No_untranslated_copy()
    {
        var dir = Path.Combine(RepoRoot(), "SelectBar", "Localization");
        var fr = ReadResx(Path.Combine(dir, "Strings.resx"));
        var copies = new List<string>();
        foreach (var file in new[] { "Strings.es.resx", "Strings.ar.resx" })
        {
            var lang = ReadResx(Path.Combine(dir, file));
            foreach (var (key, frValue) in fr)
            {
                if (lang.TryGetValue(key, out var value) && value == frValue && !IsExempt(value))
                    copies.Add($"{file} {key} = « {value} »");
            }
        }
        Assert.True(copies.Count == 0, "Valeurs restées en français :\n" + string.Join("\n", copies));
    }
}
