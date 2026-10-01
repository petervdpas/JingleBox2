using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using JingleBox2.Audio.Records;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels;
using JingleBox2.ViewModels.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Tempo as an automation lane on the master: the song speeding up or slowing down as it plays,
/// with the tempo saved in it left alone, and automation playing whichever page is in front.
/// </summary>
public class TempoLaneTests
{
    /// <summary>How long anything here is waited for.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(4);

    /// <summary>A page that is never the mixer, which is what the tracker page is.</summary>
    private sealed class OnTracker : IPageInFront
    {
        /// <inheritdoc/>
        public bool Mixer => false;

        /// <inheritdoc/>
        public bool Pads => false;
    }

    /// <summary>A tracker on a song at the tempo given, with its automation reached through targets held to the tracker page.</summary>
    private static (TrackerViewModel Tracker, ControlTargets Targets) Made(double bpm)
    {
        var tracker = new TrackerViewModel(new QuietAudio(), new SoundMachineRack(),
            new ObservableCollection<Recording>(), new SoundMachineProjects());

        tracker.Song.Bpm = bpm;
        tracker.Song.LinesPerBeat = 4;

        var targets = new ControlTargets(tracker, new SoundMachineProjects(), pages: new OnTracker());
        tracker.UseAutomation(targets);

        return (tracker, targets);
    }

    /// <summary>The master's mapping for one of its controls.</summary>
    private static ControlMapping Master(MixControl what) => new()
    {
        Kind = ControlKind.Mix,
        Mix = what,
        Scope = ControlScope.Fixed,
        Track = TrackerPlayer.MasterStrip
    };

    /// <summary>A tempo lane on the first pattern holding one tempo up to a line and another from it.</summary>
    private static void TempoStep(Song song, int line, double before, double after)
    {
        var lane = song.Patterns[0].Lane(AutomationLane.For(Master(MixControl.Tempo), TrackerPlayer.MasterStrip)!);
        double Share(double bpm) => (bpm - TrackerTiming.MinBpm) / (TrackerTiming.MaxBpm - TrackerTiming.MinBpm);

        lane.Put(0, Share(before));
        lane.Put(line - 1, Share(before));
        lane.Put(line, Share(after));
    }

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

    /// <summary>Tempo is offered on the master and on no track.</summary>
    [Fact]
    public void Tempo_is_offered_on_the_master_only()
    {
        var (tracker, targets) = Made(120);

        Assert.Contains(targets.On(TrackerPlayer.MasterStrip), one => one.Mapping.Mix == MixControl.Tempo);
        Assert.DoesNotContain(targets.On(0), one => one.Mapping.Kind == ControlKind.Mix && one.Mapping.Mix == MixControl.Tempo);

        tracker.Finished();
    }

    /// <summary>A tempo lane slows the song from its line, the saved tempo stays, and the box shows what plays.</summary>
    [Fact]
    public void A_tempo_lane_changes_how_fast_it_plays()
    {
        var (tracker, _) = Made(240);
        TempoStep(tracker.Song, 4, 240, 60);

        var times = new ConcurrentQueue<(int Line, double Ms)>();
        var clock = Stopwatch.StartNew();
        var player = tracker.Player;

        player.PositionChanged += (_, at) => times.Enqueue((at.Line, clock.Elapsed.TotalMilliseconds));
        player.Play(tracker.Song, TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => times.Count >= 7));

        Assert.Equal(60, player.PlayingBpm, 3);

        player.Stop();

        var seen = times.ToArray();

        Assert.InRange(seen[2].Ms - seen[1].Ms, 40, 110);
        Assert.InRange(seen[6].Ms - seen[5].Ms, 200, 330);
        Assert.Equal(240, tracker.Song.Bpm);
        Assert.Equal(240, player.PlayingBpm, 3);

