using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JingleBox2.Audio.Records;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The mixer's lanes belong to the song, running along the order, and a machine's, an effect's
/// and a plugin's stay in the pattern: a track can start soft, come up and go down again across
/// the whole song.
/// </summary>
public class SongLaneTests
{
    /// <summary>A song of two 16 line patterns, one slot each.</summary>
    private static Song TwoSlots()
    {
        var song = new Song();
        song.Patterns.Add(new Pattern(16, song.TrackCount) { Name = "A" });
        song.Patterns.Add(new Pattern(16, song.TrackCount) { Name = "B" });
        song.Order.Add(0);
        song.Order.Add(1);
        song.Normalize();
        return song;
    }

    /// <summary>A track's level on the mixer.</summary>
    private static ControlMapping Level(int track) =>
        new() { Kind = ControlKind.Mix, Mix = MixControl.Volume, Scope = ControlScope.Fixed, Track = track };

    /// <summary>A machine's parameter on a track.</summary>
    private static ControlMapping Cutoff(int track) =>
        new() { Kind = ControlKind.SoundDevice, Machine = "machine.oddskilla", Key = "cutoff", Scope = ControlScope.Fixed, Track = track };

    /// <summary>The mixer is the song's, and a machine is not.</summary>
    [Fact]
    public void The_mixer_is_the_songs_and_a_machine_is_not()
    {
        Assert.True(Song.IsSongWide(Level(0)));
        Assert.True(Song.IsSongWide(Level(TrackerPlayer.MasterStrip)));
        Assert.False(Song.IsSongWide(Cutoff(0)));
        Assert.False(Song.IsSongWide(null));
    }

    /// <summary>A mixer lane kept in a pattern moves into the song where the pattern first plays, and a machine's stays.</summary>
    [Fact]
    public void Mixer_lanes_in_patterns_move_into_the_song()
    {
        var song = TwoSlots();
        var level = song.Patterns[1].Lane(AutomationLane.For(Level(2), 2)!);
        level.Put(0, 0.2);
        level.Put(8, 0.9);
        song.Patterns[1].Lane(AutomationLane.For(Cutoff(2), 2)!).Put(0, 0.5);

        song.Normalize();

        var moved = song.LaneFor(Level(2), 2)!;

        Assert.Equal(new[] { 16.0, 24 }, moved.Points.Select(point => point.Time));
        Assert.Single(song.Patterns[1].Lanes);
        Assert.Equal(ControlKind.SoundDevice, song.Patterns[1].Lanes[0].Kind);
    }

    /// <summary>The song's lanes are saved with it.</summary>
    [Fact]
    public void The_songs_lanes_are_saved_with_it()
    {
        var song = TwoSlots();
        song.Lane(AutomationLane.For(Level(1), 1)!).Put(20, 0.75);

        var back = SongStore.Uncopy(SongStore.Copy(song))!;
        back.Normalize();

        Assert.Equal(0.75, back.LaneFor(Level(1), 1)!.ValueAt(20)!.Value, 6);
    }

    /// <summary>A track moved takes its song lane with it, and a track taken away takes its lane away.</summary>
    [Fact]
    public void A_song_lane_follows_its_track()
    {
        var song = TwoSlots();
        song.Lane(AutomationLane.For(Level(0), 0)!).Put(0, 0.5);
        song.Lane(AutomationLane.For(Level(TrackerPlayer.MasterStrip), TrackerPlayer.MasterStrip)!).Put(0, 0.5);

        song.MoveTrack(0, 3);

        Assert.NotNull(song.LaneFor(Level(3), 3));
        Assert.Null(song.LaneFor(Level(0), 0));

        song.SetTrackCount(2);

        Assert.Null(song.LaneFor(Level(3), 3));
        Assert.NotNull(song.LaneFor(Level(TrackerPlayer.MasterStrip), TrackerPlayer.MasterStrip));
    }

    /// <summary>Playing, a song lane is read at its place along the order, and a pattern lane at the pattern's line.</summary>
    [Fact]
    public void Playing_reads_a_song_lane_along_the_order()
    {
        var song = TwoSlots();
        var lane = song.Lane(AutomationLane.For(Level(0), 0)!);
        lane.Put(0, 0.0);
        lane.Put(16, 1.0);

        var targets = new Heard();
        var player = new AutomationPlayer(targets);

        player.Play(song, new TrackerPosition(1, 0));

        Assert.Equal(1.0, targets.Knob.Value, 6);

        player.Play(song, new TrackerPosition(0, 8));

        Assert.Equal(0.5, targets.Knob.Value, 6);
    }

