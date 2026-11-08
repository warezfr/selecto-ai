using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace SelectBar.Support;

/// <summary>
/// Capture de l'écran principal en PNG (GDI). Appelée quand la fenêtre de signalement est masquée. Fichier sans WPF : les noms
/// Point, Image ou Color restent sans ambiguïté.
/// </summary>
public static class ScreenCapture
{
    /// <summary>Largeur maximale de la capture avant compression (les écrans 4K sont réduits).</summary>
    public const int MaxWidth = 2560;

    /// <summary>Repli si la capture dépasse 5 Mo : largeur réduite.</summary>
    public const int FallbackWidth = 1600;

    /// <summary>PNG de l'écran principal. Lève InvalidOperationException si même réduite la capture dépasse la limite.</summary>
    public static byte[] CapturePrimaryScreenPng()
    {
        var bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        using var full = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(full))
        {
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        }
        var png = EncodeScaled(full, MaxWidth);
        if (png.Length > FeedbackLogic.MaxImageBytes) png = EncodeScaled(full, FallbackWidth);
        if (png.Length > FeedbackLogic.MaxImageBytes) throw new InvalidOperationException("capture trop volumineuse");
        return png;
    }

    private static byte[] EncodeScaled(Bitmap source, int maxWidth)
    {
        var scale = Math.Min(1.0, (double)maxWidth / source.Width);
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        using var target = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(target))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(source, 0, 0, width, height);
        }
        using var ms = new MemoryStream();
        target.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
