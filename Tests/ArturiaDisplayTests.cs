using System;
using System.Linq;
using JingleBox2.Midi;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Writing to a controller's screen, which is bytes down a port and nothing else.
/// </summary>
/// <remarks>
/// The rule that has to hold whatever is typed into a song's name: no byte above 127 may go
/// inside a system exclusive message, because a high bit ends the message early and the screen
/// would show whatever fragment arrived before it.
/// </remarks>
public class ArturiaDisplayTests
{
    private static (ArturiaDisplay Screen, NoMidi Midi) Wired(string device = "Minilab3 MIDI")
    {
        var midi = new NoMidi();

        return (new ArturiaDisplay(midi, () => new[] { device }), midi);
    }

    private static byte[] LastReal(NoMidi midi) => midi.Sent[^1].Bytes;

    [Fact]
    public void Every_message_is_a_whole_system_exclusive()
    {
        var (screen, midi) = Wired();

        screen.Moved("Minilab3 MIDI", ArturiaDisplay.Kind.Knob, 0.5, "Cutoff", "4.4 kHz");

        Assert.NotEmpty(midi.Sent);

        foreach (var (_, bytes) in midi.Sent)
        {
            Assert.Equal(0xF0, bytes[0]);
            Assert.Equal(0xF7, bytes[^1]);
        }
    }

    [Fact]
    public void Nothing_between_the_ends_may_have_its_top_bit_set()
    {
        var (screen, midi) = Wired();

        // A name with an accent, a dash and a symbol in it: all the things a song gets called.
        screen.Moved("Minilab3 MIDI", ArturiaDisplay.Kind.Knob, 0.5, "Café — naïve", "± 3 dB");

        foreach (var (_, bytes) in midi.Sent)
            for (int at = 1; at < bytes.Length - 1; at++)
                Assert.True(bytes[at] < 0x80, $"byte {at} was {bytes[at]:X2}");
    }

    [Fact]
    public void A_device_is_woken_once_and_not_again()
    {
        var (screen, midi) = Wired();

        screen.Moved("Minilab3 MIDI", ArturiaDisplay.Kind.Knob, 0.5, "One", "1");
        int after = midi.Sent.Count;

        screen.Moved("Minilab3 MIDI", ArturiaDisplay.Kind.Knob, 0.6, "Two", "2");

        // One more picture, not another wake as well.
        Assert.Equal(after + 1, midi.Sent.Count);
    }

    [Fact]
    public void The_same_picture_is_not_sent_twice()
    {
        var (screen, midi) = Wired();

        screen.Moved("Minilab3 MIDI", ArturiaDisplay.Kind.Knob, 0.5, "Cutoff", "4.4 kHz");
        int after = midi.Sent.Count;

        // A control that has not picked up yet draws a value that does not move, and a slow
        // sweep would be hundreds of identical messages down the port the knob arrives on.
        screen.Moved("Minilab3 MIDI", ArturiaDisplay.Kind.Knob, 0.5, "Cutoff", "4.4 kHz");

        Assert.Equal(after, midi.Sent.Count);
    }

    [Fact]
    public void A_reading_goes_to_whichever_device_it_names()
    {
        // Nothing here asks whether a device has a screen. One with no output is answered by a
        // quiet false, and a few bytes down a port nobody reads cost nothing, which is what
        // saved this from needing a profile to tell Arturia's devices from anyone else's.
        // Which devices are written to at all is the caller's business.
        var (screen, midi) = Wired();

        screen.Moved("Some other box", ArturiaDisplay.Kind.Knob, 0.5, "Cutoff", "4.4 kHz");

        Assert.All(midi.Sent, sent => Assert.Equal("Some other box", sent.Device));
    }

    [Fact]
    public void The_standing_text_goes_only_to_the_devices_it_is_given()
    {
        var midi = new NoMidi();
        var screen = new ArturiaDisplay(midi, () => new[] { "One", "Two" });

        screen.Standing("JingleBox2", "untitled");

        Assert.Contains(midi.Sent, sent => sent.Device == "One");
        Assert.Contains(midi.Sent, sent => sent.Device == "Two");
        Assert.DoesNotContain(midi.Sent, sent => sent.Device == "Three");
    }

    [Fact]
    public void And_nowhere_at_all_when_it_is_given_nobody()
    {
        var midi = new NoMidi();
        var screen = new ArturiaDisplay(midi, () => Array.Empty<string>());

        screen.Standing("JingleBox2", "untitled");

        Assert.Empty(midi.Sent);
    }

    [Fact]
    public void A_reading_is_drawn_somewhere_between_nothing_and_full()
    {
        var (screen, midi) = Wired();

        screen.Moved("Minilab3 MIDI", ArturiaDisplay.Kind.Fader, 2.5, "Level", "loud");

        // Out of range in either direction still has to be a legal seven bit number.
        foreach (var (_, bytes) in midi.Sent)
            Assert.All(bytes[1..^1], b => Assert.True(b < 0x80));
    }
}
