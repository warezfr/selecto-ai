using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SelectBar.UI;

/// <summary>Icône de l'application pour toutes les fenêtres WPF (réglages, dialogues, bulles) sans toucher à leur mise en page.</summary>
public static class AppIcon
{
    private static ImageSource? _image;

    /// <summary>À appeler une fois au démarrage : toute fenêtre sans Icon reçoit SelectoAI.ico à son chargement.</summary>
    public static void Register()
    {
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
        {
            if (s is Window w && w.Icon == null) w.Icon = Load();
        }));
    }

    /// <summary>Icône Selecto AI (pour les en-têtes de fenêtre) ; null si la ressource manque.</summary>
    public static ImageSource? Image() => Load();

    private static ImageSource? Load()
    {
        if (_image != null) return _image;
        try
        {
            // BitmapFrame d'un .ico : WPF choisit la taille adaptée parmi les images du fichier.
            var f = BitmapFrame.Create(new Uri(TrayIconChoice.PackUri(TrayIconChoice.AppIconFile)));
            f.Freeze();
            return _image = f;
        }
        catch (Exception ex)
        {
            Log.Write("Icône de l'application introuvable : " + ex.Message);
            return null;
        }
    }
}
