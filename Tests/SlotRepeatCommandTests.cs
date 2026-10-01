using System.Collections.ObjectModel;
using JingleBox2.Audio.Records;
using JingleBox2.Shortcuts.Enums;
using JingleBox2.Shortcuts.Interfaces;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>Setting a slot's repeats from the order list's and the pattern's menus.</summary>
/// <remarks>
/// Over a real tracker with a quiet engine, so what is pinned is what a press really does: which
/// slot it lands on, what the row then says, and that undo puts it back.
/// </remarks>
public class SlotRepeatCommandTests
{
    /// <summary>A tracker over nothing that sounds.</summary>
    private static TrackerViewModel Tracker() =>
        new(new QuietAudio(), new SoundMachineRack(), new ObservableCollection<Recording>(), new SoundMachineProjects());

    /// <summary>Repeating the pattern lands on the slot under the cursor and shows on its row.</summary>
    [Fact]
    public void Repeating_the_pattern_shows_on_the_row()
    {
        var tracker = Tracker();

        tracker.RepeatSlotCommand.Execute(4);

        Assert.Equal(4, tracker.Song.RepeatAt(tracker.OrderIndex).Times);
        Assert.Equal("×4", tracker.OrderEntries[tracker.OrderIndex].Repeat);
    }

    /// <summary>Looping the selected lines takes the selection's first and last line.</summary>
    [Fact]
    public void Looping_lines_takes_the_selection()
    {
        var tracker = Tracker();

        tracker.Selection = new PatternSelection(40, 0, 8, 0);
        tracker.LoopLinesCommand.Execute(3);

        var repeat = tracker.Song.RepeatAt(tracker.OrderIndex);

        Assert.Equal(new LineLoop(8, 40, 3), Assert.Single(repeat.Lines()));
        Assert.Equal("↺08–40×3", tracker.OrderEntries[tracker.OrderIndex].Repeat);
    }

    /// <summary>Looping lines with nothing selected does nothing.</summary>
    [Fact]
    public void Looping_with_nothing_selected_does_nothing()
    {
        var tracker = Tracker();

        tracker.Selection = new PatternSelection(-1, -1, -1, -1);
        tracker.LoopLinesCommand.Execute(3);

        Assert.Equal(SlotRepeat.None, tracker.Song.RepeatAt(tracker.OrderIndex));
    }

    /// <summary>Clearing puts the slot back to playing once, with an empty row.</summary>
    [Fact]
    public void Clearing_plays_once_again()
    {
        var tracker = Tracker();

        tracker.RepeatSlotCommand.Execute(2);
        tracker.ClearSlotRepeatCommand.Execute(null);

        Assert.Equal(SlotRepeat.None, tracker.Song.RepeatAt(tracker.OrderIndex));
        Assert.Equal("", tracker.OrderEntries[tracker.OrderIndex].Repeat);
    }

    /// <summary>Undo takes a repeat back off.</summary>
    [Fact]
    public void Undo_takes_it_back()
    {
        var tracker = Tracker();
        IShortcutContext keys = tracker;

        tracker.RepeatSlotCommand.Execute(8);
        keys.Do(ShortcutAction.Undo);

        Assert.Equal(1, tracker.Song.RepeatAt(tracker.OrderIndex).Times);
        Assert.Equal("", tracker.OrderEntries[tracker.OrderIndex].Repeat);
    }

    /// <summary>A second stretch elsewhere is added beside the first, and both show on the row.</summary>
    [Fact]
    public void A_second_stretch_is_added_beside_the_first()
    {
        var tracker = Tracker();

        tracker.Selection = new PatternSelection(48, 0, 63, 0);
        tracker.LoopLinesCommand.Execute(4);
        tracker.Selection = new PatternSelection(16, 1, 23, 1);
        tracker.LoopLinesCommand.Execute(2);

        Assert.Equal(2, tracker.Song.RepeatAt(tracker.OrderIndex).Lines().Count);
        Assert.Equal("\u21ba16\u201323\u00d72 \u21ba48\u201363\u00d74", tracker.OrderEntries[tracker.OrderIndex].Repeat);
    }

    /// <summary>A stretch sharing lines with one already there is refused, on any track, and changes nothing.</summary>
    [Fact]
    public void A_stretch_over_another_is_refused()
    {
        var tracker = Tracker();

        tracker.Selection = new PatternSelection(16, 0, 23, 0);
        tracker.LoopLinesCommand.Execute(2);
        tracker.Selection = new PatternSelection(20, 2, 30, 2);
        tracker.LoopLinesCommand.Execute(4);

        Assert.Equal(new LineLoop(16, 23, 2), Assert.Single(tracker.Song.RepeatAt(tracker.OrderIndex).Lines()));
        Assert.Contains("16", tracker.Status);
    }

    /// <summary>Unlooping the selected lines takes off the stretch they touch.</summary>
    [Fact]
    public void Unlooping_takes_the_stretch_off()
    {
        var tracker = Tracker();

        tracker.Selection = new PatternSelection(16, 0, 23, 0);
        tracker.LoopLinesCommand.Execute(2);
        tracker.Selection = new PatternSelection(20, 3, 20, 3);
        tracker.UnloopLinesCommand.Execute(null);

        Assert.Empty(tracker.Song.RepeatAt(tracker.OrderIndex).Lines());
    }

