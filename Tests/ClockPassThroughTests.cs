using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A clock arriving on one port and leaving by another: passing time on while running on it.
/// </summary>
/// <remarks>
/// **A machine in the middle of a chain is the case this covers**, and it is an ordinary studio
/// arrangement rather than an exotic one: something else holds the clock, this transport follows
/// it, and a third device is plugged into this one because there is nowhere else to plug it.
/// Before this the third device was sent a start and a stop and no time at all, so it began and
/// then stood still, which reads as a broken cable rather than as a missing feature.
///
/// Three rules, and each one is a different way of getting it wrong:
///
/// - **The bytes are passed on unchanged.** The one that matters is the pointer:
///   <see cref="A_pointer_is_passed_on_exactly_as_it_arrived"/> feeds a position that does not
///   survive a trip through a line and back, which is what any version that re-derived it would
///   do.
/// - **Only from the port being followed**, so a control surface's transport button somewhere
///   else does not put a start on the outputs.
/// - **And exactly once.** <see cref="Following_sends_no_transport_of_its_own"/> is the doubling
///   guard: the transport announces its own start and stop when it is the master, and while it is
///   following, the master's have already gone out.
///
/// **Every port here is a made-up name and nothing looks one up**, deliberately. The ports are
/// strings handed to a bench that writes down what it was told, so each of these answers the same
/// on a machine with a rack of gear on it and on one with no MIDI at all. That is not a detail:
/// this suite has already had one file asserting what the machine it was written on happened to
/// have, and a test that reads as a fact about the code and is really a fact about the desk
/// reports green for the rest of its life.
/// </remarks>
public class ClockPassThroughTests
{
    /// <summary>How long to wait for the clock thread to get somewhere before giving up.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>The port standing in for whatever holds the clock.</summary>
    private const string Master = "clock in";

    /// <summary>And the one standing in for whatever is being driven.</summary>
    private const string Driven = "clock out";

    /// <summary>A deck that writes down what it was told rather than sending anything.</summary>
    private sealed class Bench : IMidiClockDeck
    {
        /// <summary>What was said, in order.</summary>
        public readonly List<string> Said = new();

        /// <summary>Whether it claims to be driving anything.</summary>
        public bool Driving = true;

        /// <inheritdoc/>
        public bool IsDriving => Driving;

        /// <inheritdoc/>
        public void Drive(IReadOnlyList<string>? outputs)
        {
        }

        /// <inheritdoc/>
        public void Play(int line, int linesPerBeat)
        {
            lock (Said) Said.Add("play " + line + "/" + linesPerBeat);
        }

        /// <inheritdoc/>
        public void Ticks(int howMany)
        {
            lock (Said) Said.Add("tick " + howMany);
        }

        /// <inheritdoc/>
        public void Halt()
        {
            lock (Said) Said.Add("halt");
        }

        /// <inheritdoc/>
        public void Begin()
        {
            lock (Said) Said.Add("begin");
        }

        /// <inheritdoc/>
        public void Resume()
        {
            lock (Said) Said.Add("resume");
        }

        /// <inheritdoc/>
        public void Place(int at)
        {
            lock (Said) Said.Add("place " + at);
        }

        /// <summary>What was said, as one string.</summary>
        public string Words
        {
            get { lock (Said) return string.Join(", ", Said); }
        }
    }

    /// <summary>The settings, following one port and driving another.</summary>
    private static MidiConfig Settings()
    {
        var cfg = new MidiConfig
        {
            ClockSource = MidiClockSource.Followed,
            ClockPort = Master
        };

        cfg.ClockOutputs.Add(Driven);

        return cfg;
    }

    /// <summary>A dispatcher over a follower that is following, and a deck to pass on to.</summary>
    private static (MidiDispatcher Wire, MidiClockFollow Follow, Bench Bench) Chain()
    {
        var follow = new MidiClockFollow();

        follow.Follow(true);

        var bench = new Bench();

        var dispatcher = new MidiDispatcher(Settings(), null, null, follow: follow, deck: bench);

        return (dispatcher, follow, bench);
    }

    /// <summary>One realtime byte, as it arrives off a port.</summary>
    private static MidiMessage Byte(int value, string device, int data = 0) => new()
    {
        Device = device,
        Type = MidiMessageType.Realtime,
        Channel = 0,
        Value = value,
        Data = data,
        IsOn = false
    };

    /// <summary>Waits for something to become true, or gives up.</summary>
    private static bool Until(Func<bool> said)
    {
        var clock = Stopwatch.StartNew();

        while (clock.Elapsed < Patience)
        {
            if (said()) return true;

            Thread.Sleep(2);
        }

        return said();
    }

    /// <summary>A song of that many lines, at a tempo and a division worth measuring.</summary>
    private static Song Of(int lines, double bpm = 120, int linesPerBeat = 4)
    {
        var song = new Song { Bpm = bpm, LinesPerBeat = linesPerBeat };

        song.Patterns.Add(new Pattern(lines, song.TrackCount) { Name = "P" });
        song.Order.Add(0);
        song.Normalize();

        return song;
    }

    /// <summary>Every one of the five is passed on, in the order it arrived.</summary>
    [Fact]
    public void All_five_are_passed_on_in_order()
    {
        var (wire, _, bench) = Chain();

        wire.Handle(Byte(0xFA, Master));
        wire.Handle(Byte(0xF8, Master));
        wire.Handle(Byte(0xF8, Master));
        wire.Handle(Byte(0xF2, Master, 16));
        wire.Handle(Byte(0xFB, Master));
        wire.Handle(Byte(0xFC, Master));

        Assert.Equal("begin, tick 1, tick 1, place 16, resume, halt", bench.Words);
    }

