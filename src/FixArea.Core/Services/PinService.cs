using FixArea.Core.Models;
using FixArea.Core.Native;

namespace FixArea.Core.Services;

public sealed class PinService
{
    readonly Dictionary<Guid, IntPtr> _pins = [];

    public IReadOnlyDictionary<Guid, IntPtr> Pins => _pins;

    public IReadOnlyCollection<IntPtr> PinnedHwnds => _pins.Values.Where(h => h != IntPtr.Zero).ToHashSet();

    public void SyncFromConfig(IEnumerable<LockedRegion> regions)
    {
        var regionList = regions.ToList();
        var live = WindowEnumerator.GetTopLevelWindows();

        foreach (var region in regionList)
        {
            if (string.IsNullOrWhiteSpace(region.PinnedProcessName))
            {
                _pins.Remove(region.Id);
                continue;
            }

            if (_pins.TryGetValue(region.Id, out var existing) && NativeMethods.IsWindow(existing))
                continue;

            var candidates = live
                .Where(w => !w.IsMinimized
                            && string.Equals(w.ProcessName, region.PinnedProcessName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var match = candidates.FirstOrDefault(w => w.Bounds.Intersects(region.Bounds))
                        ?? candidates.FirstOrDefault(w =>
                            !string.IsNullOrWhiteSpace(region.PinnedWindowTitle)
                            && string.Equals(w.Title, region.PinnedWindowTitle, StringComparison.Ordinal))
                        ?? candidates.FirstOrDefault();

            if (match is null)
                _pins.Remove(region.Id);
            else
                _pins[region.Id] = match.Hwnd;
        }

        foreach (var id in _pins.Keys.Except(regionList.Select(r => r.Id)).ToList())
            _pins.Remove(id);
    }

    public IntPtr Pin(LockedRegion region, WindowInfo window)
    {
        var displaced = IntPtr.Zero;
        if (_pins.TryGetValue(region.Id, out var previous) && previous != window.Hwnd)
        {
            WindowMover.ClearTopmost(previous);
            displaced = previous;
        }

        foreach (var pair in _pins.Where(p => p.Value == window.Hwnd && p.Key != region.Id).ToList())
            _pins.Remove(pair.Key);

        region.PinnedProcessName = window.ProcessName;
        region.PinnedWindowTitle = window.Title;
        _pins[region.Id] = window.Hwnd;
        WindowMover.Apply(window.Hwnd, region.Bounds, restoreIfMaximized: true, topmost: region.AlwaysOnTop);
        return displaced;
    }

    public void Unpin(LockedRegion region)
    {
        if (_pins.TryGetValue(region.Id, out var hwnd))
            WindowMover.ClearTopmost(hwnd);
        _pins.Remove(region.Id);
        region.PinnedProcessName = null;
        region.PinnedWindowTitle = null;
    }

    public void Enforce(LockedRegion region)
    {
        if (!_pins.TryGetValue(region.Id, out var hwnd) || hwnd == IntPtr.Zero)
            return;
        if (!NativeMethods.IsWindow(hwnd))
        {
            _pins.Remove(region.Id);
            return;
        }

        WindowMover.Apply(hwnd, region.Bounds, restoreIfMaximized: true, topmost: region.AlwaysOnTop);
    }

    public Guid? RegionIdFor(IntPtr hwnd)
    {
        foreach (var pair in _pins)
        {
            if (pair.Value == hwnd)
                return pair.Key;
        }

        return null;
    }
}
