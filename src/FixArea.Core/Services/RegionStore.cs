using System.Text;
using System.Text.Json;
using FixArea.Core.Models;

namespace FixArea.Core.Services;

public sealed class RegionStore
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    readonly object _gate = new();
    readonly string _path;

    public AppConfig Config { get; private set; } = new();

    public event Action? Changed;

    public RegionStore(string? path = null)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FixArea");
        Directory.CreateDirectory(dir);
        _path = path ?? Path.Combine(dir, "config.json");
        Load();
    }

    public void Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                Config = new AppConfig();
                SaveUnlocked();
                return;
            }

            try
            {
                var json = File.ReadAllText(_path, Encoding.UTF8);
                Config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            }
            catch
            {
                Config = new AppConfig();
            }

            Normalize(Config);
        }
    }

    public void Save()
    {
        lock (_gate)
        {
            SaveUnlocked();
        }

        Changed?.Invoke();
    }

    public T Read<T>(Func<AppConfig, T> reader)
    {
        lock (_gate)
            return reader(Config);
    }

    public void Update(Action<AppConfig> mutator)
    {
        lock (_gate)
        {
            mutator(Config);
            Normalize(Config);
            SaveUnlocked();
        }

        Changed?.Invoke();
    }

    void SaveUnlocked()
    {
        var json = JsonSerializer.Serialize(Config, JsonOptions);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json, Encoding.UTF8);
        File.Copy(tmp, _path, overwrite: true);
        File.Delete(tmp);
    }

    static void Normalize(AppConfig config)
    {
        if (config.DebounceMs < 50)
            config.DebounceMs = 50;
        if (config.DebounceMs > 1000)
            config.DebounceMs = 1000;
        if (config.MinWindowWidth < 50)
            config.MinWindowWidth = 50;
        if (config.MinWindowHeight < 50)
            config.MinWindowHeight = 50;

        config.CollisionMode = CollisionMode.Resize;
        config.AlwaysOnTopDefault = true;
        if (config.Regions.Count > 1)
            config.Regions = [config.Regions[^1]];
        foreach (var region in config.Regions)
            region.AlwaysOnTop = true;
    }
}
