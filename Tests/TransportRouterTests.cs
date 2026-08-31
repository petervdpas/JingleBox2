using System.Collections.Generic;
using JingleBox2.Midi;
using Xunit;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Tests;

/// <summary>
/// The transport buttons, in both dialects, because a controller speaks one or the other
/// depending on which program it is in and neither number means anything else on a port
/// somebody has pointed at the transport.
/// </summary>
/// <remarks>
/// Read in the order the dialects were added. First the plain controllers a MiniLab sends in its
/// DAW program and the Mackie Control notes on its MCU port, with the press-and-not-the-release
/// rule and the numbers on that port that belong to somebody else. Then the two dialects that
/// carry no press at all: the realtime bytes, which every sequencer ever built understands, and
/// MIDI Machine Control, which is what a KeyStep Pro sends unless its owner changes it.
/// </remarks>
public class TransportRouterTests
{
    /// <summary>Somewhere for the three transport actions to land, in the order they landed.</summary>
    private sealed class Deck : ITransportKeys
    {
        /// <summary>What the router asked for, one word per call.</summary>
        public List<string> Did { get; } = new();

        /// <inheritdoc/>
        public void Play() => Did.Add("play");

        /// <inheritdoc/>
        public void Stop() => Did.Add("stop");

        /// <inheritdoc/>
        public void Record() => Did.Add("record");

        /// <inheritdoc/>
        public void Loop() => Did.Add("cycle");
    }

    /// <summary>A router and the deck under it, so a test can play a message and read what happened.</summary>
    private static (MidiTransportRouter Router, Deck Deck) Wired()
    {
        var deck = new Deck();

        return (new MidiTransportRouter(deck), deck);
    }

    /// <summary>
    /// A controller message off the MiniLab's main port, which is where its DAW program sends
    /// the transport.
    /// </summary>
    private static MidiMessage Cc(int number, int value = 127) => new()
    {
        Device = "Minilab3 MIDI", Type = MidiMessageType.ControlChange,
        Channel = 1, Value = number, Data = value, IsOn = value > 0
    };

    /// <summary>
    /// A note off the MCU port, which is where the same device sends the transport in its
    /// Mackie program. Arturia ask a host to use one port or the other and never both.
    /// </summary>
    private static MidiMessage Note(int number, bool down = true) => new()
    {
        Device = "Minilab3 MCU/HUI", Type = MidiMessageType.Note,
        Channel = 1, Value = number, Data = down ? 127 : 0, IsOn = down
    };

    /// <summary>Play, stop and record as CC 107, 106 and 108, which is what a DAW program sends.</summary>
    [Theory]
    [InlineData(107, "play")]
    [InlineData(106, "stop")]
    [InlineData(108, "record")]
    public void The_daw_programs_controllers(int cc, string wanted)
    {
        var (router, deck) = Wired();

        router.Handle(Cc(cc));

        Assert.Equal(new[] { wanted }, deck.Did);
    }

    /// <summary>And the same three as Mackie Control notes 0x5E, 0x5D and 0x5F.</summary>
    [Theory]
    [InlineData(0x5E, "play")]
    [InlineData(0x5D, "stop")]
    [InlineData(0x5F, "record")]
    public void And_mackie_controls_notes(int note, string wanted)
    {
        var (router, deck) = Wired();

        router.Handle(Note(note));

        Assert.Equal(new[] { wanted }, deck.Did);
    }

    /// <summary>
    /// One button press is one action.
    /// </summary>
    /// <remarks>
    /// A button sends both halves, and acting on both would stop what the press had just
    /// started.
    /// </remarks>
    [Fact]
    public void The_press_and_not_the_release()
    {
        var (router, deck) = Wired();

        router.Handle(Cc(107, 127));
        router.Handle(Cc(107, 0));

        Assert.Single(deck.Did);
    }

    /// <summary>
    /// Cycle on CC 105 turns looping on or off, and tap tempo on CC 109 still does nothing.
    /// </summary>
    /// <remarks>
    /// Cycle was read and deliberately dropped for a long time, which was the right thing to do
    /// while there was nowhere for it to go. There is: the Loop switch sits in the tracker's bar
    /// beside the Pattern or Song picker, because what the end is and what happens when you
    /// reach it are one question, and a control surface puts its cycle key in the transport row
    /// for exactly the same reason.
    ///
    /// Tap tempo is still named and left alone. It is the last one here with somewhere obvious
    /// to go that it has not been given yet.
    /// </remarks>
    [Fact]
    public void Cycle_turns_looping_on_and_tap_tempo_still_does_nothing()
    {
        var (router, deck) = Wired();

        router.Handle(Cc(105));

        Assert.Equal(new[] { "cycle" }, deck.Did);

        router.Handle(Cc(109));

        Assert.Equal(new[] { "cycle" }, deck.Did);
    }

    /// <summary>
    /// Every other controller on a transport port belongs to whoever is reading knobs.
    /// </summary>
    /// <remarks>
    /// A port with a job here still carries the device's ordinary traffic, and claiming any of
    /// it would take a knob away from the links somebody made.
    /// </remarks>
    [Fact]
    public void Every_other_controller_on_that_port_is_somebody_elses_knob()
    {
        var (router, deck) = Wired();

        router.Handle(Cc(86));
        router.Handle(Cc(1));

        Assert.Empty(deck.Did);
    }

