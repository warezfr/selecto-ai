using System;

namespace SelectBar.Setup.Logic;

/// <summary>Les quatre étapes affichées (installation : Préparation, Copie, Raccourcis, Finalisation ; désinstallation : mêmes emplacements).</summary>
public enum InstallStep { Prepare = 0, Copy = 1, Shortcuts = 2, Finalize = 3 }

public static class ProgressSteps
{
    // Bornes en pourcentage de chaque étape : la copie des fichiers pèse l'essentiel.
    private static readonly double[] Start = { 0, 8, 85, 94 };
    private static readonly double[] End = { 8, 85, 94, 100 };

    public static double StartOf(InstallStep s) => Start[(int)s];
    public static double EndOf(InstallStep s) => End[(int)s];

    /// <summary>Pourcentage global (0 à 100) pour une avancée <paramref name="fraction"/> (0 à 1) dans l'étape.</summary>
    public static double Percent(InstallStep step, double fraction)
    {
        var f = double.IsNaN(fraction) ? 0 : Math.Min(1, Math.Max(0, fraction));
        return Start[(int)step] + (End[(int)step] - Start[(int)step]) * f;
    }

    public static InstallStep StepFor(double percent)
    {
        if (percent >= End[2]) return InstallStep.Finalize;
        if (percent >= End[1]) return InstallStep.Shortcuts;
        if (percent >= End[0]) return InstallStep.Copy;
        return InstallStep.Prepare;
    }

    /// <summary>Durée totale de la progression simulée du mode --demo.</summary>
    public static readonly TimeSpan DemoDuration = TimeSpan.FromSeconds(12);

    // Instants de fin de chaque étape dans la simulation (secondes) : 1,2 / 9,6 / 10,8 / 12.
    private static readonly double[] DemoEnds = { 1.2, 9.6, 10.8, 12.0 };

    /// <summary>Pourcentage simulé après <paramref name="elapsed"/> (0 à 100, monotone).</summary>
    public static double DemoPercentAt(TimeSpan elapsed)
    {
        var t = elapsed.TotalSeconds;
        if (t <= 0) return 0;
        if (t >= DemoDuration.TotalSeconds) return 100;
        var previousEnd = 0.0;
        for (var i = 0; i < 4; i++)
        {
            if (t <= DemoEnds[i])
                return Percent((InstallStep)i, (t - previousEnd) / (DemoEnds[i] - previousEnd));
            previousEnd = DemoEnds[i];
        }
        return 100;
    }
}
