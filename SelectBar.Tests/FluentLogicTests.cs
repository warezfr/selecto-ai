using SelectBar.Localization;
using SelectBar.Settings;
using SelectBar.UI;
using Xunit;

namespace SelectBar.Tests;

[Collection("Localization")]
public class FirstRunFlowTests
{
    [Fact]
    public void Assistant_affiche_seulement_sans_configuration_existante()
    {
        Assert.True(FirstRunFlow.ShouldShow(false, false, false));
        Assert.False(FirstRunFlow.ShouldShow(true, false, false));    // mise à jour / migration depuis SelectBar
        Assert.False(FirstRunFlow.ShouldShow(false, true, false));    // déjà terminé
        Assert.False(FirstRunFlow.ShouldShow(false, false, true));    // mode capture : jamais
    }

    [Fact]
    public void Les_trois_ecrans_s_enchainent_et_s_arretent_aux_bornes()
    {
        Assert.Equal(WelcomeStep.Ai, FirstRunFlow.Next(WelcomeStep.Intro));
        Assert.Equal(WelcomeStep.Free, FirstRunFlow.Next(WelcomeStep.Ai));
        Assert.Equal(WelcomeStep.Free, FirstRunFlow.Next(WelcomeStep.Free));
        Assert.Equal(WelcomeStep.Ai, FirstRunFlow.Back(WelcomeStep.Free));
        Assert.Equal(WelcomeStep.Intro, FirstRunFlow.Back(WelcomeStep.Ai));
        Assert.Equal(WelcomeStep.Intro, FirstRunFlow.Back(WelcomeStep.Intro));
        Assert.False(FirstRunFlow.CanGoBack(WelcomeStep.Intro));
        Assert.True(FirstRunFlow.CanGoBack(WelcomeStep.Free));
    }

    [Fact]
    public void Passer_est_toujours_possible_et_le_dernier_bouton_dit_cest_parti()
    {
        foreach (var s in new[] { WelcomeStep.Intro, WelcomeStep.Ai, WelcomeStep.Free })
            Assert.True(FirstRunFlow.CanSkip(s));
        Assert.Equal("Continuer", FirstRunFlow.PrimaryLabel(WelcomeStep.Intro));
        Assert.Equal("C'est parti", FirstRunFlow.PrimaryLabel(WelcomeStep.Free));
        Assert.True(FirstRunFlow.IsLast(WelcomeStep.Free));
        Assert.False(FirstRunFlow.IsLast(WelcomeStep.Ai));
    }

    [Fact]
    public void Progression_et_points()
    {
        Assert.Equal(SelectBar.Localization.L.Format("Welcome_StepOf", 1, 3), FirstRunFlow.ProgressText(WelcomeStep.Intro));
        Assert.Equal(SelectBar.Localization.L.Format("Welcome_StepOf", 3, 3), FirstRunFlow.ProgressText(WelcomeStep.Free));
        Assert.Equal(new[] { true, true, false }, FirstRunFlow.Dots(WelcomeStep.Ai));
    }

    [Fact]
    public void Choix_d_IA_declenche_le_bon_parcours()
    {
        Assert.Null(FirstRunFlow.AssistantPreset(WelcomeAiChoice.None));
        Assert.Equal("ollama", FirstRunFlow.AssistantPreset(WelcomeAiChoice.Local));
        Assert.Equal("online", FirstRunFlow.AssistantPreset(WelcomeAiChoice.Online));
        Assert.Equal("Aucune", FirstRunFlow.AiChoiceLabel(WelcomeAiChoice.None));
        Assert.Contains("Ollama", FirstRunFlow.AiChoiceLabel(WelcomeAiChoice.Local));
        Assert.Contains("clé API", FirstRunFlow.AiChoiceLabel(WelcomeAiChoice.Online));
    }
}

public class FlyoutPlacementTests
{
    // écran 1920x1080 ; zone de travail réduite de 48 px du côté de la barre des tâches
    [Theory]
    [InlineData(0, 0, 1920, 1032, TaskbarEdge.Bottom)]
    [InlineData(0, 48, 1920, 1080, TaskbarEdge.Top)]
    [InlineData(48, 0, 1920, 1080, TaskbarEdge.Left)]
    [InlineData(0, 0, 1872, 1080, TaskbarEdge.Right)]
    [InlineData(0, 0, 1920, 1080, TaskbarEdge.Bottom)]   // barre masquée automatiquement
    public void Cote_de_la_barre_des_taches(int wl, int wt, int wr, int wb, TaskbarEdge expected) =>
        Assert.Equal(expected, FlyoutPlacement.DetectEdge(0, 0, 1920, 1080, wl, wt, wr, wb));

    [Fact]
    public void Barre_en_bas_le_flyout_se_pose_au_dessus_centre_sur_l_icone()
    {
        var (x, y) = FlyoutPlacement.Place(TaskbarEdge.Bottom, 1700, 1050, 320, 400, 0, 0, 1920, 1032);
        Assert.Equal(1540, x);
        Assert.Equal(1032 - 400 - 12, y);
    }

    [Fact]
    public void Barre_en_haut_le_flyout_se_pose_en_dessous()
    {
        var (x, y) = FlyoutPlacement.Place(TaskbarEdge.Top, 960, 20, 320, 400, 0, 48, 1920, 1080);
        Assert.Equal(800, x);
        Assert.Equal(48 + 12, y);
    }

    [Fact]
    public void Barre_a_gauche_et_a_droite()
    {
        var left = FlyoutPlacement.Place(TaskbarEdge.Left, 20, 1000, 320, 400, 48, 0, 1920, 1080);
        Assert.Equal(48 + 12, left.X);
        Assert.Equal(1080 - 400, left.Y);   // ancre trop basse : ramené dans la zone de travail
        var right = FlyoutPlacement.Place(TaskbarEdge.Right, 1900, 500, 320, 400, 0, 0, 1872, 1080);
        Assert.Equal(1872 - 320 - 12, right.X);
        Assert.Equal(300, right.Y);
    }

    [Theory]
    [InlineData(TaskbarEdge.Bottom)]
    [InlineData(TaskbarEdge.Top)]
    [InlineData(TaskbarEdge.Left)]
    [InlineData(TaskbarEdge.Right)]
    public void Le_flyout_reste_toujours_dans_la_zone_de_travail(TaskbarEdge edge)
    {
        foreach (var (ax, ay) in new[] { (0, 0), (1919, 1079), (0, 1079), (1919, 0), (960, 540) })
        {
            var (x, y) = FlyoutPlacement.Place(edge, ax, ay, 320, 400, 0, 0, 1920, 1080);
            Assert.InRange(x, 0, 1920 - 320);
            Assert.InRange(y, 0, 1080 - 400);
        }
    }
}
