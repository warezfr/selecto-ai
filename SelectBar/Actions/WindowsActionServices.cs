using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using SelectBar.Input;
using SelectBar.Online;
using SelectBar.Selection;
using SelectBar.UI;

namespace SelectBar.Actions;

/// <summary>Vrai lancement externe (le seul endroit de l'application qui appelle Process.Start pour une page, un dossier, un mail ou une application).</summary>
public sealed class ShellLaunchBackend : ILaunchBackend
{
    public void Start(LaunchKind kind, string target)
    {
        if (kind == LaunchKind.Explorer)
        {
            var explorer = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            Process.Start(new ProcessStartInfo(explorer) { Arguments = target, UseShellExecute = true });
        }
        else Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }
}

/// <summary>Implémentation Windows des effets de bord (presse-papiers, Explorateur, navigateur par défaut).</summary>
public sealed class WindowsActionServices : IActionServices, IExtensionApi, ICopilotServices
{
    public WindowsActionServices()
    {
        Launcher = new ExternalLauncher(CaptureOptions.Current.CaptureMode ? new SimulatedLaunchBackend(Log.Write) : new ShellLaunchBackend(), Log.Write, () => CursorProvider());
    }

    /// <summary>Point d'entrée unique des lancements externes (journalisés dans selectbar.log).</summary>
    public ExternalLauncher Launcher { get; }

    public Func<(int X, int Y)> CursorProvider { get; set; } = () => (0, 0);

    /// <summary>Affiche le popup de résultat (branché par AppController).</summary>
    public Func<ResultRequest, Task>? ResultPresenter { get; set; }
    /// <summary>Présente un texte produit par une extension (popup, Copier / Remplacer) : texte, éditable.</summary>
    public Action<string, bool>? ExtensionResultPresenter { get; set; }

    public Task ShowResultAsync(ResultRequest request) => ResultPresenter?.Invoke(request) ?? Task.CompletedTask;

    /// <summary>Bouton « Ouvrir dans le navigateur » du popup d'erreur : page web, prompt copié d'abord s'il est trop long pour l'URL.</summary>
    public void OpenFallback(ResultPayload payload)
    {
        if (payload.FallbackUrl == null) return;
        if (payload.FallbackClipboard != null)
        {
            CopyToClipboard(payload.FallbackClipboard);
            ((ICopilotServices)this).Notify(CopilotPrompts.ClipboardNotice);
        }
        OpenUrlAs(LaunchPolicy.PopupFallback, LaunchSource.Popup, payload.FallbackUrl);
    }

    public void CopyToClipboard(string text)
    {
        try { Clipboard.SetDataObject(text, true); }
        catch (Exception ex) { Log.Write("Copier : " + ex.Message); }
    }

    public async Task SendCutAsync()
    {
        await Task.Delay(30); // la cible reste active : la barre ne prend jamais le focus
        NativeMethods.SendCtrl(NativeMethods.VK_X);
    }

    public async Task SendPasteAsync()
    {
        await Task.Delay(30);
        NativeMethods.SendCtrl(NativeMethods.VK_V);
    }

    /// <summary>
    /// Ouvre une URL http(s)/mailto dans l'application par défaut. Les chemins file: sont seulement affichés dans
    /// l'Explorateur. Tout autre schéma est refusé (le texte vient d'une sélection ou d'une extension : non fiable).
    /// L'origine est celle du clic en cours (Launcher.Scope) ; sans clic en cours, le lancement est refusé.
    /// </summary>
    public void OpenUrl(string url) => OpenUrlCore(null, default, false, url);

    /// <summary>Même chose avec une origine explicite (boutons des réglages, repli du popup).</summary>
    public void OpenUrlAs(string origin, LaunchSource source, string url) => OpenUrlCore(origin, source, true, url);

    private void OpenUrlCore(string? origin, LaunchSource source, bool explicitOrigin, string url)
    {
        try
        {
            switch (LinkSafety.Check(url, out var target))
            {
                case LinkKind.Web:
                    var kind = target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? LaunchKind.Mail : LaunchKind.Browser;
                    if (explicitOrigin) Launcher.OpenAs(origin, source, kind, target); else Launcher.Open(kind, target);
                    break;
                case LinkKind.LocalPath:
                    RevealInExplorer(target);
                    break;
                default:
                    Log.Write("Ouvrir URL refusé : schéma ou forme non autorisé");
                    break;
            }
        }
        catch (Exception ex) { Log.Write("Ouvrir URL : " + ex.Message); }
    }

    /// <summary>Fichier : sélectionné dans l'Explorateur (jamais lancé). Dossier : ouvert. Sinon dossier parent.</summary>
    public void RevealInExplorer(string path)
    {
        try
        {
            var args = LinkSafety.ExplorerArguments(path, System.IO.File.Exists, System.IO.Directory.Exists);
            if (args == null) { Log.Write("Afficher dans l'Explorateur : chemin introuvable ou invalide"); return; }
            Launcher.Open(LaunchKind.Explorer, args);
        }
        catch (Exception ex) { Log.Write("Révéler : " + ex.Message); }
    }

    /// <summary>Ouvre un dossier dans l'Explorateur depuis un bouton des réglages ou du menu de la zone de notification.</summary>
    public void OpenFolderAs(string origin, LaunchSource source, string directory)
    {
        try
        {
            System.IO.Directory.CreateDirectory(directory);
            Launcher.OpenAs(origin, source, LaunchKind.Explorer, "\"" + directory + "\"");
        }
        catch (Exception ex) { Log.Write("Ouvrir le dossier : " + ex.Message); }
    }

    // --- Copilot ---
    bool ICopilotServices.IsAppInstalled()
    {
        try { using var k = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey("ms-copilot"); return k != null; }
        catch { return false; }
    }

    void ICopilotServices.LaunchApp()
    {
        try { Launcher.Open(LaunchKind.App, "ms-copilot:"); }
        catch (Exception ex) { Log.Write("Copilot (application) : " + ex.Message); }
    }

    void ICopilotServices.Notify(string text)
    {
        var (x, y) = CursorProvider();
        ToastWindow.ShowNear(text, x, y);
    }

    // --- API extensions ---
    void IExtensionApi.CopyText(string text) => CopyToClipboard(text);

    void IExtensionApi.PasteText(string text) => _ = PasteTextAsync(text);

    public async Task PasteTextAsync(string text)
    {
        var snapshot = ClipboardSnapshot.Take();
        CopyToClipboard(text);
        await Task.Delay(40);
        NativeMethods.SendCtrl(NativeMethods.VK_V);
        await Task.Delay(500);
        snapshot.Restore();
    }

    void IExtensionApi.OpenUrl(string url) => OpenUrl(url);

    void IExtensionApi.ShowText(string text)
    {
        var (x, y) = CursorProvider();
        ToastWindow.ShowNear(text, x, y);
    }

    void IExtensionApi.ShowResult(string text, bool isEditable) => ExtensionResultPresenter?.Invoke(text, isEditable);
}
