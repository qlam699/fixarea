namespace FixArea.Core.Models;

public sealed class AppConfig
{
    public List<LockedRegion> Regions { get; set; } = [];
    public CollisionMode CollisionMode { get; set; } = CollisionMode.Resize;
    public bool AlwaysOnTopDefault { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool EnableAppBarOnEdges { get; set; } = true;
    public bool Paused { get; set; }
    public int DebounceMs { get; set; } = 200;
    public int MinWindowWidth { get; set; } = 200;
    public int MinWindowHeight { get; set; } = 150;
    public List<string> IgnoredProcesses { get; set; } =
    [
        "explorer.exe",
        "SearchHost.exe",
        "ShellExperienceHost.exe"
    ];
}
