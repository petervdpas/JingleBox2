using System.Collections.Generic;
using JingleBox2.Midi;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The transport buttons, in both dialects, because a controller speaks one or the other
/// depending on which program it is in and neither number means anything else on a port
/// somebody has pointed at the transport.
/// </summary>
public class TransportRouterTests
{
    private sealed class Deck : ITransportKeys
    {
        public List<string> Did { get; } = new();

        public void Play() => Did.Add("play");
        public void Stop() => Did.Add("stop");
        public void Record() => Did.Add("record");
    }

    private static (MidiTransportRouter Router, Deck Deck) Wired()
    {
        var deck = new Deck();

        return (new MidiTransportRouter(deck), deck);
    }

    private static MidiMessage Cc(int number, int value = 127) => new()
    {
        Device = "Minilab3 MIDI", Type = MidiMessageType.ControlChange,
        Channel = 1, Value = number, Data = value, IsOn = value > 0
    };

    private static MidiMessage Note(int number, bool down = true) => new()
    {
        Device = "Minilab3 MCU/HUI", Type = MidiMessageType.Note,
        Channel = 1, Value = number, Data = down ? 127 : 0, IsOn = down
    };

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

    [Fact]
    public void The_press_and_not_the_release()
    {
        var (router, deck) = Wired();

        router.Handle(Cc(107, 127));
        router.Handle(Cc(107, 0));

        // A button sends both, and doing it twice would stop what the press had just started.
        Assert.Single(deck.Did);
    }

    [Fact]
    public void Loop_and_tap_tempo_are_recognised_and_do_nothing_yet()
    {
        var (router, deck) = Wired();

        router.Handle(Cc(105));
        router.Handle(Cc(109));

        Assert.Empty(deck.Did);
    }

    [Fact]
    public void Every_other_controller_on_that_port_is_somebody_elses_knob()
    {
        var (router, deck) = Wired();

        router.Handle(Cc(86));
        router.Handle(Cc(1));

        Assert.Empty(deck.Did);
    }

    [Fact]
    public void Nothing_at_all_is_nothing()
    {
        var (router, deck) = Wired();

        router.Handle(null!);

        Assert.Empty(deck.Did);
    }

    private static MidiMessage Realtime(int status) => new()
    {
        Device = "KeyStep Pro MIDI 1", Type = MidiMessageType.Realtime,
        Channel = 0, Value = status, Data = 0, IsOn = false
    };

    private static MidiMessage Mmc(params byte[] bytes) => new()
    {
        Device = "KeyStep Pro MIDI 1", Type = MidiMessageType.SystemExclusive,
        Channel = 0, Value = 0, Data = 0, IsOn = false, Bytes = bytes
    };

    [Theory]
    [InlineData(0xFA, "play")]
    [InlineData(0xFB, "play")]
    [InlineData(0xFC, "stop")]
    public void The_realtime_transport_is_one_byte_and_no_press(int status, string wanted)
    {
        var (router, deck) = Wired();

        router.Handle(Realtime(status));

        // No press to wait for, so it must be read before the guard the buttons need. Continue
        // is play, because this transport has no memory of where it was stopped.
        Assert.Equal(new[] { wanted }, deck.Did);
    }

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

    [Fact]
    public void A_unit_number_other_than_everybody_is_still_obeyed()
    {
        // 0x7F means everybody and is what hardware sends. A message addressed to one unit is
        // addressing a tape machine that has not existed for thirty years, and refusing it
        // would mean a transport button doing nothing for a reason nobody could guess.
        var (router, deck) = Wired();

        router.Handle(Mmc(0xF0, 0x7F, 0x03, 0x06, 0x02, 0xF7));

        Assert.Equal(new[] { "play" }, deck.Did);
    }

    [Fact]
    public void Somebody_elses_system_exclusive_message_is_left_alone()
    {
        var (router, deck) = Wired();

        // An identity reply, and Arturia's own settings protocol. Neither is this router's.
        router.Handle(Mmc(0xF0, 0x7E, 0x7F, 0x06, 0x02, 0x00, 0x20, 0x6B, 0xF7));
        router.Handle(Mmc(0xF0, 0x00, 0x20, 0x6B, 0x7F, 0x42, 0x02, 0xF7));

        Assert.Empty(deck.Did);
    }

    [Fact]
    public void A_machine_control_command_this_does_not_read_moves_nothing()
    {
        var (router, deck) = Wired();

        // Fast forward and rewind are named so the log says what arrived, and do nothing.
        router.Handle(Mmc(0xF0, 0x7F, 0x7F, 0x06, 0x04, 0xF7));
        router.Handle(Mmc(0xF0, 0x7F, 0x7F, 0x06, 0x05, 0xF7));

        Assert.Empty(deck.Did);
    }

    [Fact]
    public void A_truncated_machine_control_message_is_not_one()
    {
        var (router, deck) = Wired();

        router.Handle(Mmc(0xF0, 0x7F, 0x7F, 0x06));
        router.Handle(Mmc(0xF0, 0x7F, 0x7F, 0x06, 0x02));

        Assert.Empty(deck.Did);
    }
}
