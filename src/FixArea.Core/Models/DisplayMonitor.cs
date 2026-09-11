namespace FixArea.Core.Models;

public sealed class DisplayMonitor
{
    public IntPtr Handle { get; init; }
    public string DeviceName { get; init; } = "";
    public ScreenRect Bounds { get; init; }
    public ScreenRect WorkArea { get; init; }
    public bool IsPrimary { get; init; }
    public int Index { get; init; }
}
