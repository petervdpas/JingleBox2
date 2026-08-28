using System.Collections.Generic;
using System.Linq;
using JingleBox2.Midi;
using Xunit;
using JingleBox2.Midi.Enums;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tests;

/// <summary>
/// What a control surface is told, which is the half that makes it feel attached to the music
/// rather than merely wired to it.
/// </summary>
/// <remarks>
/// The tests run in the order the desk is drawn: the faders and what stops them being driven,
/// then the button lights and the ring round each knob, then the two lines of the display, and
/// last a router and its surface wired together, which is where the two halves have to agree.
///
/// Every message is compared with what was last sent and dropped if it would say the same again.
/// That is not tidiness, and several of these tests are about it.
/// </remarks>
public class MackieSurfaceTests
{
    /// <summary>The port a MiniLab 3 speaks Mackie Control on, which is where writing goes.</summary>
    private const string Port = "Minilab3 MCU/HUI";

    /// <summary>A surface over a mixer, with a wire that keeps everything written to it.</summary>
    /// <remarks>
    /// The device is set here rather than learned, since most of these tests are about what is
    /// drawn and not about how the surface found out where to write.
    /// </remarks>
    private static (MackieSurface Surface, NoMidi Wire, Desk Mixer) Wired(int tracks = 16)
    {
        var wire = new NoMidi();
        var mixer = new Desk(tracks);

        var surface = new MackieSurface(
            wire, mixer, () => tracks, track => "TR-" + (track + 1).ToString("00"))
        {
            Device = Port
        };

        return (surface, wire, mixer);
    }

    /// <summary>Everything sent to the port, one message per entry.</summary>
    private static List<byte[]> Sent(NoMidi wire) => wire.Sent.Select(one => one.Bytes).ToList();

    /// <summary>The first message sent that begins with this status byte, or nothing.</summary>
    private static byte[]? First(NoMidi wire, byte status) =>
        wire.Sent.Select(one => one.Bytes).FirstOrDefault(bytes => bytes.Length > 0 && bytes[0] == status);

    /// <summary>The motor is sent to wherever the track's level is.</summary>
    /// <remarks>
    /// Pitch bend on the strip's own channel, fourteen bits, least significant seven first.
    /// This is the half that makes the router's fader land rather than pick up: the fader is
    /// already sitting on the value by the time a hand touches it.
    /// </remarks>
    [Fact]
    public void A_fader_is_driven_to_where_the_level_is()
    {
        var (surface, wire, mixer) = Wired();

        mixer.At(0).Set(1.0);
        surface.Draw();

        var message = First(wire, 0xE0);

        Assert.NotNull(message);
        Assert.Equal(new byte[] { 0xE0, 0x7F, 0x7F }, message);
    }

    /// <summary>A second drawing with nothing changed writes nothing at all.</summary>
    /// <remarks>
    /// A display line is sixty two bytes and there are two of them. A mixer that changed for
    /// any reason at all would otherwise redraw the whole desk every time.
    /// </remarks>
    [Fact]
    public void Nothing_is_said_twice()
    {
        var (surface, wire, _) = Wired();

        surface.Draw();

        int said = wire.Sent.Count;

        surface.Draw();

        Assert.Equal(said, wire.Sent.Count);
    }

    /// <summary>While a hand is on a fader the motor is not driven, however the level moves.</summary>
    /// <remarks>
    /// Driving a motor against a hand is a fight the hand wins, in a way that feels like the
    /// desk is broken. The hand is a note in the 0x68 row, which the router reads as nothing
    /// and the surface reads as leave that one alone.
    /// </remarks>
    [Fact]
    public void A_hand_on_a_fader_is_left_alone()
    {
        var (surface, wire, mixer) = Wired();

        surface.Draw();
        wire.Sent.Clear();

        surface.Touched(0, down: true);
        mixer.At(0).Set(1.0);
        surface.Draw();

        Assert.DoesNotContain(Sent(wire), bytes => bytes[0] == 0xE0);
    }

