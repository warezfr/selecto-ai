using System.Globalization;

namespace SelectBar.Localization;

/// <summary>Cultures de l'interface (PUR, sans WPF). Langues : fr, en, es, ar ; code inconnu ⇒ fr.</summary>
public static class UiCulture
{
    public static CultureInfo Make(string code)
    {
        switch ((code ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "en":
                return CultureInfo.GetCultureInfo("en-US");
            case "es":
                return CultureInfo.GetCultureInfo("es-ES");
            case "ar":
                // Arabe : calendrier grégorien (et non hégirien) ; chiffres occidentaux 0-9.
                var ar = (CultureInfo)CultureInfo.GetCultureInfo("ar-SA").Clone();
                ar.DateTimeFormat.Calendar = new GregorianCalendar();
                ar.NumberFormat.DigitSubstitution = DigitShapes.None;
                return ar;
            default:
                return CultureInfo.GetCultureInfo("fr-FR");
        }
    }
}
