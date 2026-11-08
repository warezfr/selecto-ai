using System.Collections.Generic;
using SelectBar.Localization;

namespace SelectBar.Settings;

/// <summary>Texte de la carte « Recommandation du jour » de l'onglet Actions.</summary>
public static class Recommendation
{
    public static string Daily(AppSettings s, int extensionCount, IReadOnlyList<string> disabledBuiltInTitles)
    {
        if (!s.Enabled) return L.Get("Settings_Rec_Paused");
        if (!s.OnlineActionsEnabled) return L.Get("Settings_Rec_EnableOnline");
        if (disabledBuiltInTitles.Count > 0) return L.Format("Settings_Rec_ReenableAction", disabledBuiltInTitles[0]);
        if (extensionCount == 0) return L.Get("Settings_Rec_AddExtension");
        return L.Get("Settings_Rec_AllGood");
    }
}
