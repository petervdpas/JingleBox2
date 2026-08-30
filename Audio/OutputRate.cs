using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// Nought means nothing was chosen, which is the same nought the tracker's own output reads as
/// "follow the device". Both then land on the same default, so the card and the mixer agree
/// whether or not anybody has been to SETTINGS.
/// </remarks>
public sealed class OutputRate : IOutputRate
{
    /// <inheritdoc/>
    public int Chosen(int setting) => setting > 0 ? setting : TrackerOutput.DefaultSampleRate;
}
