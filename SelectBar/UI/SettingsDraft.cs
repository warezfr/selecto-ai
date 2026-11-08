using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SelectBar.Extensions;
using SelectBar.Settings;

namespace SelectBar.UI;

/// <summary>
/// État des réglages à enregistrement différé (V3-02) : pur, sans WPF, testé sur Mac/Linux. Les modifications restent en mémoire
/// jusqu'à « Appliquer » ; le bouton principal devient « Fermer » une fois tout enregistré.
/// </summary>
public static class SettingsDraft
{
    public static string ApplyLabel => Localization.L.Get("Settings_Apply");
    public static string CloseLabel => Localization.L.Get("Settings_Close");
    public static string SavedMessage => Localization.L.Get("Settings_Saved");
    public const int SavedMessageMs = 2500;

    /// <summary>
    /// Forme canonique du JSON : clés d'objet triées (ordre du dictionnaire des clés API sans effet), ordre des tableaux conservé
    /// (l'ordre des actions compte).
    /// </summary>
    public static string Canonical(AppSettings s)
    {
        var json = JsonSerializer.Serialize(s);
        using var doc = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            WriteCanonical(writer, doc.RootElement);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Champs écrits en arrière-plan (vérification des mises à jour) : jamais une modification en attente.</summary>
    private static readonly HashSet<string> BackgroundFields = new(StringComparer.Ordinal)
    {
        nameof(AppSettings.LastUpdateCheckUtc), nameof(AppSettings.DismissedUpdateVersion), nameof(AppSettings.DismissedUpdateAtUtc)
    };

    private static void WriteCanonical(Utf8JsonWriter w, JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                w.WriteStartObject();
                foreach (var p in e.EnumerateObject().Where(p => !BackgroundFields.Contains(p.Name)).OrderBy(p => p.Name, System.StringComparer.Ordinal))
                {
                    w.WritePropertyName(p.Name);
                    WriteCanonical(w, p.Value);
                }
                w.WriteEndObject();
                break;
            case JsonValueKind.Array:
                w.WriteStartArray();
                foreach (var item in e.EnumerateArray()) WriteCanonical(w, item);
                w.WriteEndArray();
                break;
            default:
                e.WriteTo(w);
                break;
        }
    }

    /// <summary>Vrai si les réglages courants diffèrent de ceux enregistrés.</summary>
    public static bool HasChanges(AppSettings saved, AppSettings current) => Canonical(saved) != Canonical(current);

    /// <summary>
    /// Active ou désactive une extension intégrée dans le brouillon. Un sous-menu entraîne ses sous-extensions (Parent) : chacune
    /// est lue par son propre identifiant (ExtensionLoader), donc toutes sont écrites.
    /// </summary>
    public static void SetExtensionEnabled(AppSettings s, string id, bool on, IReadOnlyList<BuiltInExtension>? catalog = null)
    {
        var list = catalog ?? ExtensionCatalog.BuiltIns;
        s.ExtensionEnabled[id] = on;
        foreach (var child in list.Where(b => b.Parent == id)) SetExtensionEnabled(s, child.Id, on, list);
    }

    /// <summary>« Appliquer » tant qu'il reste une modification, « Fermer » ensuite.</summary>
    public static string PrimaryLabel(bool dirty) => dirty ? ApplyLabel : CloseLabel;

    /// <summary>« Rétablir » ne revient en arrière que s'il y a quelque chose à défaire.</summary>
    public static bool RevertEnabled(bool dirty) => dirty;

    /// <summary>Fermer avec des modifications non enregistrées (réglages ou listes d'exclusion) demande une confirmation.</summary>
    public static bool ShouldPromptOnClose(bool dirty, bool extrasDirty) => dirty || extrasDirty;
}
