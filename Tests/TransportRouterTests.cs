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
}
