using System;
using System.Globalization;
using System.Resources;

namespace SelectBar.Localization;

/// <summary>
/// Accès aux textes de l'interface. Les clés sont en `Zone_Element` (PascalCase) ; le français (Strings.resx) est la référence.
/// Une clé absente de la langue courante retombe sur le français, puis sur la clé entre crochets.
/// </summary>
public static class L
{
    /// <summary>Nom de base des ressources (Strings.resx, Strings.en.resx…). Lu dans l'assembly qui contient cette classe.</summary>
    public const string BaseName = "SelectBar.Localization.Strings";

    private static readonly ResourceManager Resources = new ResourceManager(BaseName, typeof(L).Assembly);
    private static CultureInfo _culture = UiCulture.Make("fr");

    /// <summary>Culture courante (défaut fr-FR). Changer la culture déclenche <see cref="CultureChanged"/>.</summary>
    public static CultureInfo Culture
    {
        get => _culture;
        set
        {
            _culture = value ?? throw new ArgumentNullException(nameof(value));
            CultureChanged?.Invoke();
        }
    }

    /// <summary>Levé après chaque changement de <see cref="Culture"/>.</summary>
    public static event Action? CultureChanged;

    public static string Get(string key)
    {
        var text = Resources.GetString(key, _culture);
        return text ?? Resources.GetString(key, UiCulture.Make("fr")) ?? "[" + key + "]";
    }

    public static string Format(string key, params object[] args) =>
        string.Format(_culture, Get(key), args);

    /// <summary>Choisit la forme singulier (count = 1) ou pluriel, puis insère le nombre dans « {0} ».</summary>
    public static string Plural(int count, string keyOne, string keyOther) =>
        Format(count == 1 ? keyOne : keyOther, count);

    /// <summary>Marque de droite-à-gauche (U+200F) : ancre le sens d'un texte assemblé qui mêle arabe, chiffres et ponctuation.</summary>
    public const char Rlm = '\u200F';

    /// <summary>Entoure <paramref name="text"/> de marques RLM quand la langue courante s'écrit de droite à gauche.</summary>
    public static string Rtl(string text) => Rtl(text, Culture.TextInfo.IsRightToLeft);

    /// <summary>
    /// Version pure : en droite-à-gauche, préfixe et suffixe U+200F (sans doublon si déjà présents) ; sinon le texte est inchangé.
    /// Chaîne vide ou nulle : inchangée.
    /// </summary>
    public static string Rtl(string text, bool rtl)
    {
        if (!rtl || string.IsNullOrEmpty(text)) return text;
        var start = text[0] == Rlm ? "" : Rlm.ToString();
        var end = text[^1] == Rlm ? "" : Rlm.ToString();
        return start + text + end;
    }

    /// <summary>Marque LRI (U+2066) : ouvre un isolement de gauche-à-droite pour un morceau latin ou numérique.</summary>
    public const char Lri = '\u2066';

    /// <summary>Marque PDI (U+2069) : ferme l'isolement ouvert par <see cref="Lri"/>.</summary>
    public const char Pdi = '\u2069';

    /// <summary>Isole une taille, une unité ou un code latin (« ≈ 1,0 Go ») quand la langue courante s'écrit de droite à gauche.</summary>
    public static string Ltr(string text) => Ltr(text, Culture.TextInfo.IsRightToLeft);

    /// <summary>
    /// Version pure : en droite-à-gauche, encadre <paramref name="text"/> de LRI … PDI pour que les parenthèses voisines
    /// suivent le sens arabe. Sinon, ou chaîne vide ou nulle : inchangée.
    /// </summary>
    public static string Ltr(string text, bool rtl)
    {
        if (!rtl || string.IsNullOrEmpty(text)) return text;
        return text[0] == Lri ? text : Lri + text + Pdi;
    }
}
