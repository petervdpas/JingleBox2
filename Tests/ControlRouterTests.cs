using System.Collections.Generic;
using JingleBox2.Midi;
using Xunit;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tests;

/// <summary>
/// A knob is a stream, and reconciling it with a parameter that is somewhere else is the whole
/// of what this router does.
/// </summary>
/// <remarks>
/// Read in three groups. First what each pickup rule does to a parameter: jump, pick up, and the
/// endless knob that moves by the difference between messages and has to unwind a wrap rather
/// than leap. Then what happens while the kind is still being worked out, and which messages a
/// link refuses. Last the default layout, which is what a control nobody has pointed at anything
/// does, and the rule that a link just made moves its parameter from the very next message.
/// </remarks>
public class ControlRouterTests
{
    /// <summary>A link from an MPD218 knob to OddSkilla's duty, with the pickup rule under test.</summary>
    private static ControlMapping Link(ControlPickup pickup, string device = "MPD218 Port A", int cc = 20) => new()
    {
        Device = device,
        Channel = 1,
        Cc = cc,
        Kind = ControlKind.Instrument,
        Machine = "machine.oddskilla",
        Key = "duty",
        Pickup = pickup
    };

    /// <summary>That link's own controller message, carrying one value.</summary>
    private static MidiMessage Turn(ControlMapping link, int value) => new()
    {
        Device = link.Device,
        Type = MidiMessageType.ControlChange,
        Channel = link.Channel,
        Value = link.Cc,
        Data = value,
        IsOn = value > 0
    };

    /// <summary>
    /// A router, the parameter under it and the link between them, with the parameter starting
    /// wherever the test needs it to disagree with the hand.
    /// </summary>
    private static (MidiControlRouter Router, Knob Knob, ControlMapping Link) Desk(
        ControlPickup pickup, double at = 0.5, string device = "MPD218 Port A", int cc = 20)
    {
        var link = Link(pickup, device, cc);
        var knob = new Knob(at);
        var list = new List<ControlMapping> { link };

        return (new MidiControlRouter(() => list, new OneTarget(knob)), knob, link);
    }

    /// <summary>A button's parameter goes where the button says, with nothing to reconcile.</summary>
    [Fact]
    public void A_control_that_jumps_follows_at_once()
    {
        var (router, knob, link) = Desk(ControlPickup.Jump);

        router.Handle(Turn(link, 127));

        Assert.Equal(1.0, knob.Value, 3);
    }

    /// <summary>
    /// A position-reporting control moves nothing until the hand crosses the value.
    /// </summary>
    /// <remarks>
    /// A fader sitting at its floor while the parameter is at 0.8 would otherwise drop the
    /// parameter to the floor the instant it was touched.
    /// </remarks>
    [Fact]
    public void A_control_that_picks_up_does_nothing_until_the_hand_passes_the_value()
    {
        var (router, knob, link) = Desk(ControlPickup.Takeover, at: 0.8);

        for (int value = 0; value < 60; value++) router.Handle(Turn(link, value));

        Assert.Equal(0, knob.Writes);
        Assert.Equal(0.8, knob.Value, 3);
    }

    /// <summary>
    /// And once it has crossed, the parameter is wherever the hand ended.
    /// </summary>
    /// <remarks>
    /// 124 of 127 rather than the top, because the sweep steps by four and 124 is the last value
    /// it lands on.
    /// </remarks>
    [Fact]
    public void And_follows_once_it_has()
    {
        var (router, knob, link) = Desk(ControlPickup.Takeover, at: 0.5);

        for (int value = 0; value <= 127; value += 4) router.Handle(Turn(link, value));

        Assert.True(knob.Writes > 0);

        Assert.Equal(124 / 127.0, knob.Value, 3);
    }

    /// <summary>
    /// An endless knob is read as movement between messages rather than as a position.
    /// </summary>
    /// <remarks>
    /// The first message says only where the hand is; the second is the first that can be a
    /// move, since a difference needs two readings.
    /// </remarks>
    [Fact]
    public void An_endless_knob_moves_by_the_difference_between_messages()
    {
        var (router, knob, link) = Desk(ControlPickup.Endless, at: 0.5);

        router.Handle(Turn(link, 40));
        router.Handle(Turn(link, 41));

        Assert.True(knob.Value > 0.5);
    }

