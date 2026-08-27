using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Diagnostics;

namespace JingleBox2.Midi;

/// <summary>
/// What a controller does before anybody has pointed it at anything.
/// </summary>
/// <remarks>
/// Plug in eight faders and they are the levels of the first eight tracks. Turn an encoder and
/// it is the third knob on whatever machine is in front of you. Nobody linked any of it, nothing
/// was stored, and it works on hardware this application has never heard of.
///
/// The reason it can is that it is expressed against the machine rather than against the device.
/// A profile knows a MiniLab has eight encoders and what each is called; it cannot know that
/// encoder three should be a filter, and it never will, because that is a choice about machines
/// the profile has never heard of. What can be said without knowing either is "the third
/// encoder drives the third control on the face", and that is true of every machine including
/// one written next year.
///
/// Nothing here is ever saved. A link somebody made beats all of it, always, because that link
/// names its parameter and these name a place. So the worst this can be is uninteresting.
///
/// What it needs that <see cref="ControlSense"/> does not give is an <b>order</b>: which encoder
/// is the first one. Controller number ascending within a kind, encoders ranked among encoders
/// and faders among faders. That is right on both of a MiniLab's programs and on an MPD218,
/// because a program written for a DAW nobody has heard of has no meanings to use and numbers
/// along the row. It is wrong on a program written for a particular instrument, which numbers by
/// meaning: 74 is filter cutoff in anybody's MIDI, so that knob gets that number for what it
/// does. The second kind never points at this application.
///
/// The order shifts when a control nobody has touched yet turns up with a lower number. That is
/// accepted rather than engineered around: this is a convenience for a device you have not laid
/// out, any explicit link beats it, and the fix for finding it annoying is to lay the device out.
/// </remarks>
public sealed class DefaultLayout
{
    /// <summary>Whether a controller does anything before it has been pointed at something.</summary>
    public bool On { get; set; } = true;

    private readonly object _lock = new();

    /// <summary>What has been seen on each device, and what it turned out to be.</summary>
    private readonly Dictionary<string, Device> _devices = new(StringComparer.OrdinalIgnoreCase);

    private sealed class Device
    {
        public readonly Dictionary<(int Channel, int Cc), Control> Controls = new();

        /// <summary>Bumped whenever a new control turns up, so the places are worked out again.</summary>
        public int Seen;
    }

    private sealed class Control
    {
        public int Channel;
        public int Cc;

        /// <summary>encoder, fader, or nothing while it is still being worked out.</summary>
        public string Kind = "";

        /// <summary>How it should be read, worked out at the same time as what it is.</summary>
        public ControlPickup Pickup = ControlPickup.Sensed;

        public ControlTurn Turn = ControlTurn.Offset;

        public readonly ControlSense Sense = new();

        /// <summary>The mapping handed back for this control, made once and kept.</summary>
        public ControlMapping? Mapping;

        /// <summary>What the places were when that mapping was made.</summary>
        public int Made = -1;
    }

    /// <summary>
    /// What this message should drive, when nothing has been pointed at anything.
    /// </summary>
    /// <remarks>
    /// The same mapping object every time for a given control, which is not tidiness: the router
    /// keeps each mapping's hand state in a table keyed on the mapping itself, so a fresh one per
    /// message would reset pickup on every message and the knob would jump.
    /// </remarks>
    public ControlMapping? For(MidiMessage? message)
    {
        if (!On || message is null || message.Type != MidiMessageType.ControlChange) return null;
        if (string.IsNullOrWhiteSpace(message.Device)) return null;

        lock (_lock)
        {
            if (!_devices.TryGetValue(message.Device, out var device))
                _devices[message.Device] = device = new Device();

            var at = (message.Channel, message.Value);

            if (!device.Controls.TryGetValue(at, out var control))
            {
                device.Controls[at] = control = new Control { Channel = message.Channel, Cc = message.Value };
                device.Seen++;
            }

            if (control.Kind.Length == 0)
            {
                control.Kind = Told(control, message) ?? Watched(control, message.Data);

                if (control.Kind.Length == 0) return null;

                // Something new has a place among the others, so everything's place is stale.
                device.Seen++;
            }

            // A knob is not one of the things a layout has an opinion about: pressing a button
            // nobody assigned should do nothing rather than something surprising.
            if (control.Kind is not ("encoder" or "fader")) return null;

            if (control.Mapping is not null && control.Made == device.Seen) return control.Mapping;

            int place = Place(device, control);

            control.Mapping = Made(message.Device, control, place);
            control.Made = device.Seen;

            Log.Write(LogArea.Midi, () =>
                "layout: " + message.Device + " CC " + control.Cc + " is " + control.Kind + " "
                + (place + 1) + ", so it drives "
                + (control.Kind == "fader" ? "track " + (place + 1) + "'s level"
                                           : "control " + (place + 1) + " on the machine in front of you"));

            return control.Mapping;
        }
    }

