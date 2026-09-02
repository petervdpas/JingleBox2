
namespace JingleBox2.Rack.Controls.Interfaces;

/// <summary>
/// The value maths every range control shares: where a value sits in its range, what a drag
/// does to it, and the step grid it lands on.
/// </summary>
/// <remarks>
/// No Avalonia types anywhere in it, so every one of these can be checked without a window.
/// That is the whole reason it is apart from the controls: a knob, a fader and a slider each
/// draw differently and all three answer the same three questions, and the answers are
/// arithmetic rather than pictures.
///
/// A range that is dead or the wrong way round is an ordinary arrival rather than a fault. A
/// machine's description comes out of a file somebody wrote by hand, so a parameter whose
/// maximum is below its minimum turns up, and a control that sits at the low end is one
/// somebody can see is wrong where an exception on the drawing thread takes the panel down.
/// </remarks>
public interface IRangeValue
{
    /// <summary>Holding shift makes the same drag cover a quarter as much.</summary>
    double FineFactor { get; }

    /// <summary>Where the value sits in its range, 0 to 1. A dead range reads as the bottom.</summary>
    double Fraction(double value, double minimum, double maximum);

    /// <summary>Clamps into range and onto the step grid, measured from the minimum.</summary>
    /// <remarks>
    /// From the minimum rather than from nought, which matters for any range that does not have
    /// nought at an end: -24 to 24 in steps of 5 measured from nought cannot reach either of its
    /// own ends, so a transpose knob would stop two semitones short at both extremes.
    /// </remarks>
    double Quantize(double value, double minimum, double maximum, double step);

    /// <summary>
    /// The value a drag lands on. Dragging up raises it, and the distance is measured from
    /// where the drag started rather than from the last move: a drag that goes down and back
    /// up then ends where it began.
    /// </summary>
    double FromDrag(
    double startValue,
    double pixelsDraggedUp,
    double minimum,
    double maximum,
    double step,
    double pixelsForFullRange,
    bool fine = false);
}