    /// <summary>
    /// Coming round the top is five notches up, not most of the range down.
    /// </summary>
    /// <remarks>
    /// Read as a plain difference, 127 to 0 is the whole range in one step, and the parameter
    /// would be thrown to its floor every time the knob passed its own seam.
    /// </remarks>
    [Fact]
    public void An_endless_knob_unwinds_the_wrap_rather_than_leaping()
    {
        var (router, knob, link) = Desk(ControlPickup.Endless, at: 0.5);

        foreach (int value in new[] { 125, 126, 127, 0, 1, 2 }) router.Handle(Turn(link, value));

        Assert.InRange(knob.Value, 0.51, 0.60);
    }

    /// <summary>
    /// A control pushed into an end it has already reached is parked until the stream turns round.
    /// </summary>
    /// <remarks>
    /// Whatever the hundred messages did, the parameter cannot have gone past its own end.
    /// </remarks>
    [Fact]
    public void A_control_pushing_into_an_end_it_has_reached_is_put_aside_until_it_turns_round()
    {
        var (router, knob, link) = Desk(ControlPickup.Endless, at: 0.5);

        for (int at = 0; at < 100; at++) router.Handle(Turn(link, 40 + (at % 3)));

        Assert.InRange(knob.Value, 0.0, 1.0);
    }

    /// <summary>
    /// The three messages that decide what a control is are listened to rather than obeyed.
    /// </summary>
    /// <remarks>
    /// Three messages settles it, and applying them on the way past would move the parameter by
    /// whatever the sensing happened to see before it knew what it was looking at.
    /// </remarks>
    [Fact]
    public void Nothing_is_applied_while_it_is_still_being_worked_out()
    {
        var (router, knob, link) = Desk(ControlPickup.Sensed, at: 0.5);

        router.Handle(Turn(link, 60));
        router.Handle(Turn(link, 61));

        Assert.Equal(0, knob.Writes);

        router.Handle(Turn(link, 62));

        Assert.Equal(0, knob.Writes);
        Assert.Equal(ControlPickup.Takeover, link.Pickup);
    }

    /// <summary>
    /// What the sensing decided is written back onto the mapping, so it is decided once.
    /// </summary>
    /// <remarks>
    /// Otherwise every session would begin by spending three messages of the owner's hand
    /// working out what a control it has already met is.
    /// </remarks>
    [Fact]
    public void What_it_worked_out_is_kept_on_the_mapping()
    {
        var (router, _, link) = Desk(ControlPickup.Sensed);

        foreach (int value in new[] { 0, 127, 0 }) router.Handle(Turn(link, value));

        Assert.Equal(ControlPickup.Jump, link.Pickup);
    }

    /// <summary>
    /// The same number arriving from another box is not this link.
    /// </summary>
    /// <remarks>
    /// A link records the controller it was learned on precisely so a second device cannot drive
    /// what the first was pointed at.
    /// </remarks>
    [Fact]
    public void A_message_from_another_controller_is_not_this_link()
    {
        var (router, knob, link) = Desk(ControlPickup.Jump);

        var elsewhere = Turn(link, 127);
        var other = new MidiMessage
        {
            Device = "Some other box",
            Type = MidiMessageType.ControlChange,
            Channel = elsewhere.Channel,
            Value = elsewhere.Value,
            Data = 127,
            IsOn = true
        };

        router.Handle(other);

        Assert.Equal(0, knob.Writes);
    }

    /// <summary>Note 20 and controller 20 are the same number and different hardware.</summary>
    [Fact]
    public void A_note_is_not_a_knob()
    {
        var (router, knob, link) = Desk(ControlPickup.Jump);

        router.Handle(new MidiMessage
        {
            Device = link.Device, Type = MidiMessageType.Note,
            Channel = 1, Value = 20, Data = 127, IsOn = true
        });

        Assert.Equal(0, knob.Writes);
    }

    /// <summary>Answers only for the mapping it was given, so a test can see which one arrived.</summary>
    private sealed class OnlyFor : IControlTargets
    {
        /// <summary>Takes the one parameter every lookup will be answered with.</summary>
        public OnlyFor(Knob knob) => Knob = knob;

        /// <summary>The parameter handed back, whatever was asked for.</summary>
        public Knob Knob { get; }

        /// <summary>The last mapping looked up, which is what says where a message was routed.</summary>
        public ControlMapping? Asked { get; private set; }

