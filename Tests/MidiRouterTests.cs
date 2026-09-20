using System;
using System.Collections.Generic;
using JingleBox2.Midi;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The one place everything a hand does to a note arrives, and what it does with it.
/// </summary>
/// <remarks>
/// It decides nothing about sound, so there is nothing here about instruments, tracks or
/// machines: what is asked is that everything told reaches everything listening, in order, and
/// that one listener falling over costs itself and nothing else.
///
/// No window, no port and no engine. The router is an <see cref="IPlays"/> told by
/// <see cref="IPlays"/> and telling <see cref="IPlays"/>, which is the whole reason it can be
/// put a question to at all.
/// </remarks>
public class MidiRouterTests
{
    /// <summary>Everything told reaches everything listening.</summary>
    [Fact]
    public void Everything_told_reaches_everyone_listening()
    {
        var first = new Heard();
        var second = new Heard();

        var router = new MidiRouter(first, second);

        router.Press(3, new Note(48), 100);
        router.Let(3, new Note(48));
        router.Bend(3, -0.5);
        router.Modulate(3, 1);

        var said = new[] { "press 3 48 100", "let 3 48", "bend 3 -0.5", "modulate 3 1" };

        Assert.Equal(said, first.Said);
        Assert.Equal(said, second.Said);
    }

    /// <summary>
    /// And in the order it was given them, which is what puts the sound before the pictures.
    /// </summary>
    /// <remarks>
    /// The engine is told first on purpose. What has to be right on time is what somebody hears;
    /// a light is late by a frame and nobody can tell.
    /// </remarks>
    [Fact]
    public void They_are_told_in_the_order_they_were_given()
    {
        var order = new List<string>();

        var router = new MidiRouter(new Heard("engine", order), new Heard("lights", order));

        router.Press(0, new Note(48), 64);

        Assert.Equal(new[] { "engine", "lights" }, order);
    }

    /// <summary>
    /// A listener that falls over costs itself and nothing else.
    /// </summary>
    /// <remarks>
    /// The rule this was written knowing the price of. A drawn wheel reading one of its own
    /// properties from the port's thread threw, the exception left through the port's own
    /// delivery callback, and the device was dead for the rest of the session: keys and all,
    /// from touching a strip. A picture going wrong may not take the music with it.
    /// </remarks>
    [Fact]
    public void A_listener_that_throws_costs_only_itself()
    {
        var engine = new Heard();
        var lights = new Heard();

        var router = new MidiRouter(engine, new Throws(), lights);

        router.Press(1, new Note(50), 90);

        Assert.Equal(new[] { "press 1 50 90" }, engine.Said);
        Assert.Equal(new[] { "press 1 50 90" }, lights.Said);
    }

    /// <summary>And that is true of every one of the four, not only of a press.</summary>
    [Fact]
    public void Whatever_it_was_that_threw()
    {
        var heard = new Heard();
        var router = new MidiRouter(new Throws(), heard);

        router.Press(0, new Note(48), 1);
        router.Let(0, new Note(48));
        router.Bend(0, 1);
        router.Modulate(0, 1);

        Assert.Equal(4, heard.Said.Count);
    }

    /// <summary>
    /// A router with nobody listening is quiet rather than a fault.
    /// </summary>
    /// <remarks>
    /// Which is what the application has for the moment between being built and being wired,
    /// and what a test that only wants somewhere to send events has for ever.
    /// </remarks>
    [Fact]
    public void A_router_with_nobody_listening_says_nothing_and_throws_nothing()
    {
        var router = new MidiRouter();

        router.Press(0, new Note(48), 100);
        router.Let(0, new Note(48));
        router.Bend(0, 0);
        router.Modulate(0, 0);
    }

    /// <summary>
    /// The hand is a track number like any other, so nothing downstream needs a second door.
    /// </summary>
    /// <remarks>
    /// A keyboard under somebody's hand names <see cref="MidiRouter.TheHand"/> and a track's own
    /// MIDI in names its track, and both arrive the same way. Two doors is what there used to be,
    /// and it is why a wheel could reach the cursor's track while the keys beside it reached
    /// track three.
    /// </remarks>
    [Fact]
    public void The_hand_arrives_the_same_way_a_named_track_does()
    {
        var heard = new Heard();
        var router = new MidiRouter(heard);

        router.Press(MidiRouter.TheHand, new Note(48), 100);
        router.Press(2, new Note(48), 100);

        Assert.Equal(new[] { "press -1 48 100", "press 2 48 100" }, heard.Said);
    }

    /// <summary>Somewhere for what is played to land, in the order it landed.</summary>
    private sealed class Heard : IPlays
    {
        /// <summary>What this one is called, for a test about who was told first.</summary>
        private readonly string _name;

        /// <summary>Where the order is written down, when it is shared with another.</summary>
        private readonly List<string>? _order;

        /// <summary>One on its own, writing only its own list.</summary>
        public Heard()
        {
            _name = "";
            _order = null;
        }

        /// <summary>And one that also writes its name into a list it shares.</summary>
        /// <param name="name">What to write.</param>
        /// <param name="order">The shared list.</param>
        public Heard(string name, List<string> order)
        {
            _name = name;
            _order = order;
        }

        /// <summary>Everything it was told, in the order it was told.</summary>
        public List<string> Said { get; } = new();

        /// <inheritdoc/>
        public void Press(int track, Note note, int volume) =>
            Add("press " + track + " " + note.Semitone + " " + volume);

        /// <inheritdoc/>
        public void Let(int track, Note note) => Add("let " + track + " " + note.Semitone);

        /// <inheritdoc/>
        public void Bend(int track, double lean) =>
            Add("bend " + track + " " + lean.ToString("0.###"));

        /// <inheritdoc/>
        public void Modulate(int track, double amount) =>
            Add("modulate " + track + " " + amount.ToString("0.###"));

        /// <summary>Writes it down, and its name where one is shared.</summary>
        private void Add(string said)
        {
            Said.Add(said);

            if (_name.Length > 0) _order?.Add(_name);
        }
    }

    /// <summary>A listener that falls over at everything, which is the whole of what it is for.</summary>
    private sealed class Throws : IPlays
    {
        /// <inheritdoc/>
        public void Press(int track, Note note, int volume) => throw new InvalidOperationException("a picture went wrong");

        /// <inheritdoc/>
        public void Let(int track, Note note) => throw new InvalidOperationException("a picture went wrong");

        /// <inheritdoc/>
        public void Bend(int track, double lean) => throw new InvalidOperationException("a picture went wrong");

        /// <inheritdoc/>
        public void Modulate(int track, double amount) => throw new InvalidOperationException("a picture went wrong");
    }
}