    /// <summary>
    /// **A pointer is passed on exactly as it arrived**, which is the whole of why the deck grew
    /// members of its own for this.
    /// </summary>
    /// <remarks>
    /// The obvious implementation is to read the pointer into a line, hand the line to
    /// <see cref="IMidiClockDeck.Play"/>, and let it work the pointer out again. It is wrong, and
    /// the number here is chosen to prove it rather than to be pretty: at six lines to the beat,
    /// <c>LineAtPointer(5)</c> is line 7 and <c>PointerFor(7)</c> is sixteenth 4, so that version
    /// answers 4 and every relocation would lose a sixteenth on its way through. A chain of three
    /// machines would lose one apiece.
    ///
    /// The second assertion is that arithmetic on its own, so the test says why the first one
    /// matters rather than only that it holds.
    /// </remarks>
    [Fact]
    public void A_pointer_is_passed_on_exactly_as_it_arrived()
    {
        var (wire, _, bench) = Chain();

        wire.Handle(Byte(0xF2, Master, 5));

        Assert.Equal("place 5", bench.Words);

        var grid = new MidiClockGrid();

        Assert.Equal(4, grid.PointerFor(grid.LineAtPointer(5, 6), 6));
    }

    /// <summary>Nothing is passed on while nothing is being followed.</summary>
    /// <remarks>
    /// A machine on its own clock is the master, and there the ticks come off its own stopwatch in
    /// <c>TrackerPlayer</c>. A clock byte arriving from somewhere else must not be added to them,
    /// or the two would be summed and everything downstream would run fast.
    /// </remarks>
    [Fact]
    public void Nothing_is_passed_on_when_nothing_is_followed()
    {
        var follow = new MidiClockFollow();
        var bench = new Bench();

        var wire = new MidiDispatcher(Settings(), null, null, follow: follow, deck: bench);

        wire.Handle(Byte(0xFA, Master));
        wire.Handle(Byte(0xF8, Master));

        Assert.Equal("", bench.Words);
    }

    /// <summary>And nothing from a port that is not the one being followed.</summary>
    [Fact]
    public void Nothing_is_passed_on_from_another_port()
    {
        var (wire, _, bench) = Chain();

        wire.Handle(Byte(0xFA, "some other surface"));
        wire.Handle(Byte(0xF8, "some other surface"));

        Assert.Equal("", bench.Words);
    }

    /// <summary>A deck driving nothing is not asked to pass anything on.</summary>
    /// <remarks>
    /// The ordinary machine, which follows a clock and drives nothing. The follower still hears
    /// every byte, since that is what the transport runs on.
    /// </remarks>
    [Fact]
    public void A_deck_driving_nothing_is_left_alone_and_the_follower_is_not()
    {
        var (wire, follow, bench) = Chain();

        bench.Driving = false;

        wire.Handle(Byte(0xFA, Master));
        wire.Handle(Byte(0xF8, Master));
        wire.Handle(Byte(0xF8, Master));

        Assert.Equal("", bench.Words);
        Assert.Equal(2, follow.Ticks);
    }

    /// <summary>The follower is still told everything that is passed on.</summary>
    /// <remarks>
    /// Passing on and following are two jobs at one moment, and a version that returned after the
    /// first would leave this transport standing still while the driven device played.
    /// </remarks>
    [Fact]
    public void Passing_on_does_not_stop_the_follower_being_told()
    {
        var (wire, follow, _) = Chain();

        wire.Handle(Byte(0xF2, Master, 32));
        wire.Handle(Byte(0xFB, Master));
        wire.Handle(Byte(0xF8, Master));

        Assert.Equal(32, follow.Pointer);
        Assert.Equal(1, follow.Ticks);
    }

    /// <summary>
    /// **While following, the transport puts no start, stop or tick of its own on the wire.**
    /// </summary>
    /// <remarks>
    /// The doubling guard, and it is the one thing here that could not be seen by reading either
    /// half on its own. The player announces where it began through <c>Said</c> and its stop
    /// through <c>Hushed</c>, which is right when it is the master. Following, the go that moved
    /// it was the master's and has already been passed on, so a second would go out worked from a
    /// line rather than carried, and the driven device is told to start twice.
    ///
    /// **The transport is made to really run rather than merely left alone for a moment**, which
    /// is what stops this passing for the wrong reason: a version that slept and hoped would pass
    /// just as happily on a machine too busy to have started the clock thread at all, and would
    /// then be asserting nothing for the rest of its life.
    ///
    /// **Liveness is counted rather than read off the playhead**, and the first attempt at this
    /// got it wrong in a way worth keeping. Reading <c>Position.Line</c> looks like the obvious
    /// test and is not one: enough ticks are fed here to satisfy every wait at once, so the pass
    /// runs to the end of the pattern and the tail of <c>RunClock</c> puts the playhead back to
    /// the top. The line then reads nought for the same reason it read nought before anything
    /// started, and the two are indistinguishable. How many lines were played cannot go
    /// backwards, so it says what was actually wanted.
    /// </remarks>
    [Fact]
    public void Following_sends_no_transport_of_its_own()
    {
        var follow = new MidiClockFollow();

        follow.Follow(true);
        follow.Start();

        var bench = new Bench();

        using var player = new TrackerPlayer(new SilentAudio())
        {
            ClockDeck = bench,
            ClockFollow = follow
        };

        int lines = 0;

        player.PositionChanged += (_, _) => Interlocked.Increment(ref lines);

        player.Play(Of(16), TrackerPosition.Start, TrackerPlayMode.Pattern);

        for (int at = 0; at < 96; at++) follow.Tick();

        Assert.True(Until(() => Volatile.Read(ref lines) >= 4),
            "the transport never followed the clock: " + Volatile.Read(ref lines) + " line(s) played");

        player.Stop();

        Assert.Equal("", bench.Words);
    }
}
