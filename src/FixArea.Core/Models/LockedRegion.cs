namespace FixArea.Core.Models;

public sealed class LockedRegion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Region";
    public bool Enabled { get; set; } = true;
    public string MonitorDeviceName { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool UseAppBarIfEdge { get; set; } = true;
    public string? PinnedProcessName { get; set; }
    public string? PinnedWindowTitle { get; set; }

    public ScreenRect Bounds
    {
        get => new(X, Y, Width, Height);
        set
        {
            X = value.X;
            Y = value.Y;
            Width = value.Width;
            Height = value.Height;
        }
    }
}
