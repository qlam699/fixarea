using System.Runtime.InteropServices;
using FixArea.Core.Models;
using FixArea.Core.Native;

namespace FixArea.Core.Services;

public sealed class AppBarService : IDisposable
{
    const int EdgeTolerance = 8;
    const double CoverageRatio = 0.9;
    const int CallbackMsg = NativeConstants.WM_APP + 41;

    readonly Dictionary<Guid, Slot> _slots = [];

    public IEnumerable<IntPtr> Handles => _slots.Values.Select(s => s.Window.Handle);

    public void Sync(AppConfig config)
    {
        var wanted = new Dictionary<Guid, (LockedRegion Region, uint Edge, DisplayMonitor Monitor)>();
        if (config.EnableAppBarOnEdges)
        {
            var monitors = MonitorService.GetMonitors();
            foreach (var region in config.Regions.Where(r => r.Enabled && r.UseAppBarIfEdge))
            {
                var monitor = monitors.FirstOrDefault(m =>
                    string.Equals(m.DeviceName, region.MonitorDeviceName, StringComparison.OrdinalIgnoreCase));
                if (monitor is null)
                    continue;
                var edge = DetectEdge(region.Bounds, monitor.Bounds);
                if (edge is null)
                    continue;
                wanted[region.Id] = (region, edge.Value, monitor);
            }
        }

        foreach (var id in _slots.Keys.Except(wanted.Keys).ToList())
            Remove(id);

        foreach (var (id, entry) in wanted)
            Upsert(id, entry.Region, entry.Edge, entry.Monitor);
    }

    public static uint? DetectEdge(ScreenRect region, ScreenRect monitor)
    {
        var coversHeight = region.Height >= monitor.Height * CoverageRatio;
        var coversWidth = region.Width >= monitor.Width * CoverageRatio;

        if (coversHeight && Math.Abs(region.Left - monitor.Left) <= EdgeTolerance)
            return NativeConstants.ABE_LEFT;
        if (coversHeight && Math.Abs(region.Right - monitor.Right) <= EdgeTolerance)
            return NativeConstants.ABE_RIGHT;
        if (coversWidth && Math.Abs(region.Top - monitor.Top) <= EdgeTolerance)
            return NativeConstants.ABE_TOP;
        if (coversWidth && Math.Abs(region.Bottom - monitor.Bottom) <= EdgeTolerance)
            return NativeConstants.ABE_BOTTOM;
        return null;
    }

    void Upsert(Guid id, LockedRegion region, uint edge, DisplayMonitor monitor)
    {
        if (_slots.TryGetValue(id, out var existing))
        {
            SetPos(existing, region, edge, monitor);
            return;
        }

        var window = new NativeWindow(
            "FixAreaAppBar",
            NativeConstants.WS_EX_TOOLWINDOW
            | NativeConstants.WS_EX_NOACTIVATE
            | NativeConstants.WS_EX_LAYERED
            | NativeConstants.WS_EX_TRANSPARENT
            | NativeConstants.WS_EX_TOPMOST,
            NativeConstants.WS_POPUP,
            region.Bounds,
            "FixArea AppBar");

        var slot = new Slot { Window = window, Registered = false };
        window.Message += (msg, wParam, lParam) =>
        {
            if (msg == CallbackMsg)
                return IntPtr.Zero;
            return null;
        };
        _slots[id] = slot;

        var abd = NewData(window.Handle, edge, region.Bounds);
        NativeMethods.SHAppBarMessage(NativeConstants.ABM_NEW, ref abd);
        slot.Registered = true;
        SetPos(slot, region, edge, monitor);
    }

    void SetPos(Slot slot, LockedRegion region, uint edge, DisplayMonitor monitor)
    {
        var abd = NewData(slot.Window.Handle, edge, region.Bounds);
        NativeMethods.SHAppBarMessage(NativeConstants.ABM_QUERYPOS, ref abd);
        NativeMethods.SHAppBarMessage(NativeConstants.ABM_SETPOS, ref abd);
        NativeMethods.SetWindowPos(
            slot.Window.Handle,
            NativeConstants.HWND_TOPMOST,
            abd.rc.Left,
            abd.rc.Top,
            Math.Max(1, abd.rc.Width),
            Math.Max(1, abd.rc.Height),
            NativeConstants.SWP_NOACTIVATE | NativeConstants.SWP_SHOWWINDOW);

        using var bmp = LayeredSurface.Create(Math.Max(1, abd.rc.Width), Math.Max(1, abd.rc.Height));
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.FromArgb(1, 0, 0, 0));
        LayeredSurface.Present(slot.Window.Handle, abd.rc.Left, abd.rc.Top, bmp);
        _ = monitor;
    }

    void Remove(Guid id)
    {
        if (!_slots.Remove(id, out var slot))
            return;
        if (slot.Registered)
        {
            var abd = NewData(slot.Window.Handle, 0, default);
            NativeMethods.SHAppBarMessage(NativeConstants.ABM_REMOVE, ref abd);
        }
        slot.Window.Dispose();
    }

    static APPBARDATA NewData(IntPtr hwnd, uint edge, ScreenRect rect)
    {
        return new APPBARDATA
        {
            cbSize = Marshal.SizeOf<APPBARDATA>(),
            hWnd = hwnd,
            uCallbackMessage = (uint)CallbackMsg,
            uEdge = edge,
            rc = RECT.FromXYWH(rect.X, rect.Y, rect.Width, rect.Height)
        };
    }

    public void Dispose()
    {
        foreach (var id in _slots.Keys.ToList())
            Remove(id);
    }

    sealed class Slot
    {
        public required NativeWindow Window { get; init; }
        public bool Registered { get; set; }
    }
}