    /// <summary>Recording a fader writes into the song's lane at its place along the order, not into the pattern.</summary>
    [Fact]
    public void Recording_a_fader_writes_into_the_song()
    {
        var song = TwoSlots();
        var steps = new List<string>();

        var recorder = new AutomationRecorder(() => song, () => true, () => new TrackerPosition(1, 4), () => 0)
        {
            Armed = true,
            Changing = what => steps.Add(what)
        };

        Assert.True(recorder.Moved(Level(0), new Knob(0.5), 0.25));

        Assert.Equal(0.25, song.LaneFor(Level(0), 0)!.ValueAt(20)!.Value, 6);
        Assert.Empty(song.Patterns[1].Lanes);
        Assert.Single(steps);
    }

    /// <summary>
    /// A track's level is offered on the mixer's panel and not under the pattern, goes onto the
    /// song, and stays when another pattern is in front.
    /// </summary>
    [Fact]
    public void A_track_level_from_the_panel_is_the_songs()
    {
        var tracker = new TrackerViewModel(new QuietAudio(), new SoundMachineRack(),
            new ObservableCollection<Recording>(), new SoundMachineProjects());
        tracker.Song.Patterns.Add(new Pattern(64, tracker.Song.TrackCount) { Name = "B" });
        tracker.Song.Order.Add(1);
        tracker.Song.Normalize();
        tracker.UseAutomation(new ControlTargets(tracker, new SoundMachineProjects()));
        tracker.ShowsLanes = true;
        tracker.ShowsMixerLanes = true;
        tracker.PickTrack(0);

        Assert.DoesNotContain(tracker.Lanes!.Parameters, row => row.Choice.Mapping.Kind == ControlKind.Mix);

        var panel = tracker.MixerLanes!;
        Assert.Equal(0, panel.Track);
        Assert.All(panel.Parameters, row => Assert.Equal(ControlKind.Mix, row.Choice.Mapping.Kind));
        var level = panel.Parameters.First(row => row.Choice.Mapping.Kind == ControlKind.Mix && row.Choice.Mapping.Mix == MixControl.Volume);
        level.AddCommand.Execute(null);

        Assert.NotNull(tracker.Song.LaneFor(Level(0), 0));
        Assert.Empty(tracker.Song.Patterns[0].Lanes);
        Assert.Equal(1, tracker.MixerLaneCount);
        Assert.Equal(0, tracker.LaneCount);

        tracker.OrderIndex = 1;

        Assert.True(panel.Parameters.First(row => row.Choice.Mapping.Kind == ControlKind.Mix && row.Choice.Mapping.Mix == MixControl.Volume).HasLane);
        Assert.Equal(1, tracker.MixerLaneCount);

        tracker.Finished();
    }

    /// <summary>
    /// The mixer's panel stays on the master once it is touched, and goes to a track when the
    /// cursor moves to one.
    /// </summary>
    [Fact]
    public void The_mixers_panel_follows_the_strip_and_the_cursor()
    {
        var tracker = new TrackerViewModel(new QuietAudio(), new SoundMachineRack(),
            new ObservableCollection<Recording>(), new SoundMachineProjects());
        tracker.UseAutomation(new ControlTargets(tracker, new SoundMachineProjects()));
        tracker.ShowsMixerLanes = true;

        tracker.PickTrack(TrackerPlayer.MasterStrip);

        Assert.Equal(TrackerPlayer.MasterStrip, tracker.MixerLanes!.Track);
        Assert.True(tracker.MasterStrip!.IsSelected);
        Assert.Contains(tracker.MixerLanes.Parameters, row => row.Choice.Mapping.Mix == MixControl.Tempo);
        Assert.Equal("automation \u00b7 SONG", tracker.MixerLanesTitle);
        Assert.True(tracker.MixerShowsSongChain);

        tracker.Cursor = tracker.Cursor with { Track = 2 };

        Assert.Equal(2, tracker.MixerLanes.Track);
        Assert.False(tracker.MasterStrip.IsSelected);
        Assert.True(tracker.Strips[2].IsSelected);
        Assert.Equal("automation \u00b7 TR-03", tracker.MixerLanesTitle);
        Assert.False(tracker.MixerShowsSongChain);
        Assert.DoesNotContain(tracker.MixerLanes.Parameters, row => row.Choice.Mapping.Mix == MixControl.Tempo);

        tracker.Finished();
    }

    /// <summary>Targets that answer every mapping with one knob from nought to one.</summary>
    private sealed class Heard : IControlTargets
    {
        /// <summary>The one knob.</summary>
        public Knob Knob { get; } = new(0.5);

        /// <inheritdoc/>
        public IControlTarget? Find(ControlMapping mapping) => Knob;
    }
}
