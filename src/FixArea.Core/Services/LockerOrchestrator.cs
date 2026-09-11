using FixArea.Core.Models;
using FixArea.Core.Native;

namespace FixArea.Core.Services;

public sealed class LockerOrchestrator : IDisposable
{
    readonly WindowFilter _filter = new();
    readonly WindowWatcher _watcher = new();
    readonly PinService _pins = new();
    readonly AppBarService _appBars = new();
    readonly RegionIndicatorOverlay _indicators = new();
    readonly RegionSelectionOverlay _selection = new();
    readonly NativeMessageHost _host = new();
    readonly object _applyGate = new();
    bool _started;

    public RegionStore Store { get; }
    public PinService Pins => _pins;

    public event Action? ConfigChanged;
    public event Action<ScreenRect>? RegionDrawn;
    public event Action? SelectionCancelled;
    public event Action? HotkeyPressed;
    public event Action? PinFocusedHotkey;
    public event Action? DeleteRegionHotkey;

    public LockerOrchestrator(RegionStore? store = null)
    {
        Store = store ?? new RegionStore();
        Store.Changed += OnStoreChanged;
        _selection.Completed += rect => RegionDrawn?.Invoke(rect);
        _selection.Cancelled += () => SelectionCancelled?.Invoke();
        _host.CreateRegionHotkey += () => HotkeyPressed?.Invoke();
        _host.PinFocusedHotkey += () => PinFocusedHotkey?.Invoke();
        _host.DeleteRegionHotkey += () => DeleteRegionHotkey?.Invoke();
        _host.DisplayChanged += OnDisplayChanged;
        _watcher.WindowChanged += hwnd => HandleWindow(hwnd);
        _watcher.UserMoveEnded += HandleUserDrop;
        _watcher.DragMoved += HandleDragMoved;
    }

    public bool IsPaused => Store.Config.Paused;
    public bool IsSelecting => _selection.IsVisible;

    public void Start()
    {
        if (_started)
            return;
        _started = true;
        _host.Start();
        _filter.RegisterOwnWindow(_host.Handle);
        _watcher.DebounceMs = Store.Config.DebounceMs;
        _watcher.Start();
        RefreshChrome();
        EnforceAll();
    }

    public void RegisterUiWindow(IntPtr hwnd) => _filter.RegisterOwnWindow(hwnd);

    public void BeginRegionSelection()
    {
        if (_selection.IsVisible)
            return;
        _selection.Show();
        if (_selection.Handle != IntPtr.Zero)
            _filter.RegisterOwnWindow(_selection.Handle);
    }

    public void CancelSelection() => _selection.Hide();

    public LockedRegion CreateRegion(ScreenRect bounds, bool alwaysOnTop, WindowInfo? pinWindow)
    {
        var monitor = MonitorService.FromRect(bounds) ?? MonitorService.GetMonitors().FirstOrDefault();
        if (monitor is null)
            throw new InvalidOperationException("No monitor available.");

        bounds = bounds.ClampTo(monitor.Bounds);
        var region = new LockedRegion
        {
            Name = "Locked region",
            Enabled = true,
            MonitorDeviceName = monitor.DeviceName,
            Bounds = bounds,
            AlwaysOnTop = true,
            UseAppBarIfEdge = Store.Config.EnableAppBarOnEdges
        };

        if (pinWindow is not null)
            _pins.Pin(region, pinWindow);

        Store.Update(cfg =>
        {
            foreach (var existing in cfg.Regions.ToList())
                _pins.Unpin(existing);
            cfg.Regions.Clear();
            cfg.CollisionMode = CollisionMode.Resize;
            cfg.AlwaysOnTopDefault = true;
            cfg.Regions.Add(region);
        });
        EnforceAll();
        return region;
    }

    public void DeleteLockedRegion()
    {
        Store.Update(cfg =>
        {
            foreach (var region in cfg.Regions.ToList())
                _pins.Unpin(region);
            cfg.Regions.Clear();
        });
        RefreshChrome();
        EnforceAll();
    }

    public void DeleteRegion(Guid id)
    {
        Store.Update(cfg =>
        {
            var region = cfg.Regions.FirstOrDefault(r => r.Id == id);
            if (region is not null)
            {
                _pins.Unpin(region);
                cfg.Regions.Remove(region);
            }
        });
        RefreshChrome();
        EnforceAll();
    }

    public void SetPaused(bool paused)
    {
        Store.Update(cfg => cfg.Paused = paused);
        if (!paused)
            EnforceAll();
    }

    public void SetCollisionMode(CollisionMode mode) =>
        Store.Update(cfg => cfg.CollisionMode = mode);

    public void SetAlwaysOnTopDefault(bool value) =>
        Store.Update(cfg => cfg.AlwaysOnTopDefault = value);

