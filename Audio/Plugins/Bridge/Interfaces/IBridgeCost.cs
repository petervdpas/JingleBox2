namespace JingleBox2.Audio.Plugins.Bridge.Interfaces;

/// <summary>
/// What one crossing to a plugin's own process costs, against the time that audio had.
/// </summary>
/// <remarks>
/// <see cref="Audio.Interfaces.IRenderCost"/> says what a whole block cost and cannot say what it
/// was spent on. A song on five plugins spends most of a block inside them, and from the mixing
/// thread that is one number: five and a half milliseconds of an eleven millisecond block, with
/// nothing to say whether it went into somebody's synthesis or into getting there and back.
///
/// **Those two want opposite answers.** A plugin that is genuinely expensive is a plugin, and the
/// only thing to do about it is fewer of them or a bigger buffer. A crossing that is expensive is
/// this application's own, and it is fixed cost paid once per plugin per block whatever the plugin
/// is doing, so it is worth attacking and it grows with the number of plugins rather than with the
/// music. Told apart by measuring the same block at both ends: this is the parent's half, and the
/// child says what the plugin itself took.
///
/// Both halves in the words the mixing already reports in, so they can be read against the render
/// cost line above them without arithmetic. The milliseconds are said as well as the share,
/// because a fixed cost is what an absolute number shows and a share hides.
///
/// It answers a sentence rather than writing one, so it can be put a question to without a log,
/// a plugin or a wait. Nothing allocates on the way in: this is called from the thread the sound
/// card is waiting on.
/// </remarks>
public interface IBridgeCost
{
    /// <summary>
    /// Records one crossing, and answers a line to write down when the stretch is up.
    /// </summary>
    /// <remarks>
    /// Nothing back means there is nothing to say yet, which is almost every call.
    ///
    /// A crossing with no frames, no time or no rate is not a crossing and is ignored rather than
    /// counted as a free one, the same rule the render cost keeps and for the same reason.
    /// </remarks>
    /// <param name="frames">How many frames that crossing carried.</param>
    /// <param name="milliseconds">How long the round trip took.</param>
    /// <param name="rate">The rate the audio is made at, which turns frames into time.</param>
    string? Crossed(int frames, double milliseconds, int rate);

    /// <summary>The dearest crossing of the stretch so far, as a share of the time it had.</summary>
    double Worst { get; }

    /// <summary>How many crossings have been counted in the stretch so far.</summary>
    int Crossings { get; }
}
