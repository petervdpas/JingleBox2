using Avalonia.Media;
using JingleBox2.Machines.Ui.Records;

namespace JingleBox2.Machines.Ui.Interfaces;

/// <summary>
/// Turning a place in a recording into a place on the picture of it, and back.
/// </summary>
/// <remarks>
/// Both directions, because both are used at once: the picture is drawn from the samples and
/// the loop handles are dragged on the picture, so a disagreement between them is a handle that
/// does not follow the pointer.
/// </remarks>
public interface IWaveformGeometry
{
    /// <summary>
    /// Which peaks a viewport is showing, and how wide each of them lands.
    /// </summary>
    /// <remarks>
    /// The start is clamped so that scrolling to the far end still fills the width rather than
    /// running off it into blank space, which is what a scroll position taken at face value
    /// does once the zoom changes underneath it.
    /// </remarks>
    VisibleRange GetVisibleRange(int peakCount, WaveformViewport viewport, double width);

    /// <summary>
    /// Builds the outline: across the top following the peaks, then back along the bottom
    /// mirrored, closed into one filled shape centred on the vertical midpoint.
    /// </summary>
    StreamGeometry Build(float[] peaks, WaveformViewport viewport, double width, double height);
}
