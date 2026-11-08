using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using SelectBar.Settings;
using Wpf.Ui.Appearance;
using Wpf.Ui.Markup;
using SelectBar.Setup.Install;

namespace SelectBar.Setup.UI;

/// <summary>Charge WPF UI (Fluent, Mica) et expose les pinceaux de la palette de l'application (ThemePalette), clairs ou sombres.</summary>
internal static class SetupTheme
{
    public static bool IsDark { get; private set; }

    public static readonly FontFamily Body = new("Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily Display = new("Segoe UI Variable Display, Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static Brush Text = Brushes.Black;
    public static Brush SubText = Brushes.Gray;
    public static Brush Accent = Brushes.Blue;
    public static Brush OnAccent = Brushes.White;
    public static Brush Card = Brushes.White;
    public static Brush Separator = Brushes.LightGray;
    public static Brush Track = Brushes.LightGray;
    public static Brush CardBorder = Brushes.LightGray;
    public static Brush SelectionFill = Brushes.LightBlue;
    public static Brush BarBack = Brushes.Black;
    public static Brush BarText = Brushes.White;
    public static Brush Danger = Brushes.Red;
    public static Brush Success = Brushes.Green;

    public static bool AppsUseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not int v || v != 0;
        }
        catch { return true; }
    }

    public static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static Brush B(string hex, double alpha = 1)
    {
        var c = Hex(hex);
        if (alpha < 1) c = Color.FromArgb((byte)Math.Round(255 * alpha), c.R, c.G, c.B);
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    public static void Apply(Application app, string? forced)
    {
        IsDark = forced == "dark" || (forced == null && !AppsUseLightTheme());
        try
        {
            app.Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = IsDark ? ApplicationTheme.Dark : ApplicationTheme.Light });
            app.Resources.MergedDictionaries.Add(new ControlsDictionary());
            if (forced != null) ApplicationThemeManager.Apply(IsDark ? ApplicationTheme.Dark : ApplicationTheme.Light);
            else ApplicationThemeManager.ApplySystemTheme();
        }
        catch (Exception ex) { SetupLog.Write("WPF UI : thème non appliqué : " + ex.Message); }

        if (IsDark)
        {
            Text = B(ThemePalette.TextDark); SubText = B(ThemePalette.SubTextDark); Accent = B(ThemePalette.AccentDark);
            OnAccent = B("#000000"); Card = B(ThemePalette.CardDark, 0.85); Separator = B(ThemePalette.SeparatorDark);
            Track = B(ThemePalette.SeparatorDark); CardBorder = B(ThemePalette.ButtonBorderDark, 0.6);
            SelectionFill = B(ThemePalette.AccentDark, 0.35); BarBack = B(ThemePalette.BarBack); BarText = B(ThemePalette.BarTextDark);
            Danger = B("#FF99A4"); Success = B(ThemePalette.StatusOnHexDark);
        }
        else
        {
            Text = B(ThemePalette.Text); SubText = B(ThemePalette.SubText); Accent = B(ThemePalette.Accent);
            OnAccent = B("#FFFFFF"); Card = B(ThemePalette.Card, 0.75); Separator = B(ThemePalette.Separator);
            Track = B(ThemePalette.Separator); CardBorder = B(ThemePalette.ButtonBorder, 0.5);
            SelectionFill = B(ThemePalette.Accent, 0.28); BarBack = B(ThemePalette.BarBack); BarText = B(ThemePalette.BarTextDark);
            Danger = B("#C42B1C"); Success = B(ThemePalette.StatusOnHex);
        }
    }
}
