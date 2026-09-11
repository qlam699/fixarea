using CommunityToolkit.Mvvm.Input;
using FixArea.App.ViewModels;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace FixArea.App;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }
    public IRelayCommand ShowFromTrayCommand { get; }

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainViewModel(App.Locker);
        ShowFromTrayCommand = new RelayCommand(App.ShowMainWindow);
        Title = "FixArea";

        RootGrid.Loaded += (_, _) => FitToContent();

        AppWindow.Closing += (_, e) =>
        {
            e.Cancel = true;
            AppWindow.Hide();
        };
    }

    bool _fitting;

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

    void OpenTray_Click(object sender, RoutedEventArgs e) => App.ShowMainWindow();

    void ExitTray_Click(object sender, RoutedEventArgs e) => App.ExitApp();
}