        /// <inheritdoc/>
        public IControlTarget? Find(ControlMapping mapping)
        {
            Asked = mapping;

            return Knob;
        }
    }

    /// <summary>
    /// A control nobody pointed at anything falls to the default layout, by its kind.
    /// </summary>
    /// <remarks>
    /// This one reports positions that walk, so it is a fader, and the layout puts faders on the
    /// mixer, pinned one per track from the first. Three messages settle what it is, and it
    /// drives something from then on.
    /// </remarks>
    [Fact]
    public void A_control_nobody_pointed_at_anything_does_what_its_kind_does()
    {
        var knob = new Knob(0.5);
        var targets = new OnlyFor(knob);
        var layout = new DefaultLayout();

        var router = new MidiControlRouter(() => new List<ControlMapping>(), targets, null, layout);

        foreach (int value in new[] { 40, 41, 43, 45 })
            router.Handle(new MidiMessage
            {
                Device = "Some Other Box", Type = MidiMessageType.ControlChange,
                Channel = 1, Value = 20, Data = value, IsOn = true
            });

        Assert.NotNull(targets.Asked);
        Assert.Equal(ControlKind.Mix, targets.Asked!.Kind);
        Assert.Equal(0, targets.Asked.Track);
    }

    /// <summary>
    /// And a control the layout has just adopted takes over at once rather than picking up.
    /// </summary>
    /// <remarks>
    /// The layout has just watched three messages of this control moving in order to decide what
    /// it is, so the hand is demonstrably on it. Made to pick up as well it would sit dead until
    /// it happened to sweep past the parameter, which reads as a dead control.
    /// </remarks>
    [Fact]
    public void And_it_takes_over_at_once_rather_than_picking_up()
    {
        var knob = new Knob(0.5);
        var targets = new OnlyFor(knob);

        var router = new MidiControlRouter(() => new List<ControlMapping>(), targets, null, new DefaultLayout());

        foreach (int value in new[] { 40, 41, 43, 45 })
            router.Handle(new MidiMessage
            {
                Device = "Some Other Box", Type = MidiMessageType.ControlChange,
                Channel = 1, Value = 20, Data = value, IsOn = true
            });

        Assert.True(knob.Writes > 0);
    }

    /// <summary>
    /// A control somebody did point at something is never taken over by the layout.
    /// </summary>
    /// <remarks>
    /// What arrives at the targets is the link somebody made, not a place the layout invented,
    /// which is the whole of why a default layout is safe to have at all.
    /// </remarks>
    [Fact]
    public void And_a_control_somebody_did_point_at_something_is_not_touched_by_it()
    {
        var link = Link(ControlPickup.Jump, device: "Some Other Box", cc: 20);
        var targets = new OnlyFor(new Knob(0.5));
        var layout = new DefaultLayout();

        var router = new MidiControlRouter(() => new List<ControlMapping> { link }, targets, null, layout);

        router.Handle(Turn(link, 127));

        Assert.Same(link, targets.Asked);
        Assert.Equal(1.0, targets.Knob.Value, 3);
    }

    /// <summary>With no layout given, an unmapped control reaches nothing at all.</summary>
    [Fact]
    public void With_no_layout_at_all_an_unmapped_control_does_nothing()
    {
        var targets = new OnlyFor(new Knob(0.5));

        var router = new MidiControlRouter(() => new List<ControlMapping>(), targets);

        foreach (int value in new[] { 40, 41, 43, 45 })
            router.Handle(new MidiMessage
            {
                Device = "Some Other Box", Type = MidiMessageType.ControlChange,
                Channel = 1, Value = 20, Data = value, IsOn = true
            });

        Assert.Null(targets.Asked);
    }

    /// <summary>
    /// A link just made moves its parameter from the very next message, whatever its pickup says.
    /// </summary>
    /// <remarks>
    /// Pickup exists because the knob and the parameter disagree and your hand has not arrived.
    /// Neither is true a second after you pointed at it and turned it.
    /// </remarks>
    [Fact]
    public void A_link_just_made_moves_its_parameter_from_the_next_message()
    {
        var (router, knob, link) = Desk(ControlPickup.Takeover, at: 0.9);

        router.Caught(link);

        router.Handle(Turn(link, 10));

        Assert.True(knob.Writes > 0);
    }
}
