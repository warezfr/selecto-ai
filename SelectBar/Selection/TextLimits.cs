using System.Text;

namespace SelectBar.Selection;

/// <summary>Limites de taille : texte max 10 Mo, traitement max 50 Ko, aperçu 160 caractères.</summary>
public static class TextLimits
{
    public const int MaxTextBytes = 10 * 1024 * 1024;
    public const int MaxProcessChars = 50 * 1024;
    public const int PreviewChars = 160;

    public static bool IsTooLarge(string text) =>
        text.Length > MaxTextBytes || Encoding.UTF8.GetByteCount(text) > MaxTextBytes;

    public static string Truncate(string text) =>
        text.Length <= MaxProcessChars ? text : text.Substring(0, MaxProcessChars);

    public static string Preview(string text)
    {
        var flat = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return flat.Length <= PreviewChars ? flat : flat.Substring(0, PreviewChars - 1) + "…";
    }
}
