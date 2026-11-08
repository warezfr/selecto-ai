namespace SelectBar.Actions;

/// <summary>Glyphes Segoe Fluent Icons / MDL2 des actions intégrées.</summary>
public static class ActionGlyphs
{
    /// <summary>Icône seule dans la barre (les autres actions affichent leur libellé).</summary>
    public static string? ForBar(string id) => id switch
    {
        BuiltInActions.Search => "\uE721",   // loupe
        BuiltInActions.OpenLink => "\uE71B", // chaîne
        BuiltInActions.Reveal => "\uED25",   // dossier
        BuiltInActions.Mail => "\uE715",     // enveloppe
        BuiltInActions.Define => "\uE82D",   // livre
        BuiltInActions.Translate => "\uE774", // globe
        _ => null
    };

    /// <summary>Icône de la liste des opérations (réglages).</summary>
    public static string? ForList(string id) => ForBar(id) ?? id switch
    {
        BuiltInActions.Copy => "\uE8C8",
        BuiltInActions.Cut => "\uE8C6",
        BuiltInActions.Paste => "\uE77F",
        _ => null
    };
}
