namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// The last thing a sample goes through before it leaves for the sound card.
/// </summary>
/// <remarks>
/// Two jobs, and they are not the same job. What is merely too loud is bent rather than clipped,
/// since a chord of voices summing past full scale is music and a hard corner on it sounds like
/// a fault. What is not a real number at all is silenced, since it is not a loud sample but the
/// absence of one, and the honest thing to play for a sample that does not exist is nothing.
///
/// **What this protects is not the card.** A converter puts out a bounded voltage whatever the
/// bits say, and no signal a program can play will damage one; software that sits on the
/// hardware and writes its registers, its clocks or its firmware is a different matter and this
/// application has no path to any of that, since everything goes out through BASS. What is
/// genuinely at risk from a bad buffer is the speakers and whoever is in the room: NaN is
/// undefined at the converters and commonly arrives as full scale noise, which is how tweeters
/// and hearing are actually damaged.
///
/// One rule rather than one per way out, because there is more than one way out. The tracker's
/// mix leaves through the master and a pad leaves through its own stream, and a guard on one of
/// those is a guard on half the application.
/// </remarks>
public interface IOutputCurve
{
    /// <summary>What that sample becomes on its way out.</summary>
    /// <param name="value">The sample as whatever made it left it.</param>
    float Bend(float value);

    /// <summary>The same for a whole block of interleaved audio, in place.</summary>
    /// <param name="buffer">The audio.</param>
    /// <param name="samples">How many of its entries are real.</param>
    void Bend(float[] buffer, int samples);
}
