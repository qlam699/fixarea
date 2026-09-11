using System.Drawing;
using System.Drawing.Drawing2D;
using FixArea.Core.Models;
using FixArea.Core.Native;

namespace FixArea.Core.Services;

public sealed class RegionIndicatorOverlay : IDisposable
{
    readonly Dictionary<Guid, NativeWindow> _windows = [];
    readonly Dictionary<Guid, LockedRegion> _regions = [];
    Guid? _hoverId;

    public IEnumerable<IntPtr> Handles => _windows.Values.Select(w => w.Handle);

    public void Sync(IEnumerable<LockedRegion> regions)
    {
        _regions.Clear();
        var wanted = regions
            .Where(r => r.Enabled && r.Width > 0 && r.Height > 0)
            .ToDictionary(r => r.Id);
        foreach (var pair in wanted)
            _regions[pair.Key] = pair.Value;

        foreach (var id in _windows.Keys.Except(wanted.Keys).ToList())
            Remove(id);

        foreach (var region in wanted.Values)
            Upsert(region);
    }

    public void SetHover(Guid? regionId)
    {
        _hoverId = regionId;
    }

    public void Clear()
    {
        _hoverId = null;
        foreach (var id in _windows.Keys.ToList())
            Remove(id);
        _regions.Clear();
    }

    void Upsert(LockedRegion region)
    {
        if (_windows.TryGetValue(region.Id, out var existing))
        {
            NativeMethods.SetWindowPos(
                existing.Handle,
                NativeConstants.HWND_TOPMOST,
                region.X,
                region.Y,
                region.Width,
                region.Height,
                NativeConstants.SWP_NOACTIVATE | NativeConstants.SWP_SHOWWINDOW);
            Paint(existing.Handle, region);
            return;
        }

        var bounds = region.Bounds;
        var ex = NativeConstants.WS_EX_TOPMOST
                 | NativeConstants.WS_EX_TOOLWINDOW
                 | NativeConstants.WS_EX_LAYERED
                 | NativeConstants.WS_EX_TRANSPARENT
                 | NativeConstants.WS_EX_NOACTIVATE;
        var style = NativeConstants.WS_POPUP | NativeConstants.WS_VISIBLE;
        var window = new NativeWindow("FixAreaIndicator", ex, style, bounds, "FixArea Region");
        _windows[region.Id] = window;
        Paint(window.Handle, region);
    }

    void Paint(IntPtr hwnd, LockedRegion region)
    {
        using var bmp = LayeredSurface.Create(region.Width, region.Height);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.None;
        g.Clear(Color.Transparent);

        const int thickness = 2;
        using var pen = new Pen(Color.FromArgb(128, 255, 255, 255), thickness)
        {
            DashStyle = DashStyle.Dash,
            DashPattern = [8f, 6f],
            Alignment = PenAlignment.Inset
        };
        g.DrawRectangle(
            pen,
            1,
            1,
            Math.Max(0, region.Width - 2),
            Math.Max(0, region.Height - 2));

        LayeredSurface.Present(hwnd, region.X, region.Y, bmp);
    }

    void Remove(Guid id)
    {
        if (_windows.Remove(id, out var window))
            window.Dispose();
        _regions.Remove(id);
        if (_hoverId == id)
            _hoverId = null;
    }

    public void Dispose() => Clear();
}
