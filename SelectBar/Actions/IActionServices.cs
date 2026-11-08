using System.Threading.Tasks;
using SelectBar.Online;

namespace SelectBar.Actions;

/// <summary>Effets de bord des actions intégrées (implémentés côté Windows, simulés dans les tests).</summary>
public interface IActionServices
{
    void CopyToClipboard(string text);
    Task SendCutAsync();
    Task SendPasteAsync();
    void OpenUrl(string url);
    void RevealInExplorer(string path);
    /// <summary>Affiche le résultat d'une demande (dictionnaire, traduction, IA) dans le popup, sans quitter l'application.</summary>
    Task ShowResultAsync(ResultRequest request) => Task.CompletedTask;
}

/// <summary>API exposée aux extensions JavaScript (objet selecto).</summary>
public interface IExtensionApi
{
    void CopyText(string text);
    void PasteText(string text);
    void OpenUrl(string url);
    void ShowText(string text);
    /// <summary>Affiche un résultat produit par une extension (popup Copier / Remplacer), sans appel IA.</summary>
    void ShowResult(string text, bool isEditable);
}