    /// <summary>What the controller's own file says this is, when there is one.</summary>
    private static string? Told(Control control, MidiMessage message)
    {
        var said = Controllers.ControllerProfiles.Control(message.Device, message.Channel, message.Value);

        if (said?.Kind is not { Length: > 0 } kind) return null;

        control.Pickup = Controllers.ControllerProfiles.Pickup(message.Device, message.Channel, message.Value)
                         ?? ControlPickup.Sensed;

        // A layout has two categories and a profile has the device's own words. A knob is a
        // fader that is round: it reports a position and it has ends, so it belongs with the
        // faders here, ranked among them and pinned to a track. The distinction that matters to
        // this file is whether a control says where it is or how far it moved, and on that
        // question a knob and a fader are the same control.
        return kind == "knob" ? "fader" : kind;
    }

    /// <summary>
    /// And what watching it says, for the devices nobody has written anything about.
    /// </summary>
    /// <remarks>
    /// What it worked out is carried onto the mapping rather than thrown away. Both this and the
    /// router listen for three messages to decide the same thing about the same control, and
    /// listening twice in a row means a control does nothing for six messages the first time it
    /// is touched, which is long enough to read as broken. This has already listened; the router
    /// is told the answer.
    /// </remarks>
    private static string Watched(Control control, int value)
    {
        if (!control.Sense.Saw(value)) return "";

        control.Pickup = control.Sense.Pickup ?? ControlPickup.Sensed;
        control.Turn = control.Sense.Turn;

        return control.Pickup switch
        {
            ControlPickup.Takeover => "fader",
            ControlPickup.Relative or ControlPickup.Endless => "encoder",

            // A button. Not something this has anything to say about.
            _ => "button"
        };
    }

    /// <summary>Where this control stands among the others of its kind, counting from nought.</summary>
    private static int Place(Device device, Control control) =>
        device.Controls.Values
            .Where(one => one.Kind == control.Kind)
            .OrderBy(one => one.Channel)
            .ThenBy(one => one.Cc)
            .ToList()
            .FindIndex(one => ReferenceEquals(one, control));

    /// <summary>
    /// What a control of that kind in that place drives.
    /// </summary>
    /// <remarks>
    /// A fader is a track's level and is pinned to that track, which is what a mixer is: fader
    /// three is track three whether or not you are looking at it. An encoder follows you, because
    /// what somebody wants from a bank of knobs is the thing in front of them.
    /// </remarks>
    private static ControlMapping Made(string device, Control control, int place) =>
        control.Kind == "fader"
            ? new ControlMapping
            {
                Device = device,
                Channel = control.Channel,
                Cc = control.Cc,
                Kind = ControlKind.Mix,
                Mix = MixControl.Volume,
                Scope = ControlScope.Fixed,
                Track = place,
                Pickup = control.Pickup,
                Turn = control.Turn,
                Name = "track " + (place + 1) + " level"
            }
            : new ControlMapping
            {
                Device = device,
                Channel = control.Channel,
                Cc = control.Cc,
                Kind = ControlKind.Instrument,
                Ordinal = place,
                Scope = ControlScope.Focused,
                Pickup = control.Pickup,
                Turn = control.Turn,
                Name = "control " + (place + 1)
            };

    /// <summary>Forgets a device, for one that has been unplugged or told to do nothing.</summary>
    public void Forget(string? device)
    {
        if (string.IsNullOrWhiteSpace(device)) return;

        lock (_lock) _devices.Remove(device);
    }
}
