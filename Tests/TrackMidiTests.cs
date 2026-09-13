using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Music;
using JingleBox2.Music.Interfaces;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A track's MIDI in and out: what is stored, which track a note on the wire belongs to, and
/// what goes out of a port when a track plays.
/// </summary>
/// <remarks>
/// Asked for so a KeyStep Pro's four sequencer tracks, on channels 1 to 4 and 10, can each play
/// a track here, and so a track here can play something outside. Every rule is tested without a
/// port, a window or a clock.
/// </remarks>
public class TrackMidiTests
{
    private readonly ITrackMidiRoutes _routes = new TrackMidiRoutes();

    private readonly IMidiNoteInput _wire = new MidiNoteInput();

    /// <summary>A strip nobody has touched listens to nothing and sends nothing.</summary>
    [Fact]
    public void A_new_strip_has_no_midi_in_or_out()
    {
        var mix = new TrackMix();

        Assert.False(mix.MidiIn.IsOn);
        Assert.False(mix.MidiOut.IsOn);
    }

    /// <summary>A copy of a strip carries both routes, which is what saving and undo take.</summary>
    [Fact]
    public void A_copy_keeps_both_routes()
    {
        var mix = new TrackMix
        {
            MidiIn = new TrackMidiRoute { Port = "KeyStep Pro MIDI 1", Channel = 10 },
            MidiOut = new TrackMidiRoute { Port = "Out", Channel = 3 }
        };

        var copy = mix.Clone();

        Assert.Equal(mix.MidiIn, copy.MidiIn);
        Assert.Equal(mix.MidiOut, copy.MidiOut);
    }

    /// <summary>A channel past either end is off, and a missing route is an empty one.</summary>
    [Theory]
    [InlineData(-3)]
    [InlineData(17)]
    [InlineData(int.MaxValue)]
    public void A_channel_outside_one_to_sixteen_is_off(int channel)
    {
        var mix = new TrackMix
        {
            MidiIn = new TrackMidiRoute { Port = "a", Channel = channel },
            MidiOut = null!
        };

        mix.Clamp();

        Assert.False(mix.MidiIn.IsOn);
        Assert.Equal("a", mix.MidiIn.Port);
        Assert.NotNull(mix.MidiOut);
        Assert.False(mix.MidiOut.IsOn);
    }

    /// <summary>The routes survive the song being written down and read back.</summary>
    [Fact]
    public void The_routes_travel_with_the_song()
    {
        var song = Song.CreateDefault();
        song.Normalize();

        song.Mix[2].MidiIn = new TrackMidiRoute { Port = "KeyStep Pro MIDI 1", Channel = 10 };
        song.Mix[2].MidiOut = new TrackMidiRoute { Port = "Synth", Channel = 5 };

        var back = SongStore.Uncopy(SongStore.Copy(song));

        Assert.NotNull(back);
        Assert.Equal(10, back!.Mix[2].MidiIn.Channel);
        Assert.Equal("KeyStep Pro MIDI 1", back.Mix[2].MidiIn.Port);
        Assert.Equal(5, back.Mix[2].MidiOut.Channel);
        Assert.Equal("Synth", back.Mix[2].MidiOut.Port);
        Assert.False(back.Mix[0].MidiIn.IsOn);
    }

    /// <summary>A strip written before routes existed reads back with none.</summary>
    [Fact]
    public void A_strip_written_before_routes_reads_back_off()
    {
        var mix = System.Text.Json.JsonSerializer.Deserialize<TrackMix>("{\"Volume\":0.5}");

        Assert.NotNull(mix);
        Assert.False(mix!.MidiIn.IsOn);
        Assert.False(mix.MidiOut.IsOn);
    }

    /// <summary>A note on a channel a track listens to belongs to that track, on any port when it names none.</summary>
    [Fact]
    public void A_note_goes_to_every_track_listening_on_its_channel()
    {
        var mix = Mix(4);
        mix[0].MidiIn = new TrackMidiRoute { Channel = 10 };
        mix[1].MidiIn = new TrackMidiRoute { Port = "keystep pro midi 1", Channel = 2 };
        mix[3].MidiIn = new TrackMidiRoute { Port = "KeyStep Pro MIDI 1", Channel = 10 };

        Assert.Equal(new[] { 0, 3 }, _routes.TracksFor(mix, "KeyStep Pro MIDI 1", 10));
        Assert.Equal(new[] { 1 }, _routes.TracksFor(mix, "KeyStep Pro MIDI 1", 2));
        Assert.Equal(new[] { 0 }, _routes.TracksFor(mix, "MPD218 Port A", 10));
    }

