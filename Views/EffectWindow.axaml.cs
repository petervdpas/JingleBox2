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

        Face.LinkWanted += Offer;

        LinkKey.Watch(Face);

        Opened += (_, _) =>
        {
            if (Midi.ControlLink.Current is { } link) link.Changed += ShowLinks;

            ShowLinks();
        };

        Closed += (_, _) =>
        {
            if (Midi.ControlLink.Current is { } link) link.Changed -= ShowLinks;
        };
    }


    /// <summary>Tells the face what mode the pointer is in and what is already pointed at.</summary>
    /// <remarks>
    /// The same three things a machine's panel is told, and told the same way: the glow over a
    /// control is how somebody knows the gesture is live and which controls already have
    /// something on them. Without it the mode is on and nothing on the screen says so.
    /// </remarks>
    private void ShowLinks()
    {
        var link = Midi.ControlLink.Current;

        Face.Linking = link?.IsLinking ?? false;

        Face.Linked = link is null || Device?.Effect is not { } effect ? null : link.KeysOn(effect.Id);
    }

    /// <summary>The box this window is about, or nothing before it has one.</summary>
    private EffectDeviceViewModel? Device => DataContext as EffectDeviceViewModel;

    /// <summary>Offers the effect and the control under the pointer, while the mode is on.</summary>
    /// <param name="sender">The panel the pointer is on.</param>
    /// <param name="key">The parameter the control under it turns.</param>
    private void Offer(object? sender, string key)
    {
        if (Midi.ControlLink.Current is not { IsLinking: true } link) return;

        if (Device is not { } device) return;

        link.Offer(new Midi.ControlMapping
        {
            Kind = Midi.Enums.ControlKind.Insert,
            Scope = Midi.Enums.ControlScope.Focused,
            Machine = device.Effect.Id,
            Key = key,
            Owner = device.Name,
            Name = device.Name + " " + key
        });
    }

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
