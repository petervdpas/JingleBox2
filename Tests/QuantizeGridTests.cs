using System.Linq;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What the quantize menu offers, which is note values rather than numbers of lines.
/// </summary>
/// <remarks>
/// The menu used to be six fixed line counts, 2, 3, 4, 6, 8 and 16, and a line count means
/// nothing without knowing how many of them are in a beat. Half of those entries landed on
/// nothing musical at any given setting and the ones that did landed on something different
/// per song. The values are the thing being chosen now and the lines follow.
/// </remarks>
public class QuantizeGridTests
{
    private readonly IQuantizeGrid _grid = new QuantizeGrid();

    /// <summary>
    /// Four lines to a beat, which is the default and what most songs are: a line is a
    /// sixteenth and no triplet comes out whole.
    /// </summary>
    [Fact]
    public void FourLinesToTheBeatIsTheStraightSet()
    {
        var choices = _grid.Choices(4);

        Assert.Equal(new[] { 1, 2, 4, 8, 16 }, choices.Select(c => c.Lines).ToArray());
        Assert.StartsWith("1/16", choices[0].Label);
        Assert.StartsWith("1/4", choices[2].Label);
        Assert.DoesNotContain(choices, c => c.Label.Contains('T'));
    }

    /// <summary>
    /// Six to a beat is the triplet setting, and it earns its triplets rather than being handed
    /// them: they are the values that come out whole there and the sixteenth is the one that
    /// does not.
    /// </summary>
    [Fact]
    public void SixLinesToTheBeatEarnsItsTriplets()
    {
        var choices = _grid.Choices(6);

        Assert.Contains(choices, c => c.Label.StartsWith("1/8T") && c.Lines == 2);
        Assert.Contains(choices, c => c.Label.StartsWith("1/4T") && c.Lines == 4);
        Assert.Contains(choices, c => c.Label.StartsWith("1/8") && c.Lines == 3);
        Assert.DoesNotContain(choices, c => c.Label.StartsWith("1/16 "));
    }

    /// <summary>Finest first, and no two entries land on the same number of lines.</summary>
    [Fact]
    public void TheListIsOrderedAndHasNoDuplicates()
    {
        for (int beat = 1; beat <= 32; beat++)
        {
            var lines = _grid.Choices(beat).Select(c => c.Lines).ToList();

            Assert.NotEmpty(lines);
            Assert.Equal(lines.OrderBy(l => l).ToList(), lines);
            Assert.Equal(lines.Distinct().Count(), lines.Count);
            Assert.All(lines, l => Assert.True(l >= 1));
        }
    }

    /// <summary>
    /// The beat itself is always there, whatever the setting, since it is always a whole
    /// number of lines and is the value anybody reaches for first.
    /// </summary>
    [Fact]
    public void TheBeatIsAlwaysOffered()
    {
        for (int beat = 1; beat <= 32; beat++)
            Assert.Contains(_grid.Choices(beat), c => c.Lines == beat);
    }

    /// <summary>Each label says what it works out at, singular where it is one line.</summary>
    [Fact]
    public void EachLabelSaysWhatItComesTo()
    {
        var choices = _grid.Choices(4);

        Assert.Contains("(1 line)", choices[0].Label);
        Assert.Contains("(16 lines)", choices[^1].Label);
    }
}