    /// <summary>Nothing listens to a channel nobody picked, a port nobody named, or an off route.</summary>
    [Fact]
    public void A_note_nobody_listens_for_goes_to_no_track()
    {
        var mix = Mix(3);
        mix[0].MidiIn = new TrackMidiRoute { Port = "KeyStep Pro MIDI 1", Channel = 1 };
        mix[1].MidiIn = new TrackMidiRoute { Port = "KeyStep Pro MIDI 1", Channel = 0 };

        Assert.Empty(_routes.TracksFor(mix, "KeyStep Pro MIDI 1", 5));
        Assert.Empty(_routes.TracksFor(mix, "MiniLab", 1));
        Assert.Empty(_routes.TracksFor(mix, "KeyStep Pro MIDI 1", 0));
        Assert.Empty(_routes.TracksFor(mix, null, 1));
        Assert.Empty(_routes.TracksFor(null, "KeyStep Pro MIDI 1", 1));
    }

    /// <summary>The ports a song needs open are the ones its tracks name, once each.</summary>
    [Fact]
    public void The_ports_to_open_are_the_named_ones_that_are_on()
    {
        var mix = Mix(5);
        mix[0].MidiIn = new TrackMidiRoute { Port = "KeyStep Pro MIDI 1", Channel = 1 };
        mix[1].MidiIn = new TrackMidiRoute { Port = "keystep pro midi 1", Channel = 2 };
        mix[2].MidiIn = new TrackMidiRoute { Channel = 10 };
        mix[3].MidiIn = new TrackMidiRoute { Port = "Off Port", Channel = 0 };
        mix[4].MidiOut = new TrackMidiRoute { Port = "Out Only", Channel = 4 };

        Assert.Equal(new[] { "KeyStep Pro MIDI 1" }, _routes.InputPorts(mix));
        Assert.Empty(_routes.InputPorts(null));
    }

    /// <summary>A route out needs a port as well as a channel, and a track past the end has none.</summary>
    [Fact]
    public void A_track_sends_only_with_a_port_and_a_channel()
    {
        var mix = Mix(3);
        mix[0].MidiOut = new TrackMidiRoute { Port = "Synth", Channel = 3 };
        mix[1].MidiOut = new TrackMidiRoute { Channel = 3 };
        mix[2].MidiOut = new TrackMidiRoute { Port = "Synth", Channel = 0 };

        Assert.Equal("Synth", _routes.OutFor(mix, 0)?.Port);
        Assert.Null(_routes.OutFor(mix, 1));
        Assert.Null(_routes.OutFor(mix, 2));
        Assert.Null(_routes.OutFor(mix, 3));
        Assert.Null(_routes.OutFor(mix, -1));
        Assert.Null(_routes.OutFor(null, 0));
    }

    /// <summary>A note goes back to the number it came in as, and one outside MIDI's range has none.</summary>
    [Fact]
    public void A_note_turns_back_into_its_midi_number()
    {
        Assert.True(_wire.TryNote(60, out var middle));
        Assert.True(_wire.TryMidi(middle, out int number));
        Assert.Equal(60, number);

        Assert.False(_wire.TryMidi(Note.Off, out _));
        Assert.False(_wire.TryMidi(Note.Empty, out _));
    }

