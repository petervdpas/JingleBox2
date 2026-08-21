using Avalonia.Controls;
using JingleBox2.ViewModels;
using System;
using System.Collections.Generic;

namespace JingleBox2.Views;

/// <summary>
/// One plugin on its own, in a window that can be left open while the rest of the app is
/// used. Several can be open at once, one per device.
/// </summary>
/// <remarks>
/// These are our own controls, not the plugin's interface. A plugin's own window is a
/// different thing entirely: it means handing the plugin a native child window and running
/// its event loop, which is a job of its own.
/// </remarks>
public partial class PluginWindow : Window
{
    /// <summary>What is already open, so a device shows the window it has rather than another.</summary>
    private static readonly Dictionary<PluginDeviceViewModel, PluginWindow> Open = new();

    public PluginWindow()
    {
        InitializeComponent();
    }

    /// <summary>Opens a device's window, or brings the one it already has to the front.</summary>
    public static void Show(PluginDeviceViewModel device, Window owner)
    {
        if (device == null) return;

        if (Open.TryGetValue(device, out var existing))
        {
            existing.Activate();
            return;
        }

        var window = new PluginWindow
        {
            DataContext = device,
            Title = device.Name
        };

        // Wide plugins wrap rather than running off the edge; the window sizes itself to what
        // is in it, up to the caps set in the XAML.
        window.MaxWidth = Math.Min(900, owner.Bounds.Width > 0 ? owner.Bounds.Width : 900);

        Open[device] = window;
        device.IsOpen = true;

        window.Closed += (_, _) =>
        {
            Open.Remove(device);
            device.IsOpen = false;
        };

        window.Show(owner);
    }

    /// <summary>Closes a device's window, for a device being taken out of a chain.</summary>
    public static void Close(PluginDeviceViewModel device)
    {
        if (device == null || !Open.TryGetValue(device, out var window)) return;

        Open.Remove(device);
        device.IsOpen = false;
        window.Close();
    }
}
