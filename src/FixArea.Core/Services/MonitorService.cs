using FixArea.Core.Models;
using FixArea.Core.Native;

namespace FixArea.Core.Services;

public static class MonitorService
{
    public static IReadOnlyList<DisplayMonitor> GetMonitors()
    {
        var list = new List<DisplayMonitor>();
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdc, ref RECT lprcMonitor, IntPtr data) =>
            {
                var info = new MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
                if (!NativeMethods.GetMonitorInfo(hMonitor, ref info))
                    return true;

                list.Add(new DisplayMonitor
                {
                    Handle = hMonitor,
                    DeviceName = info.szDevice?.Trim() ?? "",
                    Bounds = ToRect(info.rcMonitor),
                    WorkArea = ToRect(info.rcWork),
                    IsPrimary = (info.dwFlags & NativeConstants.MONITORINFOF_PRIMARY) != 0
                });
                return true;
            }, IntPtr.Zero);

        var ordered = list
            .OrderByDescending(m => m.IsPrimary)
            .ThenBy(m => m.Bounds.X)
            .ThenBy(m => m.Bounds.Y)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i] = new DisplayMonitor
            {
                Handle = ordered[i].Handle,
                DeviceName = ordered[i].DeviceName,
                Bounds = ordered[i].Bounds,
                WorkArea = ordered[i].WorkArea,
                IsPrimary = ordered[i].IsPrimary,
                Index = i + 1
            };
        }

        return ordered;
    }

    public static DisplayMonitor? FindByDeviceName(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return GetMonitors().FirstOrDefault(m => m.IsPrimary);
        return GetMonitors().FirstOrDefault(m =>
            string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
    }

    public static DisplayMonitor? FromRect(ScreenRect rect)
    {
        var native = new RECT(rect.Left, rect.Top, rect.Right, rect.Bottom);
        var handle = NativeMethods.MonitorFromRect(ref native, NativeConstants.MONITOR_DEFAULTTONEAREST);
        return GetMonitors().FirstOrDefault(m => m.Handle == handle)
               ?? GetMonitors().FirstOrDefault(m => m.Bounds.ContainsPoint(rect.CenterX, rect.CenterY));
    }

    public static DisplayMonitor? FromWindow(IntPtr hwnd)
    {
        var handle = NativeMethods.MonitorFromWindow(hwnd, NativeConstants.MONITOR_DEFAULTTONEAREST);
        return GetMonitors().FirstOrDefault(m => m.Handle == handle);
    }

    public static ScreenRect GetVirtualScreen()
    {
        var x = NativeMethods.GetSystemMetrics(NativeConstants.SM_XVIRTUALSCREEN);
        var y = NativeMethods.GetSystemMetrics(NativeConstants.SM_YVIRTUALSCREEN);
        var w = NativeMethods.GetSystemMetrics(NativeConstants.SM_CXVIRTUALSCREEN);
        var h = NativeMethods.GetSystemMetrics(NativeConstants.SM_CYVIRTUALSCREEN);
        return new ScreenRect(x, y, w, h);
    }

    public static int ScreenIndexOf(string deviceName)
    {
        var monitors = GetMonitors();
        var match = monitors.FirstOrDefault(m =>
            string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
        return match?.Index ?? 0;
    }

    static ScreenRect ToRect(RECT r) => new(r.Left, r.Top, r.Width, r.Height);
}
