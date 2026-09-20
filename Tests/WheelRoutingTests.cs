using System.Collections.Generic;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Where a wheel goes: which half of the application, which track, and who has first refusal.
/// </summary>
/// <remarks>
/// The wire is settled next door in <see cref="WheelWireTests"/>. This is the half that decides
/// who hears it, which is where a bend reaching the wrong track, both tracks, or the same track
/// twice would come from.
///
/// **A wheel goes with the keys.** That is the sentence every test below is one reading of: the
/// same port, the same half, the same track. Anything that hears a key and not the wheel beside
/// it leaves a track permanently off its own pitch with nothing on the screen to say why.
/// </remarks>
public class WheelRoutingTests
{
    /// <summary>A wheel from a port given the keys reaches them.</summary>
    /// <remarks>
    /// The wheels are no job of their own: they arrive wherever that port's notes arrive, which
    /// is what "a wheel goes where the keys go" comes to in the one place that decides where a
    /// message goes at all.
    /// </remarks>
    [Fact]
    public void A_wheel_on_a_keyboard_reaches_the_keys()
    {
        var said = new List<string>();
        var dispatcher = Deck(said, MidiPortRole.Tracker);

        dispatcher.Handle(Bend("keyboard", 16383));
        dispatcher.Handle(Modulation("keyboard", 127));

        Assert.Equal(new[] { "wheel", "wheel" }, said);
    }

    /// <summary>
    /// And a port that was never given the keys does nothing with it.
    /// </summary>
    /// <remarks>
    /// A knob box ticked for the controls has no keys, so its controller one is a controller and
    /// not a wheel: read as one it would bend whatever the keyboard on the next port was playing.
    /// </remarks>
    [Fact]
    public void A_wheel_on_a_port_with_no_keys_reaches_nothing()
    {
        var said = new List<string>();
        var dispatcher = Deck(said, MidiPortRole.Pads);

        dispatcher.Handle(Bend("keyboard", 16383));
        dispatcher.Handle(Modulation("keyboard", 127));

        Assert.Empty(said);
    }

    /// <summary>
    /// A link somebody made on the modulation wheel wins, and the wheel then stands down.
    /// </summary>
    /// <remarks>
    /// The one collision in this whole arrangement, and the router answers it itself. A
    /// modulation wheel is a continuous controller like any other, so pointing it at a filter
    /// cutoff has always worked and has to go on working; what may not happen is both, which
    /// would drive the cutoff and bring in vibrato from one gesture.
    ///
    /// Asked of the wheel router rather than of the dispatcher, which is the whole of what the
    /// untangling moved: one class owns whether a message is a wheel, so there is one place to
    /// read when a wheel misbehaves.
    /// </remarks>
    [Fact]
    public void A_link_on_the_wheel_beats_the_wheel()
    {
        var heard = new Wheels();

        Assert.False(new MidiWheelRouter(heard, pointed: _ => true).Handle(Modulation("keyboard", 127)));

        Assert.Empty(heard.Said);
    }

    /// <summary>And a controller nobody pointed anywhere falls through to being a wheel.</summary>
    [Fact]
    public void A_wheel_nobody_pointed_anywhere_is_still_a_wheel()
    {
        var heard = new Wheels();

        Assert.True(new MidiWheelRouter(heard, pointed: _ => false).Handle(Modulation("keyboard", 127)));

        Assert.Equal(new[] { "modulate 1" }, heard.Said);
    }

    /// <summary>
    /// A track listening on that port and channel takes the wheel, and the keys do not also get it.
    /// </summary>
    /// <remarks>
    /// The same rule a note already keeps, and the same door: a track answers for every message
    /// it claims rather than for notes alone. A keyboard pointed at track three is pointed at it
    /// whole, since its keys and the wheels beside them are one hand.
    /// </remarks>
    [Fact]
    public void A_track_that_claims_the_port_takes_the_wheel()
    {
        var said = new List<string>();

        var dispatcher = new MidiDispatcher(
            Roles("keyboard", MidiPortRole.Tracker),
            null,
            _ => said.Add("keys"),
            tracks: _ =>
            {
                said.Add("track");
                return true;
            });

        dispatcher.Handle(Bend("keyboard", 0));

        Assert.Equal(new[] { "track" }, said);
    }

