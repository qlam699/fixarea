using FixArea.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FixArea.App;

public sealed partial class RegionActionWindow : Window
{
    readonly IReadOnlyList<WindowInfo> _windows;
    bool _finished;

    public event Action<RegionCreateAction, WindowInfo?>? Completed;
    public event Action? Cancelled;

    public RegionActionWindow(IReadOnlyList<WindowInfo> windows)
    {
        InitializeComponent();
        _windows = windows;
        Title = "Lock this region";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(520, 420));
        WindowCombo.ItemsSource = _windows;
        if (_windows.Count > 0)
            WindowCombo.SelectedIndex = 0;

        AppWindow.Closing += (_, _) =>
        {
            if (!_finished)
                Cancelled?.Invoke();
        };
    }

    public void SetBoundsLabel(string text) => BoundsText.Text = text;

    void LockOnly_Click(object sender, RoutedEventArgs e) => Finish(RegionCreateAction.LockOnly, null);

    void AlwaysOnTop_Click(object sender, RoutedEventArgs e) => Finish(RegionCreateAction.LockAlwaysOnTop, null);

    void Pin_Click(object sender, RoutedEventArgs e)
    {
        if (WindowCombo.SelectedItem is not WindowInfo info)
            return;
        Finish(RegionCreateAction.PinWindow, info);
    }

    void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_finished)
            return;
        _finished = true;
        Cancelled?.Invoke();
        Close();
    }

    void Finish(RegionCreateAction action, WindowInfo? window)
    {
        if (_finished)
            return;
        _finished = true;
        Completed?.Invoke(action, window);
        Close();
    }
}
