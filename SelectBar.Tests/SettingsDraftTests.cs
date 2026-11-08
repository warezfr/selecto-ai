using System;
using System.Collections.Generic;
using SelectBar.Extensions;
using SelectBar.Settings;
using SelectBar.UI;
using Xunit;

namespace SelectBar.Tests;

[Collection("Localization")]
public class SettingsDraftTests
{
    private static AppSettings Base() => new()
    {
        ActionOrder = new List<string> { "copy", "cut", "paste" },
        DisabledActions = new List<string> { "reveal" },
        LlmApiKeys = new Dictionary<string, string> { ["openai"] = "k1", ["groq"] = "k2" }
    };

    private static AppSettings Clone(AppSettings s) =>
        System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(s))!;

    [Fact]
    public void Clone_identique_n_est_pas_un_changement()
    {
        var saved = Base();
        Assert.False(SettingsDraft.HasChanges(saved, Clone(saved)));
    }

    [Fact]
    public void Changement_scalaire_est_un_changement()
    {
        var saved = Base();
        var current = Clone(saved);
        current.BarScalePercent += 5;
        Assert.True(SettingsDraft.HasChanges(saved, current));
    }

    [Fact]
    public void Ordre_des_actions_est_un_changement()
    {
        var saved = Base();
        var current = Clone(saved);
        (current.ActionOrder[0], current.ActionOrder[1]) = (current.ActionOrder[1], current.ActionOrder[0]);
        Assert.True(SettingsDraft.HasChanges(saved, current));
    }

    [Fact]
    public void Entree_de_cle_API_modifiee_est_un_changement()
    {
        var saved = Base();
        var current = Clone(saved);
        current.LlmApiKeys["openai"] = "autre";
        Assert.True(SettingsDraft.HasChanges(saved, current));
    }

    [Fact]
    public void Ordre_d_insertion_des_cles_API_n_est_pas_un_changement()
    {
        var saved = new AppSettings { LlmApiKeys = new Dictionary<string, string> { ["openai"] = "k1", ["groq"] = "k2" } };
        var current = new AppSettings { LlmApiKeys = new Dictionary<string, string> { ["groq"] = "k2", ["openai"] = "k1" } };
        Assert.False(SettingsDraft.HasChanges(saved, current));
    }

    [Fact]
    public void Retour_a_l_etat_enregistre_n_est_plus_un_changement()
    {
        var saved = Base();
        var current = Clone(saved);
        current.Enabled = !current.Enabled;
        Assert.True(SettingsDraft.HasChanges(saved, current));
        current.Enabled = saved.Enabled;
        Assert.False(SettingsDraft.HasChanges(saved, current));
    }

    [Fact]
    public void Libelles_du_bouton_principal()
    {
        Assert.Equal("Appliquer", SettingsDraft.PrimaryLabel(true));
        Assert.Equal("Fermer", SettingsDraft.PrimaryLabel(false));
        Assert.Equal(SelectBar.Localization.L.Get("Settings_Saved"), SettingsDraft.SavedMessage);
        Assert.Equal(2500, SettingsDraft.SavedMessageMs);
    }

    [Fact]
    public void Rétablir_n_est_actif_que_s_il_y_a_des_modifications()
    {
        Assert.True(SettingsDraft.RevertEnabled(true));
        Assert.False(SettingsDraft.RevertEnabled(false));
    }

    [Fact]
    public void Fermeture_demande_confirmation_seulement_si_non_enregistre()
    {
        Assert.True(SettingsDraft.ShouldPromptOnClose(true, false));
        Assert.True(SettingsDraft.ShouldPromptOnClose(false, true));
        Assert.False(SettingsDraft.ShouldPromptOnClose(false, false));
    }

    [Fact]
    public void Extension_desactivee_dans_le_brouillon_marque_un_changement()
    {
        var saved = Base();
        saved.ExtensionEnabled["ai.selecto.builtin.count"] = true;
        var current = Clone(saved);
        Assert.False(SettingsDraft.HasChanges(saved, current));

        current.ExtensionEnabled["ai.selecto.builtin.count"] = false;
        Assert.True(SettingsDraft.HasChanges(saved, current));

        current.ExtensionEnabled["ai.selecto.builtin.count"] = true;
        Assert.False(SettingsDraft.HasChanges(saved, current));
    }

    [Fact]
    public void Activer_une_extension_passe_par_ExtensionEnabled_et_marque_un_changement()
    {
        var saved = Base();
        var current = Clone(saved);
        SettingsDraft.SetExtensionEnabled(current, "ai.selecto.builtin.calc", true, Array.Empty<BuiltInExtension>());
        Assert.True(current.ExtensionEnabled["ai.selecto.builtin.calc"]);
        Assert.DoesNotContain("ai.selecto.builtin.calc", current.DisabledActions);
        Assert.True(SettingsDraft.HasChanges(saved, current));
    }

    [Fact]
    public void Sous_menu_entraine_toutes_ses_sous_extensions_et_pas_les_autres()
    {
        var names = new Dictionary<string, string> { ["fr"] = "Texte" };
        var catalog = new[]
        {
            new BuiltInExtension("ai.selecto.builtin.text", names, null, false),
            new BuiltInExtension("ai.selecto.builtin.text.upper", names, "ai.selecto.builtin.text", false),
            new BuiltInExtension("ai.selecto.builtin.text.lower", names, "ai.selecto.builtin.text", false),
            new BuiltInExtension("ai.selecto.builtin.count", names, null, true),
        };
        var s = Base();
        SettingsDraft.SetExtensionEnabled(s, "ai.selecto.builtin.text", true, catalog);
        Assert.Equal(3, s.ExtensionEnabled.Count);
        Assert.True(s.ExtensionEnabled["ai.selecto.builtin.text"]);
        Assert.True(s.ExtensionEnabled["ai.selecto.builtin.text.upper"]);
        Assert.True(s.ExtensionEnabled["ai.selecto.builtin.text.lower"]);
        Assert.False(s.ExtensionEnabled.ContainsKey("ai.selecto.builtin.count"));
    }
}