    /// <summary>A track's MIDI in hears the wheels on its own channel and nobody else's.</summary>
    [Fact]
    public void A_tracks_midi_in_hears_its_own_wheels()
    {
        var mix = new List<TrackMix> { new(), new() };
        mix[0].MidiIn = new TrackMidiRoute { Channel = 3 };

        var heard = new TrackRoad();
        var router = new MidiTrackRouter(() => heard, () => mix);

        Assert.True(router.Wheels(Bend("keyboard", 16383, channel: 3)));
        Assert.True(router.Wheels(Modulation("keyboard", 127, channel: 3)));

        Assert.False(router.Wheels(Bend("keyboard", 16383, channel: 4)));
        Assert.False(router.Wheels(Modulation("keyboard", 127, channel: 4)));

        Assert.Equal(new[] { "bend 0 1", "modulate 0 1" }, heard.Said);
    }

    /// <summary>The keys a track claims and the wheels beside them go down one road.</summary>
    /// <remarks>
    /// The whole of what a claim means: a keyboard pointed at track three is pointed at it
    /// whole. It used to be two contracts and two constructor arguments, so a router could be
    /// built that took the notes and dropped the wheels, and a wheel that was claimed and
    /// dropped reached nothing anywhere while looking exactly like a fault in the wire. There is
    /// no way to build that now, which is what the one road buys.
    /// </remarks>
    [Fact]
    public void A_tracks_keys_and_its_wheels_go_down_one_road()
    {
        var mix = new List<TrackMix> { new() };
        mix[0].MidiIn = new TrackMidiRoute { Channel = 1 };

        var heard = new TrackRoad();
        var router = new MidiTrackRouter(() => heard, () => mix);

        Assert.True(router.Claim(new MidiMessage
        {
            Device = "keyboard", Type = MidiMessageType.Note, Channel = 1, Value = 60, Data = 100, IsOn = true
        }));
        Assert.True(router.Claim(Bend("keyboard", 16383)));
        Assert.True(router.Claim(Modulation("keyboard", 127)));

        Assert.Equal(new[] { "down 0 C-4", "bend 0 1", "modulate 0 1" }, heard.Said);
    }

    /// <summary>
    /// The wheel goes to whichever half is holding the keys, not to whichever page is in front.
    /// </summary>
    /// <remarks>
    /// Open a song, or leave the rack, with a finger still on a key: the notes are still
    /// sounding where they were played and the bend has to follow them. Sent to the other half
    /// it would be a note left leaning with nothing that can straighten it, since the wheel
    /// coming back to rest would straighten the other half's notes rather than these.
    /// </remarks>
    [Fact]
    public void A_wheel_follows_the_keys_that_are_down()
    {
        var pattern = new Half();
        var rack = new Half();
        bool rackHasIt = true;

        var adapter = new TrackerNoteAdapter(pattern, rack, () => rackHasIt);

        adapter.TriggerNote(new Note(48), 100);

        rackHasIt = false;

        adapter.Bend(1);
        adapter.Modulate(0.5);

        Assert.Equal(new[] { "bend 1", "modulate 0.5" }, rack.Said);
        Assert.Empty(pattern.Said);
    }

    /// <summary>With no key down it goes to the half in front, which is what sets a wheel before a phrase.</summary>
    [Fact]
    public void With_nothing_held_a_wheel_goes_to_the_half_in_front()
    {
        var pattern = new Half();
        var rack = new Half();

        var adapter = new TrackerNoteAdapter(pattern, rack, () => false);

        adapter.Modulate(1);

        Assert.Equal(new[] { "modulate 1" }, pattern.Said);
        Assert.Empty(rack.Said);
    }

