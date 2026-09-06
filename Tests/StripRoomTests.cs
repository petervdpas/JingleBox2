using JingleBox2.UI;
using JingleBox2.UI.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// How tall a strip folded under something may be dragged.
/// </summary>
/// <remarks>
/// **A grip that could take the pattern to nothing is a grip that breaks the page.** The strips
/// sit in rows that are given exactly what they ask for, so nothing else in that layout can
/// refuse them: without this rule the strips fill the window and the part somebody is writing is
/// gone, which is the one thing that page is for.
///
/// The awkward half is most of what is below, and all of it is reachable with a mouse: a window
/// too small to hold what is already open, a strip asked about before the page has been laid out
/// at all, and the moment there is exactly nothing left to give.
/// </remarks>
public class StripRoomTests
{
    private readonly IStripRoom _room = new StripRoom();

    /// <summary>What is left after the floor and the other strips is what one may take.</summary>
    [Fact]
    public void What_is_left_is_what_it_may_take()
    {
        Assert.Equal(240, _room.Tallest(room: 500, others: 100, least: 160, holding: 0));
    }

    /// <summary>The other strips take their room off this one rather than off the floor.</summary>
    [Fact]
    public void The_other_strips_take_their_share_first()
    {
        double alone = _room.Tallest(500, 0, 160, 0);
        double beside = _room.Tallest(500, 100, 160, 0);

        Assert.Equal(100, alone - beside);
    }

    /// <summary>A window with nothing to spare gives nothing to grow into.</summary>
    [Fact]
    public void A_full_window_gives_nothing_away()
    {
        Assert.Equal(0, _room.Tallest(260, 100, 160, 0));
    }

    /// <summary>
    /// A window too small for what is already open refuses to grow rather than collapsing it.
    /// </summary>
    /// <remarks>
    /// The answer would otherwise be a negative number, and a strip clamped to that is a strip
    /// that folds itself up because somebody made the window smaller. A page too small for a
    /// strip is a page to be made bigger.
    /// </remarks>
    [Fact]
    public void A_window_too_small_never_collapses_what_is_open()
    {
        Assert.Equal(120, _room.Tallest(room: 200, others: 300, least: 160, holding: 120));
    }

    /// <summary>And it is never less than what the strip has to be to show what is in it.</summary>
    [Fact]
    public void It_is_never_less_than_what_the_strip_holds()
    {
        Assert.Equal(90, _room.Tallest(room: 300, others: 100, least: 160, holding: 90));
    }

    /// <summary>Before the page has been laid out there is nothing to be measured against.</summary>
    /// <remarks>
    /// A grip cannot be dragged before the page exists, and the numbers arrive from a layout
    /// that has not run, so the honest answer is no ceiling rather than a ceiling of nought.
    /// </remarks>
    [Fact]
    public void Before_the_page_exists_there_is_no_ceiling()
    {
        Assert.Equal(double.PositiveInfinity, _room.Tallest(0, 0, 160, 0));
        Assert.Equal(double.PositiveInfinity, _room.Tallest(double.NaN, 0, 160, 0));
        Assert.Equal(double.PositiveInfinity, _room.Tallest(double.PositiveInfinity, 0, 160, 0));
    }

    /// <summary>Nonsense from a layout is read as nothing rather than taken off the room.</summary>
    /// <remarks>
    /// A strip that has never been arranged has a height of nought, and a negative one would be
    /// a fault somewhere else; either way, subtracting it would hand this strip more room than
    /// the window has.
    /// </remarks>
    [Fact]
    public void Nonsense_from_a_layout_takes_nothing()
    {
        Assert.Equal(_room.Tallest(500, 0, 160, 0), _room.Tallest(500, -100, 160, 0));
        Assert.Equal(_room.Tallest(500, 100, 0, 0), _room.Tallest(500, 100, -160, 0));
    }

    /// <summary>The floor is enough lines of a pattern to be worth looking at.</summary>
    /// <remarks>
    /// Pinned rather than left to a reading of the constant, since what makes it a floor rather
    /// than a token is that it is big enough to work in.
    /// </remarks>
    [Fact]
    public void The_floor_leaves_a_pattern_worth_looking_at()
    {
        Assert.True(StripRoom.DefaultLeast >= 120);
    }
}
