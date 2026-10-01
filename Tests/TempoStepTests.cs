using System.Collections.ObjectModel;
using System.Linq;
using JingleBox2.Audio.Records;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.Shortcuts.Enums;
using JingleBox2.Shortcuts.Interfaces;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Typing <c>T</c> and two digits writes a step into the song's tempo lane, the one place tempo is
/// kept, and leaves nothing in the cell.
/// </summary>
public class TempoStepTests
{
    /// <summary>The rule on its own.</summary>
    private readonly TempoSteps _steps = new();

    /// <summary>The master's tempo, as a lane names it.</summary>
    private static ControlMapping Tempo() => new()
    {
        Kind = ControlKind.Mix,
        Mix = MixControl.Tempo,
        Scope = ControlScope.Fixed,
        Track = TrackerPlayer.MasterStrip
    };

    /// <summary>A tempo as the lane holds it.</summary>
    private static double Share(double bpm) => (bpm - TrackerTiming.MinBpm) / (TrackerTiming.MaxBpm - TrackerTiming.MinBpm);

    /// <summary>The tempo lane on a pattern, if any.</summary>
    private static AutomationLane? LaneOn(Pattern pattern) => pattern.LaneFor(Tempo(), TrackerPlayer.MasterStrip);

    /// <summary>A song of two 64 line patterns, one slot each, at 120.</summary>
    private static Song TwoSlots()
    {
        var song = new Song { Bpm = 120 };
        song.Patterns.Add(new Pattern(64, song.TrackCount) { Name = "A" });
        song.Patterns.Add(new Pattern(64, song.TrackCount) { Name = "B" });
        song.Order.Add(0);
        song.Order.Add(1);
        song.Normalize();
        return song;
    }

    /// <summary>A step on a song with no lane makes one, at the song's tempo up to the line before.</summary>
    [Fact]
    public void A_step_makes_the_lane_starting_at_the_songs_tempo()
    {
        var song = TwoSlots();

        _steps.Step(song, 80, 90);

        var lane = song.Tempo!;

        Assert.Equal(new[] { 0.0, 79, 80 }, lane.Points.Select(point => point.Time));
        Assert.Equal(Share(120), lane.ValueAt(40)!.Value, 6);
        Assert.Equal(Share(120), lane.ValueAt(79)!.Value, 6);
        Assert.Equal(Share(90), lane.ValueAt(80)!.Value, 6);
        Assert.Equal(Share(90), lane.ValueAt(127)!.Value, 6);
    }

    /// <summary>A step on the first line is the whole song at that tempo.</summary>
    [Fact]
    public void A_step_on_the_first_line_is_one_point()
    {
        var song = TwoSlots();

        _steps.Step(song, 0, 90);

        Assert.Equal(Share(90), Assert.Single(song.Tempo!.Points).Value, 6);
    }

    /// <summary>A step in a drawn slope keeps the slope up to the line before, at the value it had reached.</summary>
    [Fact]
    public void A_step_keeps_a_drawn_shape()
    {
        var song = TwoSlots();
        var lane = song.Lane(AutomationLane.For(Tempo(), TrackerPlayer.MasterStrip)!);
        lane.Put(0, Share(100));
        lane.Put(32, Share(200));

        _steps.Step(song, 16, 60);

        Assert.Equal(Share(100) + (Share(200) - Share(100)) * 15 / 32, lane.ValueAt(15)!.Value, 6);
        Assert.Equal(Share(60), lane.ValueAt(16)!.Value, 6);
    }

    /// <summary>A line outside the song writes nothing, and a tempo past what a song allows is held.</summary>
    [Fact]
    public void Nonsense_is_held_or_refused()
    {
        var song = TwoSlots();

        _steps.Step(song, 128, 90);
        _steps.Step(song, -1, 90);
        _steps.Step(song, 4, double.NaN);

        Assert.Null(song.Tempo);

        _steps.Step(song, 0, 9999);

        Assert.Equal(1.0, song.Tempo!.ValueAt(0)!.Value, 6);
    }

    /// <summary>Lines along the order run slot after slot.</summary>
    [Fact]
    public void Lines_along_the_order_run_slot_after_slot()
    {
        var song = TwoSlots();
        song.Patterns[1].Resize(32);

        Assert.Equal(96, song.TotalLines);
        Assert.Equal(64 + 5, song.LineOf(1, 5));
        Assert.Equal(new[] { 0, 64 }, song.SlotStarts);
    }

    /// <summary>A tempo lane kept inside a pattern, from before tempo was the song's, moves into the song's lane where that pattern first plays.</summary>
    [Fact]
    public void A_tempo_lane_in_a_pattern_moves_into_the_song()
    {
        var song = TwoSlots();
        var old = song.Patterns[1].Lane(AutomationLane.For(Tempo(), TrackerPlayer.MasterStrip)!);
        old.Put(0, Share(150));
        old.Put(10, Share(200));

        song.Normalize();

        Assert.Empty(song.Patterns[1].Lanes);
        Assert.Equal(new[] { 64.0, 74 }, song.Tempo!.Points.Select(point => point.Time));
    }

