namespace JingleBox2.Rack.SoundDevices.Timing;

/// <summary>Where the song is and how fast, as everything that makes a sound is told it.</summary>
/// <remarks>
/// In the sound device kit rather than beside the plugins, because a tempo is not a plugin's
/// business: it is the song's. A delay of ours wanting a dotted eighth and somebody else's plugin
/// wanting the same thing are asking the same question, and there should be one answer to it.
///
/// A class and not a record struct on purpose: it is swapped whole by one thread and read whole
/// by another, and a reference is the only thing that can be written in one go. A struct this
/// size would be read half torn.
///
/// Beats are counted from wherever playing began rather than from the top of the song. A tracker
/// has no single answer to "how far into the song are we" once the order list loops, and what
/// anything wants from this number is somewhere steady to hang a pattern off.
/// </remarks>
/// <param name="Playing">Whether the transport is rolling.</param>
/// <param name="Bpm">The song's tempo.</param>
/// <param name="Beats">How many quarter notes have gone by since playing began.</param>
/// <param name="Numerator">The top of the time signature.</param>
/// <param name="Denominator">And the bottom of it.</param>
public sealed record Transport(
    bool Playing,
    double Bpm,
    double Beats,
    int Numerator = 4,
    int Denominator = 4)
{
    /// <summary>Nothing playing, at the tempo a song has before anybody has set one.</summary>
    public static readonly Transport Still = new(false, 120.0, 0.0);

    /// <summary>
    /// The beat the bar this falls in began on, which is what VST3 calls the bar position.
    /// </summary>
    public double BarBeats
    {
        get
        {
            double perBar = Numerator * 4.0 / System.Math.Max(1, Denominator);

            if (perBar <= 0) return 0;

            return System.Math.Floor(Beats / perBar) * perBar;
        }
    }

    /// <summary>How long one beat lasts, in seconds.</summary>
    public double SecondsPerBeat => 60.0 / System.Math.Max(1.0, Bpm);
}
