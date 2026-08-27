using JingleBox2.Controllers;
using JingleBox2.Midi;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A controller's own Lua file, which can say that these bytes mean those bytes and nothing else.
/// </summary>
/// <remarks>
/// The property worth protecting: a codec cannot add a feature or take one away, and a device
/// nobody has written a file for is passed straight through untouched.
/// </remarks>
public class ControllerCodecTests
{
    private static MidiMessage Bend(string device, int value) => new()
    {
        Device = device, Type = MidiMessageType.PitchBend, Channel = 1, Value = 0, Data = value
    };

    [Fact]
    public void The_shipped_codec_turns_a_pitch_strip_into_a_controller()
    {
        using var codecs = new ControllerCodecs(new NoMidi());

        var read = codecs.Read(Bend("Minilab3 MIDI", 8192));

        Assert.NotNull(read);
        Assert.Equal(MidiMessageType.ControlChange, read!.Type);
        Assert.Equal(2, read.Value);

        // Fourteen bits down to seven: the middle of the strip is the middle of the range.
        Assert.Equal(64, read.Data);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(16383, 127)]
    public void And_scales_it_across_the_whole_range(int bend, int wanted)
    {
        using var codecs = new ControllerCodecs(new NoMidi());

        Assert.Equal(wanted, codecs.Read(Bend("Minilab3 MIDI", bend))!.Data);
    }

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

    [Fact]
    public void A_device_with_no_codec_is_passed_straight_through()
    {
        using var codecs = new ControllerCodecs(new NoMidi());

        var read = codecs.Read(Bend("MPD218 Port A", 8192));

        Assert.Equal(MidiMessageType.PitchBend, read!.Type);
        Assert.Equal(8192, read.Data);
    }

    [Fact]
    public void Nothing_at_all_is_nothing()
    {
        using var codecs = new ControllerCodecs(new NoMidi());

        Assert.Null(codecs.Read(null!));
    }
}
