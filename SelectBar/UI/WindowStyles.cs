using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using SelectBar.Input;

namespace SelectBar.UI;

internal static class WindowStyles
{
    /// <summary>WS_EX_NOACTIVATE | TOOLWINDOW | TOPMOST + refus d'activation sur clic : la fenêtre ne prend jamais le focus.</summary>
    public static void MakeNoActivate(Window w)
    {
        w.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            var ex = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
                ex | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TOPMOST);
            HwndSource.FromHwnd(hwnd)?.AddHook(NoActivateHook);
        };
    }

    private static IntPtr NoActivateHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_MOUSEACTIVATE) { handled = true; return new IntPtr(NativeMethods.MA_NOACTIVATE); }
        return IntPtr.Zero;
    }

    /// <summary>Place la fenêtre à des coordonnées écran en pixels, sans l'activer.</summary>
    public static void MoveTo(Window w, int x, int y)
    {
        var hwnd = new WindowInteropHelper(w).Handle;
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, x, y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }
}
