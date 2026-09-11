
namespace JingleBox2.Rack.Controls.Interfaces;

/// <summary>
/// Where a level sits on a meter.
/// </summary>
/// <remarks>
/// Amplitude is what the audio gives you, and a meter that plots it straight spends most of its
/// length on the loudest few decibels and shows nothing useful below half scale. The decibel
/// scale is the one worth reading, which is why it is the default rather than an option.
/// </remarks>
public interface IMeterScale
{
    /// <summary>Quiet enough to be the bottom of the meter without hiding a soft take.</summary>
    const double DefaultMinimumDecibels = -60;

    /// <summary>Amplitude at or above this is at the top, and worth a warning.</summary>
    const double ClipAmplitude = 0.999;

    /// <summary>Amplitude as decibels below full scale. Silence is treated as the floor.</summary>
    double Decibels(double amplitude, double minimumDecibels = DefaultMinimumDecibels);

    /// <summary>How far up the meter a level reaches, 0 to 1.</summary>
    double Position(double amplitude, double minimumDecibels = DefaultMinimumDecibels, bool decibels = true);

    /// <summary>
    /// Which pixel along a meter of that length a reading lands on.
    /// </summary>
    /// <remarks>
    /// **What a meter is asked to redraw for.** A reading arrives twenty times a second and
    /// almost never lands anywhere new: the level moves in the fourth decimal while the bar
    /// stays exactly where it was, and a control that repaints on the value rather than on the
    /// picture is redrawing a dozen meters sixty times a second to show the same thing. Asked
    /// this instead, it repaints when the bar actually moves.
    ///
    /// The screen's own pixels rather than a tolerance somebody picked: what matters is whether
    /// anybody could see the difference, and that is what a pixel is.
    /// </remarks>
    /// <param name="amplitude">The reading.</param>
    /// <param name="minimumDecibels">The bottom of this meter's scale.</param>
    /// <param name="pixels">How long the bar is, in pixels.</param>
    /// <returns>The pixel it fills to, which is nought for a meter with no room.</returns>
    int Step(double amplitude, double minimumDecibels, double pixels);

    /// <summary>
    /// A peak mark that falls back at a steady rate rather than sticking. Held for a moment
    /// first, so a transient is readable before it starts to drop.
    /// </summary>
    double DecayPeak(
    double peak,
    double level,
    double secondsSincePeak,
    double holdSeconds,
    double decibelsPerSecond);
}
