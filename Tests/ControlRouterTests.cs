using System.Collections.Generic;
using JingleBox2.Midi;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A knob is a stream, and reconciling it with a parameter that is somewhere else is the whole
/// of what this router does.
/// </summary>
public class ControlRouterTests
{
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

    private static MidiMessage Turn(ControlMapping link, int value) => new()
    {
        Device = link.Device,
        Type = MidiMessageType.ControlChange,
        Channel = link.Channel,
        Value = link.Cc,
        Data = value,
        IsOn = value > 0
    };

    private static (MidiControlRouter Router, Knob Knob, ControlMapping Link) Desk(
        ControlPickup pickup, double at = 0.5, string device = "MPD218 Port A", int cc = 20)
    {
        var link = Link(pickup, device, cc);
        var knob = new Knob(at);
        var list = new List<ControlMapping> { link };

        return (new MidiControlRouter(() => list, new OneTarget(knob)), knob, link);
    }

    [Fact]
    public void A_control_that_jumps_follows_at_once()
    {
        var (router, knob, link) = Desk(ControlPickup.Jump);

        router.Handle(Turn(link, 127));

        Assert.Equal(1.0, knob.Value, 3);
    }

    [Fact]
    public void A_control_that_picks_up_does_nothing_until_the_hand_passes_the_value()
    {
        var (router, knob, link) = Desk(ControlPickup.Takeover, at: 0.8);

        for (int value = 0; value < 60; value++) router.Handle(Turn(link, value));

        Assert.Equal(0, knob.Writes);
        Assert.Equal(0.8, knob.Value, 3);
    }

    [Fact]
    public void And_follows_once_it_has()
    {
        var (router, knob, link) = Desk(ControlPickup.Takeover, at: 0.5);

        for (int value = 0; value <= 127; value += 4) router.Handle(Turn(link, value));

        Assert.True(knob.Writes > 0);

        // Where the hand ended, which is 124 of 127 because of the step, not the top.
        Assert.Equal(124 / 127.0, knob.Value, 3);
    }

    [Fact]
    public void An_endless_knob_moves_by_the_difference_between_messages()
    {
        var (router, knob, link) = Desk(ControlPickup.Endless, at: 0.5);

        // The first message says where the hand is; the second is the first that can be a move.
        router.Handle(Turn(link, 40));
        router.Handle(Turn(link, 41));

        Assert.True(knob.Value > 0.5);
    }

    [Fact]
    public void An_endless_knob_unwinds_the_wrap_rather_than_leaping()
    {
        var (router, knob, link) = Desk(ControlPickup.Endless, at: 0.5);

        foreach (int value in new[] { 125, 126, 127, 0, 1, 2 }) router.Handle(Turn(link, value));

        // Five notches up, not most of the range down.
        Assert.InRange(knob.Value, 0.51, 0.60);
    }

    [Fact]
    public void A_control_pushing_into_an_end_it_has_reached_is_put_aside_until_it_turns_round()
    {
        var (router, knob, link) = Desk(ControlPickup.Endless, at: 0.5);

        for (int at = 0; at < 100; at++) router.Handle(Turn(link, 40 + (at % 3)));

        // Whatever it did, it cannot have gone past its own end.
        Assert.InRange(knob.Value, 0.0, 1.0);
    }

    [Fact]
    public void Nothing_is_applied_while_it_is_still_being_worked_out()
    {
        var (router, knob, link) = Desk(ControlPickup.Sensed, at: 0.5);

        router.Handle(Turn(link, 60));
        router.Handle(Turn(link, 61));

        Assert.Equal(0, knob.Writes);

        // Three messages settles it, and those three were being listened to rather than obeyed.
        router.Handle(Turn(link, 62));

        Assert.Equal(0, knob.Writes);
        Assert.Equal(ControlPickup.Takeover, link.Pickup);
    }

    [Fact]
    public void What_it_worked_out_is_kept_on_the_mapping()
    {
        var (router, _, link) = Desk(ControlPickup.Sensed);

        foreach (int value in new[] { 0, 127, 0 }) router.Handle(Turn(link, value));

        Assert.Equal(ControlPickup.Jump, link.Pickup);
    }

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

    [Fact]
    public void A_link_just_made_moves_its_parameter_from_the_next_message()
    {
        var (router, knob, link) = Desk(ControlPickup.Takeover, at: 0.9);

        // Pickup exists because the knob and the parameter disagree and your hand has not
        // arrived. Neither is true a second after you pointed at it and turned it.
        router.Caught(link);

        router.Handle(Turn(link, 10));

        Assert.True(knob.Writes > 0);
    }
}
