using System.Collections.Generic;
using System.Collections.ObjectModel;
using JingleBox2.Audio.Records;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.ViewModels;
using JingleBox2.ViewModels.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A link answers only while the thing it is pointed at is the thing in front of you.
/// </summary>
/// <remarks>
/// **The gate a sound device has had all along, for the two things that are not devices.** A link
/// on a machine is refused unless the track really plays it or the rack really has it open, which
/// is the whole reason one knob can carry a template for OddSkilla and another for something else:
/// at most one of them can ever be looking back at you.
///
/// A mixer link names strip one outright and a pad link names a pad, so neither had anything to be
/// refused by and both answered from wherever you were. What that came to was one turn of a knob
/// writing twice: OddSkilla's tune on the rack, which is what was wanted, and the pan of track one
/// beside it, which then marked the song unsaved. Two writes, one hand, and nothing on the screen
/// saying the second had happened.
///
/// The page is the gate for these two, and it is a page, a window or a view: the mixer is taken
/// out into a window of its own and is still the mixer, so what is counted is the view being on
/// the screen rather than which tab is chosen.
/// </remarks>
public sealed class PageGateTests
{
    /// <summary>What is on the screen, as a test says it.</summary>
    private sealed class Showing : IPageInFront
    {
        /// <inheritdoc/>
        public bool Mixer { get; set; }

        /// <inheritdoc/>
        public bool Pads { get; set; }
    }

    /// <summary>A pad box that writes down what it was told rather than playing anything.</summary>
    private sealed class Hits : IPadTrigger
    {
        /// <summary>Every pad that was fired, in order.</summary>
        public List<int> Fired { get; } = new();

        /// <inheritdoc/>
        public void TriggerPad(int padIndex, Midi.Enums.PadTriggerAction action) => Fired.Add(padIndex);
    }

    /// <summary>A tracker with a default song in it, which has tracks and a mixer.</summary>
    private static TrackerViewModel Tracker() =>
        new(new QuietAudio(),
            new SoundMachineRack(),
            new ObservableCollection<Recording>(),
            new SoundMachineProjects());

    /// <summary>A fader on track one, which names its strip and follows nothing.</summary>
    private static ControlMapping Fader() => new()
    {
        Kind = ControlKind.Mix,
        Mix = MixControl.Volume,
        Scope = ControlScope.Fixed,
        Track = 0,
        Device = "nanoKONTROL2",
        Channel = 1,
        Cc = 20
    };

    /// <summary>A button on the first pad.</summary>
    private static ControlMapping Pad() => new()
    {
        Kind = ControlKind.Pad,
        Pad = 0,
        Device = "MPD218 Port A",
        Channel = 10,
        Cc = 36,
        Sends = MidiMessageType.Note
    };

    /// <summary>**A mixer link reaches nothing while the mixer is not the thing in front.**</summary>
    [Fact]
    public void A_fader_is_silent_while_the_mixer_is_not_showing()
    {
        var tracker = Tracker();
        var showing = new Showing { Mixer = false };

        var targets = new ControlTargets(tracker, new SoundMachineProjects(), pages: showing);

        Assert.Null(targets.Find(Fader()));

        showing.Mixer = true;

        Assert.NotNull(targets.Find(Fader()));

        tracker.Finished();
    }

    /// <summary>And a pad link fires nothing while neither page that draws them is showing.</summary>
    [Fact]
    public void A_pad_is_silent_while_the_pads_are_not_showing()
    {
        var tracker = Tracker();
        var showing = new Showing { Pads = false };

        var targets = new ControlTargets(
            tracker, new SoundMachineProjects(), pads: new Hits(), pages: showing);

        Assert.Null(targets.Find(Pad()));

        showing.Pads = true;

        Assert.NotNull(targets.Find(Pad()));

        tracker.Finished();
    }

    /// <summary>
    /// The master is a strip like any other here, so it is gated like any other.
    /// </summary>
    /// <remarks>
    /// It reaches the resolver down a branch of its own, being a strip without being a track, so
    /// it is asked separately: a gate written on the track path alone would leave the master's
    /// fader answering from every page.
    /// </remarks>
    [Fact]
    public void The_master_is_gated_with_the_rest_of_the_desk()
    {
        var tracker = Tracker();
        var showing = new Showing { Mixer = false };

        var targets = new ControlTargets(tracker, new SoundMachineProjects(), pages: showing);

        var master = Fader();
        master.Track = JingleBox2.Tracker.TrackerPlayer.MasterStrip;

        Assert.Null(targets.Find(master));

        showing.Mixer = true;

        Assert.NotNull(targets.Find(master));

        tracker.Finished();
    }

    /// <summary>
    /// With nothing saying what is on the screen, everything answers.
    /// </summary>
    /// <remarks>
    /// Which is what this did before the gate existed, and is what anything built without a screen
    /// wants: a rule about what you are looking at cannot be asked where there is nothing to look
    /// at.
    /// </remarks>
    [Fact]
    public void With_no_screen_to_ask_about_everything_answers()
    {
        var tracker = Tracker();

        var targets = new ControlTargets(tracker, new SoundMachineProjects(), pads: new Hits());

        Assert.NotNull(targets.Find(Fader()));
        Assert.NotNull(targets.Find(Pad()));

        tracker.Finished();
    }
}
