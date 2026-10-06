using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using JingleBox2.Audio.Plugins.Records;
using JingleBox2.Audio.Records;
using JingleBox2.Shortcuts.Enums;
using JingleBox2.Shortcuts.Interfaces;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels;
using JingleBox2.ViewModels.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// "+ Add" under the song's instruments puts the picked one in the song and on the cursor's
/// track, as one undo step, and asks before pushing an instrument off a track that has one.
/// Clicking an instrument in the list puts the cursor on the track it plays.
/// </summary>
public sealed class AddInstrumentTests
{
    /// <summary>A tracker with a plugin instrument picked, which needs no rack to offer it.</summary>
    private static TrackerViewModel Bench(string name)
    {
        var tracker = new TrackerViewModel(new QuietAudio(), new SoundMachineRack(),
            new ObservableCollection<Recording>(), new SoundMachineProjects());

        tracker.PickedMachine = Choice(name);

        return tracker;
    }

    /// <summary>A plugin instrument that is not on this computer, which is all adding needs.</summary>
    private static InstrumentChoice Choice(string name) =>
        new(null, new PluginInfo("id." + name, name, "", "1", "/nowhere/" + name, IsInstrument: true), name, "");

    /// <summary>An empty track under the cursor is given what was added.</summary>
    [Fact]
    public async Task An_empty_cursor_track_is_given_it()
    {
        var tracker = Bench("Dark707");
        tracker.Cursor = new PatternCursor(0, 1, CellColumn.Note);

        await tracker.AddInstrumentCommand.ExecuteAsync(null);

        Assert.Single(tracker.Song.Instruments);
        Assert.Equal(0, tracker.Song.GetTrackInstrument(1));
        Assert.Null(tracker.Song.InstrumentAt(tracker.Song.GetTrackInstrument(0)));

        tracker.Finished();
    }

    /// <summary>A track that already plays something is asked about, and no keeps it there.</summary>
    [Fact]
    public async Task Answering_no_keeps_what_the_track_plays()
    {
        var tracker = Bench("First");
        tracker.Cursor = new PatternCursor(0, 2, CellColumn.Note);
        await tracker.AddInstrumentCommand.ExecuteAsync(null);

        int asked = 0;
        tracker.Asks = (_, _, _) => { asked++; return Task.FromResult(false); };
        tracker.PickedMachine = Choice("Second");
        await tracker.AddInstrumentCommand.ExecuteAsync(null);

        Assert.Equal(1, asked);
        Assert.Equal(2, tracker.Song.Instruments.Count);
        Assert.Equal(0, tracker.Song.GetTrackInstrument(2));
        Assert.Equal(-1, tracker.Song.GetInstrumentTrack(1));

        tracker.Finished();
    }

    /// <summary>Yes puts the new one on the track, and the one it replaced stays in the song.</summary>
    [Fact]
    public async Task Answering_yes_replaces_and_keeps_the_old_in_the_song()
    {
        var tracker = Bench("First");
        tracker.Cursor = new PatternCursor(0, 2, CellColumn.Note);
        await tracker.AddInstrumentCommand.ExecuteAsync(null);

        tracker.Asks = (_, _, _) => Task.FromResult(true);
        tracker.PickedMachine = Choice("Second");
        await tracker.AddInstrumentCommand.ExecuteAsync(null);

        Assert.Equal(2, tracker.Song.Instruments.Count);
        Assert.Equal(1, tracker.Song.GetTrackInstrument(2));
        Assert.Equal(-1, tracker.Song.GetInstrumentTrack(0));

        tracker.Finished();
    }

    /// <summary>An empty track is not asked about.</summary>
    [Fact]
    public async Task An_empty_track_is_not_asked_about()
    {
        var tracker = Bench("Dark707");
        tracker.Asks = (_, _, _) => throw new InvalidOperationException("asked");

        await tracker.AddInstrumentCommand.ExecuteAsync(null);

        Assert.Equal(0, tracker.Song.GetTrackInstrument(0));

        tracker.Finished();
    }

    /// <summary>One undo takes the instrument out of the song and off the track together.</summary>
    [Fact]
    public async Task One_undo_takes_both_back()
    {
        var tracker = Bench("Dark707");
        tracker.Cursor = new PatternCursor(0, 3, CellColumn.Note);

        await tracker.AddInstrumentCommand.ExecuteAsync(null);
        ((IShortcutContext)tracker).Do(ShortcutAction.Undo);

        Assert.Empty(tracker.Song.Instruments);
        Assert.Null(tracker.Song.InstrumentAt(tracker.Song.GetTrackInstrument(3)));

        tracker.Finished();
    }

    /// <summary>With nothing picked nothing is added and no track is touched.</summary>
    [Fact]
    public async Task Nothing_picked_adds_nothing()
    {
        var tracker = Bench("Dark707");
        tracker.PickedMachine = null;

        await tracker.AddInstrumentCommand.ExecuteAsync(null);

        Assert.Empty(tracker.Song.Instruments);
        Assert.Null(tracker.Song.InstrumentAt(tracker.Song.GetTrackInstrument(0)));

        tracker.Finished();
    }

    /// <summary>Clicking an instrument in the list puts the cursor on the track it plays.</summary>
    [Fact]
    public async Task Picking_an_instrument_goes_to_its_track()
    {
        var tracker = Bench("Dark707");
        tracker.Cursor = new PatternCursor(5, 2, CellColumn.Note);
        await tracker.AddInstrumentCommand.ExecuteAsync(null);

        tracker.Cursor = new PatternCursor(5, 0, CellColumn.Note);
        tracker.PickInstrument(0);

        Assert.Equal(2, tracker.Cursor.Track);
        Assert.Equal(5, tracker.Cursor.Line);

        tracker.Finished();
    }

    /// <summary>An instrument on no track, or a row that is not there, leaves the cursor alone.</summary>
    [Fact]
    public async Task Picking_an_instrument_on_no_track_stays_put()
    {
        var tracker = Bench("First");
        tracker.Cursor = new PatternCursor(0, 1, CellColumn.Note);
        await tracker.AddInstrumentCommand.ExecuteAsync(null);

        tracker.Asks = (_, _, _) => Task.FromResult(false);
        tracker.PickedMachine = Choice("Second");
        await tracker.AddInstrumentCommand.ExecuteAsync(null);

        tracker.Cursor = new PatternCursor(0, 3, CellColumn.Note);
        tracker.PickInstrument(1);
        Assert.Equal(3, tracker.Cursor.Track);

        tracker.PickInstrument(9);
        tracker.PickInstrument(-1);
        Assert.Equal(3, tracker.Cursor.Track);

        tracker.Finished();
    }
}
