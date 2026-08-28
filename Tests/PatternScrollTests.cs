using JingleBox2.Tracker;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Where the pattern sits under a cursor that stays on the middle of the screen.
/// </summary>
/// <remarks>
/// The rule holds by having half a screen of pattern above the cursor and half below, and what
/// fills that space is the song either side of this pattern. So the two places it does not hold
/// are the two places there is no song either side: the top of the first pattern and the bottom
/// of the last, where the rows come up against the edge of the window instead. Renoise behaves
/// the same way and that is where this came from.
/// </remarks>
public class PatternScrollTests
{
    private const double RowHeight = 20;
    private const double Viewport = 300;

    /// <summary>Half a screen less half a row: how far the middle is from either edge.</summary>
    private const double Half = (Viewport - RowHeight) / 2;

    /// <summary>A pattern with a neighbour on each side, as the grid works the pads out.</summary>
    private static PatternMetrics Between() => new(8, RowHeight, 4, Half, Half);

    /// <summary>The first pattern of a song: nothing before it, something after.</summary>
    private static PatternMetrics First() => new(8, RowHeight, 4, 0, Half);

    /// <summary>The last: something before it, nothing after.</summary>
    private static PatternMetrics Last() => new(8, RowHeight, 4, Half, 0);

    /// <summary>How far down the screen a row ends up, measured to the middle of the row.</summary>
    private static double Down(PatternMetrics metrics, int row, int lines) =>
        metrics.RowY(row) + RowHeight / 2
        - ViewportScroller.CentreRow(Viewport, metrics, row, lines);

    [Fact]
    public void A_row_in_the_thick_of_a_pattern_lands_on_the_middle()
    {
        Assert.Equal(Viewport / 2, Down(Between(), 32, 64), 3);
    }

    [Fact]
    public void And_every_row_of_a_pattern_between_two_others_does()
    {
        var metrics = Between();

        for (int row = 0; row < 64; row++) Assert.Equal(Viewport / 2, Down(metrics, row, 64), 3);
    }

    [Fact]
    public void The_first_pattern_of_a_song_starts_against_the_top()
    {
        var metrics = First();

        Assert.Equal(0, ViewportScroller.CentreRow(Viewport, metrics, 0, 64));
        Assert.Equal(RowHeight / 2, Down(metrics, 0, 64), 3);
    }

    [Fact]
    public void But_is_centred_again_as_soon_as_there_is_room()
    {
        Assert.Equal(Viewport / 2, Down(First(), 20, 64), 3);
    }

    [Fact]
    public void The_last_pattern_of_a_song_ends_against_the_bottom()
    {
        var metrics = Last();

        Assert.Equal(Viewport - RowHeight / 2, Down(metrics, 63, 64), 3);
    }

    [Fact]
    public void The_space_either_side_is_counted_into_the_height()
    {
        Assert.Equal(64 * RowHeight + Half * 2, Between().ContentHeight(64), 3);
        Assert.Equal(64 * RowHeight + Half, First().ContentHeight(64), 3);
    }

    [Fact]
    public void A_click_lands_on_the_row_under_it_whatever_is_above()
    {
        var metrics = Between();

        Assert.Equal(0, metrics.LineAt(metrics.RowY(0) + 1, 64));
        Assert.Equal(17, metrics.LineAt(metrics.RowY(17) + 1, 64));

        // A click on the pattern before this one is the first row of this one, not a
        // negative one and not the last.
        Assert.Equal(0, metrics.LineAt(4, 64));
    }

    [Fact]
    public void A_pattern_alone_in_a_song_and_shorter_than_the_window_does_not_scroll()
    {
        var alone = new PatternMetrics(8, RowHeight, 4);

        Assert.Equal(0, ViewportScroller.CentreRow(Viewport, alone, 3, 4));
    }
}
