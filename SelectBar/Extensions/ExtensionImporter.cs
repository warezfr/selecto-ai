using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace SelectBar.Extensions;

/// <summary>Raison pour laquelle un fichier (ou une entrée d'un zip) n'est pas ajouté.</summary>
public enum ImportIssue { NotScript, NotArchive, NoScript, TooManyEntries, OutsideRoot, BadName, TooLarge, InvalidHeader }

/// <summary>Extension acceptée : nom de fichier (avec .js) et contenu brut, copié tel quel dans le dossier utilisateur.</summary>
public sealed record ImportedScript(string FileName, byte[] Content);

/// <summary>Élément refusé : nom du fichier ou de l'archive, raison, et détail facultatif (erreur d'en-tête).</summary>
public sealed record ImportSkip(string Name, ImportIssue Issue, string? Detail = null);

public sealed record ImportResult(IReadOnlyList<ImportedScript> Scripts, IReadOnlyList<ImportSkip> Skipped);

/// <summary>
/// Lecture pure des extensions à ajouter (.js ou .zip), sans interface. Un zip ne fournit que des fichiers .js
/// à la racine, au plus 50 ; chacun fait 256 Ko au plus et doit avoir un en-tête valide. Tout le reste est refusé
/// avec une raison, sans interrompre les autres entrées.
/// </summary>
public static class ExtensionImporter
{
    /// <summary>Nombre maximal de fichiers .js dans un zip ; au-delà, l'archive entière est refusée.</summary>
    public const int MaxZipScripts = 50;

    /// <summary>Taille maximale d'un fichier .js extrait d'un zip, en octets.</summary>
    public const int MaxScriptBytes = 256 * 1024;

    private const string ExtJs = ".js";
    private const string ExtZip = ".zip";
    private const string ForbiddenNameChars = "\\/:*?\"<>|";

    /// <summary>Lit un fichier choisi par l'utilisateur (.js ou .zip). Autre extension : refusé.</summary>
    public static ImportResult ImportFile(string path)
    {
        var name = Path.GetFileName(path);
        var ext = Path.GetExtension(path);
        if (ext.Equals(ExtZip, StringComparison.OrdinalIgnoreCase))
        {
            using var stream = File.OpenRead(path);
            return ImportZip(name, stream);
        }
        if (ext.Equals(ExtJs, StringComparison.OrdinalIgnoreCase))
            return ImportScript(name, File.ReadAllBytes(path));
        return Refused(name, ImportIssue.NotScript);
    }

    /// <summary>Contrôle un fichier .js : l'en-tête doit être valide. Le contenu est gardé tel quel.</summary>
    public static ImportResult ImportScript(string fileName, byte[] bytes)
    {
        if (!ExtensionHeaderParser.TryParse(DecodeText(bytes), out _, out var error))
            return Refused(fileName, ImportIssue.InvalidHeader, error);
        return new ImportResult(new[] { new ImportedScript(fileName, bytes) }, Array.Empty<ImportSkip>());
    }

    /// <summary>Lit un zip : seulement les .js à la racine, avec les règles de taille, de nombre et d'en-tête.</summary>
    public static ImportResult ImportZip(string archiveName, Stream zip)
    {
        ZipArchive archive;
        try { archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true); }
        catch (InvalidDataException) { return Refused(archiveName, ImportIssue.NotArchive); }

        using (archive)
        {
            var candidates = archive.Entries.Where(e => e.FullName.EndsWith(ExtJs, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!candidates.Any(e => IsRootName(e.FullName))) return Refused(archiveName, ImportIssue.NoScript);
            if (candidates.Count > MaxZipScripts) return Refused(archiveName, ImportIssue.TooManyEntries);

            var scripts = new List<ImportedScript>();
            var skipped = new List<ImportSkip>();
            foreach (var entry in candidates)
            {
                var name = entry.FullName;
                if (!IsRootName(name))
                {
                    skipped.Add(new ImportSkip(name, ImportIssue.OutsideRoot));
                    continue;
                }
                if (!IsSafeFileName(name[..^ExtJs.Length]))
                {
                    skipped.Add(new ImportSkip(name, ImportIssue.BadName));
                    continue;
                }
                if (entry.Length > MaxScriptBytes)
                {
                    skipped.Add(new ImportSkip(name, ImportIssue.TooLarge));
                    continue;
                }

                byte[]? bytes;
                try { bytes = ReadLimited(entry, MaxScriptBytes); }
                catch (InvalidDataException) { skipped.Add(new ImportSkip(name, ImportIssue.NotArchive)); continue; }
                if (bytes == null)
                {
                    skipped.Add(new ImportSkip(name, ImportIssue.TooLarge));
                    continue;
                }

                var one = ImportScript(name, bytes);
                scripts.AddRange(one.Scripts);
                skipped.AddRange(one.Skipped.Select(s => s with { Name = name }));
            }
            return new ImportResult(scripts, skipped);
        }
    }

    /// <summary>Entrée à la racine du zip : pas de dossier (« / », « \ ») ni de remontée (« .. »).</summary>
    private static bool IsRootName(string name) => !(name.Contains('/') || name.Contains('\\') || name.Contains(".."));

    /// <summary>Nom de fichier libre dans le dossier : « nom.js », puis « nom-2.js », « nom-3.js », etc.</summary>
    public static string UniqueFileName(string stem, Func<string, bool> exists)
    {
        var candidate = stem + ExtJs;
        for (var n = 2; exists(candidate); n++) candidate = $"{stem}-{n}{ExtJs}";
        return candidate;
    }

    /// <summary>Un nom utilisable : non vide, sans espace de contrôle ni caractère interdit sous Windows (dont « : », qui ouvre un flux de données).</summary>
    public static bool IsSafeFileName(string stem)
    {
        if (string.IsNullOrWhiteSpace(stem)) return false;
        return !stem.Any(c => c < ' ' || ForbiddenNameChars.IndexOf(c) >= 0);
    }

    private static ImportResult Refused(string name, ImportIssue issue, string? detail = null) =>
        new(Array.Empty<ImportedScript>(), new[] { new ImportSkip(name, issue, detail) });

    /// <summary>Lit l'entrée sans dépasser <paramref name="max"/> octets (l'en-tête peut mentir sur la taille).</summary>
    private static byte[]? ReadLimited(ZipArchiveEntry entry, int max)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > max) return null;
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    /// <summary>Texte UTF-8 sans marque d'ordre des octets (BOM).</summary>
    private static string DecodeText(byte[] bytes)
    {
        var skip = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        return Encoding.UTF8.GetString(bytes, skip, bytes.Length - skip);
    }
}
