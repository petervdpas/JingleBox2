using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The commands that work inside a line, heard from outside a real player: what leaves a track's
/// MIDI out, and when.
/// </summary>
/// <remarks>
/// The module's arithmetic is <see cref="LineCommandsTests"/>. These say the player really waits
/// for each tick rather than playing a line's events all at once, which is the half no amount of
/// arithmetic can show. A line is a quarter of a second here, so a tick is about 21 ms and the
/// margins are wide enough for a loaded machine.
/// </remarks>
public class TickCommandPlayTests
{
    /// <summary>How long anything here is waited for.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(3);

    /// <summary>One pattern of four lines at a quarter of a second each, with the cells given on line nought.</summary>
    private static Song Of(params (int Track, TrackerCell Cell)[] cells)
    {
        var song = new Song { Bpm = 60, LinesPerBeat = 4 };

        song.Patterns.Add(new Pattern(4, song.TrackCount) { Name = "P" });
        song.Order.Add(0);
        song.Normalize();

        foreach (var (track, cell) in cells) song.Patterns[0][0, track] = cell;

        return song;
    }

    /// <summary>A cell holding a note and a command.</summary>
    private static TrackerCell Cell(int semitone, char letter, int parameter) =>
        new(new Note(semitone), TrackerCell.NoInstrument, TrackerCell.NoVolume, new TrackerCommand(letter, parameter));

    /// <summary>Plays the song once and gives back what left the MIDI out, with when.</summary>
    private static List<(double Ms, string What)> Played(Song song, Func<List<(double Ms, string What)>, bool> enough)
    {
        using var player = new TrackerPlayer(new SilentAudio());
        var heard = new Heard();

        player.MidiOut = heard;
        player.Loop = false;
        player.Play(song, TrackerPosition.Start, TrackerPlayMode.Pattern);

        var clock = Stopwatch.StartNew();

        while (clock.Elapsed < Patience && !enough(heard.Did)) Thread.Sleep(5);

        player.Stop();

        return heard.Did;
    }

    /// <summary>A delayed note leaves half a line after the plain note beside it.</summary>
    [Fact]
    public void A_delayed_note_leaves_late()
    {
        var did = Played(Of((0, Cell(48, 'Q', 6)), (1, Cell(50, TrackerCommand.NoCommand, 0))),
            all => all.Count(one => one.What.StartsWith("on")) >= 2);

        var plain = did.First(one => one.What.StartsWith("on 1"));
        var late = did.First(one => one.What.StartsWith("on 0"));

        Assert.InRange(late.Ms - plain.Ms, 90, 200);
    }

    /// <summary>A cut lets go of the note a quarter of a line after it began.</summary>
    [Fact]
    public void A_cut_lets_go_at_its_tick()
    {
        var did = Played(Of((0, Cell(48, 'C', 3))), all => all.Any(one => one.What == "off 0 0"));

        var on = did.First(one => one.What.StartsWith("on 0"));
        var off = did.First(one => one.What == "off 0 0");

        Assert.InRange(off.Ms - on.Ms, 40, 150);
    }

    /// <summary>A retrigger every four ticks plays the note three times within the line.</summary>
    [Fact]
    public void A_retrigger_plays_three_times()
    {
        var did = Played(Of((0, Cell(48, 'R', 0x04))), all => all.Count(one => one.What.StartsWith("on 0")) >= 3);

        var ons = did.Where(one => one.What.StartsWith("on 0")).ToArray();

        Assert.Equal(3, ons.Length);
        Assert.All(ons, one => Assert.Contains("C-4", one.What));
        Assert.InRange(ons[2].Ms - ons[0].Ms, 120, 260);
    }

    /// <summary>An arpeggio on a MIDI out plays the three notes in turn, and the next line goes back.</summary>
    [Fact]
    public void An_arpeggio_steps_through_its_notes()
    {
        var did = Played(Of((0, Cell(48, 'A', 0x47))), all => all.Count(one => one.What.StartsWith("on 0")) >= 13);

        var notes = did.Where(one => one.What.StartsWith("on 0")).Select(one => one.What.Split(' ')[3]).Take(13).ToArray();

        Assert.Equal(new[] { "C-4", "E-4", "G-4", "C-4", "E-4", "G-4", "C-4", "E-4", "G-4", "C-4", "E-4", "G-4", "C-4" }, notes);
    }

    /// <summary>A track's MIDI out that writes down what it is told and when.</summary>
    private sealed class Heard : ITrackMidiOut
    {
        /// <summary>When it was made.</summary>
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        /// <summary>What it was told.</summary>
        private readonly ConcurrentQueue<(double, string)> _did = new();

        /// <summary>What it was told, in order, with when.</summary>
        public List<(double Ms, string What)> Did => _did.ToList();

        /// <summary>Writes one thing down.</summary>
        private void Say(string what) => _did.Enqueue((_clock.Elapsed.TotalMilliseconds, what));

        /// <inheritdoc/>
        public void Prepare(IReadOnlyList<TrackMix>? mix) => Say("prepared");

        /// <inheritdoc/>
        public void NoteOn(IReadOnlyList<TrackMix>? mix, int track, int voice, Note note, int velocity) =>
            Say("on " + track + " " + voice + " " + note);

        /// <inheritdoc/>
        public void NoteOff(int track, int voice) => Say("off " + track + " " + voice);

        /// <inheritdoc/>
        public void AllOff() => Say("all off");
    }
}
