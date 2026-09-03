namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// What mixing a block costs, against the time that block had.
/// </summary>
/// <remarks>
/// **A buffer that has to be twice somebody else's is one of two faults and they want opposite
/// answers**, and from a chair the two are the same stutter. Either the mixing is genuinely
/// expensive, in which case the block is nearly all used up and the work has to get cheaper; or
/// it is cheap and late, in which case the block is mostly idle and what is wrong is when the
/// thread runs rather than how long it takes. Guessing between those is how an afternoon goes.
///
/// So each block is timed against its own length in real time. A block of 512 frames at 48 kHz
/// has 10.7 milliseconds; taking 3 of them is 28% and taking 11 is a dropout. What comes out is
/// one line every few seconds naming the worst, the mean and how many went over, which is the
/// same measurement a host's own CPU meter is and is the number to put beside another program's.
///
/// It answers a sentence rather than writing one, so it can be put a question to without a log,
/// a sound card or a wait. Nothing is said until the stretch is up, and nothing allocates on the
/// way in: this is called from the thread the sound card is waiting on.
/// </remarks>
public interface IRenderCost
{
    /// <summary>
    /// Records one block, and answers a line to write down when the stretch is up.
    /// </summary>
    /// <remarks>
    /// Nothing back means there is nothing to say yet, which is almost every call.
    ///
    /// A block with no frames, no time or no rate is not a block and is ignored rather than
    /// counted as a free one: a rate of nought would divide by it, and a zero-length block
    /// averaged in would report the mixing as cheaper than it is.
    /// </remarks>
    /// <param name="frames">How many frames were mixed.</param>
    /// <param name="milliseconds">How long that took.</param>
    /// <param name="rate">The rate the mix is made at, which is what turns frames into time.</param>
    string? Took(int frames, double milliseconds, int rate);

    /// <summary>
    /// The worst block of the stretch so far, as a fraction of the time it had. One is all of it.
    /// </summary>
    double Worst { get; }

    /// <summary>How many blocks have been counted in the stretch so far.</summary>
    int Blocks { get; }

    /// <summary>Forgets the stretch, for an output that has just been opened again.</summary>
    void Fresh();
}
