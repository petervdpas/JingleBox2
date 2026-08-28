namespace JingleBox2.UI.Interfaces;

/// <summary>
/// A level as a fader reads it and as the audio engine wants it. Faders are marked in decibels
/// with unity at 0, which is what every desk does; the engine multiplies by an amplitude.
/// </summary>
public interface IGainScale
{
    /// <summary>The bottom of a fader's travel. Anything at or below it is off.</summary>
    double MinimumDecibels { get; }

    /// <summary>Six decibels of headroom above unity, which is very nearly twice the amplitude.</summary>
    double MaximumDecibels { get; }

    /// <summary>What the engine multiplies by, for a fader sitting at that reading.</summary>
    /// <remarks>
    /// The bottom of the travel is silence rather than a very small amplitude, so a fader pulled
    /// all the way down is off rather than nearly off.
    /// </remarks>
    /// <param name="decibels">Where the fader is, clamped to its travel.</param>
    /// <returns>The amplitude to multiply by, nought at the bottom of the travel.</returns>
    double ToAmplitude(double decibels);

    /// <summary>Where a fader sits, for an amplitude the engine is using.</summary>
    /// <param name="amplitude">What is being multiplied by. Nought and below read as the bottom.</param>
    /// <returns>The reading in decibels, inside the fader's travel.</returns>
    double ToDecibels(double amplitude);
}
