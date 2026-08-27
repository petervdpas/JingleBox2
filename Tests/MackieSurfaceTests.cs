using System.Collections.Generic;
using System.Linq;
using JingleBox2.Midi;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What a control surface is told, which is the half that makes it feel attached to the music
/// rather than merely wired to it.
/// </summary>
public class MackieSurfaceTests
{
    private const string Port = "Minilab3 MCU/HUI";

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

    private static byte[]? First(NoMidi wire, byte status) =>
        wire.Sent.Select(one => one.Bytes).FirstOrDefault(bytes => bytes.Length > 0 && bytes[0] == status);

    [Fact]
    public void A_fader_is_driven_to_where_the_level_is()
    {
        var (surface, wire, mixer) = Wired();

        mixer.At(0).Set(1.0);
        surface.Draw();

        // Pitch bend on the strip's own channel, fourteen bits, least significant seven first.
        var message = First(wire, 0xE0);

        Assert.NotNull(message);
        Assert.Equal(new byte[] { 0xE0, 0x7F, 0x7F }, message);
    }

    [Fact]
    public void Nothing_is_said_twice()
    {
        var (surface, wire, _) = Wired();

        surface.Draw();

        int said = wire.Sent.Count;

        surface.Draw();

        // A display line is sixty two bytes and there are two of them. A mixer that changed for
        // any reason at all would otherwise redraw the whole desk every time.
        Assert.Equal(said, wire.Sent.Count);
    }

    [Fact]
    public void A_hand_on_a_fader_is_left_alone()
    {
        var (surface, wire, mixer) = Wired();

        surface.Draw();
        wire.Sent.Clear();

        surface.Touched(0, down: true);
        mixer.At(0).Set(1.0);
        surface.Draw();

        // Driving a motor against a hand is a fight the hand wins, in a way that feels like the
        // desk is broken.
        Assert.DoesNotContain(Sent(wire), bytes => bytes[0] == 0xE0);
    }

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

    [Fact]
    public void A_fader_is_not_told_what_it_has_just_said()
    {
        var (surface, wire, mixer) = Wired();

        surface.Draw();
        wire.Sent.Clear();

        // The loop this closes: a hand moves the fader, the level follows, the level having
        // changed asks for the fader to be moved to where it already is.
        surface.Heard(0, 16383);
        mixer.At(0).Set(1.0);
        surface.Draw();

        Assert.DoesNotContain(Sent(wire), bytes => bytes[0] == 0xE0);
    }

    [Fact]
    public void A_mute_lights_its_own_button()
    {
        var (surface, wire, mixer) = Wired();

        mixer.At(2, MixControl.Mute).Set(1);
        surface.Draw();

        Assert.Contains(Sent(wire), bytes => bytes.Length == 3
                                          && bytes[0] == 0x90 && bytes[1] == 0x10 + 2 && bytes[2] == 0x7F);
    }

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

    [Fact]
    public void A_pan_lights_the_ring_round_its_knob()
    {
        var (surface, wire, mixer) = Wired();

        mixer.At(1, MixControl.Pan).Set(0);
        surface.Draw();

        var ring = Sent(wire).FirstOrDefault(bytes => bytes.Length == 3 && bytes[0] == 0xB0 && bytes[1] == 0x30 + 1);

        Assert.NotNull(ring);

        // Lit from the centre outward, which is what a pan wants, and the centre light on
        // because a pan of nought is the middle.
        Assert.Equal(1, (ring![2] >> 4) & 0x03);
        Assert.Equal(0x40, ring[2] & 0x40);
    }

    [Fact]
    public void The_display_says_what_each_track_is()
    {
        var (surface, wire, _) = Wired();

        surface.Draw();

        var line = Sent(wire).FirstOrDefault(bytes => bytes.Length > 6 && bytes[0] == 0xF0 && bytes[5] == 0x12 && bytes[6] == 0x00);

        Assert.NotNull(line);

        // F0 00 00 66 14 12 <offset> ... F7, and seven characters a strip.
        Assert.Equal(new byte[] { 0xF0, 0x00, 0x00, 0x66, 0x14, 0x12, 0x00 }, line!.Take(7).ToArray());
        Assert.Equal(0xF7, line[^1]);

        string said = new string(line.Skip(7).Take(56).Select(b => (char)b).ToArray());

        Assert.StartsWith("TR-01  TR-02  ", said);
    }

    [Fact]
    public void Strips_past_the_last_track_say_nothing()
    {
        var (surface, wire, _) = Wired(tracks: 2);

        surface.Draw();

        var line = Sent(wire).First(bytes => bytes.Length > 6 && bytes[0] == 0xF0 && bytes[6] == 0x00);
        string said = new string(line.Skip(7).Take(56).Select(b => (char)b).ToArray());

        Assert.Equal("TR-01  TR-02  " + new string(' ', 42), said);
    }

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

    [Fact]
    public void A_surface_that_has_not_spoken_is_not_written_to()
    {
        var wire = new NoMidi();
        var surface = new MackieSurface(wire, new Desk(), () => 8, track => "TR");

        surface.Draw();

        Assert.Empty(wire.Sent);
    }

    [Fact]
    public void A_router_and_its_surface_agree_about_the_bank()
    {
        var wire = new NoMidi();
        var mixer = new Desk(16);
        var surface = new MackieSurface(wire, mixer, () => 16, track => "TR-" + (track + 1).ToString("00"));
        var router = new MidiMackieRouter(mixer, () => 16, surface);

        // Bank right, which the router owns and the surface has to be told about.
        router.Handle(new MidiMessage
        {
            Device = Port, Type = MidiMessageType.Note, Channel = 1, Value = 0x2F, Data = 127, IsOn = true
        });

        Assert.Equal(8, router.Bank);
        Assert.Equal(8, surface.Bank);

        // And it learned where to write from what arrived, without being told twice.
        Assert.Equal(Port, surface.Device);
        Assert.NotEmpty(wire.Sent);
    }

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

        // Which is what the mixer having moved would ask for.
        surface.Draw();

        Assert.DoesNotContain(Sent(wire), bytes => bytes[0] == 0xE0);
    }
}
