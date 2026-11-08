using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Markup;

namespace SelectBar.UI;

/// <summary>
/// Charge WPF UI (Windows 11 Fluent) dans l'Application créée en code : ThemesDictionary (clair ou sombre selon Windows) et
/// ControlsDictionary. Suit ensuite le thème clair/sombre ET la couleur d'accent du système, et prévient Theme pour qu'il
/// recalcule ses pinceaux. À appeler une fois, avant de créer la moindre fenêtre.
/// </summary>
internal static class FluentTheme
{
    private static bool _applied;

    /// <summary>Thème sombre actif (applications Windows en mode sombre).</summary>
    public static bool IsDark { get; private set; }

    /// <summary>Levé (thread d'interface) quand le thème ou l'accent du système change.</summary>
    public static event Action? Changed;

    /// <summary>Vrai si les APPLICATIONS de Windows sont en thème clair (AppsUseLightTheme ; la barre des tâches a sa propre clé).</summary>
    public static bool AppsUseLightTheme()
    {
        if (CaptureOptions.Current.ForceDark is { } forcedDark) return !forcedDark;   // --capture-mode --theme=light|dark
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not int v || v != 0;
        }
        catch { return true; }
    }

    public static void Apply(Application app)
    {
        if (_applied) return;
        _applied = true;
        IsDark = !AppsUseLightTheme();
        try
        {
            app.Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = IsDark ? ApplicationTheme.Dark : ApplicationTheme.Light });
            app.Resources.MergedDictionaries.Add(new ControlsDictionary());
        }
        catch (Exception ex)
        {
            Log.Write("WPF UI : ressources introuvables : " + ex.Message);
            Theme.Refresh();
            return;
        }
        try
        {
            if (CaptureOptions.Current.ForceDark != null) ApplicationThemeManager.Apply(IsDark ? ApplicationTheme.Dark : ApplicationTheme.Light);
            else ApplicationThemeManager.ApplySystemTheme();
        }
        catch (Exception ex) { Log.Write("WPF UI : thème du système non appliqué : " + ex.Message); }
        try { ApplicationThemeManager.Changed += (_, _) => OnSystemChanged(); }
        catch (Exception ex) { Log.Write("WPF UI : suivi du thème indisponible : " + ex.Message); }
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)) return;
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (CaptureOptions.Current.ForceDark != null) return;   // thème forcé en capture : ignore les changements du système
                try { ApplicationThemeManager.ApplySystemTheme(); } catch { }
                OnSystemChanged();
            }));
        };

        // Hors pages de Réglages (Theme.KeepScrollBar), toute zone défilante garde la molette et le tactile mais cache sa barre.
        EventManager.RegisterClassHandler(typeof(ScrollViewer), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
        {
            if (s is ScrollViewer sv && sv.GetValue(Theme.KeepScrollBarProperty) is not true)   // les pages de Réglages gardent leur barre (Theme.ThinScroll)
            {
                if (sv.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled) sv.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                if (sv.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled) sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
            }
        }));
        Theme.Refresh();
    }

    private static void OnSystemChanged()
    {
        IsDark = !AppsUseLightTheme();
        Theme.Refresh();
        Changed?.Invoke();
    }

    /// <summary>Accent du système (hexadécimal #AARRGGBB), ou null si illisible.</summary>
    public static string? SystemAccentHex()
    {
        try
        {
            var c = ApplicationAccentColorManager.SystemAccent;
            return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        }
        catch { return null; }
    }
}
