namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// Which kind of machine a song was written on, in one word.
/// </summary>
/// <remarks>
/// **A path is the one thing a song writes down that does not travel**, and until this there was
/// no way to know whether the ones in a song were worth looking at. A song made on Linux and
/// opened on Windows names its recordings and its plugins at places that machine has never had,
/// so comparing them there is a question whose answer means nothing: it can only say no, and on
/// the day a settings file is carried between two machines it can say yes and be wrong.
///
/// One word rather than a version or a description of the machine, since the only thing anything
/// here asks is whether a path written by that machine could mean anything on this one.
///
/// Absent from every song written before it existed, which reads back as unknown, and unknown has
/// to behave exactly as before: the paths are looked at, because that is what happened when every
/// one of those songs was last opened.
/// </remarks>
public interface IMachineWord
{
    /// <summary>What this machine is called, for a song being written now.</summary>
    string Here { get; }

    /// <summary>
    /// Whether a path written by a song from there could mean anything here.
    /// </summary>
    /// <param name="written">The word the song carries, or nothing when it does not carry one.</param>
    bool Travelled(string? written);
}
