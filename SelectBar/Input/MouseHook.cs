using System;
using System.Runtime.InteropServices;

namespace SelectBar.Input;

/// <summary>
/// Hooks bas niveau souris (WH_MOUSE_LL) + clavier (WH_KEYBOARD_LL, pour Échap / frappe).
/// À créer sur le thread UI (boucle de messages requise). Les gestionnaires doivent rester très rapides.
/// </summary>
public sealed class MouseHook : IDisposable
{
    private readonly NativeMethods.HookProc _mouseProc;
    private readonly NativeMethods.HookProc _keyProc;
    private IntPtr _mouse = IntPtr.Zero, _keyboard = IntPtr.Zero;

    public event Action<int, int>? LeftDown;
    public event Action<int, int>? LeftUp;
    public event Action? OtherButtonDown;
    public event Action<int, int>? Wheel;
    public event Action<uint>? KeyDown;

    private readonly bool _acceptInjected;

    /// <param name="acceptInjected">Mode capture uniquement : accepte les événements injectés (SendInput / mouse_event) ; en production ils sont ignorés.</param>
    public MouseHook(bool acceptInjected = false)
    {
        _acceptInjected = acceptInjected;
        _mouseProc = MouseCallback;
        _keyProc = KeyCallback;
    }

    public void Start()
    {
        var module = NativeMethods.GetModuleHandle(null);
        _mouse = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, module, 0);
        _keyboard = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyProc, module, 0);
        if (_mouse == IntPtr.Zero)
            throw new InvalidOperationException("Impossible d'installer le hook souris (code " + Marshal.GetLastWin32Error() + ").");
    }

    private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                var d = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                if (_acceptInjected || (d.flags & NativeMethods.LLMHF_INJECTED) == 0)
                {
                    switch ((int)wParam)
                    {
                        case NativeMethods.WM_LBUTTONDOWN: LeftDown?.Invoke(d.pt.X, d.pt.Y); break;
                        case NativeMethods.WM_LBUTTONUP: LeftUp?.Invoke(d.pt.X, d.pt.Y); break;
                        case NativeMethods.WM_RBUTTONDOWN: OtherButtonDown?.Invoke(); break;
                        case NativeMethods.WM_MOUSEWHEEL: Wheel?.Invoke(d.pt.X, d.pt.Y); break;
                    }
                }
            }
            catch { /* un hook ne doit jamais lever */ }
        }
        return NativeMethods.CallNextHookEx(_mouse, nCode, wParam, lParam);
    }

    private IntPtr KeyCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && ((int)wParam == NativeMethods.WM_KEYDOWN || (int)wParam == NativeMethods.WM_SYSKEYDOWN))
        {
            try
            {
                var d = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                if (_acceptInjected || (d.flags & NativeMethods.LLKHF_INJECTED) == 0) KeyDown?.Invoke(d.vkCode);
            }
            catch { }
        }
        return NativeMethods.CallNextHookEx(_keyboard, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_mouse != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_mouse); _mouse = IntPtr.Zero; }
        if (_keyboard != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_keyboard); _keyboard = IntPtr.Zero; }
    }
}
