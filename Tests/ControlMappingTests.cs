using JingleBox2.Midi;
using Xunit;
using JingleBox2.Midi.Enums;

namespace JingleBox2.Tests;

/// <summary>
/// One hardware control pointed at one thing, and the two questions asked of it constantly:
/// is this message about you, and are you and that other link the same thing?
/// </summary>
/// <remarks>
/// The first group is <c>Answers</c>, which runs per message and decides whether a link is being
/// spoken to. The second is <c>SameTarget</c> and <c>SameControl</c>, which run when somebody
/// points a knob at something and decide what that displaces: a link is displaced by exactly two
/// things, the same physical control being pointed elsewhere, or something else being pointed at
/// the same target. The copy is last, and it exists because a mapping is handed round as a
/// template and filled in.
/// </remarks>
public class ControlMappingTests
{
    /// <summary>A link from a MiniLab knob to Zampler's cutoff, which most of these vary from.</summary>
    private static ControlMapping Link(string device = "Minilab3 MIDI", int cc = 86, int channel = 1) => new()
    {
        Device = device, Channel = channel, Cc = cc,
        Kind = ControlKind.Device, Machine = "machine.zampler", Key = "cutoff"
    };

    /// <summary>A controller message as it would arrive off that port.</summary>
    private static MidiMessage Message(string device, int cc, int channel = 1) => new()
    {
        Device = device, Type = MidiMessageType.ControlChange, Channel = channel, Value = cc, Data = 64
    };

    /// <summary>A link answers the device, channel and number it was learned on.</summary>
    [Fact]
    public void A_link_answers_its_own_controller_and_number()
    {
        Assert.True(Link().Answers(Message("Minilab3 MIDI", 86)));
    }

    /// <summary>
    /// The same number on another controller is another knob.
    /// </summary>
    /// <remarks>
    /// Two devices both have a CC 22 and they are not the same knob. Without the name, a second
    /// controller would quietly drive whatever the first was pointed at.
    /// </remarks>
    [Fact]
    public void And_not_the_same_number_on_another_controller()
    {
        Assert.False(Link().Answers(Message("MPD218 Port A", 86)));
    }

    /// <summary>A different number, or the same number on another channel, is not this link.</summary>
    [Fact]
    public void Nor_another_number_or_another_channel()
    {
        Assert.False(Link().Answers(Message("Minilab3 MIDI", 87)));
        Assert.False(Link().Answers(Message("Minilab3 MIDI", 86, channel: 2)));
    }

    /// <summary>
    /// A link naming no controller answers whichever device sends the number.
    /// </summary>
    /// <remarks>
    /// Which is what a mapping made before controllers were recorded reads as, so an older
    /// settings file keeps working rather than going silent.
    /// </remarks>
    [Fact]
    public void A_link_that_names_no_controller_answers_any()
    {
        Assert.True(Link(device: "").Answers(Message("anything at all", 86)));
    }

    /// <summary>
    /// A note is not a knob, and neither is nothing.
    /// </summary>
    /// <remarks>
    /// Note 86 and controller 86 are the same number and different hardware, so a keyboard
    /// playing high enough would otherwise sweep whatever CC 86 drives.
    /// </remarks>
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

    /// <summary>
    /// Two different knobs on one machine's parameter are one target.
    /// </summary>
    /// <remarks>
    /// Pointing a second knob at a filter says you want that knob on it, not that you want two,
    /// so the newcomer displaces the old one.
    /// </remarks>
    [Fact]
    public void Two_links_on_one_machines_parameter_are_the_same_target()
    {
        var first = Link();
        var second = Link(device: "MPD218 Port A", cc: 20);

        Assert.True(first.SameTarget(second));
    }

    /// <summary>
    /// The same parameter key on another machine is another target.
    /// </summary>
    /// <remarks>
    /// Which is what makes one knob a job per machine: Zampler's cutoff and OddSkilla's are two
    /// different things and only one can ever answer at a time, since a link answers only while
    /// the track plays its machine.
    /// </remarks>
    [Fact]
    public void The_same_parameter_key_on_another_machine_is_not()
    {
        var first = Link();
        var second = Link();
        second.Machine = "machine.oddskilla";

        Assert.False(first.SameTarget(second));
    }

    /// <summary>
    /// A mixer link pinned to a track is the same target only on that track.
    /// </summary>
    /// <remarks>
    /// Two faders each pinned to their own strip are what a control surface is, so the track has
    /// to be part of what makes two of these the same thing.
    /// </remarks>
    [Fact]
    public void A_strip_control_is_the_same_target_only_on_the_same_track()
    {
        var first = new ControlMapping { Kind = ControlKind.Mix, Mix = MixControl.Volume, Scope = ControlScope.Fixed, Track = 1 };
        var same = new ControlMapping { Kind = ControlKind.Mix, Mix = MixControl.Volume, Scope = ControlScope.Fixed, Track = 1 };
        var other = new ControlMapping { Kind = ControlKind.Mix, Mix = MixControl.Volume, Scope = ControlScope.Fixed, Track = 2 };

        Assert.True(first.SameTarget(same));
        Assert.False(first.SameTarget(other));
    }

    /// <summary>A machine parameter and a mixer strip are never one target, and neither is nothing.</summary>
    [Fact]
    public void Two_kinds_of_thing_are_never_the_same_target()
    {
        var knob = Link();
        var strip = new ControlMapping { Kind = ControlKind.Mix, Mix = MixControl.Volume };

        Assert.False(knob.SameTarget(strip));
        Assert.False(knob.SameTarget(null!));
    }

    /// <summary>
    /// One knob is one knob whatever it has been pointed at since.
    /// </summary>
    /// <remarks>
    /// This is the other half of what displaces a link: pointing a control somewhere new takes
    /// it off wherever it was.
    /// </remarks>
    [Fact]
    public void The_same_physical_control_is_the_same_control_whatever_it_drives()
    {
        var first = Link();
        var second = Link();
        second.Machine = "machine.oddskilla";
        second.Key = "duty";

        Assert.True(first.SameControl(second));
    }

    /// <summary>
    /// A copy is a separate object carrying every field, the sensed ones included.
    /// </summary>
    /// <remarks>
    /// <c>Pointable.Offers</c> hangs a template on a control and it is copied before it is
    /// offered, because the controller's half is filled into the object that was handed over and
    /// then kept: one shared instance would have every link overwriting the last. A field left
    /// out of the copy is a link that comes back subtly not the one you made.
    /// </remarks>
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
