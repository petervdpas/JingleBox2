using JingleBox2.Rack.Controls;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// How far a waveform is zoomed in and where its left edge sits.
/// </summary>
/// <remarks>
/// The viewport is what a picture of a recording is looked at through, and it is published: an
/// outside machine drawing its own face gets the same control the application's do. What is
/// tested here is the arithmetic rather than the control, since the control is a drawing and
/// this is where a picture that slides sideways under somebody's hand comes from.
/// </remarks>
public class WaveformZoomTests
{
    /// <summary>The view under test.</summary>
    private readonly WaveformViewport _view = new();

    /// <summary>Zoomed right out, the whole recording is on screen and there is nowhere to go.</summary>
    [Fact]
    public void All_of_it_is_shown_to_begin_with()
    {
        Assert.Equal(WaveformViewport.MinZoom, _view.Zoom);
        Assert.Equal(0, _view.Scroll);
        Assert.Equal(1, _view.VisibleFraction);
        Assert.False(_view.CanPan);
    }

    /// <summary>Zooming in shows less of it and makes room to pan.</summary>
    [Fact]
    public void Zooming_in_shows_less()
    {
        _view.ZoomTo(4);

        Assert.Equal(4, _view.Zoom);
        Assert.Equal(0.25, _view.VisibleFraction, 6);
        Assert.True(_view.CanPan);
    }

    /// <summary>
    /// A zoom from a button holds the middle of the view still.
    /// </summary>
    /// <remarks>
    /// There is no pointer to keep still, unlike the wheel, and the alternative is the picture
    /// sliding sideways under somebody who pressed a magnifying glass.
    /// </remarks>
    [Fact]
    public void Zooming_holds_the_middle_still()
    {
        _view.ZoomTo(2);
        _view.ScrollTo(0.25);

        double centre = _view.Centre;

        _view.ZoomTo(4);

        Assert.Equal(centre, _view.Centre, 6);
    }

    /// <summary>Past the ends means the ends, rather than being refused.</summary>
    /// <remarks>
    /// A caller doubling the zoom at the far end means "as far as it goes", and one halving it
    /// at the near end means "all of it".
    /// </remarks>
    [Theory]
    [InlineData(1000, WaveformViewport.MaxZoom)]
    [InlineData(0.1, WaveformViewport.MinZoom)]
    [InlineData(-5, WaveformViewport.MinZoom)]
    public void Zoom_is_held_between_its_ends(double asked, double got)
    {
        _view.ZoomTo(asked);

        Assert.Equal(got, _view.Zoom);
    }

    /// <summary>The left edge cannot run off either end of the recording.</summary>
    [Fact]
    public void The_view_cannot_run_off_the_end()
    {
        _view.ZoomTo(2);

        _view.ScrollTo(5);
        Assert.Equal(_view.MaxScroll, _view.Scroll, 6);
        Assert.Equal(0.5, _view.Scroll, 6);

        _view.ScrollTo(-5);
        Assert.Equal(0, _view.Scroll);
    }

    /// <summary>Zooming back out puts the left edge back, since there is nowhere else for it.</summary>
    [Fact]
    public void Zooming_out_puts_the_edge_back()
    {
        _view.ZoomTo(8);
        _view.ScrollTo(_view.MaxScroll);

        _view.ZoomTo(WaveformViewport.MinZoom);

        Assert.Equal(0, _view.Scroll);
    }

    /// <summary>
    /// The wheel holds whatever is under the pointer still.
    /// </summary>
    /// <remarks>
    /// Which is the whole difference between it and a button, and it is what makes zooming with
    /// a wheel feel like looking closer rather than like the picture being replaced.
    /// </remarks>
    [Fact]
    public void The_wheel_holds_what_is_under_it_still()
    {
        const double width = 800;
        const double at = 600;

        double was = _view.XToFraction(at, width);

        Assert.True(_view.ZoomAt(4, at, width));

        Assert.Equal(was, _view.XToFraction(at, width), 6);
    }

    /// <summary>A zoom that would change nothing says so, so the wheel can be left alone.</summary>
    /// <remarks>
    /// The picture sits inside a panel that scrolls. Zoomed right out and asked to zoom out
    /// further, the wheel has to reach the panel rather than being swallowed here.
    /// </remarks>
    [Fact]
    public void A_zoom_that_changes_nothing_is_refused()
    {
        Assert.False(_view.ZoomAt(WaveformViewport.MinZoom, 100, 800));

        _view.ZoomTo(WaveformViewport.MaxZoom);

        Assert.False(_view.ZoomAt(WaveformViewport.MaxZoom * 2, 100, 800));
    }

    /// <summary>A width of nothing answers nought rather than dividing by it.</summary>
    /// <remarks>
    /// Which is what a control is asked before it has been laid out, every time.
    /// </remarks>
    [Fact]
    public void No_width_is_not_a_division_by_nought()
    {
        Assert.Equal(0, _view.XToFraction(100, 0));
        Assert.Equal(0, _view.PanDistance(50, 0));
    }
}
