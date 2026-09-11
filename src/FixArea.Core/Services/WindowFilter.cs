using FixArea.Core.Models;
using FixArea.Core.Native;

namespace FixArea.Core.Services;

public sealed class WindowFilter
{
    readonly HashSet<IntPtr> _ownWindows = [];
    readonly object _gate = new();

    public void RegisterOwnWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;
        lock (_gate)
            _ownWindows.Add(hwnd);
    }

    public void UnregisterOwnWindow(IntPtr hwnd)
    {
        lock (_gate)
            _ownWindows.Remove(hwnd);
    }

    public bool ShouldManage(IntPtr hwnd, AppConfig config, IReadOnlyCollection<IntPtr> pinnedHwnds)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
            return false;

        lock (_gate)
        {
            if (_ownWindows.Contains(hwnd))
                return false;
        }

        if (pinnedHwnds.Contains(hwnd))
            return false;

        var owner = NativeMethods.GetWindow(hwnd, NativeConstants.GW_OWNER);
        if (owner != IntPtr.Zero && pinnedHwnds.Contains(owner))
            return false;

        var root = NativeMethods.GetAncestor(hwnd, NativeConstants.GA_ROOT);
        if (root != hwnd && root != IntPtr.Zero)
        {
            if (pinnedHwnds.Contains(root))
                return false;
        }

        if (!NativeMethods.IsWindowVisible(hwnd) || NativeMethods.IsIconic(hwnd))
            return false;

        if (WindowEnumerator.IsCloaked(hwnd))
            return false;

        if (root != hwnd && root != IntPtr.Zero)
            return false;

        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeConstants.GWL_STYLE).ToInt64();
        var exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeConstants.GWL_EXSTYLE).ToInt64();

        if ((style & NativeConstants.WS_CHILD) != 0)
            return false;
        if ((exStyle & NativeConstants.WS_EX_TOOLWINDOW) != 0 && (exStyle & NativeConstants.WS_EX_APPWINDOW) == 0)
            return false;
        if ((exStyle & NativeConstants.WS_EX_NOACTIVATE) != 0)
            return false;

        var className = WindowEnumerator.GetClassName(hwnd);
        if (WindowEnumerator.IsSystemClass(className))
            return false;

        if (!NativeMethods.GetWindowRect(hwnd, out var rc))
            return false;

        var width = rc.Width;
        var height = rc.Height;
        if (width < config.MinWindowWidth || height < config.MinWindowHeight)
            return false;

        if (owner != IntPtr.Zero && (width < 400 || height < 250))
            return false;

        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        var processName = WindowEnumerator.GetProcessName(pid);
        if (IsSystemIgnored(processName))
            return false;

        var currentPid = (uint)Environment.ProcessId;
        if (pid == currentPid)
            return false;

        return true;
    }

    static readonly HashSet<string> SystemIgnored = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe",
        "SearchHost.exe",
        "ShellExperienceHost.exe"
    };

    static bool IsSystemIgnored(string processName) =>
        !string.IsNullOrWhiteSpace(processName) && SystemIgnored.Contains(processName);
}
