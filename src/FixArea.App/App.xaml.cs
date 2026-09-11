using FixArea.Core.Models;
using FixArea.Core.Services;
using Microsoft.UI.Xaml;

namespace FixArea.App;

public partial class App : Application
{
    public static LockerOrchestrator Locker { get; private set; } = null!;
    public static MainWindow? MainWnd { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => e.Handled = true;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Locker = new LockerOrchestrator();
        Locker.Start();
        AutostartService.Apply(
            Locker.Store.Config.StartWithWindows,
            Environment.ProcessPath ?? AppContext.BaseDirectory);

        MainWnd = new MainWindow();
        MainWnd.Activate();
        Locker.RegisterUiWindow(WinRT.Interop.WindowNative.GetWindowHandle(MainWnd));

        Locker.HotkeyPressed += () => Dispatch(BeginSelection);
        Locker.PinFocusedHotkey += () => Locker.PinFocusedWindow();
        Locker.DeleteRegionHotkey += () =>
        {
            Locker.DeleteLockedRegion();
            Dispatch(() => MainWnd?.ViewModel.Refresh());
        };
        Locker.RegionDrawn += rect => Dispatch(() =>
        {
            Locker.CreateRegion(rect, alwaysOnTop: true, pinWindow: null);
            ShowMainWindow();
        });
        Locker.SelectionCancelled += () => Dispatch(ShowMainWindow);
    }

    public static void BeginSelection()
    {
        MainWnd?.AppWindow.Hide();
        Locker.BeginRegionSelection();
    }

    public static void ShowMainWindow()
    {
        if (MainWnd is null)
            return;
        MainWnd.AppWindow.Show();
        MainWnd.Activate();
        MainWnd.ViewModel.Refresh();
    }

    public static void ExitApp()
    {
        Locker.Dispose();
        Current.Exit();
    }

    static void Dispatch(Action action)
    {
        if (MainWnd is null)
        {
            action();
            return;
        }

        MainWnd.DispatcherQueue.TryEnqueue(() => action());
    }
}
