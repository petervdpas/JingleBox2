using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using JingleBox2.Audio.Records;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker.Enums;
using JingleBox2.ViewModels;
using JingleBox2.ViewModels.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A play that arrives while the tracker is already playing does nothing.
/// </summary>
/// <remarks>
/// A KeyStep Pro with Transport send on Both sends two messages for one press, measured on the
/// wire on 2026-09-13: a realtime Start and machine control Play, back to back. Both are read,
/// both are play, and the second one started the song again from the top a few milliseconds
/// into the first. Over a real tracker with a quiet engine, driven through the router a device
/// really goes through.
/// </remarks>
public class DoublePlayTests
{
    /// <summary>How long a clock is given to say something before the test gives up.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>
    /// One press of a KeyStep Pro's play button is one pass.
    /// </summary>
    [Fact]
    public void Start_and_machine_control_play_together_start_the_song_once()
    {
        var tracker = Tracker();
        var router = Router(tracker);

        router.Handle(Realtime(0xFA));

        Assert.True(Until(() => tracker.Player.Position.Line >= 4), "the clock never moved");

        int was = tracker.Player.Position.Line;

        router.Handle(Mmc(0xF0, 0x7F, 0x7F, 0x06, 0x02, 0xF7));

        Assert.True(tracker.Player.IsPlaying, "the second play stopped the song");
        Assert.True(
            tracker.Player.Position.Line >= was,
            "the second play started the song again at line " + tracker.Player.Position.Line
            + " after it had reached line " + was);

        Done(tracker);
    }

    /// <summary>
    /// A stopped tracker is started by the first play, which is the case the guard must not reach.
    /// </summary>
    [Fact]
    public void A_stopped_tracker_is_started_by_play()
    {
        var tracker = Tracker();

        ((ITransportDeck)tracker).Play();

        Assert.True(tracker.Player.IsPlaying, "play on a stopped tracker did nothing");

        Done(tracker);
    }

    /// <summary>
    /// A paused tracker still answers play, since paused is not playing.
    /// </summary>
    [Fact]
    public void A_paused_tracker_is_not_refused()
    {
        var tracker = Tracker();
        ITransportDeck deck = tracker;

        deck.Play();
        Assert.True(Until(() => tracker.Player.Position.Line >= 1), "the clock never moved");

        deck.Pause();
        Assert.True(tracker.Player.IsPaused, "the pause did not take");

        deck.Play();

        Assert.True(tracker.Player.IsPlaying, "play after a pause was refused");

        Done(tracker);
    }

    /// <summary>
    /// A stop between two plays lets the second one start again, since it is no longer a repeat.
    /// </summary>
    [Fact]
    public void Play_after_a_stop_starts_again()
    {
        var tracker = Tracker();
        var router = Router(tracker);

        router.Handle(Realtime(0xFA));
        Assert.True(Until(() => tracker.Player.Position.Line >= 1), "the clock never moved");

        router.Handle(Realtime(0xFC));
        router.Handle(Mmc(0xF0, 0x7F, 0x7F, 0x06, 0x01, 0xF7));
        Assert.False(tracker.Player.IsPlaying, "stop did not stop");

        router.Handle(Realtime(0xFA));

        Assert.True(tracker.Player.IsPlaying, "play after a stop was refused");

        Done(tracker);
    }

    /// <summary>A tracker over an engine that sounds nothing, slow enough to read and long enough not to end.</summary>
    private static TrackerViewModel Tracker()
    {
        var tracker = new TrackerViewModel(
            new QuietAudio(),
            new SoundMachineRack(),
            new ObservableCollection<Recording>(),
            new SoundMachineProjects());

        tracker.Song.Bpm = 200;
        tracker.Song.LinesPerBeat = 16;
        tracker.PlayMode = TrackerPlayMode.Pattern;
        tracker.Player.Loop = false;

        return tracker;
    }

    /// <summary>The transport router as the application wires it, with the tracker as the only deck.</summary>
    private static MidiTransportRouter Router(TrackerViewModel tracker) =>
        new(new TransportAdapter(new TransportSwitch(() => tracker, tracker)));

    /// <summary>Stops the clock and lets the tracker go.</summary>
    private static void Done(TrackerViewModel tracker)
    {
        tracker.Player.Stop();
        tracker.Finished();
    }

    /// <summary>A realtime transport byte off a KeyStep Pro's port.</summary>
    private static MidiMessage Realtime(int status) => new()
    {
        Device = "KeyStep Pro MIDI 1", Type = MidiMessageType.Realtime,
        Channel = 0, Value = status, Data = 0, IsOn = false
    };

    /// <summary>A machine control message off the same port.</summary>
    private static MidiMessage Mmc(params byte[] bytes) => new()
    {
        Device = "KeyStep Pro MIDI 1", Type = MidiMessageType.SystemExclusive,
        Channel = 0, Value = 0, Data = 0, IsOn = false, Bytes = bytes
    };

    /// <summary>Waits for the clock to say something is true, or gives up and answers false.</summary>
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
}
