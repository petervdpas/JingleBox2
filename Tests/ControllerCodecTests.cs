using JingleBox2.Controllers;
using JingleBox2.Midi;
using Xunit;
using JingleBox2.Midi.Enums;

namespace JingleBox2.Tests;

/// <summary>
/// A controller's own Lua file, which can say that these bytes mean those bytes and nothing else.
/// </summary>
/// <remarks>
/// The property worth protecting: a codec cannot add a feature or take one away, and a device
/// nobody has written a file for is passed straight through untouched.
/// <para>
/// The one shipped example is controllers/minilab3.lua, and the hard justification for a
/// scripting language at all came off the device: the Pitch strip's page has no CC field, only a
/// channel and a range, so it cannot be made to send a controller on the device and a codec is
/// the only way that strip can ever be pointed at anything.
/// </para>
/// </remarks>
public class ControllerCodecTests
{
    /// <summary>A pitch bend off a named device, which is what a MiniLab's pitch strip sends.</summary>
    private static MidiMessage Bend(string device, int value) => new()
    {
        Device = device, Type = MidiMessageType.PitchBend, Channel = 1, Value = 0, Data = value
    };

    /// <summary>
    /// The shipped codec turns the MiniLab's pitch strip into CC 2, so a control that could
    /// never be pointed at anything becomes linkable.
    /// </summary>
    /// <remarks>
    /// Fourteen bits down to seven: the middle of the strip is the middle of the range.
    /// </remarks>
    [Fact]
    public void The_shipped_codec_turns_a_pitch_strip_into_a_controller()
    {
        using var codecs = new ControllerCodecs(new NoMidi());

        var read = codecs.Read(Bend("Minilab3 MIDI", 8192));

        Assert.NotNull(read);
        Assert.Equal(MidiMessageType.ControlChange, read!.Type);
        Assert.Equal(2, read.Value);

        Assert.Equal(64, read.Data);
    }

    /// <summary>Both ends of the strip land on both ends of the controller's range.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(16383, 127)]
    public void And_scales_it_across_the_whole_range(int bend, int wanted)
    {
        using var codecs = new ControllerCodecs(new NoMidi());

        Assert.Equal(wanted, codecs.Read(Bend("Minilab3 MIDI", bend))!.Data);
    }

    /// <summary>
    /// A knob on a device that has a codec still arrives as itself: a codec sits between the
    /// wire and the routing and only speaks about what it was written for.
    /// </summary>
    [Fact]
    public void Anything_the_application_already_understands_is_left_alone()
    {
        using var codecs = new ControllerCodecs(new NoMidi());

        var knob = new MidiMessage
        {
            Device = "Minilab3 MIDI", Type = MidiMessageType.ControlChange,
            Channel = 1, Value = 86, Data = 33, IsOn = true
        };

        var read = codecs.Read(knob);

        Assert.Equal(86, read!.Value);
        Assert.Equal(33, read.Data);
    }

    /// <summary>
    /// The same pitch bend off a device with no codec comes out as it went in, which is what
    /// keeps a script from being a thing anybody needs.
    /// </summary>
    [Fact]
    public void A_device_with_no_codec_is_passed_straight_through()
    {
        using var codecs = new ControllerCodecs(new NoMidi());

        var read = codecs.Read(Bend("MPD218 Port A", 8192));

        Assert.Equal(MidiMessageType.PitchBend, read!.Type);
        Assert.Equal(8192, read.Data);
    }

    /// <summary>Nothing handed in is nothing handed back, rather than an exception on the
    /// MIDI thread.</summary>
    [Fact]
    public void Nothing_at_all_is_nothing()
    {
        using var codecs = new ControllerCodecs(new NoMidi());

        Assert.Null(codecs.Read(null!));
    }
}
