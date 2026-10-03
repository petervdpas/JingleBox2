using System.Collections.Generic;
using System.Collections.ObjectModel;
using JingleBox2.Audio.Records;
using JingleBox2.Shortcuts.Enums;
using JingleBox2.Shortcuts.Interfaces;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker.Enums;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Undoing the play mode or the loop switch puts the song back, and the picker and the transport
/// are told, so the bar, the file and what plays agree.
/// </summary>
public class UndoTransportSettingsTests
{
    /// <summary>A tracker on a fresh song, with no sound card.</summary>
    private static TrackerViewModel Tracker() =>
        new(new QuietAudio(), new SoundMachineRack(), new ObservableCollection<Recording>(), new SoundMachineProjects());

    /// <summary>Undoing a change of play mode reaches the picker and the transport.</summary>
    [Fact]
    public void Undoing_the_play_mode_reaches_the_picker_and_the_transport()
    {
        var tracker = Tracker();
        IShortcutContext keys = tracker;
        var was = tracker.PlayMode;
        var other = was == TrackerPlayMode.Song ? TrackerPlayMode.Pattern : TrackerPlayMode.Song;
        var told = new List<string?>();

        tracker.PlayMode = other;
        tracker.PropertyChanged += (_, e) => told.Add(e.PropertyName);
        keys.Do(ShortcutAction.Undo);

        Assert.Equal(was, tracker.Song.PlayMode);
        Assert.Equal(was, tracker.Player.Mode);
        Assert.Contains(nameof(TrackerViewModel.PlayMode), told);

        tracker.Finished();
    }

    /// <summary>Undoing the loop switch reaches the switch and the transport.</summary>
    [Fact]
    public void Undoing_the_loop_reaches_the_switch_and_the_transport()
    {
        var tracker = Tracker();
        IShortcutContext keys = tracker;
        bool was = tracker.LoopPlayback;
        var told = new List<string?>();

        tracker.LoopPlayback = !was;
        tracker.PropertyChanged += (_, e) => told.Add(e.PropertyName);
        keys.Do(ShortcutAction.Undo);

        Assert.Equal(was, tracker.Song.Looping);
        Assert.Equal(was, tracker.Player.Loop);
        Assert.Contains(nameof(TrackerViewModel.LoopPlayback), told);

        tracker.Finished();
    }
}
