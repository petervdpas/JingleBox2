using JingleBox2.Rack.Controls.Records;

namespace JingleBox2.Rack.Controls.Interfaces;

/// <summary>
/// How far the two ends of a marked stretch may travel, and what a drag across the picture
/// marks out.
/// </summary>
/// <remarks>
/// A rule with no control in it, so where a handle lands can be put a question to without a
/// window, a pointer or a waveform. It is the arithmetic that decides whether somebody can
/// still take hold of what they have marked, which is exactly the kind of thing that is wrong
/// by a hair and stays wrong for a year.
///
/// The gap is handed in rather than known here, because how close two handles may come is a
/// distance on the screen and this knows nothing about screens: at ten times zoom the same
/// number of pixels is a tenth of the fraction it was.
///
/// Published, because a machine drawing its own face out of these controls marks stretches of a
/// recording exactly as the application does, and two spellings of this rule would be two
/// answers to how narrow a region may be.
/// </remarks>
public interface IWaveformRegion
{
    /// <summary>Where the start lands when it is dragged, which is never past the end.</summary>
    /// <param name="at">Where it is being dragged to, nought to one.</param>
    /// <param name="region">Where both ends are now.</param>
    /// <param name="gap">The closest the two may come.</param>
    double Started(double at, Region region, double gap);

    /// <summary>And the end, which is never past the start.</summary>
    /// <param name="at">Where it is being dragged to, nought to one.</param>
    /// <param name="region">Where both ends are now.</param>
    /// <param name="gap">The closest the two may come.</param>
    double Ended(double at, Region region, double gap);

    /// <summary>
    /// The region a drag from one place to another marks out.
    /// </summary>
    /// <remarks>
    /// Either order, because a hand dragging leftwards is marking the same stretch as a hand
    /// dragging rightwards and only one of them starts at the lower number.
    ///
    /// Held at least a gap wide, so a drag that goes nowhere still leaves something with two
    /// ends that can be taken hold of afterwards.
    /// </remarks>
    /// <param name="from">Where the drag began, nought to one.</param>
    /// <param name="to">Where it has got to.</param>
    /// <param name="gap">The narrowest the region may be.</param>
    Region Drawn(double from, double to, double gap);
}
