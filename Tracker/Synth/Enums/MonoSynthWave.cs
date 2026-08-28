namespace JingleBox2.Tracker.Synth.Enums;

/// <summary>
/// Which of the two shapes the oscillator makes.
/// </summary>
/// <remarks>
/// Two rather than six, because this is one oscillator into a filter and the filter is where
/// the tone comes from. The numbers are written down in songs, so they are fixed rather than
/// implied by the order.
/// </remarks>
public enum MonoSynthWave
{
    /// <summary>A ramp: everything the harmonic series has, which is what a filter wants.</summary>
    Saw = 0,

    /// <summary>A square whose two halves are uneven, and can be moved while the note sounds.</summary>
    Pulse = 1
}
