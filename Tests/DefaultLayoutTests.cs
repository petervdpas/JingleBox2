using System.Collections.Generic;
using System.Linq;
using JingleBox2.Machines;
using JingleBox2.Midi;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What a controller does before anybody has pointed it at anything.
/// </summary>
/// <remarks>
/// Faders are the first tracks' levels, pinned one per track, and encoders are the controls on
/// the face in front of you, in the order the panel reads. It works on hardware nobody has
/// written a file for, nothing is stored, and any link somebody made beats it, so the worst it
/// can be is uninteresting.
/// <para>
/// The tests run in three groups. First a device nobody has described, where the kind of a
/// control is worked out by watching the stream. Then the devices with a file, where the file
/// says what a control is without waiting and decides whether it lands on the mixer or on the
/// machine in front of you. Last the edges: the layout switched off, and notes, which are not
/// its business.
/// </para>
/// </remarks>
public class DefaultLayoutTests
{
    /// <summary>A controller nobody has ever written a file for.</summary>
    private const string Nobodys = "Some Other Box Port 1";

    /// <summary>An MPD218: six knobs and no faders at all.</summary>
    private const string Mpd = "MPD218 Port A";

    /// <summary>A nanoKONTROL2: eight sliders and eight knobs, all of them described.</summary>
    private const string Korg = "nanoKONTROL2 MIDI 1";

    /// <summary>One control change off a device, which is all the layout is ever handed.</summary>
    private static MidiMessage Cc(int number, int value, string device = Nobodys) => new()
    {
        Device = device, Type = MidiMessageType.ControlChange,
        Channel = 1, Value = number, Data = value, IsOn = value > 0
    };

    /// <summary>Works a control until the layout has decided what kind it is.</summary>
    private static ControlMapping? Turn(DefaultLayout layout, int cc, params int[] values)
    {
        ControlMapping? last = null;

        foreach (int value in values) last = layout.For(Cc(cc, value));

        return last;
    }

    /// <summary>A fader: numbers that walk.</summary>
    private static ControlMapping? Fader(DefaultLayout layout, int cc) =>
        Turn(layout, cc, 40, 41, 43, 45);

    /// <summary>An encoder: the same number over and over, counting notches.</summary>
    private static ControlMapping? Encoder(DefaultLayout layout, int cc) =>
        Turn(layout, cc, 65, 65, 65, 65);

    /// <summary>A knob on a device whose file says it is one. One message is enough.</summary>
    private static ControlMapping? Knobbed(DefaultLayout layout, int cc) =>
        layout.For(Cc(cc, 40, Mpd));

    /// <summary>
    /// A fader on a device nobody has described drives a track's level, and it stays on the
    /// track it was given rather than following what is selected.
    /// </summary>
    [Fact]
    public void A_fader_is_a_tracks_level_and_is_pinned_to_that_track()
    {
        var layout = new DefaultLayout();

        var link = Fader(layout, 20);

        Assert.NotNull(link);
        Assert.Equal(ControlKind.Mix, link!.Kind);
        Assert.Equal(MixControl.Volume, link.Mix);
        Assert.Equal(ControlScope.Fixed, link.Scope);
        Assert.Equal(0, link.Track);
    }

    /// <summary>
    /// The leftmost fader is track one, by controller number ascending rather than by which was
    /// touched first.
    /// </summary>
    /// <remarks>
    /// Worked out of order on purpose. Ascending number is right for any program written for a
    /// DAW nobody has heard of, and wrong for one written for a particular instrument, and the
    /// second kind never points at this application.
    /// </remarks>
    [Fact]
    public void Faders_take_the_tracks_in_the_order_their_numbers_run()
    {
        var layout = new DefaultLayout();

        Fader(layout, 22);
        Fader(layout, 20);
        Fader(layout, 21);

        Assert.Equal(0, Fader(layout, 20)!.Track);
        Assert.Equal(1, Fader(layout, 21)!.Track);
        Assert.Equal(2, Fader(layout, 22)!.Track);
    }

    /// <summary>
    /// An encoder points at a place on whatever panel is open, never at a machine or a
    /// parameter, because the machine in front of you tomorrow is not the one in front of you
    /// now.
    /// </summary>
    /// <remarks>
    /// A profile can know a MiniLab has eight encoders and can never know that encoder three
    /// should be a filter, so a layout is expressed against the machine rather than the device.
    /// </remarks>
    [Fact]
    public void An_encoder_is_a_control_on_the_face_in_front_of_you()
    {
        var layout = new DefaultLayout();

        var link = Encoder(layout, 30);

        Assert.NotNull(link);
        Assert.Equal(ControlKind.Instrument, link!.Kind);
        Assert.Equal(ControlScope.Focused, link.Scope);
        Assert.Equal(0, link.Ordinal);

        Assert.Equal("", link.Machine);
        Assert.Equal("", link.Key);
    }

