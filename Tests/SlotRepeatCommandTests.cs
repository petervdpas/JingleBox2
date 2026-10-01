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

        Assert.Equal((8, 40, 3), (repeat.From, repeat.To, repeat.StretchTimes));
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
}
