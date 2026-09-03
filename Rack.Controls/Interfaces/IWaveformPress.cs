
using Avalonia.Input;

namespace JingleBox2.Rack.Controls.Interfaces;

/// <summary>
/// What a press on a picture of a recording means before anything on the picture is asked
/// about it: whether the hand is moving the window rather than taking hold of something in it.
/// </summary>
/// <remarks>
/// Every waveform in this application is zoomable and therefore pannable, and each one has
/// something else the left button already does: handles on one, boundaries on another, a region
/// drawn on a third. So the gesture that moves the picture cannot be the plain left button
/// everywhere, and it has to be the same gesture in all of them or it is a gesture nobody can
/// keep in their head.
///
/// One rule rather than a test per control. Two spellings of this would drift apart, and the
/// way that fails is a drag that pans in one editor and draws a region in the next.
///
/// Public because a machine drawing a picture of a recording of its own should feel like the
/// ones we ship, which is the same argument <see cref="IMeterScale"/> and
/// <see cref="INumericInput"/> are public on.
/// </remarks>
public interface IWaveformPress
{
    /// <summary>
    /// Whether this press means move the picture rather than touch what is drawn on it.
    /// </summary>
    /// <param name="middleButton">Whether the middle button is the one that went down.</param>
    /// <param name="held">What was held on the keyboard as it did.</param>
    bool MeansPan(bool middleButton, KeyModifiers held);
}