    /// <summary>
    /// The two kinds are counted apart, or a desk with both would have two first controls
    /// pointed at the same parameter.
    /// </summary>
    [Fact]
    public void Encoders_and_faders_are_counted_apart()
    {
        var layout = new DefaultLayout();

        Fader(layout, 10);
        Encoder(layout, 20);
        Fader(layout, 11);
        Encoder(layout, 21);

        Assert.Equal(0, Fader(layout, 10)!.Track);
        Assert.Equal(1, Fader(layout, 11)!.Track);

        Assert.Equal(0, Encoder(layout, 20)!.Ordinal);
        Assert.Equal(1, Encoder(layout, 21)!.Ordinal);
    }

    /// <summary>
    /// A control does nothing until it is known what kind it is: three messages settles it, and
    /// a guess before that is a parameter thrown across its range in front of you.
    /// </summary>
    [Fact]
    public void Nothing_is_claimed_until_it_is_known_what_the_control_is()
    {
        var layout = new DefaultLayout();

        Assert.Null(layout.For(Cc(20, 40)));
        Assert.Null(layout.For(Cc(20, 41)));

        Assert.NotNull(layout.For(Cc(20, 43)));
    }

    /// <summary>
    /// Pressing something nobody assigned does nothing rather than something surprising.
    /// </summary>
    [Fact]
    public void A_button_is_not_something_a_layout_has_an_opinion_about()
    {
        var layout = new DefaultLayout();

        Assert.Null(Turn(layout, 20, 0, 127, 0, 127));
    }

    /// <summary>
    /// One control gets one mapping object, handed back every time rather than made again.
    /// </summary>
    /// <remarks>
    /// The router keeps each mapping's hand state in a table keyed on the mapping itself, so
    /// a fresh one per message would reset pickup on every message and the knob would jump.
    /// </remarks>
    [Fact]
    public void The_same_control_is_handed_back_the_same_mapping()
    {
        var layout = new DefaultLayout();

        var first = Fader(layout, 20);
        var again = layout.For(Cc(20, 50));

        Assert.Same(first, again);
    }

    /// <summary>
    /// Two desks plugged in at once each start their own count, so the first fader on either is
    /// track one and each mapping remembers which device it came off.
    /// </summary>
    [Fact]
    public void Two_controllers_are_counted_apart()
    {
        var layout = new DefaultLayout();

        Turn(layout, 20, 40, 41, 43);
        foreach (int value in new[] { 40, 41, 43 }) layout.For(Cc(20, value, "Another Box"));

        Assert.Equal(0, layout.For(Cc(20, 44))!.Track);
        Assert.Equal(0, layout.For(Cc(20, 44, "Another Box"))!.Track);
        Assert.Equal("Another Box", layout.For(Cc(20, 44, "Another Box"))!.Device);
    }

    /// <summary>
    /// A MiniLab's slider is a fader because its file says so, not after three messages.
    /// </summary>
    [Fact]
    public void A_controllers_own_file_says_what_a_control_is_without_waiting()
    {
        var layout = new DefaultLayout();

        Controllers.ControllerProfiles.Saw("Minilab3 MIDI", 1, 86);

        var link = layout.For(new MidiMessage
        {
            Device = "Minilab3 MIDI", Type = MidiMessageType.ControlChange,
            Channel = 1, Value = 14, Data = 40, IsOn = true
        });

        Assert.NotNull(link);
        Assert.Equal(ControlKind.Mix, link!.Kind);
    }

    /// <summary>
    /// An MPD218's knobs land on the machine in front of you, and they are picked up because a
    /// knob says where it is.
    /// </summary>
    /// <remarks>
    /// Six knobs and no faders. Left on the mixer they would be a six channel desk on a box
    /// built for hitting things, which is the wrong half of the application to land on. This is
    /// the whole of what a profile adds to a layout: watching cannot tell a knob from a fader,
    /// since both report a position and are picked up identically.
    /// </remarks>
    [Fact]
    public void An_mpd218s_knobs_drive_the_machine_rather_than_the_mixer()
    {
        var layout = new DefaultLayout();

        Controllers.ControllerProfiles.Saw(Mpd, 1, 22);

        var link = Knobbed(layout, 22);

        Assert.NotNull(link);
        Assert.Equal(ControlKind.Instrument, link!.Kind);
        Assert.Equal(ControlScope.Focused, link.Scope);
        Assert.Equal(0, link.Ordinal);

        Assert.Equal(ControlPickup.Takeover, link.Pickup);
    }

