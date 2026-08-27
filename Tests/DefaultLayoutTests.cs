using System.Collections.Generic;
using System.Linq;
using JingleBox2.Machines;
using JingleBox2.Midi;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What a controller does before anybody has pointed it at anything.
/// </summary>
public class DefaultLayoutTests
{
    private const string Nobodys = "Some Other Box Port 1";
    private const string Mpd = "MPD218 Port A";
    private const string Korg = "nanoKONTROL2 MIDI 1";

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

    [Fact]
    public void Faders_take_the_tracks_in_the_order_their_numbers_run()
    {
        var layout = new DefaultLayout();

        // Touched out of order on purpose: the order is the numbers, not the touching.
        Fader(layout, 22);
        Fader(layout, 20);
        Fader(layout, 21);

        Assert.Equal(0, Fader(layout, 20)!.Track);
        Assert.Equal(1, Fader(layout, 21)!.Track);
        Assert.Equal(2, Fader(layout, 22)!.Track);
    }

    [Fact]
    public void An_encoder_is_a_control_on_the_face_in_front_of_you()
    {
        var layout = new DefaultLayout();

        var link = Encoder(layout, 30);

        Assert.NotNull(link);
        Assert.Equal(ControlKind.Instrument, link!.Kind);
        Assert.Equal(ControlScope.Focused, link.Scope);
        Assert.Equal(0, link.Ordinal);

        // It names a place, never a machine or a parameter, because the machine in front of you
        // tomorrow is not the one in front of you now.
        Assert.Equal("", link.Machine);
        Assert.Equal("", link.Key);
    }

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

    [Fact]
    public void Nothing_is_claimed_until_it_is_known_what_the_control_is()
    {
        var layout = new DefaultLayout();

        Assert.Null(layout.For(Cc(20, 40)));
        Assert.Null(layout.For(Cc(20, 41)));

        // Three messages settles it, and a guess before that is a parameter thrown across its
        // range in front of you.
        Assert.NotNull(layout.For(Cc(20, 43)));
    }

    [Fact]
    public void A_button_is_not_something_a_layout_has_an_opinion_about()
    {
        var layout = new DefaultLayout();

        // Pressing something nobody assigned should do nothing rather than something surprising.
        Assert.Null(Turn(layout, 20, 0, 127, 0, 127));
    }

    [Fact]
    public void The_same_control_is_handed_back_the_same_mapping()
    {
        // The router keeps each mapping's hand state in a table keyed on the mapping itself, so
        // a fresh one per message would reset pickup on every message and the knob would jump.
        var layout = new DefaultLayout();

        var first = Fader(layout, 20);
        var again = layout.For(Cc(20, 50));

        Assert.Same(first, again);
    }

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

    [Fact]
    public void A_controllers_own_file_says_what_a_control_is_without_waiting()
    {
        var layout = new DefaultLayout();

        // A MiniLab's slider is a fader because its file says so, not after three messages.
        Controllers.ControllerProfiles.Saw("Minilab3 MIDI", 1, 86);

        var link = layout.For(new MidiMessage
        {
            Device = "Minilab3 MIDI", Type = MidiMessageType.ControlChange,
            Channel = 1, Value = 14, Data = 40, IsOn = true
        });

        Assert.NotNull(link);
        Assert.Equal(ControlKind.Mix, link!.Kind);
    }

    [Fact]
    public void An_mpd218s_knobs_drive_the_machine_rather_than_the_mixer()
    {
        var layout = new DefaultLayout();

        // Six knobs and no faders. Left on the mixer they would be a six channel desk on a box
        // built for hitting things, which is the wrong half of the application to land on.
        Controllers.ControllerProfiles.Saw(Mpd, 1, 22);

        var link = Knobbed(layout, 22);

        Assert.NotNull(link);
        Assert.Equal(ControlKind.Instrument, link!.Kind);
        Assert.Equal(ControlScope.Focused, link.Scope);
        Assert.Equal(0, link.Ordinal);

        // And picked up, because a knob says where it is.
        Assert.Equal(ControlPickup.Takeover, link.Pickup);
    }

    [Fact]
    public void And_they_take_the_machines_controls_in_the_order_their_numbers_run()
    {
        var layout = new DefaultLayout();

        Controllers.ControllerProfiles.Saw(Mpd, 1, 22);

        // Touched out of order on purpose: the order is the numbers, not the touching.
        Knobbed(layout, 25);
        Knobbed(layout, 22);

        Assert.Equal(0, Knobbed(layout, 22)!.Ordinal);
        Assert.Equal(1, Knobbed(layout, 25)!.Ordinal);

        // And a knob nobody has touched yet turning up in between moves the one above it, which
        // is accepted rather than engineered around: any link somebody makes beats all of this.
        Knobbed(layout, 23);

        Assert.Equal(1, Knobbed(layout, 23)!.Ordinal);
        Assert.Equal(2, Knobbed(layout, 25)!.Ordinal);
    }

    [Fact]
    public void A_device_nobody_has_written_a_file_for_keeps_its_knobs_on_the_mixer()
    {
        var layout = new DefaultLayout();

        // A knob and a fader both report a position and are picked up identically, so watching
        // cannot tell them apart and does not try. Saying which is which is the whole of what a
        // profile adds here, and without one nothing changes.
        var link = Fader(layout, 20);

        Assert.NotNull(link);
        Assert.Equal(ControlKind.Mix, link!.Kind);
    }

