using SelectBar.Selection;

namespace SelectBar.Actions;

/// <summary>Ce que l'on sait de la sélection courante au moment d'afficher la barre.</summary>
public sealed class ActionContext
{
    public ActionContext(string text, bool isEditable, string? appExe, bool clipboardHasText = false, string? html = null, string? rtf = null)
    {
        Text = text;
        ProcessText = TextLimits.Truncate(text);
        IsEditable = isEditable;
        AppExe = appExe;
        ClipboardHasText = clipboardHasText;
        Html = html;
        Rtf = rtf;
    }

    /// <summary>Texte complet (jusqu'à 10 Mo).</summary>
    public string Text { get; }
    /// <summary>Texte tronqué à 50 Ko, utilisé pour les regex, recherches et extensions.</summary>
    public string ProcessText { get; }
    public bool IsEditable { get; }
    public string? AppExe { get; }
    public bool ClipboardHasText { get; }
    public string? Html { get; }
    public string? Rtf { get; }
    public bool IsUrl { get; set; }
    public bool IsPath { get; set; }
    public bool IsEmail { get; set; }
    /// <summary>Première URL trouvée dans la sélection (nettoyée), ou null.</summary>
    public string? LinkTarget { get; set; }
    /// <summary>mailto:adresse de la sélection, ou null.</summary>
    public string? MailTarget { get; set; }
    /// <summary>Chemin Windows résolu (variables développées) dont le fichier ou le dossier parent existe, ou null.</summary>
    public string? PathTarget { get; set; }
}