    /// <summary>A null message is answered rather than thrown at.</summary>
    [Fact]
    public void Nothing_at_all_is_nothing()
    {
        var (router, deck) = Wired();

        router.Handle(null!);

        Assert.Empty(deck.Did);
    }

    /// <summary>
    /// One of the three realtime transport bytes, off a KeyStep Pro's main port.
    /// </summary>
    private static MidiMessage Realtime(int status) => new()
    {
        Device = "KeyStep Pro MIDI 1", Type = MidiMessageType.Realtime,
        Channel = 0, Value = status, Data = 0, IsOn = false
    };

    /// <summary>
    /// A system exclusive message off the same port, which is where machine control arrives.
    /// </summary>
    private static MidiMessage Mmc(params byte[] bytes) => new()
    {
        Device = "KeyStep Pro MIDI 1", Type = MidiMessageType.SystemExclusive,
        Channel = 0, Value = 0, Data = 0, IsOn = false, Bytes = bytes
    };

    /// <summary>
    /// 0xFA start, 0xFB continue and 0xFC stop, none of which carries a press.
    /// </summary>
    /// <remarks>
    /// There is no press to wait for, so these have to be read before the guard the buttons
    /// need. Continue is play, because this transport has no memory of where it was stopped.
    /// </remarks>
    [Theory]
    [InlineData(0xFA, "play")]
    [InlineData(0xFB, "play")]
    [InlineData(0xFC, "stop")]
    public void The_realtime_transport_is_one_byte_and_no_press(int status, string wanted)
    {
        var (router, deck) = Wired();

        router.Handle(Realtime(status));

        Assert.Equal(new[] { wanted }, deck.Did);
    }

    /// <summary>
    /// Machine control commands 0x02 play, 0x03 deferred play, 0x01 stop, 0x09 pause and 0x06
    /// record, wrapped as F0 7F unit 06 command F7.
    /// </summary>
    /// <remarks>
    /// Pause is stop for the same reason continue is play: nothing here remembers a position.
    /// </remarks>
    [Theory]
    [InlineData(0x02, "play")]
    [InlineData(0x03, "play")]
    [InlineData(0x01, "stop")]
    [InlineData(0x09, "stop")]
    [InlineData(0x06, "record")]
    public void Machine_control_is_the_other_dialect_and_a_keystep_pro_sends_it(byte command, string wanted)
    {
        var (router, deck) = Wired();

        router.Handle(Mmc(0xF0, 0x7F, 0x7F, 0x06, command, 0xF7));

        Assert.Equal(new[] { wanted }, deck.Did);
    }

    /// <summary>
    /// The unit number in a machine control message is not checked.
    /// </summary>
    /// <remarks>
    /// 0x7F means everybody and is what hardware sends. A message addressed to one unit is
    /// addressing a tape machine that has not existed for thirty years, and refusing it would
    /// mean a transport button doing nothing for a reason nobody could guess.
    /// </remarks>
    [Fact]
    public void A_unit_number_other_than_everybody_is_still_obeyed()
    {
        var (router, deck) = Wired();

        router.Handle(Mmc(0xF0, 0x7F, 0x03, 0x06, 0x02, 0xF7));

        Assert.Equal(new[] { "play" }, deck.Did);
    }

    /// <summary>
    /// A system exclusive message with another maker's header on it is left alone.
    /// </summary>
    /// <remarks>
    /// The two here are a universal identity reply and Arturia's own settings protocol. Neither
    /// is this router's, and both arrive on ports it is listening to.
    /// </remarks>
    [Fact]
    public void Somebody_elses_system_exclusive_message_is_left_alone()
    {
        var (router, deck) = Wired();

        router.Handle(Mmc(0xF0, 0x7E, 0x7F, 0x06, 0x02, 0x00, 0x20, 0x6B, 0xF7));
        router.Handle(Mmc(0xF0, 0x00, 0x20, 0x6B, 0x7F, 0x42, 0x02, 0xF7));

        Assert.Empty(deck.Did);
    }

    /// <summary>
    /// Fast forward, 0x04, and rewind, 0x05, are named and move nothing.
    /// </summary>
    /// <remarks>
    /// Named so the log says what arrived; there is nothing here for either of them to do.
    /// </remarks>
    [Fact]
    public void A_machine_control_command_this_does_not_read_moves_nothing()
    {
        var (router, deck) = Wired();

        router.Handle(Mmc(0xF0, 0x7F, 0x7F, 0x06, 0x04, 0xF7));
        router.Handle(Mmc(0xF0, 0x7F, 0x7F, 0x06, 0x05, 0xF7));

        Assert.Empty(deck.Did);
    }

    /// <summary>
    /// A machine control message that stops before its command byte is not obeyed on a guess.
    /// </summary>
    /// <remarks>
    /// A lump that ends early is a pulled cable, and reading past its end would be reading
    /// whatever happened to be after it.
    /// </remarks>
    [Fact]
    public void A_truncated_machine_control_message_is_not_one()
    {
        var (router, deck) = Wired();

        router.Handle(Mmc(0xF0, 0x7F, 0x7F, 0x06));
        router.Handle(Mmc(0xF0, 0x7F, 0x7F, 0x06, 0x02));

        Assert.Empty(deck.Did);
    }
}