        tracker.Finished();
    }

    /// <summary>While a lane has the song at another tempo, the box shows it and says so; stopped, the song's own.</summary>
    [Fact]
    public void The_box_shows_the_playing_tempo()
    {
        var (tracker, _) = Made(240);
        TempoStep(tracker.Song, 1, 240, 90);

        tracker.Player.Play(tracker.Song, TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => Math.Abs(tracker.Player.PlayingBpm - 90) < 0.01));

        Assert.Equal(90, tracker.ShownBpm, 3);
        Assert.True(tracker.TempoMoving);

        tracker.Player.Stop();

        Assert.Equal(240, tracker.ShownBpm, 3);
        Assert.False(tracker.TempoMoving);

        tracker.Finished();
    }

    /// <summary>A tempo set while stopped does nothing, and one past what a song allows is held to it.</summary>
    [Fact]
    public void Setting_the_tempo_is_held_and_only_while_playing()
    {
        using var player = new TrackerPlayer(new SilentAudio());
        var song = new Song { Bpm = 120 };
        song.Patterns.Add(new Pattern(64, song.TrackCount) { Name = "P" });
        song.Order.Add(0);
        song.Normalize();
        player.Use(song);

        player.PlayAt(200);
        Assert.Equal(120, player.PlayingBpm, 3);

        player.Play(song, TrackerPosition.Start, TrackerPlayMode.Pattern);
        Assert.True(Until(() => player.IsPlaying));

        player.PlayAt(9999);
        Assert.Equal(TrackerTiming.MaxBpm, player.PlayingBpm, 3);

        player.PlayAt(1);
        Assert.Equal(TrackerTiming.MinBpm, player.PlayingBpm, 3);

        player.PlayAt(double.NaN);
        Assert.Equal(TrackerTiming.MinBpm, player.PlayingBpm, 3);

        player.Stop();
    }

    /// <summary>A strip a hand cannot reach while the mixer is hidden is still reached by the targets a lane plays through.</summary>
    [Fact]
    public void The_page_gate_holds_hands_back_and_not_lanes()
    {
        var (tracker, targets) = Made(120);

        Assert.Null(targets.Find(Master(MixControl.Volume)));
        Assert.NotNull(targets.Everywhere.Find(Master(MixControl.Volume)));
        Assert.Same(targets.Everywhere, targets.Everywhere);

        tracker.Finished();
    }

    /// <summary>
    /// The song's automation plays through the targets with nothing held back for the page in
    /// front, so a mixer lane plays on the tracker page.
    /// </summary>
    /// <remarks>
    /// Asked with targets that write down what reaches them, since a real strip is written on the
    /// drawing thread and a test has none it may run.
    /// </remarks>
    [Fact]
    public void A_mixer_lane_plays_on_the_tracker_page()
    {
        var tracker = new TrackerViewModel(new QuietAudio(), new SoundMachineRack(),
            new ObservableCollection<Recording>(), new SoundMachineProjects());
        var held = new Gated();

        tracker.UseAutomation(held);

        var lane = tracker.Song.Patterns[0].Lane(AutomationLane.For(Master(MixControl.Volume), TrackerPlayer.MasterStrip)!);
        lane.Put(0, 0.25);
        lane.Put(63, 0.25);

        tracker.Song.Bpm = 400;
        tracker.Player.Play(tracker.Song, TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => !held.Open.Written.IsEmpty), "the lane never wrote anything");

        tracker.Player.Stop();

        Assert.Equal(0.25, held.Open.Written.First(), 6);

        tracker.Finished();
    }

    /// <summary>Targets held to a page that is never in front, whose page-free half writes down what reaches it.</summary>
    private sealed class Gated : Midi.Interfaces.IControlTargets
    {
        /// <summary>The half a lane should play through.</summary>
        public Open Open { get; } = new();

        /// <inheritdoc/>
        public Midi.Interfaces.IControlTarget? Find(ControlMapping mapping) => null;

        /// <inheritdoc/>
        public Midi.Interfaces.IControlTargets Everywhere => Open;
    }

    /// <summary>Targets that answer everything with one value from nought to one that writes down what it was given.</summary>
    private sealed class Open : Midi.Interfaces.IControlTargets, Midi.Interfaces.IControlTarget
    {
        /// <summary>Every value written, in order.</summary>
        public ConcurrentQueue<double> Written { get; } = new();

        /// <inheritdoc/>
        public Midi.Interfaces.IControlTarget? Find(ControlMapping mapping) => this;

        /// <inheritdoc/>
        public string Name => "level";

        /// <inheritdoc/>
        public double Min => 0;

        /// <inheritdoc/>
        public double Max => 1;

        /// <inheritdoc/>
        public double Value => Written.LastOrDefault();

        /// <inheritdoc/>
        public void Set(double value) => Written.Enqueue(value);
    }
}
