using System.Linq;
using JingleBox2.Controllers;
using JingleBox2.Controllers.Interfaces;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which controller gets written to, which is the half <see cref="ArturiaDisplayTests"/> leaves
/// out: that one is about what a message looks like, this one is about who receives it.
/// </summary>
/// <remarks>
/// The whole reason this exists. One protocol was being sent to every device ticked as Controls,
/// on the reasoning that a controller which is not listening costs a few bytes down a port nobody
/// reads. That holds right up until the device is listening and is not the one the message was
/// written for: the bytes are Arturia's write-a-setting, aimed at where a MiniLab 3 keeps its
/// screen, and what they mean on another manufacturer's hardware is not knowable from here.
///
/// So a screen is a fact in a device's file, like its ports, and a device whose file says nothing
/// is written to by nobody.
/// </remarks>
public class ControllerScreenTests
{
    /// <summary>What is known about the controllers, read off the real files.</summary>
    private readonly IControllerProfiles _profiles = new ControllerProfiles();

    /// <summary>Reads the folder again per test, so each starts from the files as they are.</summary>
    public ControllerScreenTests() => _profiles.Reload();

    /// <summary>A MiniLab 3's main port, which is where its screen is.</summary>
    private const string Lab = "Minilab3 MIDI";

    /// <summary>A KeyLab mkII's DAW port, which is where its screen is.</summary>
    private const string KeyDaw = "KeyLab mkII 49 DAW";

    /// <summary>The same KeyLab's other port, which is not.</summary>
    private const string KeyMidi = "KeyLab mkII 49 MIDI";

    /// <summary>
    /// A file says which of a device's ports the screen is on, and it is not guessable.
    /// </summary>
    /// <remarks>
    /// Both of these were measured rather than reasoned about, and both went against the obvious
    /// answer. A MiniLab 3 has a port named for Analog Lab, which is where a person would look,
    /// and its screen is on the main port. A KeyLab mkII is the other way about: its screen is on
    /// the DAW port and the main port ignores exactly the same message. Written to each port on
    /// its own on 2026-08-29, with somebody watching the screen.
    /// </remarks>
    [Fact]
    public void The_file_says_which_port_the_screen_is_on()
    {
        Assert.Equal("arturia", _profiles.ScreenOn(Lab));
        Assert.Equal("", _profiles.ScreenOn("Minilab3 ALV"));

        Assert.Equal("arturia", _profiles.ScreenOn(KeyDaw));
        Assert.Equal("", _profiles.ScreenOn(KeyMidi));
    }

    /// <summary>A device with no file has no screen, which is the rule the rest of a profile keeps.</summary>
    [Fact]
    public void A_controller_nobody_has_written_a_file_for_has_no_screen()
    {
        Assert.Equal("", _profiles.ScreenOn("Some Other Box Port 1"));
        Assert.Equal("", _profiles.ScreenOn(null));
    }

    /// <summary>A device the file gives no screen at all is never written to.</summary>
    /// <remarks>
    /// The defect this whole arrangement was built for: an MPD218 has no screen and was being
    /// sent Arturia's settings protocol on every knob turn.
    /// </remarks>
    [Fact]
    public void Nothing_is_written_to_a_controller_with_no_screen()
    {
        var midi = new NoMidi();
        var screen = new ArturiaDisplay(midi, null, _profiles);

        screen.Say("MPD218 Port A", "JingleBox2", "a song");
        screen.Moved("MPD218 Port A", ScreenKind.Knob, 0.5, "Cutoff", "50%");

        Assert.Empty(midi.Sent);
    }

    /// <summary>And a device that has one is.</summary>
    [Fact]
    public void A_controller_with_that_screen_is_written_to()
    {
        var midi = new NoMidi();
        var screen = new ArturiaDisplay(midi, null, _profiles);

        screen.Say(Lab, "JingleBox2", "a song");

        Assert.NotEmpty(midi.Sent);
        Assert.All(midi.Sent, one => Assert.Equal(Lab, one.Device));
    }

    /// <summary>
    /// The greeting reaches every port that has a screen and no port that has not, in one call.
    /// </summary>
    [Fact]
    public void The_greeting_goes_to_the_screens_and_nowhere_else()
    {
        var midi = new NoMidi();

        var screens = new ControllerScreens(
            () => new[] { Lab, KeyDaw, KeyMidi, "MPD218 Port A" },
            _profiles,
            new ArturiaDisplay(midi, null, _profiles),
            new MackieDisplay(midi, _profiles));

        screens.Standing("JingleBox2", "Untitled song");

        var written = midi.Sent.Select(one => one.Device).Distinct().ToList();

        Assert.Contains(Lab, written);
        Assert.Contains(KeyDaw, written);
        Assert.DoesNotContain(KeyMidi, written);
        Assert.DoesNotContain("MPD218 Port A", written);
    }

