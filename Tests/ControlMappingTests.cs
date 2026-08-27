using JingleBox2.Midi;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// One hardware control pointed at one thing, and the two questions asked of it constantly:
/// is this message about you, and are you and that other link the same thing?
/// </summary>
public class ControlMappingTests
{
    private static ControlMapping Link(string device = "Minilab3 MIDI", int cc = 86, int channel = 1) => new()
    {
        Device = device, Channel = channel, Cc = cc,
        Kind = ControlKind.Instrument, Machine = "machine.zampler", Key = "cutoff"
    };

    private static MidiMessage Message(string device, int cc, int channel = 1) => new()
    {
        Device = device, Type = MidiMessageType.ControlChange, Channel = channel, Value = cc, Data = 64
    };

    [Fact]
    public void A_link_answers_its_own_controller_and_number()
    {
        Assert.True(Link().Answers(Message("Minilab3 MIDI", 86)));
    }

    [Fact]
    public void And_not_the_same_number_on_another_controller()
    {
        // Two devices both have a CC 22 and they are not the same knob. Without the name, a
        // second controller would quietly drive whatever the first was pointed at.
        Assert.False(Link().Answers(Message("MPD218 Port A", 86)));
    }

    [Fact]
    public void Nor_another_number_or_another_channel()
    {
        Assert.False(Link().Answers(Message("Minilab3 MIDI", 87)));
        Assert.False(Link().Answers(Message("Minilab3 MIDI", 86, channel: 2)));
    }

    [Fact]
    public void A_link_that_names_no_controller_answers_any()
    {
        // Which is what a mapping made before controllers were recorded reads as.
        Assert.True(Link(device: "").Answers(Message("anything at all", 86)));
    }

    [Fact]
    public void A_note_is_never_a_knob()
    {
        var note = new MidiMessage
        {
            Device = "Minilab3 MIDI", Type = MidiMessageType.Note, Channel = 1, Value = 86, Data = 64
        };

        Assert.False(Link().Answers(note));
        Assert.False(Link().Answers(null!));
    }

    [Fact]
    public void Two_links_on_one_machines_parameter_are_the_same_target()
    {
        var first = Link();
        var second = Link(device: "MPD218 Port A", cc: 20);

        // Pointing a second knob at a filter says you want that knob on it, not that you want
        // two, so the newcomer displaces the old one.
        Assert.True(first.SameTarget(second));
    }

    [Fact]
    public void The_same_parameter_key_on_another_machine_is_not()
    {
        var first = Link();
        var second = Link();
        second.Machine = "machine.oddskilla";

        // Which is what makes one knob a job per machine: Zampler's cutoff and OddSkilla's are
        // two different things and only one can ever answer at a time.
        Assert.False(first.SameTarget(second));
    }

    [Fact]
    public void A_strip_control_is_the_same_target_only_on_the_same_track()
    {
        var first = new ControlMapping { Kind = ControlKind.Mix, Mix = MixControl.Volume, Scope = ControlScope.Fixed, Track = 1 };
        var same = new ControlMapping { Kind = ControlKind.Mix, Mix = MixControl.Volume, Scope = ControlScope.Fixed, Track = 1 };
        var other = new ControlMapping { Kind = ControlKind.Mix, Mix = MixControl.Volume, Scope = ControlScope.Fixed, Track = 2 };

        Assert.True(first.SameTarget(same));
        Assert.False(first.SameTarget(other));
    }

    [Fact]
    public void Two_kinds_of_thing_are_never_the_same_target()
    {
        var knob = Link();
        var strip = new ControlMapping { Kind = ControlKind.Mix, Mix = MixControl.Volume };

        Assert.False(knob.SameTarget(strip));
        Assert.False(knob.SameTarget(null!));
    }

    [Fact]
    public void The_same_physical_control_is_the_same_control_whatever_it_drives()
    {
        var first = Link();
        var second = Link();
        second.Machine = "machine.oddskilla";
        second.Key = "duty";

        Assert.True(first.SameControl(second));
    }

    [Fact]
    public void A_copy_carries_everything()
    {
        var one = Link();
        one.Pickup = ControlPickup.Endless;
        one.Turn = ControlTurn.Twos;
        one.Scope = ControlScope.Fixed;
        one.Track = 3;
        one.Name = "Zampler cutoff";

        var copy = ControlMapping.Copy(one);

        Assert.NotSame(one, copy);
        Assert.True(one.SameControl(copy));
        Assert.True(one.SameTarget(copy));
        Assert.Equal(ControlPickup.Endless, copy.Pickup);
        Assert.Equal(ControlTurn.Twos, copy.Turn);
        Assert.Equal(3, copy.Track);
        Assert.Equal("Zampler cutoff", copy.Name);
    }
}
