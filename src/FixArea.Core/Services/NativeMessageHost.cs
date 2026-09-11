using FixArea.Core.Models;
using FixArea.Core.Native;

namespace FixArea.Core.Services;

public sealed class NativeMessageHost : IDisposable
{
    NativeWindow? _window;
    readonly HashSet<int> _hotkeys = [];
    bool _sessionRegistered;

    public IntPtr Handle => _window?.Handle ?? IntPtr.Zero;

    public event Action? CreateRegionHotkey;
    public event Action? PinFocusedHotkey;
    public event Action? DeleteRegionHotkey;
    public event Action? DisplayChanged;

    public void Start()
    {
        if (_window is not null)
            return;

        _window = new NativeWindow(
            "FixAreaHost",
            NativeConstants.WS_EX_TOOLWINDOW | NativeConstants.WS_EX_NOACTIVATE,
            NativeConstants.WS_POPUP,
            new ScreenRect(0, 0, 1, 1),
            "FixArea Host");
        NativeMethods.ShowWindow(_window.Handle, NativeConstants.SW_HIDE);
        _window.Message += OnMessage;

        TryRegister(NativeConstants.HOTKEY_CREATE, NativeConstants.VK_R);
        TryRegister(NativeConstants.HOTKEY_PIN, NativeConstants.VK_RETURN);
        TryRegister(NativeConstants.HOTKEY_DELETE, NativeConstants.VK_X);

        _sessionRegistered = NativeMethods.WTSRegisterSessionNotification(
            _window.Handle,
            NativeConstants.NOTIFY_FOR_THIS_SESSION);
    }

    void TryRegister(int id, uint vk)
    {
        if (_window is null)
            return;
        if (NativeMethods.RegisterHotKey(
                _window.Handle,
                id,
                NativeConstants.MOD_CONTROL | NativeConstants.MOD_ALT | NativeConstants.MOD_NOREPEAT,
                vk))
            _hotkeys.Add(id);
    }

    IntPtr? OnMessage(uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch ((int)msg)
        {
            case NativeConstants.WM_HOTKEY:
                switch (wParam.ToInt32())
                {
                    case NativeConstants.HOTKEY_CREATE:
                        CreateRegionHotkey?.Invoke();
                        break;
                    case NativeConstants.HOTKEY_PIN:
                        PinFocusedHotkey?.Invoke();
                        break;
                    case NativeConstants.HOTKEY_DELETE:
                        DeleteRegionHotkey?.Invoke();
                        break;
                }
                return IntPtr.Zero;
            case NativeConstants.WM_DISPLAYCHANGE:
            case NativeConstants.WM_SETTINGCHANGE:
            case NativeConstants.WM_POWERBROADCAST:
                DisplayChanged?.Invoke();
                return IntPtr.Zero;
            case NativeConstants.WM_WTSSESSION_CHANGE:
                var code = wParam.ToInt32();
                if (code == NativeConstants.WTS_SESSION_UNLOCK || code == NativeConstants.WTS_SESSION_LOGON)
                    DisplayChanged?.Invoke();
                return IntPtr.Zero;
        }

        return null;
    }

    public void Dispose()
    {
        if (_window is not null)
        {
            foreach (var id in _hotkeys)
                NativeMethods.UnregisterHotKey(_window.Handle, id);
            _hotkeys.Clear();
            if (_sessionRegistered)
                NativeMethods.WTSUnRegisterSessionNotification(_window.Handle);
            _window.Dispose();
            _window = null;
        }
    }
}
