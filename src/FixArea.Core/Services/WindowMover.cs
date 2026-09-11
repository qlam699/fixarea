using FixArea.Core.Models;
using FixArea.Core.Native;

namespace FixArea.Core.Services;

public static class WindowMover
{
    public static bool Apply(IntPtr hwnd, ScreenRect rect, bool restoreIfMaximized, bool topmost = false)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd) || rect.IsEmpty)
            return false;

        if (restoreIfMaximized && NativeMethods.IsZoomed(hwnd))
            NativeMethods.ShowWindow(hwnd, NativeConstants.SW_RESTORE);

        var insertAfter = topmost ? NativeConstants.HWND_TOPMOST : IntPtr.Zero;
        var flags = NativeConstants.SWP_NOACTIVATE | (topmost ? 0 : NativeConstants.SWP_NOZORDER);
        return NativeMethods.SetWindowPos(hwnd, insertAfter, rect.X, rect.Y, rect.Width, rect.Height, flags);
    }

    public static void ClearTopmost(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
            return;
        NativeMethods.SetWindowPos(
            hwnd,
            NativeConstants.HWND_NOTOPMOST,
            0, 0, 0, 0,
            NativeConstants.SWP_NOMOVE | NativeConstants.SWP_NOSIZE | NativeConstants.SWP_NOACTIVATE);
    }
}
