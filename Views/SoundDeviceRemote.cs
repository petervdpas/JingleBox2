using System;
using JingleBox2.Rack.SoundDevices.Interfaces;
using JingleBox2.Rack.Controls;

namespace JingleBox2.Views;

/// <summary>
/// Remote control on a device's face, wherever that face is standing.
/// </summary>
/// <remarks>
/// Pointing a hardware knob at something is a device thing: rest the pointer on a control while
/// the mode is on, touch the knob, and the link names the device's id and that control's key. The
/// face has no idea which device it is drawing and should not: it draws what it is handed. So the
/// three acts that make a face pointable live here, in one place, rather than on each view that
/// happens to show one.
///
/// The three are: offering what is under the pointer, taking a link off again, and lighting what
/// is already pointed at so somebody can see the mode is live and which controls are spoken for.
///
/// It was written out three times before this, on a machine's panel, on the rack's effect face
/// and in an effect's own window, and two of the three were wrong in the same way: they said
/// <c>Insert</c>, which is a plugin, and every link of that kind is thrown away as the settings
/// are read. One place, one mistake to make, and it is made once.
/// </remarks>
public sealed class SoundDeviceRemote
{
    /// <summary>What a device's control offers a knob. Holds nothing, so one is enough.</summary>
    private static readonly Midi.Interfaces.ISoundDeviceLinks Links = new Midi.SoundDeviceLinks();

    /// <summary>The face this is about.</summary>
    private readonly PanelView _face;

    /// <summary>Which device that face is drawing at the moment, or nothing.</summary>
    private readonly Func<ISoundDevice?> _device;

    /// <summary>
    /// Makes a face pointable at whichever device it is showing.
    /// </summary>
    /// <remarks>
    /// The device is asked for rather than held, since a face outlives what it draws: the rack
    /// draws whichever effect is picked, and a track's panel whichever machine the track plays.
    /// </remarks>
    /// <param name="face">The panel being drawn.</param>
    /// <param name="device">What it is showing, asked each time it matters.</param>
    public SoundDeviceRemote(PanelView face, Func<ISoundDevice?> device)
    {
        _face = face;
        _device = device;

        face.LinkWanted += (_, key) => Offer(key, action: false);
        face.LinkActionWanted += (_, action) => Offer(action, action: true);
        face.UnlinkWanted += (_, key) => Drop(key);
    }

    /// <summary>
    /// Follows the desk, so the face lights up when the mode is turned over from anywhere.
    /// </summary>
    /// <remarks>
    /// The mode is one application-wide thing and the keystroke that turns it over may be pressed
    /// on any window, so a face that only looked when it was built would sit dark while the mode
    /// was on.
    /// </remarks>
    public void Watch()
    {
        if (Midi.ControlLink.Current is { } link) link.Changed += Show;

        _face.PropertyChanged += Drawn;

        Show();
    }

    /// <summary>
    /// The face is drawing a different device, so what is pointed at it is asked again.
    /// </summary>
    /// <remarks>
    /// **A panel is shown one device and then another while it stays exactly where it is**, which
    /// is what the rack does: the page and its data context are the same objects throughout and
    /// only the face and the values underneath are swapped, through bindings, with no code of
    /// this window's running at all. Told only when it was built and when its data context moved,
    /// this went on reporting the keys of the machine before it.
    ///
    /// Which is not a mark in the wrong place so much as a mark that is wrong: a face draws a
    /// control as already pointed at by its parameter's key, and machines share those names, so
    /// a fresh machine's cutoff, resonance, attack and release all came up as taken on a device
    /// nobody had ever pointed anything at.
    ///
    /// Asked of the face rather than of whoever swapped it, so no call site can forget: what this
    /// decorates is that control, and the two things it is drawn from are the face and the
    /// values.
    /// </remarks>
    /// <param name="sender">The face.</param>
    /// <param name="what">Which of its properties moved.</param>
    private void Drawn(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs what)
    {
        if (what.Property != Rack.Controls.PanelView.FaceProperty
            && what.Property != Rack.Controls.PanelView.ValuesProperty)
            return;

        Show();
    }

    /// <summary>And lets go, for a face that is going away.</summary>
    public void Stop()
    {
        if (Midi.ControlLink.Current is { } link) link.Changed -= Show;

        _face.PropertyChanged -= Drawn;
    }

    /// <summary>Tells the face what mode the pointer is in and what is already pointed at.</summary>
    public void Show()
    {
        var link = Midi.ControlLink.Current;

        _face.Linking = link?.IsLinking ?? false;

        string id = _device()?.Id ?? "";

        _face.Linked = link is null || id.Length == 0 ? null : link.KeysOn(id);
        _face.LinkedActions = link is null || id.Length == 0 ? null : link.ActionsOn(id);
    }

    /// <summary>Offers the device and the control under the pointer, while the mode is on.</summary>
    /// <param name="key">The parameter's key, or the action's word.</param>
    /// <param name="action">True for a button, which is a press rather than a value.</param>
    private void Offer(string key, bool action)
    {
        if (Midi.ControlLink.Current is not { IsLinking: true } link) return;

        if (_device() is not { } device) return;

        link.Offer(action
            ? Links.Action(device.Id, device.Name, key)
            : Links.On(device.Id, device.Name, key));
    }

    /// <summary>Takes whatever is pointed at that control off it.</summary>
    /// <param name="key">The parameter's key, or the action's word.</param>
    private void Drop(string key)
    {
        if (Midi.ControlLink.Current is not { IsLinking: true } link) return;

        if (_device() is not { } device) return;

        link.Unlink(device.Id, key);
    }
}
