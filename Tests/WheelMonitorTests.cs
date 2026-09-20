using System;
using System.Collections.Generic;
using JingleBox2.Midi;
using JingleBox2.Midi.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What a drawn wheel reads: the monitor every keyboard in the application watches.
/// </summary>
/// <remarks>
/// A wheel on the screen is a picture of the one under somebody's hand, the same way the drawn
/// keyboard is a picture of which keys are down, and it is right for the same reason: there is
/// one monitor, it is wired to the stream when the application starts, and it is never taken
/// off. So a panel opened mid-bend shows the bend, and two panels open at once agree.
///
/// Nothing here draws anything. The monitor is public and takes no window, which is what lets
/// the whole of it be put a question to.
/// </remarks>
public class WheelMonitorTests
{
    /// <summary>A wheel that went past is where the monitor says it is.</summary>
    [Fact]
    public void The_monitor_holds_where_each_wheel_was_left()
    {
        var monitor = new MidiMonitor();

        monitor.Bend(-0.25);
        monitor.Modulate(0.75);

        Assert.Equal(-0.25, monitor.Lean);
        Assert.Equal(0.75, monitor.Amount);
    }

    /// <summary>And both start at rest, which is where a keyboard nobody has touched is.</summary>
    [Fact]
    public void Both_wheels_start_at_rest()
    {
        var monitor = new MidiMonitor();

        Assert.Equal(0, monitor.Lean);
        Assert.Equal(0, monitor.Amount);
    }

    /// <summary>
    /// Everything that went past goes on past, untouched.
    /// </summary>
    /// <remarks>
    /// The monitor stands in front of whoever plays the notes and must not be a filter: a wheel
    /// that lit the picture and never reached the sound is exactly the fault this whole exercise
    /// started from.
    /// </remarks>
    [Fact]
    public void Every_wheel_move_goes_on_to_whoever_plays_it()
    {
        var played = new Wheels();
        var monitor = new MidiMonitor(turning: played);

        monitor.Bend(1);
        monitor.Modulate(0.5);

        Assert.Equal(new[] { "bend 1", "modulate 0.5" }, played.Said);
    }

    /// <summary>
    /// It says so only when something really moved.
    /// </summary>
    /// <remarks>
    /// A wheel held still sends the same value over and over on some devices, and what listens
    /// to this repaints. A picture redrawn because nothing happened is the shape this codebase
    /// has already paid for twice.
    /// </remarks>
    [Fact]
    public void A_wheel_that_has_not_moved_says_nothing()
    {
        var monitor = new MidiMonitor();
        int said = 0;

        monitor.Moved += (_, _) => said++;

        monitor.Bend(0.5);
        monitor.Bend(0.5);
        monitor.Bend(0.5);

        Assert.Equal(1, said);
    }

    /// <summary>
    /// And a wheel moving is not a key moving.
    /// </summary>
    /// <remarks>
    /// The two are separate events on purpose. A wheel arrives tens of times a second, and a
    /// drawn keyboard with no wheels beside it has no business repainting for any of it.
    /// </remarks>
    [Fact]
    public void A_wheel_does_not_wake_the_keys()
    {
        var monitor = new MidiMonitor();
        int keys = 0;
        int wheels = 0;

        monitor.Changed += (_, _) => keys++;
        monitor.Moved += (_, _) => wheels++;

        monitor.Bend(1);
        monitor.Modulate(1);

        Assert.Equal(0, keys);
        Assert.Equal(2, wheels);

        monitor.Pressed(48);

        Assert.Equal(1, keys);
        Assert.Equal(2, wheels);
    }

    /// <summary>
    /// A monitor with nobody behind it still watches, which is what a panel on its own has.
    /// </summary>
    [Fact]
    public void A_monitor_on_its_own_still_holds_the_wheels()
    {
        var monitor = new MidiMonitor();

        monitor.Bend(1);

        Assert.Equal(1, monitor.Lean);
    }

    /// <summary>
    /// A listener that throws does not stop the wheel reaching the music.
    /// </summary>
    /// <remarks>
    /// The order this class promises, and it was the other way round once: the onlookers were
    /// told first, so a drawn wheel reading one of its own properties from the port's thread
    /// threw, the message left through the port's own delivery, and the bend never happened.
    /// Passing it on is the contract; telling whatever draws a picture of it is a courtesy, and
    /// a courtesy may not cost the thing it is about.
    /// </remarks>
    [Fact]
    public void A_listener_that_throws_does_not_stop_the_music()
    {
        var played = new Wheels();
        var monitor = new MidiMonitor(turning: played);

        monitor.Moved += (_, _) => throw new InvalidOperationException("a picture went wrong");

        Assert.Throws<InvalidOperationException>(() => monitor.Bend(1));

        Assert.Equal(new[] { "bend 1" }, played.Said);
    }

    /// <summary>Somewhere for the passed on wheels to land, in the order they landed.</summary>
    private sealed class Wheels : IWheels
    {
        /// <summary>Each move, in the order it arrived.</summary>
        public List<string> Said { get; } = new();

        /// <inheritdoc/>
        public void Bend(double lean) => Said.Add("bend " + lean.ToString("0.###"));

        /// <inheritdoc/>
        public void Modulate(double amount) => Said.Add("modulate " + amount.ToString("0.###"));
    }
}