    /// <summary>And the fader is put back where the level is as soon as the hand lifts.</summary>
    [Fact]
    public void And_put_back_the_moment_the_hand_comes_off()
    {
        var (surface, wire, mixer) = Wired();

        surface.Draw();
        surface.Touched(0, down: true);
        mixer.At(0).Set(1.0);
        wire.Sent.Clear();

        surface.Touched(0, down: false);

        Assert.Contains(Sent(wire), bytes => bytes[0] == 0xE0);
    }

    /// <summary>A position that arrived is written down as though it had been sent.</summary>
    /// <remarks>
    /// The loop this closes: a hand moves the fader, the level follows, the level having
    /// changed asks for the fader to be moved to where it already is. No timing and no
    /// suppression window, because the desk simply already knows what it said.
    /// </remarks>
    [Fact]
    public void A_fader_is_not_told_what_it_has_just_said()
    {
        var (surface, wire, mixer) = Wired();

        surface.Draw();
        wire.Sent.Clear();

        surface.Heard(0, 16383);
        mixer.At(0).Set(1.0);
        surface.Draw();

        Assert.DoesNotContain(Sent(wire), bytes => bytes[0] == 0xE0);
    }

    /// <summary>A muted track lights the mute button on its own strip: note on at 0x7F.</summary>
    [Fact]
    public void A_mute_lights_its_own_button()
    {
        var (surface, wire, mixer) = Wired();

        mixer.At(2, MixControl.Mute).Set(1);
        surface.Draw();

        Assert.Contains(Sent(wire), bytes => bytes.Length == 3
                                          && bytes[0] == 0x90 && bytes[1] == 0x10 + 2 && bytes[2] == 0x7F);
    }

    /// <summary>And unmuting sends the same note at nought, which is how a light is put out.</summary>
    [Fact]
    public void And_goes_dark_again()
    {
        var (surface, wire, mixer) = Wired();

        mixer.At(2, MixControl.Mute).Set(1);
        surface.Draw();
        wire.Sent.Clear();

        mixer.At(2, MixControl.Mute).Set(0);
        surface.Draw();

        Assert.Contains(Sent(wire), bytes => bytes.Length == 3
                                          && bytes[0] == 0x90 && bytes[1] == 0x10 + 2 && bytes[2] == 0x00);
    }

    /// <summary>The ring round a knob is CC 0x30 plus the strip, the mode in bits 4 and 5.</summary>
    /// <remarks>
    /// Lit from the centre outward, which is what a pan wants, and the centre light on
    /// because a pan of nought is the middle.
    /// </remarks>
    [Fact]
    public void A_pan_lights_the_ring_round_its_knob()
    {
        var (surface, wire, mixer) = Wired();

        mixer.At(1, MixControl.Pan).Set(0);
        surface.Draw();

        var ring = Sent(wire).FirstOrDefault(bytes => bytes.Length == 3 && bytes[0] == 0xB0 && bytes[1] == 0x30 + 1);

        Assert.NotNull(ring);

        Assert.Equal(1, (ring![2] >> 4) & 0x03);
        Assert.Equal(0x40, ring[2] & 0x40);
    }

    /// <summary>The top display line carries the track names, seven characters a strip.</summary>
    /// <remarks>
    /// F0 00 00 66 14 12 &lt;offset&gt; ... F7, and seven characters a strip. The second line sits
    /// at offset 0x38 and carries the pan, since the fader is already showing the level in the
    /// one way a number cannot.
    /// </remarks>
    [Fact]
    public void The_display_says_what_each_track_is()
    {
        var (surface, wire, _) = Wired();

        surface.Draw();

        var line = Sent(wire).FirstOrDefault(bytes => bytes.Length > 6 && bytes[0] == 0xF0 && bytes[5] == 0x12 && bytes[6] == 0x00);

        Assert.NotNull(line);

        Assert.Equal(new byte[] { 0xF0, 0x00, 0x00, 0x66, 0x14, 0x12, 0x00 }, line!.Take(7).ToArray());
        Assert.Equal(0xF7, line[^1]);

        string said = new string(line.Skip(7).Take(56).Select(b => (char)b).ToArray());

        Assert.StartsWith("TR-01  TR-02  ", said);
    }

