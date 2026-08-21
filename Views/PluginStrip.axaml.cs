using Avalonia.Controls;
using Avalonia.Interactivity;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

/// <summary>
/// The plugin chain control. Its behaviour is in PluginChainViewModel, so the same strip works
/// for a pad, a tracker track, or anything else that grows one later. Opening a window is the
/// one thing that has to happen here: windows are a view's business.
/// </summary>
public partial class PluginStrip : UserControl
{
    private PluginChainViewModel? _chain;

    public PluginStrip()
    {
        InitializeComponent();

        // A device that leaves the chain takes its window with it, wherever the removal came
        // from: the strip's menu, a song being opened, or a pad profile changing.
        DataContextChanged += (_, _) =>
        {
            if (_chain != null) _chain.DeviceClosing -= PluginWindow.CloseFor;

            _chain = DataContext as PluginChainViewModel;

            if (_chain != null) _chain.DeviceClosing += PluginWindow.CloseFor;
        };
    }

    private void OnOpenDevice(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control) return;
        if (control.DataContext is not PluginDeviceViewModel device) return;
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        PluginWindow.Show(device, owner);
    }
}
