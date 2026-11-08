using System.Linq;
using SelectBar.Packaging;
using SelectBar.Localization;
using SelectBar.Update;
using SelectBar.UI;
using Xunit;

namespace SelectBar.Tests;

[Collection("Localization")]
public class TrayFlyoutModelTests
{
    public TrayFlyoutModelTests() => L.Culture = UiCulture.Make("fr");

    private static readonly UpdateInfo Update400 = new("4.0.1", "https://example.test/setup.exe", "00", 1, null);

    [Fact]
    public void Mise_a_jour_affichee_dans_l_edition_Site()
    {
        var model = TrayFlyoutModel.Build(AppEdition.Site, true, 0, Update400);

        Assert.Equal(L.Format("Tray_UpdateAvailable", "4.0.1"), model.UpdateLine);
    }

    [Fact]
    public void Mise_a_jour_masquee_dans_l_edition_store()
    {
        var model = TrayFlyoutModel.Build(AppEdition.Store, true, 0, Update400);

        Assert.Null(model.UpdateLine);
    }

    [Fact]
    public void Pas_de_ligne_de_mise_a_jour_sans_mise_a_jour()
    {
        var model = TrayFlyoutModel.Build(AppEdition.Site, true, 0, null);

        Assert.Null(model.UpdateLine);
    }

    [Fact]
    public void Barre_en_pause_affiche_En_pause()
    {
        var model = TrayFlyoutModel.Build(AppEdition.Site, false, 12, null);

        Assert.False(model.BarOn);
        Assert.Equal(L.Get("Tray_StatusPaused"), model.StatusLine);
    }

    [Fact]
    public void Barre_active_compte_les_actions_au_singulier_et_au_pluriel()
    {
        var one = TrayFlyoutModel.Build(AppEdition.Site, true, 1, null);
        var many = TrayFlyoutModel.Build(AppEdition.Site, true, 12, null);

        Assert.Equal(L.Plural(1, "Tray_StatusOnOne", "Tray_StatusOnOther"), one.StatusLine);
        Assert.Equal(L.Plural(12, "Tray_StatusOnOne", "Tray_StatusOnOther"), many.StatusLine);
        Assert.NotEqual(one.StatusLine, many.StatusLine);
    }

    [Fact]
    public void Ordre_des_entrees_sans_entree_de_licence_ni_d_achat()
    {
        var model = TrayFlyoutModel.Build(AppEdition.Site, true, 0, null);

        Assert.Equal(
            new[]
            {
                TrayEntryKind.Settings, TrayEntryKind.Extensions, TrayEntryKind.About,
                TrayEntryKind.Report, TrayEntryKind.Quit
            },
            model.Entries.Select(e => e.Kind).ToArray());
    }

    [Fact]
    public void Libelles_des_entrees_viennent_des_ressources()
    {
        var model = TrayFlyoutModel.Build(AppEdition.Site, true, 0, null);
        var labels = model.Entries.ToDictionary(e => e.Kind, e => e.Label);

        Assert.Equal(L.Get("Tray_Settings"), labels[TrayEntryKind.Settings]);
        Assert.Equal(L.Get("Tray_Extensions"), labels[TrayEntryKind.Extensions]);
        Assert.Equal(L.Get("Tray_About"), labels[TrayEntryKind.About]);
        Assert.Equal(L.Get("Tray_ReportProblem"), labels[TrayEntryKind.Report]);
        Assert.Equal(L.Get("Tray_Quit"), labels[TrayEntryKind.Quit]);
    }

    [Fact]
    public void Chaque_entree_a_un_glyphe_non_vide()
    {
        var model = TrayFlyoutModel.Build(AppEdition.Site, true, 0, null);

        Assert.All(model.Entries, e => Assert.False(string.IsNullOrEmpty(e.Glyph)));
    }
}
