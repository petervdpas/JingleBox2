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
    /// <summary>The recorder: what is coming in, and what a take is putting out.</summary>
    public const string Record = "record";

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
