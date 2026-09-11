using System.Text;
using FixArea.Core.Models;
using FixArea.Core.Native;

namespace FixArea.Core.Services;

public static class WindowEnumerator
{
    static readonly HashSet<string> SystemClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow",
        "DV2ControlHost",
        "MsgrIMEWindowClass",
        "SysShadow",
        "Button",
        "ImmersiveLauncher",
        "ImmersiveBackgroundWindow",
        "SearchPane",
        "Windows.Internal.Shell.TabProxyWindow",
        "TaskListOverlayWnd",
        "TaskListThumbnailWnd",
        "ForegroundStaging",
        "MultitaskingViewFrame",
        "XamlExplorerHostIslandWindow",
        "Windows.UI.Composition.DesktopWindowContentBridge",
        "PseudoConsoleWindow"
    };

    public static IReadOnlyList<WindowInfo> GetTopLevelWindows()
    {
        var result = new List<WindowInfo>();
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            var info = TryDescribe(hwnd);
            if (info is not null)
                result.Add(info);
            return true;
        }, IntPtr.Zero);
        return result;
    }

    public static WindowInfo? TryDescribe(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
            return null;
        if (!NativeMethods.GetWindowRect(hwnd, out var rc))
            return null;

        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        return new WindowInfo
        {
            Hwnd = hwnd,
            Title = GetTitle(hwnd),
            ClassName = GetClassName(hwnd),
            ProcessName = GetProcessName(pid),
            ProcessId = pid,
            Bounds = new ScreenRect(rc.Left, rc.Top, rc.Width, rc.Height),
            IsMaximized = NativeMethods.IsZoomed(hwnd),
            IsMinimized = NativeMethods.IsIconic(hwnd)
        };
    }

    public static bool IsSystemClass(string className) =>
        SystemClasses.Contains(className);

    public static string GetTitle(IntPtr hwnd)
    {
        var len = NativeMethods.GetWindowTextLength(hwnd);
        if (len <= 0)
            return "";
        var sb = new StringBuilder(len + 2);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string GetClassName(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string GetProcessName(uint pid)
    {
        if (pid == 0)
            return "";
        var handle = NativeMethods.OpenProcess(NativeConstants.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero)
            return "";
        try
        {
            var sb = new StringBuilder(1024);
            var size = sb.Capacity;
            if (!NativeMethods.QueryFullProcessImageName(handle, 0, sb, ref size))
                return "";
            return Path.GetFileName(sb.ToString());
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    public static bool IsCloaked(IntPtr hwnd)
    {
        if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeConstants.DWMWA_CLOAKED, out var cloaked, sizeof(int)) != 0)
            return false;
        return cloaked != 0;
    }
}
