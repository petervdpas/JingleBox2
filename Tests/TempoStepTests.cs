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

    /// <summary>A step on a pattern with no lane makes one, at the song's tempo up to the line before.</summary>
    [Fact]
    public void A_step_makes_the_lane_starting_at_the_songs_tempo()
    {
        var pattern = new Pattern(64, 4);

        _steps.Step(pattern, 16, 90, 120);

        var lane = LaneOn(pattern)!;

        Assert.Equal(new[] { 0.0, 15, 16 }, lane.Points.Select(point => point.Time));
        Assert.Equal(Share(120), lane.ValueAt(8)!.Value, 6);
        Assert.Equal(Share(120), lane.ValueAt(15)!.Value, 6);
        Assert.Equal(Share(90), lane.ValueAt(16)!.Value, 6);
        Assert.Equal(Share(90), lane.ValueAt(63)!.Value, 6);
    }

    /// <summary>A step on the first line is the whole pattern at that tempo.</summary>
    [Fact]
    public void A_step_on_the_first_line_is_one_point()
    {
        var pattern = new Pattern(64, 4);

        _steps.Step(pattern, 0, 90, 120);

        var point = Assert.Single(LaneOn(pattern)!.Points);

        Assert.Equal(Share(90), point.Value, 6);
    }

    /// <summary>A step in a drawn slope keeps the slope up to the line before, at the value it had reached.</summary>
    [Fact]
    public void A_step_keeps_a_drawn_shape()
    {
        var pattern = new Pattern(64, 4);
        var lane = pattern.Lane(AutomationLane.For(Tempo(), TrackerPlayer.MasterStrip)!);
        lane.Put(0, Share(100));
        lane.Put(32, Share(200));

        _steps.Step(pattern, 16, 60, 120);

        Assert.Equal(Share(100), lane.ValueAt(0)!.Value, 6);
        Assert.Equal(Share(100) + (Share(200) - Share(100)) * 15 / 32, lane.ValueAt(15)!.Value, 6);
        Assert.Equal(Share(60), lane.ValueAt(16)!.Value, 6);
    }

    /// <summary>A point already on the line before is left alone.</summary>
    [Fact]
    public void A_point_on_the_line_before_is_left_alone()
    {
        var pattern = new Pattern(64, 4);
        var lane = pattern.Lane(AutomationLane.For(Tempo(), TrackerPlayer.MasterStrip)!);
        lane.Put(7, Share(150));

        _steps.Step(pattern, 8, 60, 120);

        Assert.Equal(Share(150), lane.ValueAt(7)!.Value, 6);
        Assert.Equal(2, lane.Points.Count);
    }

    /// <summary>A line outside the pattern writes nothing, and a tempo past what a song allows is held.</summary>
    [Fact]
    public void Nonsense_is_held_or_refused()
    {
        var pattern = new Pattern(16, 4);

        _steps.Step(pattern, 16, 90, 120);
        _steps.Step(pattern, -1, 90, 120);
        _steps.Step(pattern, 4, double.NaN, 120);

        Assert.Null(LaneOn(pattern));

        _steps.Step(pattern, 0, 9999, 120);

        Assert.Equal(1.0, LaneOn(pattern)!.ValueAt(0)!.Value, 6);
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
        Assert.Equal(Share(90), LaneOn(pattern)!.ValueAt(16)!.Value, 6);
        Assert.Contains("90 beats a minute from line 16", tracker.Status);

        keys.Do(ShortcutAction.Undo);

        Assert.True(LaneOn(pattern) is null || LaneOn(pattern)!.Points.Count == 0);
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
        Assert.Null(LaneOn(pattern));
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

        Assert.Equal(Share(180), LaneOn(pattern)!.ValueAt(8)!.Value, 6);
        Assert.All(Enumerable.Range(8, 8), line => Assert.True(pattern[line, 1].Effect.IsNone));

        tracker.Finished();
    }
}
