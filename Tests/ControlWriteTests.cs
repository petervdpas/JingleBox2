using System;
using System.Collections.Generic;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What a controller has done, carried from the port's thread to the drawing thread.
/// </summary>
/// <remarks>
/// The whole of what is worth checking is what happens between the booking and the trip, which is
/// why the trip is handed in: here it is held in a hand and run when the test says so, which is
/// exactly the window a hand on a fast pad falls into.
///
/// It was written because of a fault somebody could see. Two hits of one pad in the same
/// millisecond arrived as one, so a pad in toggle mode was left playing when it had been told to
/// stop, and the light on the screen disagreed with the hand that had played it.
/// </remarks>
public class ControlWriteTests
{
    /// <summary>A link, since a value in flight is kept per link.</summary>
    private static ControlMapping Link(int cc = 20) => new()
    {
        Device = "MPD218 Port A",
        Channel = 1,
        Cc = cc,
        Kind = ControlKind.Mix
    };

    /// <summary>The trip to the drawing thread, held so a test decides when it happens.</summary>
    private sealed class Hand
    {
        /// <summary>What has been booked and not yet run.</summary>
        private readonly List<Action> _booked = new();

        /// <summary>How many trips were asked for, since one trip carries whatever arrived.</summary>
        public int Booked => _booked.Count;

        /// <summary>Takes the booking.</summary>
        public void Post(Action work) => _booked.Add(work);

        /// <summary>Runs what was booked, the way the drawing thread would.</summary>
        public void Run()
        {
            var due = _booked.ToArray();

            _booked.Clear();

            foreach (var work in due) work();
        }
    }

    /// <summary>Two presses in one trip are two presses.</summary>
    [Fact]
    public void Presses_do_not_coalesce()
    {
        var hand = new Hand();
        var writes = new ControlWrites(hand.Post);

        int fired = 0;

        writes.Pressed(_ => fired++, 1);
        writes.Pressed(_ => fired++, 1);

        hand.Run();

        Assert.Equal(2, fired);
    }

    /// <summary>And they land in the order they were made.</summary>
    [Fact]
    public void Presses_land_in_order()
    {
        var hand = new Hand();
        var writes = new ControlWrites(hand.Post);

        var said = new List<int>();

        writes.Pressed(_ => said.Add(1), 1);
        writes.Pressed(_ => said.Add(2), 1);
        writes.Pressed(_ => said.Add(3), 1);

        hand.Run();

        Assert.Equal(new[] { 1, 2, 3 }, said);
    }

    /// <summary>A knob's position still coalesces, which is what the trip is for.</summary>
    [Fact]
    public void A_position_keeps_only_where_it_ended_up()
    {
        var hand = new Hand();
        var writes = new ControlWrites(hand.Post);
        var link = Link();

        var landed = new List<double>();

        for (int step = 1; step <= 20; step++) writes.Moved(link, landed.Add, step);

        hand.Run();

        Assert.Equal(new[] { 20.0 }, landed);
    }

    /// <summary>Two links moving at once are two values, since one link has one in flight.</summary>
    [Fact]
    public void Two_links_keep_a_value_each()
    {
        var hand = new Hand();
        var writes = new ControlWrites(hand.Post);

        var landed = new List<double>();

        writes.Moved(Link(20), landed.Add, 1);
        writes.Moved(Link(21), landed.Add, 2);

        hand.Run();

        Assert.Equal(2, landed.Count);
    }

    /// <summary>Whatever arrives before the trip runs shares that one trip.</summary>
    [Fact]
    public void One_trip_carries_the_lot()
    {
        var hand = new Hand();
        var writes = new ControlWrites(hand.Post);
        var link = Link();

        writes.Moved(link, _ => { }, 1);
        writes.Pressed(_ => { }, 1);
        writes.Moved(link, _ => { }, 2);

        Assert.Equal(1, hand.Booked);
    }

    /// <summary>And the next one books another, or nothing would ever land again.</summary>
    [Fact]
    public void The_next_one_books_another_trip()
    {
        var hand = new Hand();
        var writes = new ControlWrites(hand.Post);

        writes.Pressed(_ => { }, 1);
        hand.Run();

        writes.Pressed(_ => { }, 1);

        Assert.Equal(1, hand.Booked);
    }

    /// <summary>A value on its way is what the parameter is about to hold.</summary>
    /// <remarks>
    /// What anything working out its next value from this one has to read, since between the
    /// message and the drawing thread the parameter still holds the old number.
    /// </remarks>
    [Fact]
    public void A_value_in_flight_can_be_read_back()
    {
        var hand = new Hand();
        var writes = new ControlWrites(hand.Post);
        var link = Link();

        writes.Moved(link, _ => { }, 0.75);

        Assert.Equal(0.75, writes.Waiting(link));

        hand.Run();

        Assert.Null(writes.Waiting(link));
    }

    /// <summary>A press is not a value, so nothing is waiting to be picked up from one.</summary>
    [Fact]
    public void A_press_is_never_waiting()
    {
        var hand = new Hand();
        var writes = new ControlWrites(hand.Post);
        var link = Link();

        writes.Pressed(_ => { }, 1);

        Assert.Null(writes.Waiting(link));
    }

    /// <summary>One write that throws does not take the rest of the desk with it.</summary>
    [Fact]
    public void A_write_that_throws_is_swallowed()
    {
        var hand = new Hand();
        var writes = new ControlWrites(hand.Post);

        int fired = 0;

        writes.Pressed(_ => throw new InvalidOperationException("no"), 1);
        writes.Pressed(_ => fired++, 1);
        writes.Moved(Link(), _ => fired++, 1);

        hand.Run();

        Assert.Equal(2, fired);
    }

    /// <summary>
    /// More presses than one trip will carry are dropped rather than kept for ever.
    /// </summary>
    /// <remarks>
    /// A hand cannot make sixty four presses between two frames, so anything past that is a
    /// device sending nonsense or a drawing thread that has stopped, and a list that grows until
    /// the memory does helps neither.
    /// </remarks>
    [Fact]
    public void A_flood_of_presses_is_bounded()
    {
        var hand = new Hand();
        var writes = new ControlWrites(hand.Post);

        int fired = 0;

        for (int press = 0; press < 500; press++) writes.Pressed(_ => fired++, 1);

        hand.Run();

        Assert.Equal(64, fired);
    }
}