    /// <summary>
    /// A device is claimed by one protocol, and the ones that do not speak for it are not asked.
    /// </summary>
    [Fact]
    public void Each_device_is_written_by_the_protocol_its_file_names()
    {
        var midi = new NoMidi();
        var arturia = new ArturiaDisplay(midi, null, _profiles);
        var mackie = new MackieDisplay(midi, _profiles);

        Assert.True(arturia.Writes(KeyDaw));
        Assert.False(mackie.Writes(KeyDaw));

        Assert.False(arturia.Writes(KeyMidi));
        Assert.False(mackie.Writes(KeyMidi));
    }

    /// <summary>
    /// A Mackie display stands aside for the surface driving that same device, since a mix under
    /// the faders is worth more than a greeting and both write to the same place.
    /// </summary>
    [Fact]
    public void A_live_surface_keeps_its_own_display()
    {
        var midi = new NoMidi();
        string claimed = "";
        var mackie = new MackieDisplay(midi, new EveryScreenIsMackie(), () => claimed);

        mackie.Say("Desk Port 1", "JingleBox2", "a song");
        Assert.NotEmpty(midi.Sent);

        midi.Sent.Clear();
        claimed = "Desk Port 1";

        mackie.Say("Desk Port 1", "JingleBox2 again", "another song");
        Assert.Empty(midi.Sent);
    }

    /// <summary>
    /// The Mackie display draws no picture, so a reading is the two lines and nothing else.
    /// </summary>
    [Fact]
    public void A_mackie_display_says_the_words_because_it_cannot_draw()
    {
        var midi = new NoMidi();
        var mackie = new MackieDisplay(midi, new EveryScreenIsMackie());

        mackie.Moved("Desk Port 1", ScreenKind.Fader, 0.5, "Level", "-6.0 dB");

        Assert.Equal(2, midi.Sent.Count);
        Assert.Equal(0xF0, midi.Sent[0].Bytes[0]);
        Assert.Equal(0xF7, midi.Sent[0].Bytes[^1]);
        Assert.Contains("Level", System.Text.Encoding.ASCII.GetString(midi.Sent[0].Bytes));
        Assert.Contains("-6.0 dB", System.Text.Encoding.ASCII.GetString(midi.Sent[1].Bytes));
    }

    /// <summary>A profile that gives every device a Mackie screen, for the two tests above.</summary>
    /// <remarks>
    /// Which port a desk's screen is on is what a file answers, and there is no file here for a
    /// bare Mackie surface: nobody needs one, since a surface says what every control on it is.
    /// So the question is answered directly rather than through a file that would exist only to
    /// be read by these two tests.
    /// </remarks>
    private sealed class EveryScreenIsMackie : IControllerProfiles
    {
        /// <summary>The real one, for everything this is not about.</summary>
        private readonly ControllerProfiles _real = new();

        /// <inheritdoc/>
        public string ScreenOn(string? device) => "mackie";

        /// <inheritdoc/>
        public bool ScreenWakes(string? device) => false;

        /// <inheritdoc/>
        public void Reload() => _real.Reload();

        /// <inheritdoc/>
        public ControllerProfile? For(string? device) => _real.For(device);

        /// <inheritdoc/>
        public string Called(string? device) => _real.Called(device);

        /// <inheritdoc/>
        public bool Knows(string? device) => _real.Knows(device);

        /// <inheritdoc/>
        public void Saw(string? device, int channel, int cc) => _real.Saw(device, channel, cc);

        /// <inheritdoc/>
        public string ProgramOn(string? device) => _real.ProgramOn(device);

        /// <inheritdoc/>
        public string Named(string? device, int channel, int cc) => _real.Named(device, channel, cc);

        /// <inheritdoc/>
        public string PortIs(string? device) => _real.PortIs(device);

        /// <inheritdoc/>
        public bool PortTakes(string? device, JingleBox2.Midi.Enums.MidiDeviceRole role) =>
            _real.PortTakes(device, role);

        /// <inheritdoc/>
        public ControlPickup? Pickup(string? device, int channel, int cc) => _real.Pickup(device, channel, cc);

        /// <inheritdoc/>
        public ControllerControl? Control(string? device, int channel, int cc) => _real.Control(device, channel, cc);
    }

