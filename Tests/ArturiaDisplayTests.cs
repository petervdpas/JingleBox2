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
/// <para>
/// Three groups here: what a message has to look like on the wire, what is not sent twice, and
/// where a reading goes. Nothing in the class asks whether a device has a screen.
/// </para>
/// </remarks>
public class ArturiaDisplayTests
{
    /// <summary>A screen writing to one device, and the port it is writing down.</summary>
    private static (ArturiaDisplay Screen, NoMidi Midi) Wired(string device = "Minilab3 MIDI")
    {
        var midi = new NoMidi();

        return (new ArturiaDisplay(midi, () => new[] { device }), midi);
    }

    /// <summary>The bytes of the last thing sent, the picture the screen ends up holding.</summary>
    private static byte[] LastReal(NoMidi midi) => midi.Sent[^1].Bytes;

    /// <summary>
    /// Every message starts with F0 and ends with F7, because a fragment reaching the device is
    /// a screen showing whatever arrived before the break.
    /// </summary>
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

    /// <summary>
    /// No byte between the two ends may have its top bit set, whatever a parameter or a song is
    /// called.
    /// </summary>
    /// <remarks>
    /// The name worked here carries an accent, a dash and a symbol in it, which is all the
    /// things a song gets called. A high bit ends the message early and the screen would show
    /// the fragment that arrived before it.
    /// </remarks>
    [Fact]
    public void Nothing_between_the_ends_may_have_its_top_bit_set()
    {
        var (screen, midi) = Wired();

        screen.Moved("Minilab3 MIDI", ArturiaDisplay.Kind.Knob, 0.5, "Café — naïve", "± 3 dB");

        foreach (var (_, bytes) in midi.Sent)
            for (int at = 1; at < bytes.Length - 1; at++)
                Assert.True(bytes[at] < 0x80, $"byte {at} was {bytes[at]:X2}");
    }

    /// <summary>
    /// The message that puts a device into the mode where it will show anything is sent once,
    /// and every reading after it is one more picture rather than a wake as well.
    /// </summary>
    [Fact]
    public void A_device_is_woken_once_and_not_again()
    {
        var (screen, midi) = Wired();

        screen.Moved("Minilab3 MIDI", ArturiaDisplay.Kind.Knob, 0.5, "One", "1");
        int after = midi.Sent.Count;

        screen.Moved("Minilab3 MIDI", ArturiaDisplay.Kind.Knob, 0.6, "Two", "2");

        Assert.Equal(after + 1, midi.Sent.Count);
    }

    /// <summary>
    /// A reading identical to the one already on the screen is dropped rather than sent again.
    /// </summary>
    /// <remarks>
    /// A control that has not picked up yet draws a value that does not move, and a slow
    /// sweep would be hundreds of identical messages down the port the knob arrives on.
    /// </remarks>
    [Fact]
    public void The_same_picture_is_not_sent_twice()
    {
        var (screen, midi) = Wired();

        screen.Moved("Minilab3 MIDI", ArturiaDisplay.Kind.Knob, 0.5, "Cutoff", "4.4 kHz");
        int after = midi.Sent.Count;

        screen.Moved("Minilab3 MIDI", ArturiaDisplay.Kind.Knob, 0.5, "Cutoff", "4.4 kHz");

        Assert.Equal(after, midi.Sent.Count);
    }

    /// <summary>
    /// A reading is written to the device the caller named, whatever that device is.
    /// </summary>
    /// <remarks>
    /// Nothing here asks whether a device has a screen. One with no output is answered by a
    /// quiet false, and a few bytes down a port nobody reads cost nothing, which is what
    /// saved this from needing a profile to tell Arturia's devices from anyone else's.
    /// Which devices are written to at all is the caller's business.
    /// </remarks>
    [Fact]
    public void A_reading_goes_to_whichever_device_it_names()
    {
        var (screen, midi) = Wired();

        screen.Moved("Some other box", ArturiaDisplay.Kind.Knob, 0.5, "Cutoff", "4.4 kHz");

        Assert.All(midi.Sent, sent => Assert.Equal("Some other box", sent.Device));
    }

    /// <summary>
    /// The text a screen rests on goes to every device the caller listed and to no other.
    /// </summary>
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

    /// <summary>
    /// With nothing plugged in, nothing is sent: the ordinary case is a machine with no
    /// controller on it at all.
    /// </summary>
    [Fact]
    public void And_nowhere_at_all_when_it_is_given_nobody()
    {
        var midi = new NoMidi();
        var screen = new ArturiaDisplay(midi, () => Array.Empty<string>());

        screen.Standing("JingleBox2", "untitled");

        Assert.Empty(midi.Sent);
    }

    /// <summary>
    /// A reading outside nought to one is clamped rather than trusted: whatever the bar is drawn
    /// from, it still has to be a legal seven bit number.
    /// </summary>
    [Fact]
    public void A_reading_is_drawn_somewhere_between_nothing_and_full()
    {
        var (screen, midi) = Wired();

        screen.Moved("Minilab3 MIDI", ArturiaDisplay.Kind.Fader, 2.5, "Level", "loud");

        foreach (var (_, bytes) in midi.Sent)
            Assert.All(bytes[1..^1], b => Assert.True(b < 0x80));
    }
}
