using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// Nought means nothing was chosen, which is the same nought the tracker's own output reads as
/// "follow the device". The card opens at the recommended rate and the mixer follows the card,
/// so the two agree whether or not anybody has been to SETTINGS.
/// </remarks>
public sealed class OutputRate : IOutputRate
{
    /// <summary>The sound server's cycle, whose rate is what nothing chosen opens at.</summary>
    private readonly ISoundServerClock _clock;

    /// <summary>Which rates are offered and which is recommended.</summary>
    private readonly IAudioChoices _choices;

    /// <summary>Builds one that answers for this machine.</summary>
    /// <param name="clock">The sound server's cycle, asked of the real server where nothing is said.</param>
    /// <param name="choices">What is recommended, the shipped rule where nothing is said.</param>
    public OutputRate(ISoundServerClock? clock = null, IAudioChoices? choices = null)
    {
        _clock = clock ?? new SoundServerClock();
        _choices = choices ?? new AudioChoices();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Nothing chosen opens at the sound server's own rate where it is one that is offered, so
    /// nothing is converted on the way out, and at 44100 where the server cannot be asked.
    /// </remarks>
    public int Chosen(int setting) => setting > 0 ? setting : _choices.RecommendedRate(_clock.Read());
}
