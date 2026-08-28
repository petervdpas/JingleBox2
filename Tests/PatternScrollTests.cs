using JingleBox2.Tracker;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Where the pattern sits under a cursor that stays on the middle of the screen.
/// </summary>
/// <remarks>
/// The rule has no exceptions, which is the part worth pinning down: the room above line 00 and
/// below the last line is always there, so line 00 of a song's first pattern is on the middle of
/// the screen exactly as any other row is, with nothing above it. Whether a neighbouring pattern
/// is drawn in that room is a separate question and changes none of these numbers.
/// </remarks>
public class PatternScrollTests
{
    private const double RowHeight = 20;
    private const double Viewport = 300;

    /// <summary>Half a screen less half a row: how far the middle is from either edge.</summary>
    private const double Half = (Viewport - RowHeight) / 2;

    private static PatternMetrics Metrics() => new(8, RowHeight, 4, Half, Half);

    /// <summary>How far down the screen a row ends up, measured to the middle of the row.</summary>
    private static double Down(int row, int lines) =>
        Metrics().RowY(row) + RowHeight / 2
        - ViewportScroller.CentreRow(Viewport, Metrics(), row, lines);

    [Fact]
    public void Every_row_of_a_pattern_lands_on_the_middle()
    {
        for (int row = 0; row < 64; row++) Assert.Equal(Viewport / 2, Down(row, 64), 3);
    }

    [Fact]
    public void The_first_row_included()
    {
        Assert.Equal(0, ViewportScroller.CentreRow(Viewport, Metrics(), 0, 64));
        Assert.Equal(Viewport / 2, Down(0, 64), 3);
    }

    [Fact]
    public void And_the_last()
    {
        Assert.Equal(63 * RowHeight, ViewportScroller.CentreRow(Viewport, Metrics(), 63, 64), 3);
        Assert.Equal(Viewport / 2, Down(63, 64), 3);
    }

    [Fact]
    public void A_pattern_shorter_than_the_window_is_centred_too()
    {
        Assert.Equal(Viewport / 2, Down(0, 4), 3);
        Assert.Equal(Viewport / 2, Down(3, 4), 3);
    }

    [Fact]
    public void The_space_either_side_is_counted_into_the_height()
    {
        Assert.Equal(64 * RowHeight + Half * 2, Metrics().ContentHeight(64), 3);
    }

    [Fact]
    public void A_click_lands_on_the_row_under_it_whatever_is_above()
    {
        var metrics = Metrics();

        Assert.Equal(0, metrics.LineAt(metrics.RowY(0) + 1, 64));
        Assert.Equal(17, metrics.LineAt(metrics.RowY(17) + 1, 64));

        // A click in the room above the pattern is its first row, not a negative one.
        Assert.Equal(0, metrics.LineAt(4, 64));

        // And below it, its last.
        Assert.Equal(63, metrics.LineAt(metrics.RowY(64) + 40, 64));
    }
}
