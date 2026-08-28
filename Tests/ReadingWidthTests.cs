using JingleBox2.Machines.Ui;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// How much room a reading needs, which is a question about the range and not about the value.
/// </summary>
/// <remarks>
/// A fader used to be measured by what it was showing, so its width followed its value. Two of
/// the mixer's strips were turned down to -10.0 dB, one character more than the others, and the
/// fader that wide pushed the meter beside it into the strip's own border. The rule is here
/// rather than in the control because it is a fact about text, and the control is a fact about
/// Avalonia.
///
/// With every strip honest about its width the mixer's cards had to grow from 120 to 134, which
/// is what the contents always needed.
/// </remarks>
public class ReadingWidthTests
{
    /// <summary>
    /// The mixer's own fader: -60 to +6, one decimal, in decibels. Wherever it is set, it asks
    /// for the same room, which is the whole point. Which of the equally long readings comes
    /// back does not matter, and is not asserted.
    /// </summary>
    [Fact]
    public void A_level_asks_for_the_same_room_wherever_it_is_set()
    {
        int room = "-60.0 dB".Length;

        foreach (double value in new double[] { 6, 0, -1, -10, -60 })
            Assert.Equal(room, NumericInput.Widest(value, -60, 6, "0.0", " dB").Length);
    }

    /// <summary>And it is the ends that say how much room that is, not the value.</summary>
    [Fact]
    public void The_room_comes_from_the_ends()
    {
        Assert.Equal("-60.0 dB", NumericInput.Widest(0, -60, 6, "0.0", " dB"));
    }

    /// <summary>Nothing stops a control being handed a value from outside its own ends.</summary>
    [Fact]
    public void And_at_a_value_longer_than_either_end()
    {
        Assert.Equal("-120.0 dB", NumericInput.Widest(-120, -60, 6, "0.0", " dB"));
    }

    /// <summary>A range that is all positive is at its longest at the top.</summary>
    [Fact]
    public void The_widest_end_is_not_always_the_lowest()
    {
        Assert.Equal("1000ms", NumericInput.Widest(20, 20, 1000, "0", "ms"));
    }

    /// <summary>
    /// A unit is optional and a missing one is not the string "null": both spellings of "no
    /// unit" have to come back as the bare number.
    /// </summary>
    [Fact]
    public void A_reading_with_no_unit_is_just_the_number()
    {
        Assert.Equal("-1.00", NumericInput.Widest(0, -1, 1, "0.00", ""));
        Assert.Equal("-1.00", NumericInput.Widest(0, -1, 1, "0.00", null));
    }
}
