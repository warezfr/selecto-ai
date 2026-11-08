using System;
using System.Windows.Interop;
using SelectBar.Input;

namespace SelectBar.UI;

/// <summary>Fenêtre de messages cachée : écouteur presse-papiers et raccourci global.</summary>
public sealed class MessageWindow : IDisposable
{
    private const int HotkeyId = 0x5342;
    private readonly HwndSource _source;
    private bool _hotkeyRegistered;

    public event Action? ClipboardUpdated;
    public event Action? HotkeyPressed;

    public MessageWindow()
    {
        var p = new HwndSourceParameters("SelectoAIMessageWindow") { ParentWindow = NativeMethods.HWND_MESSAGE, Width = 0, Height = 0 };
        _source = new HwndSource(p);
        _source.AddHook(WndProc);
        NativeMethods.AddClipboardFormatListener(_source.Handle);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_CLIPBOARDUPDATE) ClipboardUpdated?.Invoke();
        else if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId) { HotkeyPressed?.Invoke(); handled = true; }
        return IntPtr.Zero;
    }

    /// <summary>Enregistre (ou retire si modifiers == 0) le raccourci global ; false si refusé par Windows.</summary>
    public bool SetHotkey(uint modifiers, uint vk)
    {
        if (_hotkeyRegistered) { NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId); _hotkeyRegistered = false; }
        if (modifiers == 0) return true;
        _hotkeyRegistered = NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, modifiers, vk);
        return _hotkeyRegistered;
    }

    public void Dispose()
    {
        SetHotkey(0, 0);
        NativeMethods.RemoveClipboardFormatListener(_source.Handle);
        _source.Dispose();
    }
}
