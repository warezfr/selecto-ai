using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SelectBar.Actions;
using SelectBar.Settings;

namespace SelectBar.Extensions;

public static class ExtensionLoader
{
    private const string OldSamplePrefix = "com.selectbar.sample.";
    private const string RetiredSuffix = ".ancien";

    /// <summary>Dossier des extensions intégrées, copié à côté de l'exécutable.</summary>
    public static string BuiltInDirectory => Path.Combine(AppContext.BaseDirectory, "BuiltInExtensions");

    /// <summary>Renomme en « .js.ancien » (sans les supprimer) les exemples de la 3.0 restés dans le dossier utilisateur. Renvoie leur nombre ; ceux déjà renommés sont ignorés sans bruit.</summary>
    public static int RetireOldSamples(string extensionsDir)
    {
        var count = 0;
        if (!Directory.Exists(extensionsDir)) return 0;
        foreach (var file in Directory.GetFiles(extensionsDir, "*.js"))
        {
            try
            {
                // exemple déjà renommé « .ancien » (le .js est revenu à côté, réinstallation ou copie) : rien à faire, et rien à journaliser
                if (File.Exists(file + RetiredSuffix)) continue;
                if (!ExtensionHeaderParser.TryParse(File.ReadAllText(file), out var meta, out _)) continue;
                if (!meta!.Identifier.StartsWith(OldSamplePrefix, StringComparison.OrdinalIgnoreCase)) continue;
                File.Move(file, file + RetiredSuffix);
                count++;
            }
            catch (Exception ex) { Log.Write($"Exemple d'extension {Path.GetFileName(file)} : {ex.Message}"); }
        }
        return count;
    }

    /// <summary>
    /// Charge les extensions intégrées puis celles de l'utilisateur ; une extension utilisateur de même identifiant
    /// remplace l'intégrée. Les extensions désactivées dans les réglages sont ignorées.
    /// <paramref name="onSamplesRetired"/> est appelé une fois si des anciens exemples ont été renommés.
    /// </summary>
    public static List<ActionDefinition> LoadAll(string userDir, IExtensionApi api, AppSettings settings, Action? onSamplesRetired = null)
    {
        if (RetireOldSamples(userDir) > 0) onSamplesRetired?.Invoke();

        var chosen = new Dictionary<string, Loaded>(StringComparer.Ordinal);
        var order = new List<string>();
        Collect(BuiltInDirectory, chosen, order);
        Collect(userDir, chosen, order);

        var result = new List<ActionDefinition>();
        foreach (var id in order)
        {
            var item = chosen[id];
            if (!IsActive(settings, chosen, id, item.Meta)) continue;
            var source = item.Source;
            var name = item.FileName;
            Func<ActionContext, Task> exec = ctx =>
            {
                try { ExtensionHost.Run(source, ctx, api, settings.OnlineActionsEnabled); }
                catch (Exception ex) { Log.Write($"Extension {name} : {ex.Message}"); }
                return Task.CompletedTask;
            };
            // Action inline : le résultat est calculé à l'affichage de la barre, dans le délai de la barre rapide.
            Func<ActionContext, string?>? inline = null;
            if (item.Meta.Inline) inline = ctx => ExtensionHost.RunInline(source, ctx, settings.OnlineActionsEnabled, InlineTimeout);
            if (!ExtensionMapper.TryMap(item.Meta, exec, out var action, out var err, inline, opensMenu: !ExtensionHeaderParser.HasCode(source)))
            { Log.Write($"Extension ignorée {name} : {err}"); continue; }
            result.Add(action!);
        }

        // Un sous-menu sans sous-action activée n'apparaît pas.
        var parents = new HashSet<string>(result.Select(a => a.ParentId).OfType<string>());
        result.RemoveAll(a => a.OpensMenu && !parents.Contains(a.Id));
        return result;
    }

    /// <summary>
    /// Activation : réglage explicite, sinon valeur par défaut. Une sous-action sans réglage explicite suit son sous-menu
    /// (interrupteur « Texte » des Réglages) ; ses propres valeurs par défaut ne s'appliquent alors pas.
    /// </summary>
    private static bool IsActive(AppSettings settings, Dictionary<string, Loaded> chosen, string id, ExtensionMetadata meta)
    {
        var parentActive = meta.Parent != null && chosen.TryGetValue(meta.Parent, out var menu)
            && ExtensionCatalog.IsEnabled(settings, meta.Parent, menu.Meta.EnabledByDefault);
        return ExtensionCatalog.IsActive(settings, id, meta.EnabledByDefault, meta.Parent, parentActive);
    }

    /// <summary>Délai d'une action inline dans la barre rapide : au-delà, rien n'est affiché.</summary>
    private static readonly TimeSpan InlineTimeout = TimeSpan.FromMilliseconds(50);

    private sealed record Loaded(ExtensionMetadata Meta, string Source, string FileName);

    private static void Collect(string dir, Dictionary<string, Loaded> chosen, List<string> order)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.js").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var source = File.ReadAllText(file);
                if (!ExtensionHeaderParser.TryParse(source, out var meta, out var err))
                { Log.Write($"Extension ignorée {Path.GetFileName(file)} : {err}"); continue; }

                if (!chosen.ContainsKey(meta!.Identifier)) order.Add(meta.Identifier);
                chosen[meta.Identifier] = new Loaded(meta, source, Path.GetFileName(file));
            }
            catch (Exception ex) { Log.Write($"Extension {file} : {ex.Message}"); }
        }
    }
}
