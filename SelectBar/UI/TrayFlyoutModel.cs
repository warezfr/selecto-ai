using System.Collections.Generic;
using SelectBar.Packaging;
using SelectBar.Localization;
using SelectBar.Update;

namespace SelectBar.UI;

/// <summary>Entrées du flyout de la zone de notification, dans l'ordre d'affichage.</summary>
public enum TrayEntryKind { Settings, Extensions, About, Report, Quit }

/// <summary>Une ligne du flyout : glyphe Segoe Fluent, libellé localisé, action associée.</summary>
public sealed record TrayEntry(string Glyph, string Label, TrayEntryKind Kind);

/// <summary>
/// Ce qu'affiche le flyout de la zone de notification (spec §5.2). Logique PURE : aucune dépendance WPF.
/// </summary>
public sealed record TrayFlyoutModel(
    string StatusLine,
    bool BarOn,
    string? UpdateLine,
    IReadOnlyList<TrayEntry> Entries)
{
    // Glyphes Segoe Fluent (Segoe MDL2 Assets) : à vérifier visuellement dans la VM.
    private const string GlyphSettings = "";
    private const string GlyphExtensions = "";
    private const string GlyphAbout = "";
    private const string GlyphReport = "";
    private const string GlyphQuit = "";

    /// <summary>
    /// Construit le modèle. La ligne de mise à jour n'apparaît jamais dans l'édition Store
    /// (la mise à jour y passe par le Store).
    /// </summary>
    public static TrayFlyoutModel Build(AppEdition edition, bool barOn, int actionCount, UpdateInfo? update)
    {
        var statusLine = barOn
            ? L.Plural(actionCount, "Tray_StatusOnOne", "Tray_StatusOnOther")
            : L.Get("Tray_StatusPaused");

        var updateLine = update != null && edition != AppEdition.Store
            ? L.Format("Tray_UpdateAvailable", update.Version)
            : null;

        var entries = new List<TrayEntry>
        {
            new(GlyphSettings, L.Get("Tray_Settings"), TrayEntryKind.Settings),
            new(GlyphExtensions, L.Get("Tray_Extensions"), TrayEntryKind.Extensions),
            new(GlyphAbout, L.Get("Tray_About"), TrayEntryKind.About),
            new(GlyphReport, L.Get("Tray_ReportProblem"), TrayEntryKind.Report),
        };
        entries.Add(new TrayEntry(GlyphQuit, L.Get("Tray_Quit"), TrayEntryKind.Quit));

        return new TrayFlyoutModel(
            statusLine,
            barOn,
            updateLine,
            entries);
    }
}