    /// <summary>The red cross on a loop's line takes that loop off and leaves the others.</summary>
    [Fact]
    public void The_cross_on_a_loop_takes_only_that_loop_off()
    {
        var tracker = Tracker();

        tracker.Selection = new PatternSelection(16, 0, 23, 0);
        tracker.LoopLinesCommand.Execute(2);
        tracker.Selection = new PatternSelection(48, 1, 63, 1);
        tracker.LoopLinesCommand.Execute(4);

        var row = tracker.OrderEntries[tracker.OrderIndex];
        Assert.Equal(2, row.Stretches!.Count);

        tracker.RemoveStretchCommand.Execute(row.Stretches[0]);

        Assert.Equal(new LineLoop(48, 63, 4), Assert.Single(tracker.Song.RepeatAt(tracker.OrderIndex).Lines()));
        Assert.Single(tracker.OrderEntries[tracker.OrderIndex].Stretches!);
    }

    /// <summary>The count and the loops are on the row apart, so each loop can be drawn on a line of its own.</summary>
    [Fact]
    public void The_count_and_the_loops_are_apart()
    {
        var tracker = Tracker();

        tracker.RepeatSlotCommand.Execute(3);
        tracker.Selection = new PatternSelection(0, 0, 7, 0);
        tracker.LoopLinesCommand.Execute(2);

        var row = tracker.OrderEntries[tracker.OrderIndex];

        Assert.Equal("\u00d73", row.Times);
        Assert.True(row.HasTimes);
        Assert.Equal("\u21ba00\u201307\u00d72", Assert.Single(row.Stretches!).Label);
    }

    /// <summary>Copying a pattern gives the copy's slot the same repeats and loops as the slot it came from.</summary>
    [Fact]
    public void Copying_a_pattern_copies_its_repeats()
    {
        var tracker = Tracker();

        tracker.RepeatSlotCommand.Execute(2);
        tracker.Selection = new PatternSelection(32, 0, 40, 0);
        tracker.LoopLinesCommand.Execute(2);
        tracker.Selection = new PatternSelection(48, 0, 63, 0);
        tracker.LoopLinesCommand.Execute(2);

        int from = tracker.OrderIndex;
        tracker.CopyPatternCommand.Execute(null);

        Assert.Equal(from + 1, tracker.OrderIndex);
        Assert.Equal(tracker.Song.RepeatAt(from).Times, tracker.Song.RepeatAt(from + 1).Times);
        Assert.Equal(tracker.Song.RepeatAt(from).Lines(), tracker.Song.RepeatAt(from + 1).Lines());
    }

    /// <summary>Ending the slot after the cursor's line shows on its row, and the red cross plays it whole again.</summary>
    [Fact]
    public void Ending_a_slot_shows_on_the_row_and_the_cross_takes_it_off()
    {
        var tracker = Tracker();

        tracker.Cursor = new PatternCursor(23, 0, JingleBox2.Tracker.Enums.CellColumn.Note);
        tracker.EndSlotHereCommand.Execute(null);

        var row = tracker.OrderEntries[tracker.OrderIndex];

        Assert.Equal(23, tracker.Song.LastLineOf(tracker.OrderIndex));
        Assert.True(row.HasBreak);
        Assert.Equal("to 23", row.Break);

        tracker.PlayWholePatternCommand.Execute(row.Slot);

        Assert.False(tracker.OrderEntries[tracker.OrderIndex].HasBreak);
        Assert.Equal(tracker.Song.PatternAt(tracker.OrderIndex)!.Lines - 1, tracker.Song.LastLineOf(tracker.OrderIndex));
    }

    /// <summary>Ending a slot is one step of undo.</summary>
    [Fact]
    public void Ending_a_slot_undoes()
    {
        var tracker = Tracker();
        IShortcutContext keys = tracker;

        tracker.Cursor = new PatternCursor(7, 0, JingleBox2.Tracker.Enums.CellColumn.Note);
        tracker.EndSlotHereCommand.Execute(null);
        keys.Do(ShortcutAction.Undo);

        Assert.False(tracker.OrderEntries[tracker.OrderIndex].HasBreak);
    }

    /// <summary>Lines past the slot's end cannot be looped, and it says why.</summary>
    [Fact]
    public void Lines_past_the_end_cannot_be_looped()
    {
        var tracker = Tracker();

        tracker.Cursor = new PatternCursor(15, 0, JingleBox2.Tracker.Enums.CellColumn.Note);
        tracker.EndSlotHereCommand.Execute(null);

        tracker.Selection = new PatternSelection(12, 0, 20, 0);
        tracker.LoopLinesCommand.Execute(2);

        Assert.Empty(tracker.Song.RepeatAt(tracker.OrderIndex).Lines());
        Assert.Contains("stops after line 15", tracker.Status);
    }
}