    [Fact]
    public void A_nanokontrols_sliders_go_to_the_mixer_and_its_knobs_to_the_machine()
    {
        var layout = new DefaultLayout();

        Controllers.ControllerProfiles.Saw(Korg, 1, 0);

        // Eight faders on the first eight tracks, which is what a mixer is, and eight knobs on
        // whatever panel is open. Nobody linked any of it. This is the whole reason the two
        // words are kept apart in a controller's file.
        var slider = layout.For(Cc(0, 40, Korg));
        var knob = layout.For(Cc(16, 40, Korg));

        Assert.Equal(ControlKind.Mix, slider!.Kind);
        Assert.Equal(ControlScope.Fixed, slider.Scope);
        Assert.Equal(0, slider.Track);

        Assert.Equal(ControlKind.Instrument, knob!.Kind);
        Assert.Equal(ControlScope.Focused, knob.Scope);
        Assert.Equal(0, knob.Ordinal);

        // Counted apart, so with the whole surface worked slider 8 is track 8 and knob 8 is the
        // machine's eighth control, rather than the two kinds sharing one run of sixteen.
        for (int at = 0; at < 8; at++)
        {
            layout.For(Cc(at, 40, Korg));
            layout.For(Cc(16 + at, 40, Korg));
        }

        Assert.Equal(7, layout.For(Cc(7, 40, Korg))!.Track);
        Assert.Equal(7, layout.For(Cc(23, 40, Korg))!.Ordinal);

        // And a strip button is not something a layout has an opinion about.
        Assert.Null(layout.For(Cc(41, 127, Korg)));
    }

    [Fact]
    public void A_modulation_strip_is_left_alone()
    {
        var layout = new DefaultLayout();

        // It is picked up exactly as a fader is, so it would be easy to file it with them. It
        // springs back, which is the whole difference: a track whose level it drove would drop
        // to nothing the moment a thumb came off.
        Controllers.ControllerProfiles.Saw("Minilab3 MIDI", 1, 86);

        Assert.Null(layout.For(new MidiMessage
        {
            Device = "Minilab3 MIDI", Type = MidiMessageType.ControlChange,
            Channel = 1, Value = 1, Data = 40, IsOn = true
        }));
    }

    [Fact]
    public void What_it_worked_out_is_carried_on_the_mapping()
    {
        // Both this and the router listen for three messages to decide the same thing about the
        // same control. Listening twice in a row means a control does nothing for six messages
        // the first time it is touched, which is long enough to read as broken.
        var layout = new DefaultLayout();

        Assert.Equal(ControlPickup.Takeover, Fader(layout, 20)!.Pickup);
        Assert.Equal(ControlPickup.Relative, Encoder(layout, 30)!.Pickup);
    }

    [Fact]
    public void It_can_be_switched_off_and_then_says_nothing()
    {
        var layout = new DefaultLayout { On = false };

        Assert.Null(Fader(layout, 20));
    }

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
public class PanelOrderTests
{
    private static MachineElement Knob(string parameter) =>
        new() { Element = MachineElementKinds.Knob, Parameter = parameter };

    private static MachinePanel Panel(params MachineElement[] children)
    {
        var root = new MachineElement { Element = MachineElementKinds.Grid };
        root.Children.AddRange(children);

        return new MachinePanel { Root = root };
    }

    [Fact]
    public void Controls_come_in_the_order_the_eye_goes_over_them()
    {
        var panel = Panel(Knob("cutoff"), Knob("resonance"), Knob("drive"));

        Assert.Equal(new[] { "cutoff", "resonance", "drive" }, PanelOrder.Of(panel));
    }

    [Fact]
    public void Everything_in_a_group_comes_before_what_stands_after_it()
    {
        var group = new MachineElement { Element = MachineElementKinds.Group };
        group.Children.Add(Knob("attack"));
        group.Children.Add(Knob("decay"));

        var panel = Panel(Knob("cutoff"), group, Knob("level"));

        Assert.Equal(new[] { "cutoff", "attack", "decay", "level" }, PanelOrder.Of(panel));
    }

    [Fact]
    public void A_parameter_shown_twice_keeps_the_place_of_the_first()
    {
        // Which happens wherever a value is printed beside the knob that turns it.
        var panel = Panel(Knob("cutoff"), Knob("resonance"), Knob("cutoff"));

        Assert.Equal(new[] { "cutoff", "resonance" }, PanelOrder.Of(panel));
    }

    [Fact]
    public void Things_that_turn_nothing_are_not_counted()
    {
        var label = new MachineElement { Element = MachineElementKinds.Label };

        var panel = Panel(label, Knob("cutoff"));

        Assert.Equal(new[] { "cutoff" }, PanelOrder.Of(panel));
    }

    [Theory]
    [InlineData(0, "cutoff")]
    [InlineData(1, "resonance")]
    [InlineData(9, "")]
    [InlineData(-1, "")]
    public void And_a_place_answers_the_parameter_at_it(int ordinal, string wanted) =>
        Assert.Equal(wanted, PanelOrder.At(Panel(Knob("cutoff"), Knob("resonance")), ordinal));

    [Fact]
    public void A_panel_that_is_not_there_reads_as_nothing()
    {
        Assert.Empty(PanelOrder.Of(null));
        Assert.Equal("", PanelOrder.At(null, 0));
    }
}