    /// <summary>A strip with no track under it is blank, not left showing the last song's.</summary>
    [Fact]
    public void Strips_past_the_last_track_say_nothing()
    {
        var (surface, wire, _) = Wired(tracks: 2);

        surface.Draw();

        var line = Sent(wire).First(bytes => bytes.Length > 6 && bytes[0] == 0xF0 && bytes[6] == 0x00);
        string said = new string(line.Skip(7).Take(56).Select(b => (char)b).ToArray());

        Assert.Equal("TR-01  TR-02  " + new string(' ', 42), said);
    }

    /// <summary>Banking moves the names on the display along with the faders under them.</summary>
    [Fact]
    public void Banking_moves_what_the_display_says()
    {
        var (surface, wire, _) = Wired();

        surface.Draw();
        wire.Sent.Clear();

        surface.Bank = 8;
        surface.Draw();

        var line = Sent(wire).First(bytes => bytes.Length > 6 && bytes[0] == 0xF0 && bytes[6] == 0x00);
        string said = new string(line.Skip(7).Take(56).Select(b => (char)b).ToArray());

        Assert.StartsWith("TR-09  TR-10  ", said);
    }

    /// <summary>Nothing is written until a surface has said something and given its address.</summary>
    /// <remarks>
    /// Which port to write to is learned from what arrives rather than configured: a surface
    /// speaks and listens on the same port, so a device moved to another socket still works.
    /// Until it has spoken there is nowhere to write to.
    /// </remarks>
    [Fact]
    public void A_surface_that_has_not_spoken_is_not_written_to()
    {
        var wire = new NoMidi();
        var surface = new MackieSurface(wire, new Desk(), () => 8, track => "TR");

        surface.Draw();

        Assert.Empty(wire.Sent);
    }

    /// <summary>The router owns the bank and the surface has to be told about it.</summary>
    /// <remarks>
    /// Note 0x2F is bank right. The surface also learned where to write from the message that
    /// arrived, without being told twice, which is the whole of how a device is addressed here.
    /// </remarks>
    [Fact]
    public void A_router_and_its_surface_agree_about_the_bank()
    {
        var wire = new NoMidi();
        var mixer = new Desk(16);
        var surface = new MackieSurface(wire, mixer, () => 16, track => "TR-" + (track + 1).ToString("00"));
        var router = new MidiMackieRouter(mixer, () => 16, surface);

        router.Handle(new MidiMessage
        {
            Device = Port, Type = MidiMessageType.Note, Channel = 1, Value = 0x2F, Data = 127, IsOn = true
        });

        Assert.Equal(8, router.Bank);
        Assert.Equal(8, surface.Bank);

        Assert.Equal(Port, surface.Device);
        Assert.NotEmpty(wire.Sent);
    }

    /// <summary>Both halves together: a fader moved by hand is not driven back at it.</summary>
    /// <remarks>
    /// Drawing after the move is what the mixer having changed would ask for, and it is the
    /// exact moment the loop would close if the position had not been written down as sent.
    /// </remarks>
    [Fact]
    public void A_fader_moved_by_hand_is_not_echoed_back_to_it()
    {
        var wire = new NoMidi();
        var mixer = new Desk(16);
        var surface = new MackieSurface(wire, mixer, () => 16, track => "TR");
        var router = new MidiMackieRouter(mixer, () => 16, surface);

        router.Handle(new MidiMessage
        {
            Device = Port, Type = MidiMessageType.PitchBend, Channel = 1, Value = 0, Data = 9000, IsOn = false
        });

        wire.Sent.Clear();

        surface.Draw();

        Assert.DoesNotContain(Sent(wire), bytes => bytes[0] == 0xE0);
    }
}
