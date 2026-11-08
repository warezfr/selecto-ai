using System;
using System.Linq;
using SelectBar.Support;
using Xunit;

namespace SelectBar.Tests;

[Collection("Localization")]
public class FeedbackLogicTests
{
    private static readonly long[] NoImage = Array.Empty<long>();

    // ------------------------------------------------------------------ types

    [Fact]
    public void Les_types_suivent_le_contrat_dans_l_ordre()
    {
        var codes = FeedbackLogic.Types.Select(t => t.Code).ToArray();
        Assert.Equal(new[] { "bug", "installation", "antivirus", "ia-locale", "actions-en-ligne", "suggestion", "autre" }, codes);
    }

    [Fact]
    public void Chaque_type_a_un_libelle_francais_non_vide_et_unique()
    {
        var labels = FeedbackLogic.Types.Select(t => t.Label).ToArray();
        Assert.All(labels, l => Assert.False(string.IsNullOrWhiteSpace(l)));
        Assert.Equal(labels.Length, labels.Distinct().Count());
    }

    [Theory]
    [InlineData("bug", true)]
    [InlineData("autre", true)]
    [InlineData("BUG", false)]
    [InlineData("inconnu", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Un_code_de_type_est_reconnu_seulement_s_il_figure_au_contrat(string? code, bool known)
    {
        Assert.Equal(known, FeedbackLogic.IsKnownType(code));
    }

    // ------------------------------------------------------------------ validation

    [Fact]
    public void Un_formulaire_complet_est_valide()
    {
        Assert.Null(FeedbackLogic.ValidateDraft("bug", "Le bouton ne répond pas.", "", true, NoImage, 0));
    }

    [Fact]
    public void Le_type_est_obligatoire()
    {
        Assert.NotNull(FeedbackLogic.ValidateDraft(null, "Le bouton ne répond pas.", "", true, NoImage, 0));
        Assert.NotNull(FeedbackLogic.ValidateDraft("inconnu", "Le bouton ne répond pas.", "", true, NoImage, 0));
    }

    [Fact]
    public void Le_message_fait_entre_10_et_5000_caracteres_une_fois_epure()
    {
        Assert.NotNull(FeedbackLogic.ValidateDraft("bug", "123456789", "", true, NoImage, 0));
        Assert.Null(FeedbackLogic.ValidateDraft("bug", "1234567890", "", true, NoImage, 0));
        Assert.NotNull(FeedbackLogic.ValidateDraft("bug", "          ", "", true, NoImage, 0));
        Assert.Null(FeedbackLogic.ValidateDraft("bug", new string('a', 5000), "", true, NoImage, 0));
        Assert.NotNull(FeedbackLogic.ValidateDraft("bug", new string('a', 5001), "", true, NoImage, 0));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("ada@exemple.fr", true)]
    [InlineData("ada.lovelace+test@sous.exemple.fr", true)]
    [InlineData("pas-un-email", false)]
    [InlineData("ada@exemple", false)]
    [InlineData("a..b@exemple.fr", false)]
    [InlineData("ada@@exemple.fr", false)]
    public void L_adresse_e_mail_est_facultative_mais_verifiee(string? email, bool valid)
    {
        var error = FeedbackLogic.ValidateDraft("bug", "Le bouton ne répond pas.", email, true, NoImage, 0);
        Assert.Equal(valid, error is null);
    }

    [Fact]
    public void Le_consentement_est_obligatoire()
    {
        Assert.NotNull(FeedbackLogic.ValidateDraft("bug", "Le bouton ne répond pas.", "", false, NoImage, 0));
    }

    [Fact]
    public void Les_limites_de_pieces_jointes_sont_appliquees()
    {
        const long mo = 1024 * 1024;
        Assert.Null(FeedbackLogic.ValidateAttachments(new long[] { 2 * mo }, 500 * 1024));
        Assert.NotNull(FeedbackLogic.ValidateAttachments(new long[] { 5 * mo + 1 }, 0));          // capture > 5 Mo
        Assert.NotNull(FeedbackLogic.ValidateAttachments(new long[] { 1, 1, 1, 1 }, 0));          // plus de 3 captures
        Assert.NotNull(FeedbackLogic.ValidateAttachments(NoImage, mo + 1));                       // journal > 1 Mo
        Assert.NotNull(FeedbackLogic.ValidateAttachments(new long[] { 4_500_000, 4_500_000, 4_500_000 }, 0)); // 13,5 Mo au total
    }

    [Fact]
    public void Un_formulaire_avec_pieces_jointes_trop_lourdes_est_refuse()
    {
        Assert.NotNull(FeedbackLogic.ValidateDraft("bug", "Le bouton ne répond pas.", "", true, new long[] { 6 * 1024 * 1024 }, 0));
    }

    // ------------------------------------------------------------------ réponse du serveur

    [Fact]
    public void Un_201_avec_un_ticket_est_un_succes()
    {
        var outcome = FeedbackLogic.Interpret(201, "{\"ticket\":\"SEL-2026-0042\"}");
        Assert.True(outcome.Ok);
        Assert.Equal("SEL-2026-0042", outcome.Ticket);
        Assert.Equal("Merci ! Ton ticket SEL-2026-0042 est enregistré.", FeedbackLogic.ThankYou(outcome.Ticket));
    }

    [Theory]
    [InlineData("")]
    [InlineData("pas du json")]
    [InlineData("{\"autre\":\"x\"}")]
    [InlineData("{\"ticket\":\"n'importe quoi\"}")]
    public void Un_201_sans_ticket_valide_est_une_reponse_inattendue(string body)
    {
        var outcome = FeedbackLogic.Interpret(201, body);
        Assert.False(outcome.Ok);
        Assert.Equal(FeedbackLogic.MalformedMessage, outcome.Error);
    }

    [Theory]
    [InlineData(400, "un champ n'est pas valide")]
    [InlineData(403, "a refusé cet envoi")]
    [InlineData(413, "trop lourdes")]
    [InlineData(429, "Trop de demandes")]
    [InlineData(503, "momentanément indisponible")]
    [InlineData(500, "momentanément indisponible")]
    [InlineData(418, "Envoi impossible (erreur 418)")]
    public void Chaque_code_d_erreur_a_un_message_clair_en_francais(int status, string fragment)
    {
        var outcome = FeedbackLogic.Interpret(status, "{\"error\":{\"code\":\"x\"}}");
        Assert.False(outcome.Ok);
        Assert.Equal("", outcome.Ticket);
        Assert.Contains(fragment, outcome.Error);
    }

    // ------------------------------------------------------------------ journal nettoyé

    [Fact]
    public void Le_journal_piege_ne_garde_aucune_donnee_personnelle_ni_cle()
    {
        var raw = string.Join("\n", new[]
        {
            "2026-10-23 10:00:00 Fichier : C:\\Users\\Jean\\AppData\\Roaming\\SelectoAI\\selecto.log",
            "2026-10-23 10:00:01 Machine DESKTOP-EXEMPLE (session jean)",
            "2026-10-23 10:00:02 Contact jean243@exemple.fr",
            "2026-10-23 10:00:05 Authorization: Bearer abcdefghijklmnop1234",
            "2026-10-23 10:00:06 Clé sk-proj-AbCdEf1234567890XyZ utilisée",
            "2026-10-23 10:00:07 Jeanne n'est pas le nom",
        });

        var clean = FeedbackLogic.SanitizeLog(raw, @"C:\Users\Jean", "Jean", "DESKTOP-EXEMPLE");

        Assert.Contains("%USERPROFILE%\\AppData\\Roaming\\SelectoAI\\selecto.log", clean);
        Assert.Contains("<e-mail>", clean);
        Assert.Contains("<ordinateur>", clean);
        Assert.Contains("<utilisateur>", clean);
        Assert.Contains("Jeanne", clean);                 // mot entier seulement
        Assert.DoesNotContain("Jean\\", clean);
        Assert.DoesNotContain("jean243", clean);
        Assert.DoesNotContain("DESKTOP-EXEMPLE", clean);
        Assert.DoesNotContain("abcdefghijklmnop1234", clean);
        Assert.DoesNotContain("sk-proj", clean);
    }

    [Fact]
    public void Le_profil_et_les_noms_sont_masques_sans_tenir_compte_de_la_casse()
    {
        var clean = FeedbackLogic.SanitizeLog("C:\\USERS\\JEAN\\x et JEAN", @"c:\users\jean", "jean", "pc-bureau");
        Assert.Equal("%USERPROFILE%\\x et <utilisateur>", clean);
    }

    [Fact]
    public void Un_chemin_d_un_autre_utilisateur_est_aussi_masque()
    {
        var clean = FeedbackLogic.SanitizeLog("Lancé depuis D:\\Users\\Invité\\Bureau", null, null, null);
        Assert.Equal("Lancé depuis %USERPROFILE%\\Bureau", clean);
    }

    [Fact]
    public void Le_journal_garde_les_2000_dernieres_lignes()
    {
        var raw = string.Join("\n", Enumerable.Range(1, 3000).Select(i => $"ligne {i:D4}"));
        var clean = FeedbackLogic.SanitizeLog(raw, null, null, null);
        var lines = clean.Split('\n');
        Assert.Equal(2000, lines.Length);
        Assert.Equal("ligne 1001", lines[0]);
        Assert.Equal("ligne 3000", lines[^1]);
    }

    [Fact]
    public void Une_ligne_longue_est_tronquee_a_500_caracteres()
    {
        var clean = FeedbackLogic.SanitizeLog("debut " + new string('x', 3000), null, null, null);
        Assert.True(clean.Length <= FeedbackLogic.MaxLogLineLength);
        Assert.EndsWith(FeedbackLogic.TruncationMarker, clean);
    }

    [Fact]
    public void Les_fins_de_ligne_windows_et_le_saut_final_sont_geres()
    {
        Assert.Equal("ligne un\nligne deux", FeedbackLogic.SanitizeLog("ligne un\r\nligne deux\r\n", null, null, null));
        Assert.Equal("", FeedbackLogic.SanitizeLog("", null, null, null));
        Assert.Equal("", FeedbackLogic.SanitizeLog(null, null, null, null));
    }

    [Fact]
    public void Les_cles_api_et_mots_de_passe_affiches_sont_masques()
    {
        var fakeKey = "AIzaSy" + new string('A', 33);   // construite à l'exécution : aucun littéral de clé dans le source
        var clean = FeedbackLogic.SanitizeLog($"token=abc123secret mot de passe: hunter2 {fakeKey}", null, null, null);
        Assert.DoesNotContain("abc123secret", clean);
        Assert.DoesNotContain("hunter2", clean);
        Assert.DoesNotContain("AIzaSy", clean);
        Assert.DoesNotContain(fakeKey, clean);
    }

    [Fact]
    public void FitToBytes_retire_les_lignes_les_plus_anciennes()
    {
        const string text = "aaaa\naaaa\naaaa\naaaa\naaaa";   // 24 octets
        Assert.Equal("aaaa\naaaa\naaaa", FeedbackLogic.FitToBytes(text, 14));
        Assert.Equal(text, FeedbackLogic.FitToBytes(text, 1000));
    }
}
