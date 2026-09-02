
namespace JingleBox2.Rack.Ui.Interfaces;

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
