namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Where a take lives before it has a name.
/// </summary>
/// <remarks>
/// **A take is not a file on the shelf until somebody says it is.** Pressing Record used to
/// write straight into the recordings folder under whatever name the box happened to hold, so
/// every false start, every level check and every accident was a row in the list somebody had to
/// go and delete, with a search box over it as though it were work worth finding again. What a
/// recorder wants there is a scratchpad: play it, listen to it, keep it under a name or throw it
/// away, and what is left when the application closes was never wanted.
///
/// So the audio really is written to disc, because a take has to be read back to be drawn and
/// played and trimmed, and the folder it is written to is this one. Nothing in it outlives the
/// run: <see cref="Sweep"/> empties it on the way in as well as on the way out, since a run that
/// ended badly leaves files here and they are by definition the ones nobody asked to keep.
///
/// It holds one take, which is the whole of what a scratchpad is: recording again is starting
/// again. A take carried across is the shelf's job, and getting there is <see cref="Keep"/>.
/// </remarks>
public interface ITakeScratch
{
    /// <summary>The folder unnamed takes are written to, made if it is not there.</summary>
    string Folder { get; }

    /// <summary>Empties the folder, leaving it there.</summary>
    /// <remarks>
    /// Answers rather than throwing on a file it cannot remove: a scratch file that is still
    /// open somewhere is a nuisance and not a reason to fail whatever asked.
    /// </remarks>
    void Sweep();

    /// <summary>
    /// Moves a take out of the scratchpad and onto the shelf under a name.
    /// </summary>
    /// <remarks>
    /// A move rather than a copy, which is the same reasoning the bin already keeps: a take is
    /// the one thing here that can be a hundred megabytes, and writing it twice to give it a
    /// name would be paying for the name in seconds.
    ///
    /// Nothing already on the shelf is written over. A name that is taken is refused rather than
    /// numbered, because by the time this is reached the name has been through the box's own
    /// check and a second answer here would be two rules disagreeing about one name.
    /// </remarks>
    /// <param name="from">The scratch file.</param>
    /// <param name="folder">Where the shelf keeps its takes.</param>
    /// <param name="name">What to call it, without the extension.</param>
    /// <returns>Where it landed, or null where there was nothing to move or the name was taken.</returns>
    string? Keep(string from, string folder, string name);

    /// <summary>Throws one scratch file away, and does nothing for one that is not there.</summary>
    /// <param name="path">The file to drop.</param>
    void Drop(string? path);
}
