using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SelectBar.Localization;
using SelectBar.Online;
using SelectBar.Settings;

namespace SelectBar.Actions;

public static class BuiltInActions
{
    public const string Copy = "builtin.copy", Cut = "builtin.cut", Paste = "builtin.paste",
        Search = "builtin.search", OpenLink = "builtin.openlink", Reveal = "builtin.reveal", Define = "builtin.define",
        Mail = "builtin.mail", Spell = "builtin.spell",
        Copilot = "builtin.copilot", CopilotSummarize = "builtin.copilot.summarize", CopilotExplain = "builtin.copilot.explain",
        CopilotTranslate = "builtin.copilot.translate", CopilotRephrase = "builtin.copilot.rephrase",
        Translate = "builtin.translate", Ai = "builtin.ai", AiSummarize = "builtin.ai.summarize", AiExplain = "builtin.ai.explain",
        AiRephrase = "builtin.ai.rephrase", AiCorrect = "builtin.ai.correct";

    public const string DictionaryUrl = "https://en.wiktionary.org/wiki/***";

    private static readonly Regex ShortWords = new(@"^\s*\S+(\s+\S+){0,2}\s*$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
    private static readonly Regex SingleWord = new(@"^[\p{L}][\p{L}'’-]{1,39}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Ordre d'enregistrement = ordre par défaut dans la barre : Rechercher, liens/dossier/e-mail juste après, puis le reste.
    /// spell : suggestions orthographiques d'un mot (vide = mot correct) ; pasteText : remplace la sélection éditable.
    /// </summary>
    public static List<ActionDefinition> Create(IActionServices svc, Func<AppSettings> settings,
        Func<string, IReadOnlyList<string>>? spell = null, Func<string, Task>? pasteText = null, ICopilotServices? copilot = null) => new List<ActionDefinition>
    {
        new ActionDefinition
        {
            Id = Search, Title = L.Get("Action_Search"), BuiltIn = true,
            Execute = c =>
            {
                var s = settings();
                svc.OpenUrl(SearchEngines.BuildUrl(s.SearchEngineId, c.ProcessText, s.CustomSearchUrl));
                return Task.CompletedTask;
            }
        },
        new ActionDefinition
        {
            Id = OpenLink, Title = L.Get("Action_OpenLink"), BuiltIn = true, RequiresUrl = true,
            Execute = c => { svc.OpenUrl(TextClassifier.NormalizeUrl(c.LinkTarget ?? c.ProcessText)); return Task.CompletedTask; }
        },
        new ActionDefinition
        {
            Id = Reveal, Title = L.Get("Action_Reveal"), BuiltIn = true, RequiresPath = true,
            Execute = c =>
            {
                var path = c.PathTarget ?? TextClassifier.ResolvePath(c.ProcessText);
                if (path != null) svc.RevealInExplorer(path);
                return Task.CompletedTask;
            }
        },
        new ActionDefinition
        {
            Id = Mail, Title = L.Get("Action_Mail"), BuiltIn = true,
            Predicate = c => c.IsEmail,
            Execute = c =>
            {
                var target = c.MailTarget;
                if (target == null)
                {
                    var addr = TextClassifier.FindEmail(c.ProcessText);
                    if (addr != null) target = "mailto:" + addr;
                }
                if (target != null) svc.OpenUrl(target);
                return Task.CompletedTask;
            }
        },
        new ActionDefinition
        {
            Id = Spell, Title = L.Get("Action_Spell"), Icon = "Abc", BuiltIn = true, Context = ContextRequirement.EditableOnly,
            Predicate = c => spell != null && pasteText != null && IsMisspelledWord(c, spell),
            Execute = c =>
            {
                var word = c.ProcessText.Trim();
                var best = spell?.Invoke(word).FirstOrDefault();
                return best == null || pasteText == null ? Task.CompletedTask : pasteText(best);
            }
        },
        new ActionDefinition
        {
            Id = Cut, Title = L.Get("Action_Cut"), BuiltIn = true, Context = ContextRequirement.EditableOnly,
            Execute = _ => svc.SendCutAsync()
        },
        new ActionDefinition
        {
            Id = Copy, Title = L.Get("Action_Copy"), BuiltIn = true,
            Execute = c => { svc.CopyToClipboard(c.Text); return Task.CompletedTask; }
        },
        new ActionDefinition
        {
            Id = Paste, Title = L.Get("Action_Paste"), BuiltIn = true, Context = ContextRequirement.EditableOnly, RequiresClipboardText = true,
            Execute = _ => svc.SendPasteAsync()
        },
        new ActionDefinition
        {
            Id = Define, Title = L.Get("Action_Define"), BuiltIn = true,
            Predicate = c => settings().OnlineActionsEnabled && !c.IsUrl && !c.IsPath && !c.IsEmail && ShortWords.IsMatch(c.ProcessText),
            Execute = c => Show(svc, settings, c, ResultKind.Define)
        },
        new ActionDefinition
        {
            Id = Translate, Title = L.Get("Action_Translate"), BuiltIn = true,
            Predicate = c => settings().OnlineActionsEnabled && !c.IsUrl && !c.IsPath && !c.IsEmail && c.ProcessText.Any(char.IsLetter),
            Execute = c => Show(svc, settings, c, ResultKind.Translate)
        },
    }.Concat(AiActions(svc, settings)).ToList();

    private static Task Show(IActionServices svc, Func<AppSettings> settings, ActionContext c, ResultKind kind) =>
        svc.ShowResultAsync(new ResultRequest
        {
            Kind = kind, Text = c.ProcessText.Trim(), IsEditable = c.IsEditable,
            TargetLanguage = LanguageCodes.Normalize(settings().TargetLanguage, "fr")
        });

    /// <summary>Menu « IA » (flèche) : Résumer, Expliquer, Reformuler, Corriger. Le bouton lui-même explique (ou corrige dans un champ éditable).</summary>
    private static IEnumerable<ActionDefinition> AiActions(IActionServices svc, Func<AppSettings> settings)
    {
        ActionDefinition Make(string id, string title, ResultKind kind, string? parent, string? icon, Func<ActionContext, ResultKind>? pick = null) => new()
        {
            Id = id, Title = title, Icon = icon, BuiltIn = true, ParentId = parent,
            Predicate = c => settings().OnlineActionsEnabled && c.ProcessText.Any(char.IsLetterOrDigit),
            Execute = c => Show(svc, settings, c, pick?.Invoke(c) ?? kind)
        };

        yield return Make(Ai, L.Get("Action_Ai"), ResultKind.Explain, null, "✦", c => c.IsEditable ? ResultKind.Correct : ResultKind.Explain);
        yield return Make(AiSummarize, L.Get("Action_Summarize"), ResultKind.Summarize, Ai, null);
        yield return Make(AiExplain, L.Get("Action_Explain"), ResultKind.Explain, Ai, null);
        yield return Make(AiRephrase, L.Get("Action_Rephrase"), ResultKind.Rephrase, Ai, null);
        yield return Make(AiCorrect, L.Get("Action_Correct"), ResultKind.Correct, Ai, null);
    }

    private static bool IsMisspelledWord(ActionContext c, Func<string, IReadOnlyList<string>> spell)
    {
        var w = c.ProcessText.Trim();
        if (!SingleWord.IsMatch(w) || c.IsUrl || c.IsPath || c.IsEmail) return false;
        try { return spell(w).Count > 0; } catch { return false; }
    }
}
