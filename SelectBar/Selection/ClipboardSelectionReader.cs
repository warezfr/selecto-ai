using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SelectBar.Input;
using SelectBar.UI;

namespace SelectBar.Selection;

public sealed record ClipboardRead(string Text, string? Html, string? Rtf);

/// <summary>Repli : sauvegarde le presse-papiers, envoie Ctrl+C, lit le texte, restaure après 800 ms. Thread UI uniquement.</summary>
public sealed class ClipboardSelectionReader
{
    public const int ChangeTimeoutMs = 600;
    public const int RestoreDelayMs = 800;

    private readonly MessageWindow _window;
    private TaskCompletionSource<bool>? _waiter;

    public ClipboardSelectionReader(MessageWindow window)
    {
        _window = window;
        _window.ClipboardUpdated += () => _waiter?.TrySetResult(true);
    }

    public async Task<ClipboardRead?> ReadAsync()
    {
        var snapshot = ClipboardSnapshot.Take();
        var before = NativeMethods.GetClipboardSequenceNumber();
        _waiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        NativeMethods.SendCtrl(NativeMethods.VK_C);

        var changed = NativeMethods.GetClipboardSequenceNumber() != before;
        if (!changed)
        {
            var done = await Task.WhenAny(_waiter.Task, Task.Delay(ChangeTimeoutMs));
            changed = done == _waiter.Task || NativeMethods.GetClipboardSequenceNumber() != before;
        }
        _waiter = null;
        if (!changed) return null; // rien n'était sélectionné : le presse-papiers n'a pas bougé

        await Task.Delay(20); // laisse l'application finir d'écrire tous les formats
        string? text = null, html = null, rtf = null;
        try
        {
            text = ClipboardSnapshot.Retry(() => Clipboard.ContainsText() ? Clipboard.GetText() : null);
            if (text != null)
            {
                html = ClipboardSnapshot.Retry(() => Clipboard.ContainsText(TextDataFormat.Html) ? Clipboard.GetText(TextDataFormat.Html) : null);
                rtf = ClipboardSnapshot.Retry(() => Clipboard.ContainsText(TextDataFormat.Rtf) ? Clipboard.GetText(TextDataFormat.Rtf) : null);
            }
        }
        catch { }

        var afterCopy = NativeMethods.GetClipboardSequenceNumber();
        _ = Task.Delay(RestoreDelayMs).ContinueWith(_ =>
        {
            // N'écrase pas si l'utilisateur a copié autre chose entre-temps.
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (NativeMethods.GetClipboardSequenceNumber() == afterCopy) snapshot.Restore();
            }));
        }, TaskScheduler.Default);

        return string.IsNullOrEmpty(text) ? null : new ClipboardRead(text, html, rtf);
    }
}

/// <summary>Copie en mémoire de tous les formats du presse-papiers.</summary>
public sealed class ClipboardSnapshot
{
    private readonly Dictionary<string, object> _data = new();

    public static ClipboardSnapshot Take()
    {
        var snap = new ClipboardSnapshot();
        try
        {
            var obj = Retry(() => Clipboard.GetDataObject());
            if (obj != null)
                foreach (var fmt in obj.GetFormats(false))
                {
                    try { var d = obj.GetData(fmt, false); if (d != null) snap._data[fmt] = d; } catch { }
                }
        }
        catch { }
        return snap;
    }

    public void Restore()
    {
        try
        {
            if (_data.Count == 0) { Retry(() => { Clipboard.Clear(); return 0; }); return; }
            var obj = new DataObject();
            foreach (var kv in _data) { try { obj.SetData(kv.Key, kv.Value); } catch { } }
            Retry(() => { Clipboard.SetDataObject(obj, true); return 0; });
        }
        catch { }
    }

    /// <summary>Le presse-papiers peut être verrouillé par une autre appli : quelques essais.</summary>
    public static T? Retry<T>(Func<T?> op)
    {
        for (var i = 0; i < 6; i++)
        {
            try { return op(); }
            catch (System.Runtime.InteropServices.ExternalException) { Thread.Sleep(20); }
        }
        return default;
    }
}
