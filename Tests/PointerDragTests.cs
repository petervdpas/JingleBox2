using JingleBox2.UI;
using JingleBox2.UI.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// When a press has become a drag, which is what stops a click selecting a block.
/// </summary>
/// <remarks>
/// The pattern grid had no such rule: a block began the moment the pointer was over a different
/// cell from the one it was pressed on, and a row is under twenty pixels tall, so a click near
/// the edge of one needed a single pixel of movement to select two lines. That is the fault
/// this exists to keep out, and it is the kind that comes back the next time somebody tidies
/// the pointer handlers, so the rule is a thing that can be asked rather than a comparison
/// buried in a control.
/// </remarks>
public class PointerDragTests
{
    private readonly IPointerDrag _drag = new PointerDrag();

    /// <summary>A press that has not moved is a click, whatever else is true.</summary>
    [Fact]
    public void A_press_that_has_not_moved_is_not_a_drag()
    {
        Assert.False(_drag.Begun(100, 100, 100, 100));
    }

    /// <summary>
    /// And the pixel or two a hand puts into pressing a button is still a click. This is the
    /// case the pattern grid was getting wrong, since one pixel down the page is a different
    /// row when the press landed near a row's edge.
    /// </summary>
    [Fact]
    public void A_hand_shaking_on_the_button_is_not_a_drag()
    {
        Assert.False(_drag.Begun(100, 100, 101, 100));
        Assert.False(_drag.Begun(100, 100, 100, 102));
        Assert.False(_drag.Begun(100, 100, 97, 103));
    }

    /// <summary>A real movement is, in either direction and either way along it.</summary>
    [Fact]
    public void A_real_movement_is_a_drag()
    {
        Assert.True(_drag.Begun(100, 100, 120, 100));
        Assert.True(_drag.Begun(100, 100, 100, 120));
        Assert.True(_drag.Begun(100, 100, 80, 100));
        Assert.True(_drag.Begun(100, 100, 100, 80));
    }

    /// <summary>
    /// The two axes are asked about on their own rather than as a distance. A movement mostly
    /// across the page would otherwise start a drag down it that nobody made, which matters
    /// here because a row is much shorter than a cell is wide.
    /// </summary>
    [Fact]
    public void The_axes_are_not_added_together()
    {
        Assert.False(_drag.Begun(100, 100, 104, 104));
        Assert.True(_drag.Begun(100, 100, 107, 100));
    }

    /// <summary>Exactly the threshold is not past it, so the reading has one meaning.</summary>
    [Fact]
    public void The_threshold_itself_is_not_past_it()
    {
        Assert.False(_drag.Begun(0, 0, _drag.Threshold, 0));
        Assert.True(_drag.Begun(0, 0, _drag.Threshold + 0.5, 0));
    }

    /// <summary>One can be given a threshold of its own, and a nonsense one is refused.</summary>
    [Fact]
    public void A_threshold_of_its_own_is_allowed_and_a_nonsense_one_is_not()
    {
        Assert.True(new PointerDrag(1).Begun(0, 0, 2, 0));
        Assert.Equal(0, new PointerDrag(-5).Threshold);
        Assert.Equal(PointerDrag.DefaultThreshold, new PointerDrag(double.NaN).Threshold);
    }
}
