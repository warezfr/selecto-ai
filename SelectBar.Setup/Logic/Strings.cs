using System;
using System.Collections.Generic;
using System.Globalization;

namespace SelectBar.Setup.Logic;

/// <summary>Textes de l'installateur, français et anglais. Une clé absente renvoie la clé elle-même (visible, jamais d'exception).</summary>
public static class Strings
{
    private static readonly Dictionary<string, (string Fr, string En)> Table = new()
    {
        ["app.name"] = ("Selecto AI", "Selecto AI"),
        ["app.tagline"] = ("Sélectionne. Agis.", "Select. Act."),
        ["window.install"] = ("Installation de {0}", "{0} Setup"),
        ["window.uninstall"] = ("Désinstallation de {0}", "{0} Uninstall"),

        ["welcome.install"] = ("Installer", "Install"),
        ["welcome.update"] = ("Mettre à jour", "Update"),
        ["welcome.options"] = ("Options", "Options"),
        ["welcome.version"] = ("Version {0} · {1}", "Version {0} · {1}"),
        ["welcome.updating"] = ("Mise à jour depuis la version {0} · tes réglages sont conservés", "Updating from version {0} · your settings are kept"),
        ["welcome.demoText1"] = ("Une idée à retenir : ", "An idea worth keeping: "),
        ["welcome.demoSelected"] = ("sélectionne un texte, la barre apparaît", "select some text and the bar appears"),
        ["welcome.demoText2"] = (" au-dessus, prête à agir.", " right above it, ready to act."),
        ["welcome.demoText3"] = ("Rien à ouvrir, rien à copier-coller ailleurs.", "Nothing to open, nothing to paste elsewhere."),

        ["opt.title"] = ("Options d'installation", "Install options"),
        ["opt.folder"] = ("Dossier d'installation", "Install folder"),
        ["opt.browse"] = ("Parcourir…", "Browse…"),
        ["opt.desktop"] = ("Ajouter un raccourci sur le Bureau", "Add a Desktop shortcut"),
        ["opt.startup"] = ("Lancer Selecto AI avec Windows", "Launch Selecto AI when Windows starts"),
        ["opt.language"] = ("Langue", "Language"),
        ["opt.ok"] = ("OK", "OK"),

        ["step.prepare"] = ("Préparation", "Preparing"),
        ["step.copy"] = ("Copie des fichiers", "Copying files"),
        ["step.shortcuts"] = ("Raccourcis", "Shortcuts"),
        ["step.finalize"] = ("Finalisation", "Finishing up"),
        ["ustep.prepare"] = ("Fermeture de Selecto AI", "Closing Selecto AI"),
        ["ustep.copy"] = ("Suppression des fichiers", "Removing files"),
        ["ustep.shortcuts"] = ("Raccourcis et registre", "Shortcuts and registry"),
        ["ustep.finalize"] = ("Finalisation", "Finishing up"),
        ["progress.cancel"] = ("Annuler", "Cancel"),
        ["progress.cancelling"] = ("Annulation…", "Cancelling…"),

        ["slide1.title"] = ("Une barre d'actions sous ta sélection", "An action bar under your selection"),
        ["slide1.text"] = ("Sélectionne un texte dans n'importe quelle application : copier, chercher, traduire, résumer en un clic.", "Select text in any app: copy, search, translate, summarize in one click."),
        ["slide2.title"] = ("Une IA locale et gratuite", "Free, local AI"),
        ["slide2.text"] = ("Un moteur d'IA tourne sur ton PC, sans compte ni abonnement. Ton texte reste chez toi.", "An AI engine runs on your PC, with no account or subscription. Your text stays with you."),
        ["slide3.title"] = ("Des actions en ligne", "Online actions"),
        ["slide3.text"] = ("Dictionnaire, Wikipédia, traduction, recherche web : et tu peux ajouter tes propres actions.", "Dictionary, Wikipedia, translation, web search: and you can add your own actions."),

        ["done.title"] = ("{0} est installé", "{0} is installed"),
        ["done.tip"] = ("Sélectionne un texte pour voir la barre.", "Select some text to see the bar."),
        ["done.launch"] = ("Lancer Selecto AI", "Launch Selecto AI"),
        ["done.finish"] = ("Terminer", "Finish"),

        ["err.title"] = ("L'installation a échoué", "Installation failed"),
        ["err.utitle"] = ("La désinstallation a échoué", "Uninstall failed"),
        ["err.log"] = ("Journal : {0}", "Log: {0}"),
        ["err.copy"] = ("Copier les détails", "Copy details"),
        ["err.copied"] = ("Copié", "Copied"),
        ["err.retry"] = ("Réessayer", "Try again"),
        ["err.close"] = ("Fermer", "Close"),
        ["err.downgrade"] = ("La version {0} de Selecto AI est déjà installée, plus récente que celle de cet installateur ({1}). La rétrogradation est refusée.", "Selecto AI version {0} is already installed and is newer than this installer ({1}). Downgrading is refused."),
        ["err.nopayload"] = ("Cet installateur ne contient pas les fichiers de l'application. Télécharge-le de nouveau.", "This installer does not contain the application files. Please download it again."),
        ["err.baddir"] = ("Le dossier d'installation n'est pas valide : {0}", "The install folder is not valid: {0}"),
        ["err.cancelled"] = ("Installation annulée.", "Installation cancelled."),
        ["err.demo"] = ("Exemple d'erreur affiché par le mode démonstration : le fichier SelectoAI.exe est utilisé par un autre programme.", "Sample error shown by demo mode: SelectoAI.exe is in use by another program."),

        ["un.title"] = ("Désinstaller {0} ?", "Uninstall {0}?"),
        ["un.body"] = ("L'application et ses raccourcis seront supprimés. Tes réglages et ton IA locale sont conservés, sauf si tu coches la case ci-dessous.", "The app and its shortcuts will be removed. Your settings and local AI are kept unless you tick the box below."),
        ["un.purge"] = ("Supprimer aussi mes réglages et mon IA locale ({0})", "Also delete my settings and local AI ({0})"),
        ["un.purgeHint"] = ("Définitif. Un Ollama installé séparément n'est jamais touché.", "This cannot be undone. A separately installed Ollama is never touched."),
        ["un.button"] = ("Désinstaller", "Uninstall"),
        ["un.cancel"] = ("Annuler", "Cancel"),
        ["un.done"] = ("{0} est désinstallé", "{0} is uninstalled"),
        ["un.doneTip"] = ("Tu peux fermer cette fenêtre.", "You can close this window."),
        ["un.shortcut"] = ("Désinstaller {0}", "Uninstall {0}"),

        ["close.confirm"] = ("Quitter maintenant ? L'opération en cours sera annulée.", "Quit now? The operation in progress will be cancelled."),
        ["running.other"] = ("Une autre fenêtre d'installation de Selecto AI est déjà ouverte.", "Another Selecto AI setup window is already open."),
    };

    public static IEnumerable<string> Keys => Table.Keys;

    public static string Get(string key, string lang)
    {
        if (!Table.TryGetValue(key, out var v)) return key;
        return lang == "en" ? v.En : v.Fr;
    }

    public static string Format(string key, string lang, params object[] args)
    {
        var pattern = Get(key, lang);
        try { return string.Format(CultureInfo.InvariantCulture, pattern, args); }
        catch (FormatException) { return pattern; }
    }

    /// <summary>« fr » ou « en » (toute autre valeur : null).</summary>
    public static string? NormalizeLang(string? value)
    {
        var v = (value ?? "").Trim().ToLowerInvariant();
        if (v.StartsWith("fr", StringComparison.Ordinal)) return "fr";
        if (v.StartsWith("en", StringComparison.Ordinal)) return "en";
        return null;
    }

    /// <summary>Langue demandée > langue de Windows (français ou anglais) > français.</summary>
    public static string ResolveLang(string? requested, string? uiCulture)
        => NormalizeLang(requested) ?? NormalizeLang(uiCulture) ?? "fr";
}
