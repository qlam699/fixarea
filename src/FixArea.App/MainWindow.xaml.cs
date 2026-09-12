using CommunityToolkit.Mvvm.Input;
using FixArea.App.ViewModels;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace FixArea.App;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }
    public IRelayCommand ShowFromTrayCommand { get; }
    public IRelayCommand ExitFromTrayCommand { get; }

    bool _exiting;
    bool _fitting;

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainViewModel(App.Locker);
        ShowFromTrayCommand = new RelayCommand(App.ShowMainWindow);
        ExitFromTrayCommand = new RelayCommand(RequestExit);
        Title = "FixArea";

        RootGrid.Loaded += (_, _) => FitToContent();

        AppWindow.Closing += (_, e) =>
        {
            if (_exiting)
                return;

            e.Cancel = true;
            AppWindow.Hide();
        };
    }

    public void RequestExit()
    {
        if (_exiting)
            return;

        _exiting = true;

        try
        {
            TrayIcon.Dispose();
        }
        catch
        {
            // Ignore tray cleanup failures during shutdown.
        }

        App.ExitApp();
    }

    void FitToContent()
    {
        if (_fitting || RootGrid.XamlRoot is null)
            return;

        _fitting = true;
        try
        {
            RootGrid.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            var scale = RootGrid.XamlRoot.RasterizationScale;
            var width = Math.Max(520, RootGrid.DesiredSize.Width);
            var height = RootGrid.DesiredSize.Height + 8;
            AppWindow.ResizeClient(new SizeInt32(
                (int)Math.Ceiling(width * scale),
                (int)Math.Ceiling(height * scale)));
        }
        finally
        {
            _fitting = false;
        }
    }

    void Create_Click(object sender, RoutedEventArgs e) => ViewModel.CreateRegionCommand.Execute(null);

    void Delete_Click(object sender, RoutedEventArgs e) => ViewModel.DeleteRegionCommand.Execute(null);
}
