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

    /// <summary>What a take being auditioned sends to the desk.</summary>
    /// <remarks>
    /// The player's alone now. It carried the input being listened to as well while the recorder
    /// was one block, which is two busses down one cable and is why neither could be read.
    /// </remarks>
    public const string Takes = "takes";

    /// <summary>
    /// What the recorder's own bus sends to the desk, which is everything pointed at RECORD.
    /// </summary>
    /// <remarks>
    /// The same word at both ends, like <see cref="Takes"/>, <see cref="Pads"/> and
    /// <see cref="Song"/>: a cable is one thing and is called one thing, and where it lands on
    /// the desk is the IN strip. Whether anything travels along it is Hear it, which is that
    /// cable said in a switch.
    /// </remarks>
    public const string Input = "input";

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
