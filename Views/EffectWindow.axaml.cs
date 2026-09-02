using System.Collections.Generic;
using Avalonia.Controls;
using JingleBox2.ViewModels;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <summary>
/// One of our effects off a chain, in a window of its own.
/// </summary>
/// <remarks>
/// A plugin opens in <see cref="PluginWindow"/>, which has a stranger's own interface to embed
/// and everything that goes with that. One of ours has none of it: the face is described, the
/// engine is in this process, and the window is a frame around a panel.
///
/// One window per box, brought forward rather than opened twice, which is the rule every window
/// in this application keeps. The block in the chain draws itself brighter while its window is
/// up, so the two have to agree about when it closes.
/// </remarks>
public partial class EffectWindow : Window
{
    /// <summary>What is already open, so a box shows the window it has rather than another.</summary>
    private static readonly Dictionary<object, EffectWindow> Open = new();

    /// <summary>What makes this face pointable at the effect it is drawing.</summary>
    private readonly DeviceRemote _remote;

    /// <summary>A box's colour mixed into the theme's. Holds nothing, so one is enough.</summary>
    private readonly IPanelTint _tint = new PanelTint();

    /// <summary>
    /// Builds the window and lets the other mouse mode be reached from inside it.
    /// </summary>
    /// <remarks>
    /// The pointer goes where the windows are, so the pointing gesture has to be answered on
    /// every window with something pointable on it. What is offered is the effect and the
    /// control under the pointer, which is a fact about your hardware and this effect rather
    /// than about the track it is standing on.
    /// </remarks>
    public EffectWindow()
    {
        InitializeComponent();

        LinkKey.Listen(this);

        DeckKeys.Listen(this);

        _remote = new DeviceRemote(Face, () => Device?.Effect);

        LinkKey.Watch(Face);

        Opened += (_, _) => _remote.Watch();

        Closed += (_, _) => _remote.Stop();
    }


    /// <summary>The box this window is about, or nothing before it has one.</summary>
    private EffectDeviceViewModel? Device => DataContext as EffectDeviceViewModel;

    /// <summary>
    /// Opens that box's face over the app's window, or brings the one that is open forward.
    /// </summary>
    /// <param name="device">The box on the chain, and nothing is opened without one.</param>
    /// <param name="owner">The window this one sits over, and nothing is opened without one.</param>
    public static void Show(EffectDeviceViewModel? device, Window? owner)
    {
        if (device == null || owner == null) return;

        if (Open.TryGetValue(device, out var already))
        {
            already.Activate();
            return;
        }

        var window = new EffectWindow { DataContext = device };

        window._tint.Apply(window.Plate, device.Effect.Theme);

        Open[device] = window;

        device.IsOpen = true;

        window.Closed += (_, _) =>
        {
            Open.Remove(device);

            device.IsOpen = false;
        };

        window.Show(owner);
    }

    /// <summary>Closes the window belonging to that box, for the box going away.</summary>
    /// <remarks>
    /// Called when a box is taken out of a chain: a window showing something that is no longer
    /// on the chain is a window nothing can close.
    /// </remarks>
    /// <param name="device">The box being taken out.</param>
    public static void CloseFor(object? device)
    {
        if (device == null) return;

        if (Open.TryGetValue(device, out var window)) window.Close();
    }
}
