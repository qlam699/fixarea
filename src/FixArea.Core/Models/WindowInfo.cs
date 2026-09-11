namespace FixArea.Core.Models;

public sealed class WindowInfo
{
    public IntPtr Hwnd { get; init; }
    public string Title { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string ProcessName { get; init; } = "";
    public uint ProcessId { get; init; }
    public ScreenRect Bounds { get; init; }
    public bool IsMaximized { get; init; }
    public bool IsMinimized { get; init; }

    public override string ToString() =>
        string.IsNullOrWhiteSpace(Title)
            ? $"{ProcessName} ({Hwnd.ToInt64():X})"
            : $"{Title} — {ProcessName}";
}
