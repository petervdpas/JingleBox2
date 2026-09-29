
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

    /// <summary>Amplitude at or above this is at full scale, and worth a warning.</summary>
    const double ClipAmplitude = 0.999;

    /// <summary>
    /// The top of a scale that stops at full scale, which is what every call means unless told otherwise.
    /// </summary>
    /// <remarks>
    /// A scale can be asked to run past full scale instead, which is how a desk meter reads: 0 is
    /// the reference and the scale carries on above it, so a level that has gone over has
    /// somewhere to be shown rather than piling up against the top.
    /// </remarks>
    const double FullScaleDecibels = 0;

    /// <summary>
    /// Amplitude as decibels relative to full scale, held between the floor and the top.
    /// Silence is treated as the floor.
    /// </summary>
    /// <param name="amplitude">The reading, where 1 is full scale.</param>
    /// <param name="minimumDecibels">The bottom of the scale.</param>
    /// <param name="maximumDecibels">The top of the scale, 0 for full scale.</param>
    /// <returns>The decibels.</returns>
    double Decibels(double amplitude, double minimumDecibels = DefaultMinimumDecibels,
                    double maximumDecibels = FullScaleDecibels);

    /// <summary>How far up the meter a level reaches, 0 to 1.</summary>
    /// <param name="amplitude">The reading, where 1 is full scale.</param>
    /// <param name="minimumDecibels">The bottom of the scale.</param>
    /// <param name="decibels">False to plot the amplitude straight, which ignores both ends.</param>
    /// <param name="maximumDecibels">The top of the scale, 0 for full scale.</param>
    /// <returns>The fraction of the meter.</returns>
    double Position(double amplitude, double minimumDecibels = DefaultMinimumDecibels, bool decibels = true,
                    double maximumDecibels = FullScaleDecibels);

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
    /// <param name="maximumDecibels">The top of this meter's scale, 0 for full scale.</param>
    /// <returns>The pixel it fills to, which is nought for a meter with no room.</returns>
    int Step(double amplitude, double minimumDecibels, double pixels, double maximumDecibels = FullScaleDecibels);

    /// <summary>
    /// A peak mark that falls back at a steady rate rather than sticking. Held for a moment
    /// first, so a transient is readable before it starts to drop.
    /// </summary>
    /// <param name="peak">Where the mark is.</param>
    /// <param name="level">Where the reading is.</param>
    /// <param name="secondsSincePeak">How long since the mark was last pushed up.</param>
    /// <param name="holdSeconds">How long it sits still first.</param>
    /// <param name="decibelsPerSecond">How fast it falls after that.</param>
    /// <param name="maximumDecibels">The top of the scale, 0 for full scale.</param>
    /// <returns>Where the mark is now.</returns>
    double DecayPeak(
    double peak,
    double level,
    double secondsSincePeak,
    double holdSeconds,
    double decibelsPerSecond,
    double maximumDecibels = FullScaleDecibels);
}