    /// <summary>
    /// A MiniLab 3 receives byte for byte what it always received.
    /// </summary>
    /// <remarks>
    /// The one thing that had to survive making screens generic, and the one thing that could not
    /// be checked by plugging something in, since the MiniLab was not on the desk the day the
    /// KeyLab arrived. So the bytes are written down here instead: the wake, then the two lines,
    /// exactly as the class sent them before any of this existed.
    ///
    /// The address in the middle is the interesting part. <c>04</c> is Arturia's write-a-string,
    /// and <c>02 60 01</c> is where a MiniLab keeps its screen. A KeyLab mkII turned out to keep
    /// its screen at the same address, which is why one protocol serves both and why finding it
    /// took writing to it: reading there answers "nothing".
    /// </remarks>
    [Fact]
    public void A_minilab_gets_exactly_the_bytes_it_always_got()
    {
        var midi = new NoMidi();
        var screen = new ArturiaDisplay(midi, null, _profiles);

        screen.Say(Lab, "JingleBox2", "Untitled");

        Assert.Equal(2, midi.Sent.Count);

        Assert.Equal(
            new byte[] { 0xF0, 0x00, 0x20, 0x6B, 0x7F, 0x42, 0x02, 0x02, 0x40, 0x6A, 0x21, 0xF7 },
            midi.Sent[0].Bytes);

        Assert.Equal(
            new byte[]
            {
                0xF0, 0x00, 0x20, 0x6B, 0x7F, 0x42, 0x04, 0x02, 0x60, 0x01,
                (byte)'J', (byte)'i', (byte)'n', (byte)'g', (byte)'l', (byte)'e',
                (byte)'B', (byte)'o', (byte)'x', (byte)'2',
                0x00, 0x02,
                (byte)'U', (byte)'n', (byte)'t', (byte)'i', (byte)'t', (byte)'l', (byte)'e', (byte)'d',
                0xF7
            },
            midi.Sent[1].Bytes);
    }

    /// <summary>And a reading on one draws the same picture it always drew.</summary>
    /// <remarks>
    /// The picture bytes moved out of the enum and into the protocol when the enum became
    /// something every screen shares, so these three numbers are the ones that had to come
    /// through the move unchanged: a knob is 03, a fader 04 and a pad 05.
    /// </remarks>
    [Theory]
    [InlineData(ScreenKind.Knob, 0x03)]
    [InlineData(ScreenKind.Fader, 0x04)]
    [InlineData(ScreenKind.Pad, 0x05)]
    public void And_a_reading_is_drawn_as_it_always_was(ScreenKind kind, byte drawn)
    {
        var midi = new NoMidi();
        var screen = new ArturiaDisplay(midi, null, _profiles);

        screen.Moved(Lab, kind, 1.0, "Cutoff", "100%");

        var last = midi.Sent[^1].Bytes;

        Assert.Equal(0x1F, last[9]);
        Assert.Equal(drawn, last[10]);
        Assert.Equal(127, last[12]);
    }

    /// <summary>
    /// Every screen gets the whole message, both chunks, whatever it does with them.
    /// </summary>
    /// <remarks>
    /// The afternoon's real lesson, and it is the opposite of the clever thing. A KeyLab mkII
    /// renders the first chunk and ignores the second, so it shows one row where a MiniLab shows
    /// two. Sending it only the chunk it renders looks like the considerate answer and makes it
    /// render nothing whatever: the message is taken whole or not at all, and a screen that had
    /// been working stopped for the rest of the afternoon on the strength of that improvement.
    /// So there is no per device trimming here and nothing in any file about how many rows a
    /// screen has. One message, and a device shows what it shows.
    /// </remarks>
    [Fact]
    public void Every_screen_gets_both_chunks()
    {
        var midi = new NoMidi();
        var screen = new ArturiaDisplay(midi, null, _profiles);

        screen.Say(KeyDaw, "JingleBox2", "Behind the Faders");

        var bytes = midi.Sent[^1].Bytes;
        string said = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.Contains("JingleBox2", said);
        Assert.Contains("Behind the Fade", said);

        int chunk = System.Array.IndexOf(bytes, (byte)0x00, 10);
        Assert.Equal(0x02, bytes[chunk + 1]);
    }

    /// <summary>
    /// And a MiniLab gets the identical message, because there is only one message.
    /// </summary>
    /// <remarks>
    /// Which is the whole of what "generic" means here. The MiniLab draws both rows from it and
    /// the KeyLab draws the first, and neither device is named anywhere in the code that builds
    /// it.
    /// </remarks>
    [Fact]
    public void A_minilab_gets_the_same_message_a_keylab_does()
    {
        var midi = new NoMidi();
        var screen = new ArturiaDisplay(midi, null, _profiles);

        screen.Say(Lab, "JingleBox2", "Behind the Faders");
        var toLab = midi.Sent[^1].Bytes;

        screen.Say(KeyDaw, "JingleBox2", "Behind the Faders");
        var toKey = midi.Sent[^1].Bytes;

        Assert.Equal(toLab, toKey);
    }

