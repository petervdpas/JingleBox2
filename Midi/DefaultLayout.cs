using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Enums;
using JingleBox2.Controllers.Interfaces;
using JingleBox2.Controllers;

namespace JingleBox2.Midi;

/// <summary>
/// What a controller does before anybody has pointed it at anything.
/// </summary>
/// <remarks>
/// Plug in eight faders and they are the levels of the first eight tracks. Turn a knob or an
/// encoder and it is the third control on whatever machine is in front of you. Nobody linked any
/// of it, nothing was stored, and it works on hardware this application has never heard of.
///
/// Faders to the mixer and knobs to the machine is a statement about desks rather than about
/// electronics. Both report a position and are picked up identically, so watching cannot tell
/// them apart and does not try: a device nobody has written a file for keeps its knobs on the
/// mixer, exactly as before. A file that says which is which moves them, and that is the whole
/// of what a profile adds here. It is what makes an MPD218 useful on arrival, since six knobs
/// and no faders would otherwise be a six channel mixer on a box built for hitting things.
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
    /// <summary>What is known about the controllers plugged in. Holds a cache, so it is shared rather than made twice.</summary>
    private readonly IControllerProfiles _profiles;

    /// <summary>Takes what is known about the controllers, or asks for its own.</summary>
    /// <param name="profiles">
    /// What is known about the controllers plugged in. Left out, one of its own; the application
    /// hands the same one to everything, since what a device is doing is remembered in it.
    /// </param>
    public DefaultLayout(IControllerProfiles? profiles = null) => _profiles = profiles ?? new ControllerProfiles();

    /// <summary>Whether a controller does anything before it has been pointed at something.</summary>
    public bool On { get; set; } = true;

    private readonly object _lock = new();

    /// <summary>What has been seen on each controller, and what it turned out to be.</summary>
    private readonly Dictionary<string, Controller> _controllers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>One controller, and everything seen on it so far.</summary>
    private sealed class Controller
    {
        /// <summary>Its controls, by channel and number.</summary>
        public readonly Dictionary<(int Channel, int Cc), Control> Controls = new();

        /// <summary>Bumped whenever a new control turns up, so the places are worked out again.</summary>
        public int Seen;
    }

    /// <summary>One control on one controller, and what it turned out to be.</summary>
    private sealed class Control
    {
        /// <summary>1 to 16, as the message said it.</summary>
        public int Channel;

        /// <summary>Its controller number, which is half of its address and none of its meaning.</summary>
        public int Cc;

        /// <summary>encoder, fader, or nothing while it is still being worked out.</summary>
        public string Kind = "";

        /// <summary>How it should be read, worked out at the same time as what it is.</summary>
        public ControlPickup Pickup = ControlPickup.Sensed;

        /// <summary>Which way an encoder counts, worked out at the same time.</summary>
        public ControlTurn Turn = ControlTurn.Offset;

        /// <summary>What is watching it, for a device with no file to say.</summary>
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
    /// message would reset pickup on every message and the knob would jump. A control turning up
    /// that nothing had seen before gives everything a new place among the others, so every
    /// mapping is worked out again on the next message that mentions it.
    ///
    /// A pad or a button is not something a layout has an opinion about. Pressing one nobody has
    /// assigned should do nothing rather than something surprising.
    /// </remarks>
    public ControlMapping? For(MidiMessage? message)
    {
        if (!On || message is null || message.Type != MidiMessageType.ControlChange) return null;
        if (string.IsNullOrWhiteSpace(message.Device)) return null;

        lock (_lock)
        {
            if (!_controllers.TryGetValue(message.Device, out var device))
                _controllers[message.Device] = device = new Controller();

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

                device.Seen++;
            }

            if (Job(control.Kind) is not (Mix or Machine)) return null;

            if (control.Mapping is not null && control.Made == device.Seen) return control.Mapping;

            int place = Place(device, control);

            control.Mapping = Made(message.Device, control, place);
            control.Made = device.Seen;

            Log.Write(LogArea.Midi, () =>
                "layout: " + message.Device + " CC " + control.Cc + " is " + control.Kind + " "
                + (place + 1) + ", so it drives "
                + (Job(control.Kind) == Mix ? "track " + (place + 1) + "'s level"
                                            : "control " + (place + 1) + " on the machine in front of you"));

            return control.Mapping;
        }
    }

    /// <summary>What the controller's own file says this is, when there is one.</summary>
    private string? Told(Control control, MidiMessage message)
    {
        var said = _profiles.Control(message.Device, message.Channel, message.Value);

        if (said?.Kind is not { Length: > 0 } kind) return null;

        control.Pickup = _profiles.Pickup(message.Device, message.Channel, message.Value)
                         ?? ControlPickup.Sensed;

        return kind;
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
    ///
    /// Anything that is neither a position nor a count of notches is a button, and a button is
    /// not something this has anything to say about.
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

            _ => "button"
        };
    }

    /// <summary>The mixer, and the machine in front of you. All a layout has to point at.</summary>
    private const string Mix = "mix";

    /// <summary>Whatever face is in front of you, which is what a knob follows.</summary>
    private const string Machine = "machine";

    /// <summary>
    /// What a control of this kind is for here.
    /// </summary>
    /// <remarks>
    /// A fader belongs to the mixer and a knob belongs to the machine, and that is a statement
    /// about the desk rather than about the electronics: both of them report a position and are
    /// picked up identically, so <see cref="ControlSense"/> cannot tell them apart and does not
    /// try. Only a profile knows which is which, which is the whole of what a profile adds here.
    /// A device with no file keeps its knobs on the mixer, as it always did.
    ///
    /// Knobs and encoders share one order rather than having one each, because they are the same
    /// job done two ways. A desk with both would otherwise have two first controls, both pointed
    /// at the same parameter.
    ///
    /// A pad and a button get nothing, and so does a modulation strip, which is the one worth
    /// saying out loud. A strip is picked up exactly as a fader is and it would be easy to file
    /// it with them, but it is a performance control rather than a mixer one: it springs back, it
    /// is played while a note sounds, and a track whose level it drove would drop to nothing the
    /// moment your thumb came off.
    /// </remarks>
    private static string Job(string kind) => kind switch
    {
        "fader" => Mix,
        "knob" or "encoder" => Machine,

        _ => ""
    };

    /// <summary>Where this control stands among the others doing its job, counting from nought.</summary>
    private static int Place(Controller device, Control control) =>
        device.Controls.Values
            .Where(one => Job(one.Kind) == Job(control.Kind))
            .OrderBy(one => one.Channel)
            .ThenBy(one => one.Cc)
            .ToList()
            .FindIndex(one => ReferenceEquals(one, control));

    /// <summary>
    /// What a control of that kind in that place drives.
    /// </summary>
    /// <remarks>
    /// A fader is a track's level and is pinned to that track, which is what a mixer is: fader
    /// three is track three whether or not you are looking at it. A knob or an encoder follows
    /// you, because what somebody wants from a bank of knobs is the thing in front of them.
    /// </remarks>
    private static ControlMapping Made(string device, Control control, int place) =>
        Job(control.Kind) == Mix
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
                Kind = ControlKind.SoundDevice,
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

        lock (_lock) _controllers.Remove(device);
    }
}
