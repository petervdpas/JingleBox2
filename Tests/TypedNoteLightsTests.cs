using System.Collections.ObjectModel;
using JingleBox2.Audio.Records;
using JingleBox2.Midi;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A note played by hand into the pattern lights the drawn keyboards beside it.
/// </summary>
/// <remarks>
/// It never used to. Typing a part sounded each note and said so to nothing, so a machine's
/// window open beside the pattern sat dark while somebody wrote into it, and the hardware was the
/// only keyboard that lit anything. That is the shape the one road exists to end: a letter, a
/// mouse on a drawn key and a key on a keyboard are one gesture, and anything that hears one and
/// not the others is a picture that is wrong depending on which you used.
///
/// No window. <see cref="TrackerViewModel"/> takes an engine that makes no sound, which is what
/// the transport tests already hand it.
/// </remarks>
public class TypedNoteLightsTests
{
    /// <summary>A note typed into the pattern is a key down, as far as anything drawing is concerned.</summary>
    [Fact]
    public void A_note_played_by_hand_lights_the_keyboards()
    {
        var tracker = Bench();

        tracker.Plays.Press(MidiRouter.TheHand, new Note(48), 100);

        Assert.True(tracker.MidiKeys!.Holds(48));
    }

    /// <summary>And letting go puts it out, which is the half that leaves a light stuck.</summary>
    [Fact]
    public void And_letting_go_puts_it_out()
    {
        var tracker = Bench();

        tracker.Plays.Press(MidiRouter.TheHand, new Note(48), 100);
        tracker.Plays.Let(MidiRouter.TheHand, new Note(48));

        Assert.False(tracker.MidiKeys!.Holds(48));
    }

    /// <summary>
    /// A tracker with no monitor behind it still plays, rather than refusing.
    /// </summary>
    /// <remarks>
    /// Which is what it has for the moment between being built and being wired, and what it has
    /// for ever in a test that only wants to hear a note. The road is the same road; it simply
    /// has one fewer thing on the end of it.
    /// </remarks>
    [Fact]
    public void A_tracker_with_nobody_watching_still_plays()
    {
        var tracker = new TrackerViewModel(
            new QuietAudio(), new SoundMachineRack(),
            new ObservableCollection<Recording>(), new SoundMachineProjects());

        tracker.Plays.Press(MidiRouter.TheHand, new Note(48), 100);
        tracker.Plays.Let(MidiRouter.TheHand, new Note(48));
    }

    /// <summary>A tracker with a monitor on it, which is how the application wires one.</summary>
    private static TrackerViewModel Bench()
    {
        var tracker = new TrackerViewModel(
            new QuietAudio(), new SoundMachineRack(),
            new ObservableCollection<Recording>(), new SoundMachineProjects());

        tracker.MidiKeys = new MidiMonitor();

        return tracker;
    }
}
