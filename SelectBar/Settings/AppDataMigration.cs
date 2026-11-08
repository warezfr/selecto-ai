using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SelectBar.Settings;

public enum OllamaMigration { NothingToMove, AlreadyThere, Moved, Failed }

public sealed record MigrationReport(bool RoamingMigrated, int FilesCopied, OllamaMigration Ollama);

/// <summary>
/// Migration silencieuse depuis « SelectBar » vers « Selecto AI » au premier lancement : réglages, extensions, exclusions, clé de licence
/// (%APPDATA%) puis tampon d'essai et moteur IA (%LOCALAPPDATA%). L'ancien dossier reste INTACT (seul le dossier du moteur IA est déplacé).
/// Aucune exception ne sort : en cas d'échec, l'application démarre quand même (réglages par défaut ou ancien chemin du moteur).
/// </summary>
public static class AppDataMigration
{
    /// <summary>Fichiers qui n'ont aucun intérêt dans le nouveau dossier : journaux et rapport d'auto-test.</summary>
    public static bool IsSkipped(string relativePath)
    {
        var name = Path.GetFileName(relativePath);
        return name.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".migrating", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "selftest-report.txt", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Vrai si l'ancien dossier existe et que le nouveau n'existe pas ou ne contient que des journaux
    /// (le journal peut avoir créé le dossier avant la migration : il ne doit pas la bloquer).
    /// </summary>
    public static bool ShouldMigrate(string oldDir, string newDir)
    {
        if (!Directory.Exists(oldDir)) return false;
        if (!Directory.Exists(newDir)) return true;
        return !Directory.EnumerateFiles(newDir, "*", SearchOption.AllDirectories)
            .Any(f => !IsSkipped(Path.GetRelativePath(newDir, f)));
    }

    /// <summary>Liste des copies à faire : tous les fichiers de l'ancien dossier (hors journaux), sans écraser un fichier déjà présent.</summary>
    public static IReadOnlyList<(string Source, string Target)> PlanCopy(string oldDir, string newDir)
    {
        var ops = new List<(string, string)>();
        if (!Directory.Exists(oldDir)) return ops;
        foreach (var file in Directory.EnumerateFiles(oldDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(oldDir, file);
            if (IsSkipped(rel)) continue;
            var target = Path.Combine(newDir, rel);
            if (File.Exists(target)) continue;
            ops.Add((file, target));
        }
        return ops;
    }

    /// <summary>Copie un fichier via un nom temporaire : une interruption ne laisse jamais un réglage tronqué sous son vrai nom.</summary>
    public static void CopyFileAtomic(string source, string destination)
    {
        var dir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var temp = destination + ".migrating";
        File.Copy(source, temp, overwrite: true);
        File.Move(temp, destination, overwrite: true);
    }

    /// <summary>%APPDATA%\SelectBar -> %APPDATA%\SelectoAI (copie, l'ancien dossier n'est pas touché).</summary>
    public static (bool Migrated, int Files) MigrateRoaming(string roamingBase, Action<string>? log = null)
    {
        var oldDir = Path.Combine(roamingBase, AppPaths.LegacyFolderName);
        var newDir = Path.Combine(roamingBase, AppPaths.FolderName);
        try
        {
            if (!ShouldMigrate(oldDir, newDir)) return (false, 0);
            var count = 0;
            foreach (var (source, target) in PlanCopy(oldDir, newDir))
            {
                CopyFileAtomic(source, target);
                count++;
            }
            Directory.CreateDirectory(newDir);
            log?.Invoke($"Migration depuis l'ancien dossier : {count} fichier(s) copié(s).");
            return (true, count);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log?.Invoke("Migration des réglages interrompue : " + ex.Message);
            return (false, 0);
        }
    }

    /// <summary>%LOCALAPPDATA%\SelectBar -> SelectoAI : déplacement du dossier du moteur IA.</summary>
    public static OllamaMigration MigrateLocal(string localBase, Action<string>? log = null)
    {
        var oldDir = Path.Combine(localBase, AppPaths.LegacyFolderName);
        var newDir = Path.Combine(localBase, AppPaths.FolderName);
        var ollama = OllamaMigration.NothingToMove;
        if (!Directory.Exists(oldDir)) return ollama;

        var oldOllama = Path.Combine(oldDir, "ollama");
        var newOllama = Path.Combine(newDir, "ollama");
        if (Directory.Exists(oldOllama))
        {
            try
            {
                if (Directory.Exists(newOllama) && !Directory.EnumerateFileSystemEntries(newOllama).Any())
                    Directory.Delete(newOllama);   // dossier vide créé trop tôt : il ne doit pas bloquer le déplacement
                if (Directory.Exists(newOllama))
                {
                    ollama = OllamaMigration.AlreadyThere;
                }
                else
                {
                    Directory.CreateDirectory(newDir);
                    Directory.Move(oldOllama, newOllama);   // même volume : simple renommage, instantané même pour des Go de modèles
                    ollama = OllamaMigration.Moved;
                    log?.Invoke("Moteur IA déplacé vers le nouveau dossier.");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // moteur en cours d'exécution, antivirus... : OllamaPaths.Resolve réutilise l'ancien chemin
                ollama = OllamaMigration.Failed;
                log?.Invoke("Déplacement du moteur IA impossible (l'ancien chemin reste utilisé) : " + ex.Message);
            }
        }
        return ollama;
    }

    public static MigrationReport Run(string roamingBase, string localBase, Action<string>? log = null)
    {
        var (migrated, files) = MigrateRoaming(roamingBase, log);
        var ollama = MigrateLocal(localBase, log);
        return new MigrationReport(migrated, files, ollama);
    }

    /// <summary>Exécute la migration avec les dossiers Windows réels. À appeler avant tout accès aux réglages et au journal.</summary>
    public static MigrationReport RunDefault(Action<string>? log = null) =>
        Run(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), log);
}