    /// <summary>
    /// Knobs take the panel's controls by controller number ascending, whatever order they were
    /// touched in.
    /// </summary>
    /// <remarks>
    /// A knob nobody has touched yet turning up in between moves the one above it, which is
    /// accepted rather than engineered around: any link somebody makes beats all of this.
    /// </remarks>
    [Fact]
    public void And_they_take_the_machines_controls_in_the_order_their_numbers_run()
    {
        var layout = new DefaultLayout();

        Controllers.ControllerProfiles.Saw(Mpd, 1, 22);

        Knobbed(layout, 25);
        Knobbed(layout, 22);

        Assert.Equal(0, Knobbed(layout, 22)!.Ordinal);
        Assert.Equal(1, Knobbed(layout, 25)!.Ordinal);

        Knobbed(layout, 23);

        Assert.Equal(1, Knobbed(layout, 23)!.Ordinal);
        Assert.Equal(2, Knobbed(layout, 25)!.Ordinal);
    }

    /// <summary>
    /// Teaching the layout about knobs took nothing away from a device with no file: its round
    /// controls stay on the mixer exactly as before.
    /// </summary>
    /// <remarks>
    /// A knob and a fader both report a position and are picked up identically, so watching
    /// cannot tell them apart and does not try. Saying which is which is the whole of what a
    /// profile adds here, and without one nothing changes.
    /// </remarks>
    [Fact]
    public void A_device_nobody_has_written_a_file_for_keeps_its_knobs_on_the_mixer()
    {
        var layout = new DefaultLayout();

        var link = Fader(layout, 20);

        Assert.NotNull(link);
        Assert.Equal(ControlKind.Mix, link!.Kind);
    }

    /// <summary>
    /// A nanoKONTROL2 is a working mixer and a working panel the moment it is unwrapped.
    /// </summary>
    /// <remarks>
    /// Eight faders on the first eight tracks, which is what a mixer is, and eight knobs on
    /// whatever panel is open. Nobody linked any of it. This is the whole reason the two
    /// words are kept apart in a controller's file. The two kinds are counted apart, so with the
    /// whole surface worked slider 8 is track 8 and knob 8 is the machine's eighth control,
    /// rather than the two sharing one run of sixteen; and a strip button is not something a
    /// layout has an opinion about.
    /// </remarks>
    [Fact]
    public void A_nanokontrols_sliders_go_to_the_mixer_and_its_knobs_to_the_machine()
    {
        var layout = new DefaultLayout();

        Controllers.ControllerProfiles.Saw(Korg, 1, 0);

        var slider = layout.For(Cc(0, 40, Korg));
        var knob = layout.For(Cc(16, 40, Korg));

        Assert.Equal(ControlKind.Mix, slider!.Kind);
        Assert.Equal(ControlScope.Fixed, slider.Scope);
        Assert.Equal(0, slider.Track);

        Assert.Equal(ControlKind.Instrument, knob!.Kind);
        Assert.Equal(ControlScope.Focused, knob.Scope);
        Assert.Equal(0, knob.Ordinal);

        for (int at = 0; at < 8; at++)
        {
            layout.For(Cc(at, 40, Korg));
            layout.For(Cc(16 + at, 40, Korg));
        }

        Assert.Equal(7, layout.For(Cc(7, 40, Korg))!.Track);
        Assert.Equal(7, layout.For(Cc(23, 40, Korg))!.Ordinal);

        Assert.Null(layout.For(Cc(41, 127, Korg)));
    }

    /// <summary>
    /// A modulation strip is the control that looks like it belongs on the mixer and does not.
    /// </summary>
    /// <remarks>
    /// It is picked up exactly as a fader is, so it would be easy to file it with them. It
    /// springs back, which is the whole difference: a track whose level it drove would drop
    /// to nothing the moment a thumb came off.
    /// </remarks>
    [Fact]
    public void A_modulation_strip_is_left_alone()
    {
        var layout = new DefaultLayout();

        Controllers.ControllerProfiles.Saw("Minilab3 MIDI", 1, 86);

        Assert.Null(layout.For(new MidiMessage
        {
            Device = "Minilab3 MIDI", Type = MidiMessageType.ControlChange,
            Channel = 1, Value = 1, Data = 40, IsOn = true
        }));
    }

    /// <summary>
    /// The mapping carries what the layout worked out, so the router does not have to work it
    /// out again.
    /// </summary>
    /// <remarks>
    /// Both this and the router listen for three messages to decide the same thing about the
    /// same control. Listening twice in a row means a control does nothing for six messages
    /// the first time it is touched, which is long enough to read as broken.
    /// </remarks>
    [Fact]
    public void What_it_worked_out_is_carried_on_the_mapping()
    {
        var layout = new DefaultLayout();

        Assert.Equal(ControlPickup.Takeover, Fader(layout, 20)!.Pickup);
        Assert.Equal(ControlPickup.Relative, Encoder(layout, 30)!.Pickup);
    }

