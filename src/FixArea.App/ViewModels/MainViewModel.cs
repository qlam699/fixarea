using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixArea.Core.Services;

namespace FixArea.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    readonly LockerOrchestrator _locker;

    [ObservableProperty] bool startWithWindows;
    [ObservableProperty] bool hasRegion;
    [ObservableProperty] string regionStatus = "No locked region";

    public MainViewModel(LockerOrchestrator locker)
    {
        _locker = locker;
        _locker.ConfigChanged += () => App.MainWnd?.DispatcherQueue.TryEnqueue(Refresh);
        Refresh();
    }

    bool _loading;

    public void Refresh()
    {
        _loading = true;
        try
        {
            var cfg = _locker.Store.Config;
            StartWithWindows = cfg.StartWithWindows;
            var region = cfg.Regions.FirstOrDefault();
            HasRegion = region is not null;
            RegionStatus = region is null
                ? "No locked region"
                : $"Locked: {region.Bounds}";
        }
        finally
        {
            _loading = false;
        }
    }

    [RelayCommand]
    void CreateRegion() => App.BeginSelection();

    [RelayCommand]
    void DeleteRegion()
    {
        _locker.DeleteLockedRegion();
        Refresh();
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading)
            return;
        var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "FixArea.App.exe");
        _locker.SetStartWithWindows(value, exe);
    }
}
