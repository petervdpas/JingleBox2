namespace JingleBox2.UI;

/// <summary>What each fixed point on a block is called.</summary>
/// <remarks>
/// The other half of <see cref="PatchNodes"/> and here for the same reason: a port's name is what
/// the picture prints and what a level is looked up by, so it is a literal in one place rather
/// than the same word typed wherever it was needed.
///
/// Only the fixed ones. What a source on the machine gives out is <see cref="Out"/> whatever the
/// program is, and a track's is the name its own strip wears, which is the song's to decide.
/// </remarks>
public static class PatchPorts
{
    /// <summary>What a source on the machine gives out.</summary>
    public const string Out = "out";

    /// <summary>Where the recorder listens.</summary>
    public const string Capture = "capture";

    /// <summary>What the recorder sends to the desk: a take, or the input being listened to.</summary>
    public const string Takes = "takes";

    /// <summary>The pads, together.</summary>
    public const string Pads = "pads";

    /// <summary>What the whole desk sums to, and what the song sums to before it.</summary>
    /// <remarks>
    /// One word on two blocks, which is right: each is the sum of what arrives at it. Which
    /// master is meant is the block, and that is what <see cref="Records.SignalPoint"/> carries
    /// both halves of.
    /// </remarks>
    public const string Master = "master";

    /// <summary>Where the song arrives on the desk.</summary>
    public const string Song = "song";

    /// <summary>Where that lands on the machine.</summary>
    public const string Playback = "playback";

    /// <summary>What the tracker gives out before a song has any tracks.</summary>
    public const string Mix = "mix";
}