    public void SetEnableAppBar(bool value) =>
        Store.Update(cfg => cfg.EnableAppBarOnEdges = value);

    public void SetStartWithWindows(bool value, string executablePath)
    {
        Store.Update(cfg => cfg.StartWithWindows = value);
        AutostartService.Apply(value, executablePath);
    }

    public void SetRegionEnabled(Guid id, bool enabled)
    {
        Store.Update(cfg =>
        {
            var region = cfg.Regions.FirstOrDefault(r => r.Id == id);
            if (region is not null)
                region.Enabled = enabled;
        });
        EnforceAll();
    }

    public void AddIgnoredProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return;
        Store.Update(cfg =>
        {
            if (!cfg.IgnoredProcesses.Any(p => string.Equals(p, processName, StringComparison.OrdinalIgnoreCase)))
                cfg.IgnoredProcesses.Add(processName.Trim());
        });
    }

    public void RemoveIgnoredProcess(string processName)
    {
        Store.Update(cfg =>
            cfg.IgnoredProcesses.RemoveAll(p => string.Equals(p, processName, StringComparison.OrdinalIgnoreCase)));
    }

    public IReadOnlyList<WindowInfo> ListPinnableWindows()
    {
        var config = Store.Config;
        return WindowEnumerator.GetTopLevelWindows()
            .Where(w => _filter.ShouldManage(w.Hwnd, config, Array.Empty<IntPtr>()) && !string.IsNullOrWhiteSpace(w.Title))
            .OrderBy(w => w.Title)
            .ToList();
    }

    public void PinExisting(Guid regionId, WindowInfo window) => TryPinWindow(regionId, window);

    public bool PinFocusedWindow()
    {
        var region = Store.Config.Regions.FirstOrDefault(r => r.Enabled);
        if (region is null)
            return false;

        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return false;

        var root = NativeMethods.GetAncestor(hwnd, NativeConstants.GA_ROOT);
        if (root != IntPtr.Zero)
            hwnd = root;

        var info = WindowEnumerator.TryDescribe(hwnd);
        if (info is null || info.IsMinimized)
            return false;
        if (!_filter.ShouldManage(hwnd, Store.Config, Array.Empty<IntPtr>()))
            return false;

        return TryPinWindow(region.Id, info);
    }

    public void UnpinRegion(Guid id)
    {
        Store.Update(cfg =>
        {
            var region = cfg.Regions.FirstOrDefault(r => r.Id == id);
            if (region is not null)
                _pins.Unpin(region);
        });
        EnforceAll();
    }

    public bool TryPinWindow(Guid regionId, WindowInfo window)
    {
        var displaced = IntPtr.Zero;
        var pinned = false;
        Store.Update(cfg =>
        {
            foreach (var other in cfg.Regions.Where(r => r.Id != regionId))
            {
                if (_pins.RegionIdFor(window.Hwnd) == other.Id)
                    _pins.Unpin(other);
            }

            var region = cfg.Regions.FirstOrDefault(r => r.Id == regionId);
            if (region is null)
                return;

            displaced = _pins.Pin(region, window);
            pinned = true;
        });

        if (pinned)
            _watcher.MarkSelfMoved(window.Hwnd);

        if (displaced != IntPtr.Zero && displaced != window.Hwnd)
            HandleWindow(displaced, forcePush: true);

        return pinned;
    }

    public void EnforceAll()
    {
        if (Store.Config.Paused)
            return;

        _pins.SyncFromConfig(Store.Config.Regions);
        foreach (var region in Store.Config.Regions.Where(r => r.Enabled))
        {
            _pins.Enforce(region);
            if (_pins.Pins.TryGetValue(region.Id, out var hwnd))
                _watcher.MarkSelfMoved(hwnd);
        }

        foreach (var window in WindowEnumerator.GetTopLevelWindows())
            HandleWindow(window.Hwnd);
    }

    void HandleDragMoved(IntPtr hwnd)
    {
        if (!_started)
            return;
        var info = WindowEnumerator.TryDescribe(hwnd);
        if (info is null)
            return;

        NativeMethods.GetCursorPos(out var cursor);
        var target = CollisionEngine.FindDropTarget(cursor.X, cursor.Y, info.Bounds, Store.Config.Regions);
        _indicators.SetHover(target?.Id);
    }

    void HandleUserDrop(IntPtr hwnd)
    {
        if (!_started)
            return;
        if (_watcher.WasSelfMoved(hwnd))
        {
            _indicators.SetHover(null);
            return;
        }

        var shouldPush = false;
        NativeMethods.GetCursorPos(out var cursor);

        lock (_applyGate)
        {
            var config = Store.Config;
            var info = WindowEnumerator.TryDescribe(hwnd);
            if (info is null || info.IsMinimized)
            {
                _indicators.SetHover(null);
                return;
            }

            var target = CollisionEngine.FindDropTarget(cursor.X, cursor.Y, info.Bounds, config.Regions);
            var currentPin = _pins.RegionIdFor(hwnd);

            if (currentPin is Guid pinnedTo)
            {
                var region = config.Regions.FirstOrDefault(r => r.Id == pinnedTo && r.Enabled);
                if (region is null)
                {
                    _indicators.SetHover(null);
                    return;
                }

                if (target is not null && target.Id != pinnedTo)
                {
                    TryPinWindow(target.Id, info);
                    _indicators.SetHover(null);
                    return;
                }

                if (target is not null || !CollisionEngine.IsMostlyOutside(cursor.X, cursor.Y, info.Bounds, region.Bounds))
                {
                    if (!info.Bounds.ApproximatelyEquals(region.Bounds))
                    {
                        _watcher.MarkSelfMoved(hwnd);
                        _pins.Enforce(region);
                    }
                    _indicators.SetHover(null);
                    return;
                }

                Store.Update(cfg =>
                {
                    var current = cfg.Regions.FirstOrDefault(r => r.Id == pinnedTo);
                    if (current is not null)
                        _pins.Unpin(current);
                });
                shouldPush = true;
            }
            else
            {
                if (!_filter.ShouldManage(hwnd, config, _pins.PinnedHwnds))
                {
                    _indicators.SetHover(null);
                    return;
                }

                if (target is not null)
                {
                    TryPinWindow(target.Id, info);
                    _indicators.SetHover(null);
                    return;
                }

                shouldPush = true;
            }
        }

        _indicators.SetHover(null);
        if (shouldPush)
            HandleWindow(hwnd, forcePush: true);
    }

    void HandleWindow(IntPtr hwnd, bool forcePush = false)
    {
        if (!_started)
            return;
        if (!forcePush && Store.Config.Paused)
            return;
        if (_watcher.WasSelfMoved(hwnd))
            return;

        lock (_applyGate)
        {
            var config = Store.Config;
            var pinId = _pins.RegionIdFor(hwnd);
            if (pinId is Guid)
                return;

            if (!_filter.ShouldManage(hwnd, config, _pins.PinnedHwnds))
                return;

            var info = WindowEnumerator.TryDescribe(hwnd);
            if (info is null)
                return;

            var monitor = MonitorService.FromWindow(hwnd);
            if (monitor is null)
                return;

            var locked = config.Regions
                .Where(r => r.Enabled
                            && string.Equals(r.MonitorDeviceName, monitor.DeviceName, StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Bounds)
                .ToList();
            if (locked.Count == 0)
                return;

            var next = CollisionEngine.Resolve(
                info.Bounds,
                info.IsMaximized,
                monitor.WorkArea,
                locked,
                forcePush ? CollisionMode.MoveThenResize : config.CollisionMode);

            if (next is ScreenRect rect && rect != info.Bounds)
            {
                _watcher.MarkSelfMoved(hwnd);
                WindowMover.Apply(hwnd, rect, restoreIfMaximized: info.IsMaximized);
            }
        }
    }

    void OnStoreChanged()
    {
        _watcher.DebounceMs = Store.Config.DebounceMs;
        RefreshChrome();
        ConfigChanged?.Invoke();
    }

    void OnDisplayChanged()
    {
        Store.Update(cfg =>
        {
            var monitors = MonitorService.GetMonitors();
            foreach (var region in cfg.Regions)
            {
                var monitor = monitors.FirstOrDefault(m =>
                    string.Equals(m.DeviceName, region.MonitorDeviceName, StringComparison.OrdinalIgnoreCase));
                if (monitor is null)
                {
                    region.Enabled = false;
                    continue;
                }

                var clamped = region.Bounds.ClampTo(monitor.Bounds);
                if (clamped.Width < 32 || clamped.Height < 32)
                    region.Enabled = false;
                else
                    region.Bounds = clamped;
            }
        });
        EnforceAll();
    }

    void RefreshChrome()
    {
        var config = Store.Config;
        _pins.SyncFromConfig(config.Regions);
        _appBars.Sync(config);
        foreach (var hwnd in _appBars.Handles)
            _filter.RegisterOwnWindow(hwnd);
        _indicators.Sync(config.Regions);
        foreach (var hwnd in _indicators.Handles)
            _filter.RegisterOwnWindow(hwnd);
    }

    public void Dispose()
    {
        Store.Changed -= OnStoreChanged;
        _selection.Dispose();
        _indicators.Dispose();
        _appBars.Dispose();
        _watcher.Dispose();
        _host.Dispose();
    }
}
