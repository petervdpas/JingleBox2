using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using JingleBox2.Audio.Records;
using JingleBox2.Midi.Interfaces;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A track's MIDI in and out over a real tracker: the pattern playing out of a port, and a note
/// arriving on a track's channel being played and written into that track.
/// </summary>
public class TrackMidiPlayTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>A note in the pattern on a track that sends goes out, and stopping lets go of it.</summary>
    [Fact]
    public void A_pattern_note_goes_out_and_stopping_lets_go()
    {
        var tracker = Tracker();
        var out_ = new Heard();
        tracker.Player.MidiOut = out_;

        tracker.Song.Mix[1].MidiOut = new TrackMidiRoute { Port = "Synth", Channel = 3 };
        tracker.Song.Patterns[0][0, 1] = new TrackerCell(new Note(48), TrackerCell.NoInstrument, 0x40, TrackerCommand.None);
        tracker.Song.Patterns[0][2, 1] = new TrackerCell(Note.Off, TrackerCell.NoInstrument, TrackerCell.NoVolume, TrackerCommand.None);

        tracker.Player.Play(tracker.Song, TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => out_.Did.Contains("off 1 0")), "the note never went out and ended: " + out_.Says);

        tracker.Player.Stop();

        var did = out_.Did;

        Assert.True(did.IndexOf("prepared") >= 0 && did.IndexOf("prepared") < did.IndexOf("on 1 0 C-4 64"),
            "the ports were not opened before the note: " + out_.Says);
        Assert.True(did.IndexOf("on 1 0 C-4 64") < did.IndexOf("off 1 0"), "the note ended before it began: " + out_.Says);
        Assert.Equal("all off", did.Last());

        tracker.Finished();
    }

    /// <summary>A note on a track's channel is written into that track, not the cursor's, while armed.</summary>
    [Fact]
    public void A_note_on_a_tracks_channel_is_written_into_that_track()
    {
        var tracker = Tracker();
        tracker.IsRecording = true;

        tracker.EnterTrackNote(2, new Note(36), 100);
        tracker.EnterTrackNote(2, new Note(43), 90);

        var pattern = tracker.Song.Patterns[0];

        Assert.Equal(new Note(36), pattern[0, 2, 0].Note);
        Assert.Equal(100, pattern[0, 2, 0].Volume);
        Assert.Equal(new Note(43), pattern[0, 2, 1].Note);
        Assert.Equal(TrackerCell.Empty, pattern[0, 0, 0]);
        Assert.Equal(0, tracker.Cursor.Track);

        tracker.Finished();
    }

    /// <summary>Unarmed, a note on a track's channel writes nothing, and a track past the end is ignored.</summary>
    [Fact]
    public void Unarmed_or_past_the_end_writes_nothing()
    {
        var tracker = Tracker();

        tracker.EnterTrackNote(1, new Note(36), 100);

        tracker.IsRecording = true;
        tracker.EnterTrackNote(99, new Note(36), 100);
        tracker.EnterTrackNote(-1, new Note(36), 100);

        var pattern = tracker.Song.Patterns[0];

        for (int track = 0; track < tracker.Song.TrackCount; track++)
            Assert.Equal(TrackerCell.Empty, pattern[0, track, 0]);

        tracker.Finished();
    }

    /// <summary>A key held down and struck again is not written twice, and letting go frees it.</summary>
    [Fact]
    public void A_held_key_is_written_once()
    {
        var tracker = Tracker();
        tracker.IsRecording = true;

        tracker.EnterTrackNote(1, new Note(36), 100);
        tracker.EnterTrackNote(1, new Note(36), 100);

        var pattern = tracker.Song.Patterns[0];

        Assert.Equal(new Note(36), pattern[0, 1, 0].Note);
        Assert.Equal(1, tracker.Song.ColumnsOn(1));

        tracker.LetTrackNote(1, new Note(36));
        tracker.LetTrackNote(1, new Note(36));

        tracker.Finished();
    }

    /// <summary>A played note goes out of the track's port too, and letting go of it ends it there.</summary>
    [Fact]
    public void A_note_played_in_goes_out_of_the_track()
    {
        var tracker = Tracker();
        var out_ = new Heard();
        tracker.Player.MidiOut = out_;

        tracker.EnterTrackNote(1, new Note(48), 100);
        tracker.LetTrackNote(1, new Note(48));

        Assert.Equal(2, out_.Did.Count);
        Assert.StartsWith("on 1 ", out_.Did[0]);
        Assert.EndsWith(" C-4 100", out_.Did[0]);
        Assert.StartsWith("off 1 ", out_.Did[1]);
        Assert.NotEqual("on 1 0 C-4 100", out_.Did[0]);

        tracker.Finished();
    }

    /// <summary>
    /// A block built before the machine's ports were known offers them once they are, without the cursor moving.
    /// </summary>
    /// <remarks>
    /// The tracker points its strip at the first track while it is being built, which is before
    /// anybody has told it what ports there are, so the first track's block offered any port and
    /// nothing else until the cursor went to another track.
    /// </remarks>
    [Fact]
    public void The_block_offers_ports_told_after_it_was_built()
    {
        var tracker = Tracker();

        Assert.NotNull(tracker.TrackEffect.Midi);
        Assert.DoesNotContain("KeyStep Pro MIDI 1", tracker.TrackEffect.Midi!.InPorts);

        tracker.MidiInputs = () => new[] { "KeyStep Pro MIDI 1" };
        tracker.MidiOutputs = () => new[] { "KeyStep Pro MIDI 1" };
        tracker.ListMidiPorts();

        Assert.Contains("KeyStep Pro MIDI 1", tracker.TrackEffect.Midi!.InPorts);
        Assert.Contains("KeyStep Pro MIDI 1", tracker.TrackEffect.Midi!.OutPorts);
        Assert.Equal(0, tracker.Cursor.Track);

        tracker.Finished();
    }

    /// <summary>
    /// The line a moment belongs to is the nearest one, so a note arriving just before a line starts lands on that line.
    /// </summary>
    /// <remarks>
    /// A KeyStep Pro starting the transport starts its own sequencer about twenty milliseconds
    /// before this one's clock gets going, so every note it sends arrives just ahead of the line
    /// it belongs to. Written on the line last drawn, all of them landed one line early.
    /// Two second lines, so nothing here depends on how fast the machine running it is.
    /// </remarks>
    [Fact]
    public void A_moment_belongs_to_the_nearest_line()
    {
        var tracker = Tracker();
        tracker.Song.Bpm = 30;
        tracker.Song.LinesPerBeat = 1;

        var player = tracker.Player;
        Assert.Equal(TrackerPosition.Start, player.NearestLine(Stopwatch.GetTimestamp()));

        player.Play(tracker.Song, TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => player.Position.Line == 1), "the clock never reached line 1");

        long now = Stopwatch.GetTimestamp();
        long second = Stopwatch.Frequency;

        Assert.Equal(1, player.NearestLine(now).Line);
        Assert.Equal(2, player.NearestLine(now + second + second / 2).Line);
        Assert.Equal(1, player.NearestLine(now - second * 10).Line);

        player.Stop();
        tracker.Finished();
    }

    /// <summary>
    /// A note that arrives late in a line, while playing and armed, is written on the line it is nearest.
    /// </summary>
    [Fact]
    public void A_note_late_in_a_line_is_written_on_the_next()
    {
        var tracker = Tracker();
        tracker.Song.Bpm = 30;
        tracker.Song.LinesPerBeat = 1;
        tracker.IsRecording = true;

        var player = tracker.Player;
        player.Play(tracker.Song, TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => player.Position.Line == 1), "the clock never reached line 1");

        long late = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 3 / 2;

        tracker.EnterTrackNote(3, new Note(36), 100, late);

        var pattern = tracker.Song.Patterns[0];

        Assert.Equal(new Note(36), pattern[2, 3, 0].Note);
        Assert.Equal(TrackerCell.Empty, pattern[1, 3, 0]);

        player.Stop();
        tracker.Finished();
    }

    /// <summary>
    /// A key played on the cursor's track late in a line, while playing and armed, is written on the next line too.
    /// </summary>
    [Fact]
    public void A_keyboard_note_late_in_a_line_is_written_on_the_next()
    {
        var tracker = Tracker();
        tracker.Song.Bpm = 30;
        tracker.Song.LinesPerBeat = 1;
        tracker.IsRecording = true;

        var player = tracker.Player;
        player.Play(tracker.Song, TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => player.Position.Line == 1), "the clock never reached line 1");

        long now = Stopwatch.GetTimestamp();

        tracker.EnterNote(new Note(40), 90, now + Stopwatch.Frequency * 3 / 2);
        tracker.LetNote(new Note(40));
        tracker.EnterNote(new Note(41), 90, now);

        var pattern = tracker.Song.Patterns[0];

        Assert.Equal(new Note(40), pattern[2, 0, 0].Note);
        Assert.Equal(new Note(41), pattern[1, 0, 0].Note);

        player.Stop();
        tracker.Finished();
    }

    private static TrackerViewModel Tracker()
    {
        var tracker = new TrackerViewModel(
            new QuietAudio(),
            new SoundMachineRack(),
            new ObservableCollection<Recording>(),
            new SoundMachineProjects());

        tracker.Song.Bpm = 400;
        tracker.Song.LinesPerBeat = 16;
        tracker.Player.Loop = false;

        return tracker;
    }

    private static bool Until(Func<bool> said)
    {
        var clock = Stopwatch.StartNew();

        while (clock.Elapsed < Patience)
        {
            if (said()) return true;
            Thread.Sleep(5);
        }

        return said();
    }

    /// <summary>A route out that writes down what it was asked, from whichever thread asked.</summary>
    private sealed class Heard : ITrackMidiOut
    {
        private readonly ConcurrentQueue<string> _did = new();

        public List<string> Did => _did.ToList();

        public string Says => string.Join(", ", _did);

        /// <inheritdoc/>
        public void Prepare(IReadOnlyList<TrackMix>? mix) => _did.Enqueue("prepared");

        /// <inheritdoc/>
        public void NoteOn(IReadOnlyList<TrackMix>? mix, int track, int voice, Note note, int velocity) =>
            _did.Enqueue("on " + track + " " + voice + " " + note + " " + velocity);

        /// <inheritdoc/>
        public void NoteOff(int track, int voice) => _did.Enqueue("off " + track + " " + voice);

        /// <inheritdoc/>
        public void AllOff() => _did.Enqueue("all off");
    }
}
