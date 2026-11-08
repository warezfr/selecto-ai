using System;
using System.Text;
using Jint;
using SelectBar.Actions;
using SelectBar.Localization;

namespace SelectBar.Extensions;

/// <summary>
/// Exécute une extension JS avec Jint. Objet global <c>selecto</c> :
/// text, editable, locale, inline, calc(expr), copyText(s), pasteText(s), openUrl(u), showText(s), showResult(s).
/// Un alias de compatibilité pointe vers le même objet ; son nom est décodé à l'exécution.
/// </summary>
public static class ExtensionHost
{
    /// <summary>Nom de l'alias de compatibilité, encodé en base64.</summary>
    public const string LegacyGlobalNameBase64 = "cG9wY2xpcA==";

    private static string LegacyGlobalName => Encoding.UTF8.GetString(Convert.FromBase64String(LegacyGlobalNameBase64));

    /// <summary>Exécution normale (clic sur l'action) : délai de 5 s. <paramref name="onlineAllowed"/> : taux de change autorisés.</summary>
    public static void Run(string script, ActionContext ctx, IExtensionApi api, bool onlineAllowed = false) =>
        Execute(script, ctx, api, onlineAllowed, false, TimeSpan.FromSeconds(5));

    /// <summary>
    /// Exécution « inline » (affichage du résultat dans la barre) : seul le dernier texte passé à showText est gardé.
    /// Renvoie null si rien n'est affiché, si le script échoue ou s'il dépasse <paramref name="timeout"/> (la barre ne l'affiche pas).
    /// </summary>
    public static string? RunInline(string script, ActionContext ctx, bool onlineAllowed, TimeSpan timeout)
    {
        var capture = new InlineCapture();
        try
        {
            Execute(script, ctx, capture, onlineAllowed, true, timeout);
        }
        catch (Exception)
        {
            return null;
        }
        return string.IsNullOrWhiteSpace(capture.Text) ? null : capture.Text;
    }

    private static void Execute(string script, ActionContext ctx, IExtensionApi api, bool onlineAllowed, bool inline, TimeSpan timeout)
    {
        var engine = new Engine(o => o.TimeoutInterval(timeout).LimitRecursion(200));
        engine.SetValue("__text", ctx.ProcessText);
        engine.SetValue("__editable", ctx.IsEditable);
        engine.SetValue("__locale", L.Culture.TwoLetterISOLanguageName);
        engine.SetValue("__inline", inline);
        engine.SetValue("__calc", new Func<string, string?>(text => Calculate(text, onlineAllowed)));
        engine.SetValue("__copyText", new Action<string>(api.CopyText));
        engine.SetValue("__pasteText", new Action<string>(api.PasteText));
        engine.SetValue("__openUrl", new Action<string>(api.OpenUrl));
        engine.SetValue("__showText", new Action<string>(api.ShowText));
        engine.SetValue("__showResult", new Action<string>(text => api.ShowResult(text, ctx.IsEditable)));
        engine.Execute(
            "var selecto = { text: __text, editable: __editable, locale: __locale, inline: __inline, calc: __calc, copyText: __copyText, pasteText: __pasteText, openUrl: __openUrl, showText: __showText, showResult: __showResult };");
        engine.SetValue(LegacyGlobalName, engine.GetValue("selecto"));
        engine.Execute(script);
    }

    /// <summary>Calcul d'une sélection : expression arithmétique, sinon conversion d'unité ou de devise. Null si rien.</summary>
    private static string? Calculate(string text, bool onlineAllowed)
    {
        var culture = L.Culture;
        return CalcEngine.Evaluate(text, culture)
            ?? UnitConverter.TryConvert(text, culture, () => CurrencyRates.Shared.Current(onlineAllowed));
    }

    /// <summary>Reçoit les textes affichés par un script inline ; les autres effets sont ignorés.</summary>
    private sealed class InlineCapture : IExtensionApi
    {
        public string? Text { get; private set; }

        public void CopyText(string text) { }
        public void PasteText(string text) { }
        public void OpenUrl(string url) { }
        public void ShowText(string text) => Text = text;
        public void ShowResult(string text, bool isEditable) { }
    }
}
