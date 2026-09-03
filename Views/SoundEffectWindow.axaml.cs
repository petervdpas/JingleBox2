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
public partial class SoundEffectWindow : Window
{
    /// <summary>What is already open, so a box shows the window it has rather than another.</summary>
    private static readonly Dictionary<object, SoundEffectWindow> Open = new();

    /// <summary>What makes this face pointable at the effect it is drawing.</summary>
    private readonly SoundDeviceRemote _remote;

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
    ///
    /// And the window says its chain is the one in front, which is the other half of the same
    /// gesture: the link says which effect and never which chain, so without this a knob pointed
    /// at a box on the master or on a pad reached nothing at all, and one on a track reached
    /// whichever track an instrument window had last claimed. The same thing the instrument
    /// window says about its track, said about a chain, since a chain is not always on a track.
    ///
    /// Said when it opens as well as when it is brought forward, because opening a window is
    /// coming to the front and there is no guarantee anything else will say so: whether a window
    /// is told it was activated is the window manager's business, and under a bare X server there
    /// is nobody to tell it. Saying it twice costs one assignment.
    /// </remarks>
    public SoundEffectWindow()
    {
        InitializeComponent();

        LinkKey.Listen(this);


        _remote = new SoundDeviceRemote(Face, () => Device?.Effect);

        LinkKey.Watch(Face);

        Opened += (_, _) =>
        {
            _remote.Watch();

            Device?.InFront();
        };

        Activated += (_, _) => Device?.InFront();

        Closed += (_, _) =>
        {
            _remote.Stop();

            Device?.NotInFront();
        };
    }


    /// <summary>The box this window is about, or nothing before it has one.</summary>
    private SoundEffectViewModel? Device => DataContext as SoundEffectViewModel;

    /// <summary>
    /// Opens that box's face over the app's window, or brings the one that is open forward.
    /// </summary>
    /// <param name="device">The box on the chain, and nothing is opened without one.</param>
    /// <param name="owner">The window this one sits over, and nothing is opened without one.</param>
    public static void Show(SoundEffectViewModel? device, Window? owner)
    {
        if (device == null || owner == null) return;

        if (Open.TryGetValue(device, out var already))
        {
            already.Activate();
            return;
        }

        var window = new SoundEffectWindow { DataContext = device };

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
