namespace JingleBox2.UI;

/// <summary>What each block on the routing table is called underneath.</summary>
/// <remarks>
/// Data rather than behaviour, which is why it is a static class like the rest of the words this
/// application spells in more than one place. It is here because it was already spelled twice: by
/// the graph that draws the blocks and by the answer to what a block is putting out. Two
/// spellings of an id is how a block comes to have a meter that reads nothing.
///
/// Literals rather than anything built, so a search for one of them finds every use.
/// </remarks>
public static class PatchNodes
{
    /// <summary>
    /// The take: what is coming in, and what it passes on to the desk.
    /// </summary>
    /// <remarks>
    /// **RECORD is two busses and was one block, which is what made the picture unreadable.**
    /// What arrives at the input sums onto the recorder's own bus, is what the file is written
    /// from, and goes to the desk as one pair while Hear it is on; a take being auditioned is a
    /// different bus altogether, is a source like the pads, and has nothing to do with what is
    /// being recorded. Drawn as one block with an in port and an out port, those two read as one
    /// thing passing through, so the strip headed PLAY on the desk had no block at all and
    /// somebody reading the picture would think a take being played was going into the take.
    ///
    /// The id is still `record`, since it is what a stored place and a stored cable name, and a
    /// block that changed its id would lose every arrangement anybody has made.
    /// </remarks>
    public const string Record = "record";

    /// <summary>A take being auditioned, which is the PLAY strip on the desk.</summary>
    /// <remarks>
    /// A source and nothing else: it gives out and takes nothing in, the way FIRE does. See
    /// <see cref="Record"/> for why the two are not one block.
    /// </remarks>
    public const string Play = "play";

    /// <summary>The song's tracks, which give out one pair each.</summary>
    public const string Tracker = "tracker";

    /// <summary>
    /// The song's own master, where the tracks are summed before the desk hears them.
    /// </summary>
    /// <remarks>
    /// A block of its own because it is one in the engine: `TrackMixer` sums every track into
    /// `Song.Master`, applies the song's chain, its level and its pan and then the saturation,
    /// and **one** stereo pair leaves for the output bus. The picture used to draw four track
    /// pairs arriving at the desk, which is the inside of the tracker drawn as though it were
    /// the wiring.
    /// </remarks>
    public const string Song = "song";

    /// <summary>The pads, together, however many are down.</summary>
    public const string Fire = "fire";

    /// <summary>The desk, which is where everything is summed.</summary>
    public const string Mixer = "mixer";

    /// <summary>What the machine is playing the mix out of.</summary>
    public const string Output = "output";
}
