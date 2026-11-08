using System;
using SelectBar.Localization;

namespace SelectBar.Settings;

/// <summary>Niveau d'IA choisi à l'écran 2 de la fenêtre « Bienvenue ».</summary>
public enum WelcomeAiChoice { None, Local, Online }

/// <summary>Écrans de la fenêtre « Bienvenue » (dans l'ordre).</summary>
public enum WelcomeStep { Intro = 0, Ai = 1, Free = 2 }

/// <summary>Logique PURE du premier lancement : quand l'afficher, comment les trois écrans s'enchaînent (aucune dépendance WPF).</summary>
public static class FirstRunFlow
{
    public const int StepCount = 3;

    /// <summary>
    /// L'assistant s'ouvre seulement si aucune configuration n'existait au démarrage (settings.json absent) et qu'il n'a pas déjà été
    /// terminé. Un utilisateur qui met à jour (ou migré depuis SelectBar) a déjà un fichier : il ne voit jamais l'assistant.
    /// </summary>
    public static bool ShouldShow(bool settingsFileExisted, bool firstRunCompleted, bool captureMode) =>
        !captureMode && !settingsFileExisted && !firstRunCompleted;

    public static WelcomeStep Next(WelcomeStep step) => step switch
    {
        WelcomeStep.Intro => WelcomeStep.Ai,
        WelcomeStep.Ai => WelcomeStep.Free,
        _ => WelcomeStep.Free
    };

    public static WelcomeStep Back(WelcomeStep step) => step switch
    {
        WelcomeStep.Free => WelcomeStep.Ai,
        _ => WelcomeStep.Intro
    };

    public static bool IsLast(WelcomeStep step) => step == WelcomeStep.Free;
    public static bool CanGoBack(WelcomeStep step) => step != WelcomeStep.Intro;

    /// <summary>« Passer » est toujours possible : il ferme l'assistant sans rien exiger.</summary>
    public static bool CanSkip(WelcomeStep step) => true;

    /// <summary>Texte du bouton principal : « Continuer » puis « C'est parti » sur le dernier écran.</summary>
    public static string PrimaryLabel(WelcomeStep step) => IsLast(step) ? L.Get("Welcome_Start") : L.Get("Welcome_Continue");

    /// <summary>Indicateur de progression : « 2 sur 3 ».</summary>
    public static string ProgressText(WelcomeStep step) => Localization.L.Rtl(Localization.L.Format("Welcome_StepOf", (int)step + 1, StepCount));

    /// <summary>Indicateur à points : vrai pour le point de l'écran courant (et des précédents, la progression se remplit).</summary>
    public static bool[] Dots(WelcomeStep step)
    {
        var dots = new bool[StepCount];
        for (var i = 0; i < StepCount; i++) dots[i] = i <= (int)step;
        return dots;
    }

    /// <summary>Titre de l'écran d'IA : libellé de chaque choix (mêmes mots que les réglages).</summary>
    public static string AiChoiceLabel(WelcomeAiChoice c) => c switch
    {
        WelcomeAiChoice.None => L.Get("Settings_FirstRun_None"),
        WelcomeAiChoice.Local => L.Get("Settings_FirstRun_Local"),
        _ => L.Get("Settings_FirstRun_Provider")
    };

    /// <summary>Le choix « Sur mon PC » déclenche le parcours d'installation existant ; « Mon fournisseur » la saisie de clé ; « Aucune » ne touche à rien.</summary>
    public static string? AssistantPreset(WelcomeAiChoice c) => c switch
    {
        WelcomeAiChoice.Local => "ollama",
        WelcomeAiChoice.Online => "online",
        _ => null
    };
}