    /// <summary>A tracker to type into.</summary>
    private static TrackerViewModel Tracker() =>
        new(new QuietAudio(), new SoundMachineRack(), new ObservableCollection<Recording>(), new SoundMachineProjects());

    /// <summary>Typing T5A writes 90 into the tempo lane, leaves the cell empty, steps down and undoes in one.</summary>
    [Fact]
    public void Typing_a_tempo_writes_the_lane_and_not_the_cell()
    {
        var tracker = Tracker();
        var pattern = tracker.Song.Patterns[0];
        IShortcutContext keys = tracker;

        tracker.IsRecording = true;
        tracker.EditStep = 1;
        tracker.Cursor = new PatternCursor(16, 2, CellColumn.Effect);

        tracker.EnterEffectCommand('T');
        tracker.EnterHexDigit('5');
        tracker.EnterHexDigit('A');

        Assert.True(pattern[16, 2].Effect.IsNone);
        Assert.Equal(17, tracker.Cursor.Line);
        Assert.Equal(Share(90), tracker.Song.Tempo!.ValueAt(16)!.Value, 6);
        Assert.Contains("90 beats a minute from line 16", tracker.Status);

        keys.Do(ShortcutAction.Undo);

        Assert.True(tracker.Song.Tempo is null || tracker.Song.Tempo.Points.Count == 0);
        Assert.True(pattern[16, 2].Effect.IsNone);

        tracker.Finished();
    }

    /// <summary>A tempo under twenty is refused, says so, and leaves nothing behind.</summary>
    [Fact]
    public void A_tempo_under_twenty_is_refused()
    {
        var tracker = Tracker();
        var pattern = tracker.Song.Patterns[0];

        tracker.IsRecording = true;
        tracker.Cursor = new PatternCursor(4, 0, CellColumn.Effect);

        tracker.EnterEffectCommand('T');
        tracker.EnterHexDigit('0');
        tracker.EnterHexDigit('5');

        Assert.True(pattern[4, 0].Effect.IsNone);
        Assert.Null(tracker.Song.Tempo);
        Assert.Contains("under the 20", tracker.Status);

        tracker.Finished();
    }

    /// <summary>The command popup's tempo goes into the lane from the selection's first line, and into no cell.</summary>
    [Fact]
    public void The_popups_tempo_goes_into_the_lane()
    {
        var tracker = Tracker();
        var pattern = tracker.Song.Patterns[0];

        tracker.Cursor = new PatternCursor(8, 1, CellColumn.Note);
        tracker.Selection = new PatternSelection(8, 1, 15, 1);

        tracker.SetCommand(new TrackerCommand(TrackerCommand.Tempo, 0xB4));

        Assert.Equal(Share(180), tracker.Song.Tempo!.ValueAt(8)!.Value, 6);
        Assert.All(Enumerable.Range(8, 8), line => Assert.True(pattern[line, 1].Effect.IsNone));

        tracker.Finished();
    }

    /// <summary>A tempo lane is saved with the song and comes back as a tempo lane with its points.</summary>
    [Fact]
    public void A_tempo_lane_is_saved_with_the_song()
    {
        var song = TwoSlots();

        _steps.Step(song, 16, 90);

        var back = SongStore.Uncopy(SongStore.Copy(song))!;
        back.Normalize();

        var lane = back.Tempo!;

        Assert.Equal(MixControl.Tempo, lane.Mix);
        Assert.Equal(Share(90), lane.ValueAt(16)!.Value, 6);
        Assert.Equal(Share(120), lane.ValueAt(8)!.Value, 6);
    }

    /// <summary>
    /// Opening another song, the song master's lane panel shows that song's lanes and not the
    /// last song's, so what is drawn goes into the song that plays.
    /// </summary>
    [Fact]
    public void Another_song_shows_its_own_lanes()
    {
        var tracker = Tracker();
        tracker.UseAutomation(new ControlTargets(tracker, new SoundMachineProjects()));
        tracker.ShowsMixerLanes = true;
        tracker.PickTrack(TrackerPlayer.MasterStrip);

        _steps.Step(tracker.Song, 0, 90);
        tracker.MixerLanes!.Restock();
        tracker.MixerLanes.Chosen = tracker.MixerLanes.Parameters.First(row => row.HasLane);

        var other = Song.CreateDefault();
        tracker.Restore(other);

        var shown = tracker.MixerLanes.Parameters.Single(row => row.Choice.Mapping.Mix == MixControl.Tempo);

        Assert.False(shown.HasLane);
        Assert.False(tracker.MixerLanes.Chosen?.HasLane ?? false);

        tracker.Finished();
    }
}
