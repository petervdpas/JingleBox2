using Avalonia.Controls;
using Avalonia.VisualTree;
using JingleBox2.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

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

    /// <summary>
    /// Builds the window, and takes on the two duties an embedded plugin window puts on
    /// whoever is holding it.
    /// </summary>
    /// <remarks>
    /// XEMBED makes the embedder responsible for telling the plugin when its window is the one
    /// being used, every time, not once when it was handed over. Without these the plugin
    /// believes whatever it was told at attach, which after the first click on anything else is
    /// that it is not active: it carries on drawing from its own timers and ignores everything
    /// clicked on it.
    ///
    /// <see cref="LinkKey"/>.Listen is the other one: the pointer goes where the windows are,
    /// so the other mouse mode has to be reachable from all of them.
    /// </remarks>
    public PluginWindow()
    {
        InitializeComponent();

        LinkKey.Listen(this);

        Activated += (_, _) => TellPlugin(true);
        Deactivated += (_, _) => TellPlugin(false);
    }

    /// <summary>Passes this window's activation to the plugin drawing inside it, if there is one.</summary>
    private void TellPlugin(bool active)
    {
        foreach (var host in this.GetVisualDescendants().OfType<PluginEditorHost>())
        {
            host.WindowActivated(active);
        }
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
    public static void Show(object key, PluginControlsViewModel panel, string title, Window owner, Action? closed = null)
    {
        Show(key, panel, title, owner, null, closed);
    }

    /// <summary>
    /// The one that actually opens a window, which both public overloads reach.
    /// </summary>
    /// <remarks>
    /// The plugin's interface is opened before the window is built, so the window can size
    /// itself to whatever the plugin turns out to be. A plugin drawing its own interface is a
    /// picture at a size it chose, so it is let out of the caps that keep a wall of host-drawn
    /// knobs from filling the screen.
    ///
    /// The plugin is taken out of its window on the way out rather than after: letting the
    /// window go first leaves the plugin drawing into something that is not there, which is a
    /// crash on closing rather than on opening. Only the picture is put away; the plugin itself
    /// carries on playing.
    /// </remarks>
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

        panel.Prepare();

        var window = new PluginWindow
        {
            DataContext = new PluginWindowViewModel(panel, title, device),
            Title = title
        };

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

        window.Closing += (_, _) =>
        {
            panel.Close();
        };

        window.Closed += (_, _) =>
        {
            Open.Remove(key);
            closed?.Invoke();
        };

        window.Show(owner);
    }

    /// <summary>
    /// Closes a window, for whatever owned it going away. Named apart from Window.Close so
    /// that closing a key and closing a window cannot be mistaken for each other.
    /// </summary>
    public static void CloseFor(object key)
    {
        if (key == null || !Open.TryGetValue(key, out var window)) return;

        Open.Remove(key);

        if (key is PluginDeviceViewModel device) device.IsOpen = false;

        window.Close();
    }
}
