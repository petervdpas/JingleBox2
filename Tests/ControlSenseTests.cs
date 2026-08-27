using JingleBox2.Midi;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Telling a button from a fader from an endless encoder, with nothing to go on but the values.
/// </summary>
/// <remarks>
/// A MIDI message says a controller number and a value and nothing at all about the thing that
/// sent it. Getting this wrong is not merely untidy: an encoder read as a position slams the
/// parameter to one end of its range in front of you.
/// </remarks>
public class ControlSenseTests
{
    private static ControlSense After(params int[] values)
    {
        var sense = new ControlSense();

        foreach (int value in values) sense.Saw(value);

        return sense;
    }

    [Fact]
    public void Nothing_is_decided_before_three_messages()
    {
        var sense = new ControlSense();

        Assert.False(sense.Saw(10));
        Assert.False(sense.Saw(11));
        Assert.Null(sense.Pickup);

        Assert.True(sense.Saw(12));
        Assert.NotNull(sense.Pickup);
    }

    [Fact]
    public void Only_the_two_ends_is_a_button()
    {
        Assert.Equal(ControlPickup.Jump, After(0, 127, 0).Pickup);
    }

    [Fact]
    public void Numbers_that_walk_are_a_position()
    {
        Assert.Equal(ControlPickup.Takeover, After(40, 41, 43).Pickup);
    }

    [Fact]
    public void The_same_number_near_the_middle_is_an_encoder_counting_from_centre()
    {
        var sense = After(65, 65, 65);

        Assert.Equal(ControlPickup.Relative, sense.Pickup);
        Assert.Equal(ControlTurn.Offset, sense.Turn);
    }

    [Fact]
    public void The_same_small_number_is_an_encoder_counting_from_nothing()
    {
        var sense = After(1, 1, 1);

        Assert.Equal(ControlPickup.Relative, sense.Pickup);
        Assert.Equal(ControlTurn.Twos, sense.Turn);
    }

    [Fact]
    public void The_same_number_at_the_top_is_the_same_convention_going_the_other_way()
    {
        // 127 repeated is one notch anticlockwise in two's complement. Read as a position it is
        // a button held down, which is why the repeat is asked about before the two ends are.
        var sense = After(127, 127, 127);

        Assert.Equal(ControlPickup.Relative, sense.Pickup);
        Assert.Equal(ControlTurn.Twos, sense.Turn);
    }

    [Fact]
    public void Once_it_knows_it_stops_listening()
    {
        var sense = After(40, 41, 43);

        Assert.True(sense.Saw(0));
        Assert.Equal(ControlPickup.Takeover, sense.Pickup);
    }

    [Theory]
    [InlineData(ControlPickup.Jump, ControlTurn.Offset, "jumps")]
    [InlineData(ControlPickup.Takeover, ControlTurn.Offset, "picks up")]
    [InlineData(ControlPickup.Endless, ControlTurn.Offset, "endless knob")]
    [InlineData(ControlPickup.Relative, ControlTurn.Twos, "encoder")]
    [InlineData(ControlPickup.Relative, ControlTurn.Offset, "encoder, from centre")]
    [InlineData(ControlPickup.Sensed, ControlTurn.Offset, "listening")]
    public void It_says_what_it_decided_in_words(ControlPickup pickup, ControlTurn turn, string said) =>
        Assert.Equal(said, ControlSense.Describe(pickup, turn));
}
