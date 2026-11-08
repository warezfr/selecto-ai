using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;

namespace SelectBar.UI;

/// <summary>Correcteur orthographique de Windows via la vérification d'un TextBox WPF (thread UI uniquement).</summary>
public sealed class SpellService
{
    private readonly Func<string> _language;
    private string _lastWord = "";
    private string _lastLang = "";
    private IReadOnlyList<string> _lastResult = Array.Empty<string>();

    public SpellService(Func<string> language) { _language = language; }

    /// <summary>Suggestions pour un mot mal orthographié ; liste vide si le mot est correct ou si le correcteur échoue.</summary>
    public IReadOnlyList<string> Suggest(string word)
    {
        var lang = _language();
        if (word == _lastWord && lang == _lastLang) return _lastResult;
        IReadOnlyList<string> result = Array.Empty<string>();
        try
        {
            var tb = new TextBox { Text = word, Language = XmlLanguage.GetLanguage(lang), Width = 400, Height = 60 };
            SpellCheck.SetIsEnabled(tb, true);
            tb.Measure(new Size(400, 60));
            tb.Arrange(new Rect(0, 0, 400, 60));
            tb.UpdateLayout();
            var idx = tb.GetNextSpellingErrorCharacterIndex(0, LogicalDirection.Forward);
            if (idx >= 0)
            {
                var err = tb.GetSpellingError(idx);
                if (err != null) result = err.Suggestions.Take(5).ToList();
            }
        }
        catch (Exception ex) { Log.Write("Orthographe : " + ex.Message); }
        _lastWord = word; _lastLang = lang; _lastResult = result;
        return result;
    }
}
