namespace JingleBox2.Rack.SoundDevices.Timing;

/// <summary>
/// A length of musical time, as a knob with a Free position at one end of it.
/// </summary>
/// <remarks>
/// One control rather than two. A wobble or a delay that can run either at its own speed or in
/// time with the song wants a switch for which, and a choice of note length for the second, and
/// putting both on one control is how every machine that has ever done this does it: turn it off
/// the end and the knob beside it rules, turn it anywhere else and the song does.
///
/// The names are written out one by one and their order is the order of the numbers a device
/// saves, so a preset written today still means the same thing when another length is added. New
/// ones go on the end.
/// </remarks>
public static class Division
{
    /// <summary>The Free position, where the rate or the time beside it is the answer.</summary>
    public const int Free = 0;

    /// <summary>What each position is called, for a panel to put on a switch.</summary>
    public static readonly string[] Names =
        { "Free", "1/1", "1/2", "1/4", "1/8", "1/8T", "1/16", "1/16T", "1/32" };

    /// <summary>How many beats each position is, with Free standing for none.</summary>
    private static readonly double[] Beats =
        { 0.0, 4.0, 2.0, 1.0, 0.5, 1.0 / 3.0, 0.25, 1.0 / 6.0, 0.125 };

    /// <summary>The last position there is, which is what a panel clamps to.</summary>
    public static int Most => Names.Length - 1;

    /// <summary>Whether that position means the song decides rather than the knob.</summary>
    public static bool Synced(int at) => at > Free && at <= Most;

    /// <summary>How many quarter notes one of these lasts, or nought for Free.</summary>
    public static double BeatsIn(int at) => Synced(at) ? Beats[at] : 0.0;

    /// <summary>
    /// How long one of these lasts in seconds at that tempo, or the spare answer for Free.
    /// </summary>
    /// <param name="at">Which length.</param>
    /// <param name="transport">Where the song is, which is where the tempo comes from.</param>
    /// <param name="free">What to answer with when nothing is synced, in seconds.</param>
    public static double SecondsIn(int at, Transport transport, double free)
    {
        if (!Synced(at)) return free;

        return BeatsIn(at) * (transport ?? Transport.Still).SecondsPerBeat;
    }

    /// <summary>
    /// How often one of these comes round, in hertz, or the spare answer for Free.
    /// </summary>
    /// <remarks>
    /// The other way of asking the same question, for the things whose knob is a rate rather than
    /// a time. A wobble set to a quarter note at a hundred and twenty is two hertz.
    /// </remarks>
    public static double HertzIn(int at, Transport transport, double free)
    {
        if (!Synced(at)) return free;

        double seconds = SecondsIn(at, transport, 0.0);

        return seconds > 0.0 ? 1.0 / seconds : free;
    }
}