    /// <summary>A blank volume column sends a full key, and a note is never sent at nought, which would be its own release.</summary>
    [Theory]
    [InlineData(TrackerCell.NoVolume, 127)]
    [InlineData(0x80, 127)]
    [InlineData(0x7F, 127)]
    [InlineData(0x40, 64)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    public void A_volume_becomes_a_velocity(int volume, int velocity) =>
        Assert.Equal(velocity, _wire.VelocityFor(volume));

    /// <summary>A note sent out is a note on to the track's port and channel.</summary>
    [Fact]
    public void A_note_out_is_sent_to_the_tracks_port_and_channel()
    {
        var (midi, out_) = Out();
        var mix = Mix(2);
        mix[1].MidiOut = new TrackMidiRoute { Port = "Synth", Channel = 3 };

        out_.NoteOn(mix, 1, 0, NoteAt(60), 100);

        Assert.Equal(("Synth", "92 3C 64"), midi.Said.Single());
    }

    /// <summary>A second note in the same voice lets go of the first before it starts.</summary>
    [Fact]
    public void A_new_note_in_a_voice_ends_the_one_before()
    {
        var (midi, out_) = Out();
        var mix = Mix(1);
        mix[0].MidiOut = new TrackMidiRoute { Port = "Synth", Channel = 1 };

        out_.NoteOn(mix, 0, 0, NoteAt(60), 100);
        out_.NoteOn(mix, 0, 0, NoteAt(62), 90);
        out_.NoteOn(mix, 0, 1, NoteAt(64), 80);

        Assert.Equal(
            new[] { ("Synth", "90 3C 64"), ("Synth", "80 3C 00"), ("Synth", "90 3E 5A"), ("Synth", "90 40 50") },
            midi.Said);
    }

    /// <summary>A note is let go of where it was sent, even after the route has moved.</summary>
    [Fact]
    public void A_note_ends_where_it_was_sent()
    {
        var (midi, out_) = Out();
        var mix = Mix(1);
        mix[0].MidiOut = new TrackMidiRoute { Port = "Synth", Channel = 1 };

        out_.NoteOn(mix, 0, 0, NoteAt(60), 100);

        mix[0].MidiOut = new TrackMidiRoute { Port = "Other", Channel = 9 };

        out_.NoteOff(0, 0);
        out_.NoteOff(0, 0);

        Assert.Equal(new[] { ("Synth", "90 3C 64"), ("Synth", "80 3C 00") }, midi.Said);
    }

    /// <summary>Stopping lets go of everything still held, once.</summary>
    [Fact]
    public void Everything_held_is_let_go_of_together()
    {
        var (midi, out_) = Out();
        var mix = Mix(2);
        mix[0].MidiOut = new TrackMidiRoute { Port = "A", Channel = 1 };
        mix[1].MidiOut = new TrackMidiRoute { Port = "B", Channel = 2 };

        out_.NoteOn(mix, 0, 0, NoteAt(60), 100);
        out_.NoteOn(mix, 1, 3, NoteAt(48), 100);

        midi.Said.Clear();

        out_.AllOff();
        out_.AllOff();

        Assert.Equal(2, midi.Said.Count);
        Assert.Contains(("A", "80 3C 00"), midi.Said);
        Assert.Contains(("B", "81 30 00"), midi.Said);
    }

    /// <summary>A track with no route out, a note with no number and a track past the end send nothing.</summary>
    [Fact]
    public void Nothing_is_sent_without_a_route_or_a_note()
    {
        var (midi, out_) = Out();
        var mix = Mix(1);

        out_.NoteOn(mix, 0, 0, NoteAt(60), 100);

        mix[0].MidiOut = new TrackMidiRoute { Port = "Synth", Channel = 1 };

        out_.NoteOn(mix, 0, 0, Note.Off, 100);
        out_.NoteOn(mix, 5, 0, NoteAt(60), 100);
        out_.NoteOn(null, 0, 0, NoteAt(60), 100);
        out_.NoteOff(0, 0);

        Assert.Empty(midi.Said);
    }

    /// <summary>The ports a song sends to are opened before the first note, so the clock never waits on one.</summary>
    [Fact]
    public void Ports_are_opened_before_playing()
    {
        var (midi, out_) = Out();
        var mix = Mix(3);
        mix[0].MidiOut = new TrackMidiRoute { Port = "Synth", Channel = 1 };
        mix[1].MidiOut = new TrackMidiRoute { Port = "synth", Channel = 2 };
        mix[2].MidiOut = new TrackMidiRoute { Port = "Nowhere", Channel = 0 };

        out_.Prepare(mix);
        out_.Prepare(null);

        Assert.Equal(new[] { "Synth" }, midi.Opened);
    }

    /// <summary>A note on a listened channel is claimed and played on the track, and its release follows it.</summary>
    [Fact]
    public void A_listened_note_is_played_on_its_track_and_claimed()
    {
        var mix = Mix(4);
        mix[3].MidiIn = new TrackMidiRoute { Channel = 10 };
        var notes = new TrackNotes();
        var router = new MidiTrackRouter(notes, () => mix);

        Assert.True(router.Handle(NoteMessage("KeyStep Pro MIDI 1", 10, 36, 100, on: true)));
        Assert.True(router.Handle(NoteMessage("KeyStep Pro MIDI 1", 10, 36, 0, on: false)));

        Assert.Equal(new[] { "down 3 C-2 100", "up 3 C-2" }, notes.Did);
    }

    /// <summary>What no track listens for is left for the cursor, and anything that is not a note is never claimed.</summary>
    [Fact]
    public void What_no_track_listens_for_is_not_claimed()
    {
        var mix = Mix(2);
        mix[0].MidiIn = new TrackMidiRoute { Channel = 1 };
        var notes = new TrackNotes();
        var router = new MidiTrackRouter(notes, () => mix);

        Assert.False(router.Handle(NoteMessage("KeyStep Pro MIDI 1", 2, 60, 100, on: true)));
        Assert.False(router.Handle(new MidiMessage
        {
            Device = "KeyStep Pro MIDI 1", Type = MidiMessageType.ControlChange, Channel = 1, Value = 74, Data = 5
        }));
        Assert.False(router.Handle(NoteMessage("KeyStep Pro MIDI 1", 1, 200, 100, on: true)));
        Assert.False(router.Handle(null!));
        Assert.False(new MidiTrackRouter(notes, () => null).Handle(NoteMessage("x", 1, 60, 1, on: true)));

        Assert.Empty(notes.Did);
    }

    /// <summary>A note a track claims does not also reach the cursor's track, and one it does not claim still does.</summary>
    [Fact]
    public void A_claimed_note_skips_the_tracker_job()
    {
        var cfg = new MidiConfig();
        new MidiPortBindings().SetRole(cfg.Devices, "KeyStep Pro MIDI 1", MidiPortRole.Tracker);

        var reached = new List<int>();
        var dispatcher = new MidiDispatcher(cfg, null, msg => reached.Add(msg.Channel),
            tracks: msg => msg.Channel == 10);

        dispatcher.Handle(NoteMessage("KeyStep Pro MIDI 1", 10, 36, 100, on: true));
        dispatcher.Handle(NoteMessage("KeyStep Pro MIDI 1", 1, 60, 100, on: true));

        Assert.Equal(new[] { 1 }, reached);
    }

    /// <summary>A port a track listens to is claimed even when SETTINGS gave it no job at all.</summary>
    [Fact]
    public void A_port_with_no_job_still_reaches_its_track()
    {
        var cfg = new MidiConfig();
        var claimed = new List<int>();

        var dispatcher = new MidiDispatcher(cfg, null, _ => throw new InvalidOperationException("no job"),
            tracks: msg => { claimed.Add(msg.Value); return true; });

        dispatcher.Handle(NoteMessage("KeyStep Pro MIDI 1", 10, 36, 100, on: true));

        Assert.Equal(new[] { 36 }, claimed);
    }

    /// <summary>The ports a song listens to are opened beside the ones with a job, once each.</summary>
    [Fact]
    public void A_song_port_is_opened_beside_the_ones_with_a_job()
    {
        var cfg = new MidiConfig();
        var bindings = new MidiPortBindings();
        bindings.SetRole(cfg.Devices, "MiniLab", MidiPortRole.Tracker);

        var open = bindings.Listening(cfg, new[] { "KeyStep Pro MIDI 1", "minilab", " ", "" });

        Assert.Equal(new[] { "MiniLab", "KeyStep Pro MIDI 1" }, open);
        Assert.Equal(new[] { "MiniLab" }, bindings.Listening(cfg, null));
    }

    /// <summary>A strip list of this many default strips.</summary>
    private static List<TrackMix> Mix(int tracks) =>
        Enumerable.Range(0, tracks).Select(_ => new TrackMix()).ToList();

    /// <summary>The tracker note a MIDI number means.</summary>
    private Note NoteAt(int midi)
    {
        Assert.True(_wire.TryNote(midi, out var note));
        return note;
    }

    /// <summary>A fresh route out over a service that writes down what it is asked.</summary>
    private static (Wire Midi, ITrackMidiOut Out) Out()
    {
        var midi = new Wire();
        return (midi, new TrackMidiOut(midi));
    }

    /// <summary>A note message as the service hands one over, channel counted from one.</summary>
    private static MidiMessage NoteMessage(string device, int channel, int note, int velocity, bool on) => new()
    {
        Device = device, Type = MidiMessageType.Note, Channel = channel, Value = note, Data = velocity, IsOn = on
    };

    /// <summary>Where claimed notes land, written down as words.</summary>
    private sealed class TrackNotes : ITrackNotes
    {
        public readonly List<string> Did = new();

        /// <inheritdoc/>
        public void PressOnTrack(int track, Note note, int volume) =>
            Did.Add("down " + track + " " + note + " " + volume);

        /// <inheritdoc/>
        public void ReleaseOnTrack(int track, Note note) => Did.Add("up " + track + " " + note);
    }

    /// <summary>A MIDI service that opens anything and writes every message down as hex.</summary>
    private sealed class Wire : IMidiService
    {
        public readonly List<(string, string)> Said = new();

        public readonly List<string> Opened = new();

        /// <inheritdoc/>
        public IReadOnlyList<string> GetInputDevices() => Array.Empty<string>();

        /// <inheritdoc/>
        public IReadOnlyList<string> GetOutputDevices() => Array.Empty<string>();

        /// <inheritdoc/>
        public IReadOnlyList<string> OpenDevices => Array.Empty<string>();

        /// <inheritdoc/>
        public bool Open(string device) => true;

        /// <inheritdoc/>
        public bool OpenFor(string device)
        {
            Opened.Add(device);
            return true;
        }

        /// <inheritdoc/>
        public void Close(string device) { }

        /// <inheritdoc/>
        public void CloseAll() { }

        /// <inheritdoc/>
        public event EventHandler<MidiMessage>? MessageReceived { add { } remove { } }

        /// <inheritdoc/>
        public bool Send(string device, byte[] bytes)
        {
            Said.Add((device, string.Join(" ", bytes.Select(one => one.ToString("X2")))));
            return true;
        }

        /// <inheritdoc/>
        public void Dispose() { }
    }
}