    /// <summary>And once the key is let go of, the wheel goes back to whichever half is in front.</summary>
    [Fact]
    public void A_wheel_follows_the_front_again_once_the_key_is_up()
    {
        var pattern = new Half();
        var rack = new Half();
        bool rackHasIt = true;

        var adapter = new TrackerNoteAdapter(pattern, rack, () => rackHasIt);

        adapter.TriggerNote(new Note(48), 100);
        adapter.ReleaseNote(new Note(48));

        rackHasIt = false;

        adapter.Bend(-1);

        Assert.Equal(new[] { "bend -1" }, pattern.Said);
    }

    /// <summary>A dispatcher over one port with that job, and somewhere for the wheels to land.</summary>
    /// <remarks>
    /// The wheels arrive through the tracker job, which is what they are: the same delegate the
    /// notes from that port go to, since a wheel goes where the keys go.
    /// </remarks>
    private static MidiDispatcher Deck(List<string> said, MidiPortRole role) =>
        new(Roles("keyboard", role), null, msg => { if (Turns(msg)) said.Add("wheel"); });

    /// <summary>Whether that message is one of the two wheels, by the one rule that decides.</summary>
    private static bool Turns(MidiMessage msg) => new ControlJobs().Turns(msg);

    /// <summary>Settings giving one port one job.</summary>
    private static MidiConfig Roles(string port, MidiPortRole role)
    {
        var cfg = new MidiConfig();

        new MidiPortBindings().SetRole(cfg.Devices, port, role);

        return cfg;
    }

    /// <summary>A pitch wheel message, as the wire hands one over.</summary>
    private static MidiMessage Bend(string device, int bend, int channel = 1) => new()
    {
        Device = device, Type = MidiMessageType.PitchBend, Channel = channel, Value = 0, Data = bend
    };

    /// <summary>And a modulation wheel one.</summary>
    private static MidiMessage Modulation(string device, int value, int channel = 1) => new()
    {
        Device = device, Type = MidiMessageType.ControlChange, Channel = channel, Value = 1, Data = value
    };

    /// <summary>One half of the application, writing down what its wheels were told.</summary>
    private sealed class Half : IPlaysNotes, IWheels
    {
        /// <summary>Each wheel move, in the order it arrived.</summary>
        public List<string> Said { get; } = new();

        /// <inheritdoc/>
        public void PlayMidiNote(Note note, int volume)
        {
        }

        /// <inheritdoc/>
        public void ReleaseMidiNote(Note note)
        {
        }

        /// <inheritdoc/>
        public void Bend(double lean) => Said.Add("bend " + lean.ToString("0.###"));

        /// <inheritdoc/>
        public void Modulate(double amount) => Said.Add("modulate " + amount.ToString("0.###"));
    }

    /// <summary>Somewhere for a wheel to land, in the order it landed.</summary>
    private sealed class Wheels : IPlays
    {
        /// <summary>Each move, in the order it arrived.</summary>
        public List<string> Said { get; } = new();

        /// <inheritdoc/>
        public void Press(int track, Note note, int volume)
        {
        }

        /// <inheritdoc/>
        public void Let(int track, Note note)
        {
        }

        /// <inheritdoc/>
        public void Bend(int track, double lean) => Said.Add("bend " + lean.ToString("0.###"));

        /// <inheritdoc/>
        public void Modulate(int track, double amount) =>
            Said.Add("modulate " + amount.ToString("0.###"));
    }

    /// <summary>The road a track's MIDI in goes down, keys and wheels alike, each naming its track.</summary>
    private sealed class TrackRoad : IPlays
    {
        /// <summary>Everything that went down it, with its track, in order.</summary>
        public List<string> Said { get; } = new();

        /// <inheritdoc/>
        public void Press(int track, Note note, int volume) =>
            Said.Add("down " + track + " " + note);

        /// <inheritdoc/>
        public void Let(int track, Note note) => Said.Add("up " + track + " " + note);

        /// <inheritdoc/>
        public void Bend(int track, double lean) =>
            Said.Add("bend " + track + " " + lean.ToString("0.###"));

        /// <inheritdoc/>
        public void Modulate(int track, double amount) =>
            Said.Add("modulate " + track + " " + amount.ToString("0.###"));
    }
}