    /// <summary>
    /// The switching-on message goes only to a device whose file asks for it.
    /// </summary>
    /// <remarks>
    /// It is not a wake and it is not part of writing to a screen: it is Arturia's write-a-setting,
    /// preset 02, param 40, control 6A, value 21, and a MiniLab 3 shows nothing until it has had
    /// one. A KeyLab mkII shows nothing once it has, and the same text works without it. So the
    /// device that wants it says so, and the message is never sent on spec.
    /// </remarks>
    [Fact]
    public void Only_a_device_that_asks_is_switched_on()
    {
        Assert.True(_profiles.ScreenWakes(Lab));
        Assert.False(_profiles.ScreenWakes(KeyDaw));
        Assert.False(_profiles.ScreenWakes("MPD218 Port A"));

        var midi = new NoMidi();
        var screen = new ArturiaDisplay(midi, null, _profiles);

        screen.Say(KeyDaw, "JingleBox2", "Untitled song");

        Assert.Single(midi.Sent);
        Assert.Equal(0x04, midi.Sent[0].Bytes[6]);

        midi.Sent.Clear();
        screen.Say(Lab, "JingleBox2", "Untitled song");

        Assert.Equal(2, midi.Sent.Count);
        Assert.Equal(0x02, midi.Sent[0].Bytes[6]);
    }

    /// <summary>
    /// A reading reaches the controller's screen even though the knob is on another of its ports.
    /// </summary>
    /// <remarks>
    /// The thing that would have made a KeyLab's screen useless. Its knobs arrive on the MIDI port
    /// and its screen is on the DAW port, so a reading written back to the port it came from
    /// reaches nothing, and the screen could only ever have said hello. A MiniLab hides this
    /// completely, since there both are the same port.
    /// </remarks>
    [Fact]
    public void A_reading_goes_to_the_controllers_screen_not_the_port_it_came_from()
    {
        var midi = new NoMidi();

        var screens = new ControllerScreens(
            () => new[] { Lab, KeyDaw, KeyMidi, "MPD218 Port A" },
            _profiles,
            new ArturiaDisplay(midi, null, _profiles));

        screens.Moved(KeyMidi, ScreenKind.Knob, 0.5, "Cutoff", "50%");

        Assert.Single(midi.Sent);
        Assert.Equal(KeyDaw, midi.Sent[0].Device);
        Assert.Contains("Cutoff", System.Text.Encoding.ASCII.GetString(midi.Sent[0].Bytes));
    }

    /// <summary>Where the two are one port, which is a MiniLab, nothing is redirected.</summary>
    [Fact]
    public void A_reading_on_the_screens_own_port_stays_there()
    {
        var midi = new NoMidi();

        var screens = new ControllerScreens(
            () => new[] { Lab, KeyDaw, KeyMidi },
            _profiles,
            new ArturiaDisplay(midi, null, _profiles));

        screens.Moved(Lab, ScreenKind.Knob, 0.5, "Cutoff", "50%");

        Assert.All(midi.Sent, one => Assert.Equal(Lab, one.Device));
    }

    /// <summary>A controller with no screen anywhere still receives nothing.</summary>
    [Fact]
    public void A_reading_from_a_controller_with_no_screen_goes_nowhere()
    {
        var midi = new NoMidi();

        var screens = new ControllerScreens(
            () => new[] { Lab, KeyDaw, KeyMidi, "MPD218 Port A" },
            _profiles,
            new ArturiaDisplay(midi, null, _profiles));

        screens.Moved("MPD218 Port A", ScreenKind.Knob, 0.5, "Cutoff", "50%");

        Assert.Empty(midi.Sent);
    }

    /// <summary>
    /// With two of the same controller on the desk, a reading goes to that one's screen.
    /// </summary>
    /// <remarks>
    /// Decided by how much of the port name is shared, which is all there is to go on: the
    /// operating system does not say which ports are one controller, and both units answer an
    /// identity request identically.
    /// </remarks>
    [Fact]
    public void With_two_of_them_a_reading_goes_to_the_right_one()
    {
        var midi = new NoMidi();

        const string second = "2- KeyLab mkII 49 MIDI";
        const string secondDaw = "2- KeyLab mkII 49 DAW";

        var screens = new ControllerScreens(
            () => new[] { KeyMidi, KeyDaw, second, secondDaw },
            _profiles,
            new ArturiaDisplay(midi, null, _profiles));

        screens.Moved(second, ScreenKind.Knob, 0.5, "Cutoff", "50%");

        Assert.Single(midi.Sent);
        Assert.Equal(secondDaw, midi.Sent[0].Device);
    }
}
