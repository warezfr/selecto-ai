using System;
using System.Threading.Tasks;

namespace SelectBar.Selection;

public sealed record SelectionResult(string Text, bool IsEditable, string? Html, string? Rtf);

/// <summary>Orchestration : UIA (timeout 300 ms) puis repli presse-papiers selon SelectionDecision.</summary>
public sealed class SelectionService
{
    public const int UiaTimeoutMs = 300;
    private readonly ClipboardSelectionReader _clipboard;

    public SelectionService(ClipboardSelectionReader clipboard) => _clipboard = clipboard;

    public async Task<SelectionResult?> ReadAsync(int x, int y, string? exe)
    {
        var uiaTask = Task.Run(() => UiaSelectionReader.Read(x, y));
        UiaResult uia;
        if (await Task.WhenAny(uiaTask, Task.Delay(UiaTimeoutMs)) == uiaTask)
            uia = await uiaTask;
        else
            uia = new UiaResult(UiaOutcome.Timeout, null, false);

        switch (SelectionDecision.Decide(uia.Outcome, exe))
        {
            case SelectionSource.Uia:
                return Build(uia.Text!, uia.IsEditable, null, null);
            case SelectionSource.Clipboard:
                var c = await _clipboard.ReadAsync();
                return c == null ? null : Build(c.Text, uia.IsEditable, c.Html, c.Rtf);
            default:
                return null;
        }
    }

    private static SelectionResult? Build(string text, bool editable, string? html, string? rtf) =>
        string.IsNullOrWhiteSpace(text) || TextLimits.IsTooLarge(text) ? null : new SelectionResult(text, editable, html, rtf);
}
