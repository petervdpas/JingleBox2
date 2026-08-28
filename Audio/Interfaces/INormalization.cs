namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Peak normalization: finds the loudest moment in a recording and lifts the whole thing so
/// that moment sits just under full scale.
/// </summary>
/// <remarks>
/// One multiply per sample, and nothing about the balance of the recording changes; it only
/// stops being quiet.
///
/// Peak rather than loudness on purpose. A peak measure is exact, reversible in the head
/// ("this went up 4 dB"), and cannot pump or clip. A loudness measure would sound more even
/// across takes but needs a limiter behind it to be safe, which is a different feature.
/// </remarks>
public interface INormalization
{
    /// <summary>Just under full scale. Room for a resampler to overshoot without clipping.</summary>
    double DefaultTargetDecibels { get; }

    /// <summary>The quietest target worth offering. Below this the lift is barely audible.</summary>
    double MinTargetDecibels { get; }

    /// <summary>Full scale, which is as loud as a target can be asked to be.</summary>
    double MaxTargetDecibels { get; }

    /// <summary>
    /// The most a recording can be lifted, about 40 dB.
    /// </summary>
    /// <remarks>
    /// Past this the file is silence or close to it, and all that would come up is the noise
    /// floor, at which point the lift is doing harm rather than nothing.
    /// </remarks>
    double MaxGain { get; }

    /// <summary>Below this a file counts as silent and is left alone.</summary>
    double SilenceAmplitude { get; }

    /// <summary>The loudest sample in the file, 0 to 1.</summary>
    /// <remarks>
    /// Each sample is widened to an int before its magnitude is taken, because the magnitude of
    /// the lowest short there is has no answer in a short and throws rather than saturating.
    /// </remarks>
    /// <param name="samples">The whole recording, or null.</param>
    double PeakOf(short[]? samples);

    /// <summary>
    /// What to multiply every sample by to put the peak on the target. One means leave it
    /// alone, which is the answer for silence and for a file that is already there.
    /// </summary>
    /// <param name="peak">The loudest sample, 0 to 1, as <see cref="PeakOf"/> reports it.</param>
    /// <param name="targetDecibels">Where that peak should end up. Out of range values are clamped.</param>
    double GainFor(double peak, double targetDecibels);

    /// <summary>Applies a gain in place, rounding to the nearest step and never wrapping round.</summary>
    /// <param name="samples">The recording, changed where it lies.</param>
    /// <param name="gain">What to multiply by. A gain of one leaves the recording untouched.</param>
    void Apply(short[]? samples, double gain);

    /// <summary>An amplitude as decibels, with silence reading as the quietest target.</summary>
    /// <param name="amplitude">A level, 0 to 1.</param>
    double ToDecibels(double amplitude);

    /// <summary>Decibels as an amplitude, the other half of <see cref="ToDecibels"/>.</summary>
    /// <param name="decibels">A level in decibels, at or below full scale.</param>
    double ToAmplitude(double decibels);
}