    /// <summary>Switched off it answers nothing, and every control goes back to unlinked.</summary>
    [Fact]
    public void It_can_be_switched_off_and_then_says_nothing()
    {
        var layout = new DefaultLayout { On = false };

        Assert.Null(Fader(layout, 20));
    }

    /// <summary>
    /// Notes and nothing at all are both refused: a layout is about continuous controllers, and
    /// a pad hit is somebody playing.
    /// </summary>
    [Fact]
    public void Notes_are_not_its_business()
    {
        var layout = new DefaultLayout();

        Assert.Null(layout.For(new MidiMessage
        {
            Device = Nobodys, Type = MidiMessageType.Note, Channel = 1, Value = 60, Data = 100, IsOn = true
        }));

        Assert.Null(layout.For(null));
    }
}

/// <summary>The order a panel reads in, which is what "the third knob" means.</summary>
/// <remarks>
/// A default layout points an encoder at a place rather than at a parameter, so the place has to
/// mean the same thing to a person and to the code: the third control your eye lands on.
/// </remarks>
public class PanelOrderTests
{
    /// <summary>A knob on a panel, which is the one element kind that always turns something.</summary>
    private static MachineElement Knob(string parameter) =>
        new() { Element = MachineElementKinds.Knob, Parameter = parameter };

    /// <summary>A panel holding the given elements straight under its root grid.</summary>
    private static MachinePanel Panel(params MachineElement[] children)
    {
        var root = new MachineElement { Element = MachineElementKinds.Grid };
        root.Children.AddRange(children);

        return new MachinePanel { Root = root };
    }

    /// <summary>Controls come back in the order they are drawn, which is how they are read.</summary>
    [Fact]
    public void Controls_come_in_the_order_the_eye_goes_over_them()
    {
        var panel = Panel(Knob("cutoff"), Knob("resonance"), Knob("drive"));

        Assert.Equal(new[] { "cutoff", "resonance", "drive" }, PanelOrder.Of(panel));
    }

    /// <summary>
    /// A group is walked where it stands, so its contents come before whatever follows the group
    /// rather than after everything else.
    /// </summary>
    [Fact]
    public void Everything_in_a_group_comes_before_what_stands_after_it()
    {
        var group = new MachineElement { Element = MachineElementKinds.Group };
        group.Children.Add(Knob("attack"));
        group.Children.Add(Knob("decay"));

        var panel = Panel(Knob("cutoff"), group, Knob("level"));

        Assert.Equal(new[] { "cutoff", "attack", "decay", "level" }, PanelOrder.Of(panel));
    }

    /// <summary>
    /// One parameter is one place however many elements show it, which happens wherever a value
    /// is printed beside the knob that turns it.
    /// </summary>
    [Fact]
    public void A_parameter_shown_twice_keeps_the_place_of_the_first()
    {
        var panel = Panel(Knob("cutoff"), Knob("resonance"), Knob("cutoff"));

        Assert.Equal(new[] { "cutoff", "resonance" }, PanelOrder.Of(panel));
    }

    /// <summary>
    /// A label names a thing rather than a value, so it takes no place: an encoder pointed at
    /// one would reach nothing.
    /// </summary>
    [Fact]
    public void Things_that_turn_nothing_are_not_counted()
    {
        var label = new MachineElement { Element = MachineElementKinds.Label };

        var panel = Panel(label, Knob("cutoff"));

        Assert.Equal(new[] { "cutoff" }, PanelOrder.Of(panel));
    }

    /// <summary>
    /// A place answers the parameter standing at it, and a place the panel does not reach that
    /// far answers nothing rather than the nearest one.
    /// </summary>
    [Theory]
    [InlineData(0, "cutoff")]
    [InlineData(1, "resonance")]
    [InlineData(9, "")]
    [InlineData(-1, "")]
    public void And_a_place_answers_the_parameter_at_it(int ordinal, string wanted) =>
        Assert.Equal(wanted, PanelOrder.At(Panel(Knob("cutoff"), Knob("resonance")), ordinal));

    /// <summary>
    /// No panel is not a failure: a knob turned with nothing open reaches nothing quietly.
    /// </summary>
    [Fact]
    public void A_panel_that_is_not_there_reads_as_nothing()
    {
        Assert.Empty(PanelOrder.Of(null));
        Assert.Equal("", PanelOrder.At(null, 0));
    }
}
