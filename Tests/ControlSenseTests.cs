using JingleBox2.Midi;
using Xunit;
using JingleBox2.Midi.Enums;

namespace JingleBox2.Tests;

/// <summary>
/// Telling a button from a fader from an endless encoder, with nothing to go on but the values.
/// </summary>
/// <remarks>
/// A MIDI message says a controller number and a value and nothing at all about the thing that
/// sent it. Getting this wrong is not merely untidy: an encoder read as a position slams the
/// parameter to one end of its range in front of you.
///
/// The order is how the decision is made. Nothing at all until three messages have been seen,
/// then each of the shapes those three can have, then the rule that it stops listening once it
/// has decided, and last the words it puts on what it decided so SETTINGS can print them.
/// </remarks>
public class ControlSenseTests
{
    /// <summary>A fresh sense that has already been shown those values, in that order.</summary>
    private static ControlSense After(params int[] values)
    {
        var sense = new ControlSense();

        foreach (int value in values) sense.Saw(value);

        return sense;
    }

    /// <summary>
    /// Two messages settle nothing, and the third is what decides.
    /// </summary>
    /// <remarks>
    /// Any two values are consistent with more than one kind of control, so deciding early
    /// would mean deciding wrongly on a knob somebody happened to nudge.
    /// </remarks>
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

    /// <summary>Only ever 0 and 127 is a button, so its parameter jumps rather than picking up.</summary>
    [Fact]
    public void Only_the_two_ends_is_a_button()
    {
        Assert.Equal(ControlPickup.Jump, After(0, 127, 0).Pickup);
    }

    /// <summary>Values that step along say where the hand is, which is a fader or a round one.</summary>
    [Fact]
    public void Numbers_that_walk_are_a_position()
    {
        Assert.Equal(ControlPickup.Takeover, After(40, 41, 43).Pickup);
    }

    /// <summary>
    /// The same number near the middle over and over is an encoder counting from 64.
    /// </summary>
    /// <remarks>
    /// A position that never changes while the hand is moving is not a position at all: it is a
    /// notch count, sent against a resting value the firmware chose.
    /// </remarks>
    [Fact]
    public void The_same_number_near_the_middle_is_an_encoder_counting_from_centre()
    {
        var sense = After(65, 65, 65);

        Assert.Equal(ControlPickup.Relative, sense.Pickup);
        Assert.Equal(ControlTurn.Offset, sense.Turn);
    }

    /// <summary>A small number repeated is the other convention, counting up from nothing.</summary>
    [Fact]
    public void The_same_small_number_is_an_encoder_counting_from_nothing()
    {
        var sense = After(1, 1, 1);

        Assert.Equal(ControlPickup.Relative, sense.Pickup);
        Assert.Equal(ControlTurn.Twos, sense.Turn);
    }

    /// <summary>
    /// The same number at the top of the range is that convention going the other way.
    /// </summary>
    /// <remarks>
    /// 127 repeated is one notch anticlockwise in two's complement. Read as a position it is a
    /// button held down, which is why the repeat is asked about before the two ends are.
    /// </remarks>
    [Fact]
    public void The_same_number_at_the_top_is_the_same_convention_going_the_other_way()
    {
        var sense = After(127, 127, 127);

        Assert.Equal(ControlPickup.Relative, sense.Pickup);
        Assert.Equal(ControlTurn.Twos, sense.Turn);
    }

    /// <summary>
    /// A decision made is a decision kept, whatever arrives afterwards.
    /// </summary>
    /// <remarks>
    /// A fader swept to its floor sends a 0, and re-reading that as the first half of a button
    /// would have the kind of a control change under the owner's hand.
    /// </remarks>
    [Fact]
    public void Once_it_knows_it_stops_listening()
    {
        var sense = After(40, 41, 43);

        Assert.True(sense.Saw(0));
        Assert.Equal(ControlPickup.Takeover, sense.Pickup);
    }

    /// <summary>The words SETTINGS and the link lists print for each pairing of pickup and turn.</summary>
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
