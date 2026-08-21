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
/// What is inside is the plugin's own interface where it has one, and the host's knobs where
/// it has not. Either way the window itself, its title and its bypass button are the host's.
/// </remarks>
public partial class PluginWindow : Window
{
    /// <summary>What is already open, so a thing shows the window it has rather than another.</summary>
    private static readonly Dictionary<object, PluginWindow> Open = new();

    public PluginWindow()
    {
        InitializeComponent();
    }

    /// <summary>Opens a device's window, or brings the one it already has to the front.</summary>
    public static void Show(PluginDeviceViewModel device, Window owner)
    {
        if (device == null) return;

        device.IsOpen = true;

        Show(device, device.Panel, device.Name, owner, device, () => device.IsOpen = false);
    }

    /// <summary>
    /// Opens a plugin that is not in a chain, an instrument for instance, in the same kind of
    /// window. The key is whatever owns it, so asking twice brings the same window forward.
    /// </summary>
    public static void Show(object key, PluginControlsViewModel panel, string title, Window owner)
    {
        Show(key, panel, title, owner, null, null);
    }

    private static void Show(
        object key,
        PluginControlsViewModel panel,
        string title,
        Window owner,
        PluginDeviceViewModel? device,
        Action? closed)
    {
        if (key == null || panel == null) return;

        if (Open.TryGetValue(key, out var existing))
        {
            existing.Activate();
            return;
        }

        // The plugin's interface is opened before the window is built, so the window can size
        // itself to whatever the plugin turns out to be.
        panel.Prepare();

        var window = new PluginWindow
        {
            DataContext = new PluginWindowViewModel(panel, title, device),
            Title = title
        };

        // A plugin drawing its own interface is a picture at a size it chose, so it is let out
        // of the caps that keep a wall of knobs from filling the screen.
        if (panel.HasOwnWindow)
        {
            window.MaxWidth = double.PositiveInfinity;
            window.MaxHeight = double.PositiveInfinity;
        }
        else
        {
            window.MaxWidth = Math.Min(900, owner.Bounds.Width > 0 ? owner.Bounds.Width : 900);
        }

        Open[key] = window;

        window.Closed += (_, _) =>
        {
            Open.Remove(key);
            closed?.Invoke();

            // The plugin's interface goes with the window. The plugin itself carries on
            // playing; only its picture is put away.
            panel.Close();
        };

        window.Show(owner);
    }

    /// <summary>Closes a window, for whatever owned it going away.</summary>
    public static void Close(object key)
    {
        if (key == null || !Open.TryGetValue(key, out var window)) return;

        Open.Remove(key);

        if (key is PluginDeviceViewModel device) device.IsOpen = false;

        window.Close();
    }
}
