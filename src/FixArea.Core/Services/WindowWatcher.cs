using System.Collections.Concurrent;
using FixArea.Core.Models;
using FixArea.Core.Native;

namespace FixArea.Core.Services;

public sealed class WindowWatcher : IDisposable
{
    readonly ConcurrentDictionary<IntPtr, byte> _userMoving = new();
    readonly ConcurrentDictionary<IntPtr, long> _selfMovedUntil = new();
    readonly ConcurrentDictionary<IntPtr, long> _lastEvent = new();
    readonly HashSet<IntPtr> _pending = [];
    readonly object _pendingGate = new();
    WinEventDelegate? _callback;
    IntPtr _hookShow;
    IntPtr _hookFg;
    IntPtr _hookMove;
    IntPtr _hookLocation;
    Thread? _thread;
    Timer? _timer;
    uint _threadId;
    volatile bool _running;

    public int DebounceMs { get; set; } = 200;
    public event Action<IntPtr>? WindowChanged;
    public event Action<IntPtr>? UserMoveEnded;
    public event Action<IntPtr>? DragMoved;

    public void Start()
    {
        if (_thread is not null)
            return;

        _running = true;
        _thread = new Thread(ThreadProc)
        {
            IsBackground = true,
            Name = "FixArea.WindowWatcher",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        if (_threadId != 0)
            NativeMethods.PostThreadMessage(_threadId, NativeConstants.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread?.Join(2000);
        _thread = null;
        _threadId = 0;
    }

    public void MarkSelfMoved(IntPtr hwnd)
    {
        _selfMovedUntil[hwnd] = Environment.TickCount64 + Math.Max(250, DebounceMs * 2);
    }

    public bool WasSelfMoved(IntPtr hwnd)
    {
        if (_selfMovedUntil.TryGetValue(hwnd, out var until) && Environment.TickCount64 < until)
            return true;
        _selfMovedUntil.TryRemove(hwnd, out _);
        return false;
    }

    void ThreadProc()
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        _callback = OnWinEvent;
        const uint flags = NativeConstants.WINEVENT_OUTOFCONTEXT | NativeConstants.WINEVENT_SKIPOWNPROCESS;

        _hookFg = NativeMethods.SetWinEventHook(
            NativeConstants.EVENT_SYSTEM_FOREGROUND, NativeConstants.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _callback, 0, 0, flags);
        _hookMove = NativeMethods.SetWinEventHook(
            NativeConstants.EVENT_SYSTEM_MOVESIZESTART, NativeConstants.EVENT_SYSTEM_MINIMIZEEND,
            IntPtr.Zero, _callback, 0, 0, flags);
        _hookShow = NativeMethods.SetWinEventHook(
            NativeConstants.EVENT_OBJECT_SHOW, NativeConstants.EVENT_OBJECT_HIDE,
            IntPtr.Zero, _callback, 0, 0, flags);
        _hookLocation = NativeMethods.SetWinEventHook(
            NativeConstants.EVENT_OBJECT_LOCATIONCHANGE, NativeConstants.EVENT_OBJECT_UNCLOAKED,
            IntPtr.Zero, _callback, 0, 0, flags);

        GC.KeepAlive(_callback);
        _timer = new Timer(_ => FlushPending(), null, DebounceMs, DebounceMs);

        while (_running)
        {
            var ret = NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0);
            if (ret <= 0)
                break;
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }

        _timer?.Dispose();
        _timer = null;
        if (_hookFg != IntPtr.Zero) NativeMethods.UnhookWinEvent(_hookFg);
        if (_hookMove != IntPtr.Zero) NativeMethods.UnhookWinEvent(_hookMove);
        if (_hookShow != IntPtr.Zero) NativeMethods.UnhookWinEvent(_hookShow);
        if (_hookLocation != IntPtr.Zero) NativeMethods.UnhookWinEvent(_hookLocation);
        _hookFg = _hookMove = _hookShow = _hookLocation = IntPtr.Zero;
    }

    void OnWinEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (hwnd == IntPtr.Zero || idObject != NativeConstants.OBJID_WINDOW || idChild != NativeConstants.CHILDID_SELF)
            return;

        switch (eventType)
        {
            case NativeConstants.EVENT_SYSTEM_MOVESIZESTART:
                _userMoving[hwnd] = 1;
                DragMoved?.Invoke(hwnd);
                return;
            case NativeConstants.EVENT_SYSTEM_MOVESIZEEND:
                _userMoving.TryRemove(hwnd, out _);
                UserMoveEnded?.Invoke(hwnd);
                return;
            case NativeConstants.EVENT_OBJECT_HIDE:
                _userMoving.TryRemove(hwnd, out _);
                return;
        }

        if (eventType == NativeConstants.EVENT_OBJECT_LOCATIONCHANGE && IsLeftMouseDown())
        {
            _userMoving[hwnd] = 1;
            DragMoved?.Invoke(hwnd);
            return;
        }

        if (_userMoving.ContainsKey(hwnd))
        {
            if (!IsLeftMouseDown())
            {
                _userMoving.TryRemove(hwnd, out _);
                UserMoveEnded?.Invoke(hwnd);
            }
            return;
        }

        if (WasSelfMoved(hwnd))
            return;

        Queue(hwnd);
    }

    static bool IsLeftMouseDown() =>
        (NativeMethods.GetAsyncKeyState(NativeConstants.VK_LBUTTON) & 0x8000) != 0;

    void Queue(IntPtr hwnd)
    {
        var now = Environment.TickCount64;
        if (_lastEvent.TryGetValue(hwnd, out var last) && now - last < DebounceMs)
        {
            lock (_pendingGate)
                _pending.Add(hwnd);
            return;
        }

        _lastEvent[hwnd] = now;
        WindowChanged?.Invoke(hwnd);
    }

    void FlushPending()
    {
        List<IntPtr> items;
        lock (_pendingGate)
        {
            items = _pending.ToList();
            _pending.Clear();
        }

        var now = Environment.TickCount64;
        foreach (var hwnd in items)
        {
            if (_lastEvent.TryGetValue(hwnd, out var last) && now - last < DebounceMs)
                continue;
            _lastEvent[hwnd] = now;
            WindowChanged?.Invoke(hwnd);
        }
    }

    public void Dispose() => Stop();
}
