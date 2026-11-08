using System.Collections.Generic;
using System.Linq;
using SelectBar.Settings;

namespace SelectBar.Extensions;

/// <summary>Une extension intégrée (fichier de BuiltInExtensions/) : identifiant, noms par langue, sous-menu, activation par défaut.</summary>
public sealed record BuiltInExtension(string Id, IReadOnlyDictionary<string, string> Names, string? Parent, bool EnabledByDefault);

/// <summary>Catalogue des extensions intégrées et règle d'activation : active si présente à vrai dans les réglages, inactive si présente à faux, sinon valeur par défaut.</summary>
public static class ExtensionCatalog
{
    public static readonly IReadOnlyList<BuiltInExtension> BuiltIns = new[]
    {
        new BuiltInExtension("ai.selecto.builtin.count",
            new Dictionary<string, string> { ["fr"] = "Compter", ["en"] = "Count", ["es"] = "Contar", ["ar"] = "عدّ" },
            null, EnabledByDefault: true),
        new BuiltInExtension("ai.selecto.builtin.calc",
            Names("Calculer", "Calculate", "Calcular", "احسب"), null, EnabledByDefault: false),
        new BuiltInExtension(TextMenu,
            Names("Texte", "Text", "Texto", "نص"), null, EnabledByDefault: false),
        Child("upper", Names("MAJUSCULES", "UPPERCASE", "MAYÚSCULAS", "أحرف كبيرة")),
        Child("lower", Names("minuscules", "lowercase", "minúsculas", "أحرف صغيرة")),
        Child("title", Names("Titre", "Title Case", "Título", "حالة العنوان")),
        Child("sentence", Names("Première lettre", "Sentence case", "Primera letra", "أول حرف")),
        Child("spaces", Names("Supprimer les doubles espaces", "Remove double spaces", "Quitar espacios dobles", "إزالة المسافات المزدوجة")),
        Child("join", Names("Joindre les lignes", "Join lines", "Unir las líneas", "دمج الأسطر")),
        Child("sort", Names("Trier les lignes", "Sort lines", "Ordenar las líneas", "فرز الأسطر")),
    };

    /// <summary>Identifiant du sous-menu « Texte ».</summary>
    public const string TextMenu = "ai.selecto.builtin.text";

    private static Dictionary<string, string> Names(string fr, string en, string es, string ar) =>
        new() { ["fr"] = fr, ["en"] = en, ["es"] = es, ["ar"] = ar };

    private static BuiltInExtension Child(string suffix, Dictionary<string, string> names) =>
        new(TextMenu + "." + suffix, names, TextMenu, EnabledByDefault: false);

    public static bool IsEnabled(AppSettings settings, string id, bool enabledByDefault) =>
        settings.ExtensionEnabled.TryGetValue(id, out var enabled) ? enabled : enabledByDefault;

    /// <summary>
    /// Activation finale d'une extension. Une sous-action (<paramref name="parent"/> non nul) sans réglage explicite suit
    /// l'état de son sous-menu (<paramref name="parentActive"/>) ; avec un réglage, celui-ci prime.
    /// </summary>
    public static bool IsActive(AppSettings settings, string id, bool enabledByDefault, string? parent, bool parentActive)
    {
        if (parent != null && !settings.ExtensionEnabled.ContainsKey(id)) return parentActive;
        return IsEnabled(settings, id, enabledByDefault);
    }

    /// <summary>Activation d'une extension intégrée (par défaut : celle du catalogue ; inconnue du catalogue : active).</summary>
    public static bool IsEnabled(AppSettings settings, string id) =>
        IsEnabled(settings, id, BuiltIns.FirstOrDefault(b => b.Id == id)?.EnabledByDefault ?? true);
}
