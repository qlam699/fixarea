using System.Runtime.InteropServices;
using FixArea.Core.Models;
using FixArea.Core.Native;

namespace FixArea.Core.Services;

internal sealed class NativeWindow : IDisposable
{
    static int _counter;
    readonly WndProc _wndProc;
    readonly string _className;
    readonly IntPtr _hInstance;
    bool _classRegistered;

    public IntPtr Handle { get; private set; }

    public event Func<uint, IntPtr, IntPtr, IntPtr?>? Message;

    public NativeWindow(string classPrefix, uint exStyle, uint style, ScreenRect bounds, string title, IntPtr? cursor = null)
    {
        _hInstance = NativeMethods.GetModuleHandle(null);
        _className = classPrefix + "-" + Interlocked.Increment(ref _counter);
        _wndProc = WndProcCore;

        var wc = new WNDCLASSEXW
        {
            cbSize = Marshal.SizeOf<WNDCLASSEXW>(),
            style = NativeConstants.CS_HREDRAW | NativeConstants.CS_VREDRAW,
            lpfnWndProc = _wndProc,
            hInstance = _hInstance,
            hCursor = cursor ?? NativeMethods.LoadCursor(IntPtr.Zero, NativeMethods.IDC_ARROW),
            lpszClassName = _className
        };

        if (NativeMethods.RegisterClassEx(ref wc) == 0)
            throw new InvalidOperationException("RegisterClassEx failed: " + Marshal.GetLastWin32Error());
        _classRegistered = true;

        Handle = NativeMethods.CreateWindowEx(
            exStyle,
            _className,
            title,
            style,
            bounds.X,
            bounds.Y,
            Math.Max(1, bounds.Width),
            Math.Max(1, bounds.Height),
            IntPtr.Zero,
            IntPtr.Zero,
            _hInstance,
            IntPtr.Zero);

        if (Handle == IntPtr.Zero)
            throw new InvalidOperationException("CreateWindowEx failed: " + Marshal.GetLastWin32Error());

        GC.KeepAlive(_wndProc);
    }

    IntPtr WndProcCore(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        var handled = Message?.Invoke(msg, wParam, lParam);
        if (handled.HasValue)
            return handled.Value;
        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(Handle);
            Handle = IntPtr.Zero;
        }

        if (_classRegistered)
        {
            NativeMethods.UnregisterClass(_className, _hInstance);
            _classRegistered = false;
        }
    }
}
