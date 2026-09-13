using System.Collections.Generic;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// Cuts the drums out of a recording into files of their own: one of each drum it hears.
/// </summary>
/// <remarks>
/// **Really cut, into files.** A kit that plays windows of one long recording is still that
/// recording: it cannot be handed on without the whole of it, and every pad is a promise about
/// where in it a hit was. A drum in a file of its own is a sample like any other, playable from
/// its first frame, and a kit of them is a kit of samples.
///
/// What is cut is what <see cref="IDrumListener"/> heard and chose, from just before the hit to
/// where it has died away or the next hit lands. Each file is faded in over its first
/// millisecond and out over its last few, so a cut made in the middle of whatever else was
/// ringing does not click.
///
/// **Nothing is ever overwritten.** The files go in a folder named for the recording, and a
/// recording chopped a second time gets a folder of its own beside the first: a song already
/// playing the first chop goes on sounding as it did.
/// </remarks>
public interface IDrumChopper
{
    /// <summary>
    /// The folder a chop of that recording goes in: named for it under the chops folder, with a
    /// number after it where that name is taken.
    /// </summary>
    /// <param name="recording">The recording being chopped.</param>
    /// <param name="chops">The folder chops are kept under.</param>
    string FolderFor(string recording, string chops);

    /// <summary>
    /// Cuts one of each drum in the recording into its own file in that folder, in the order a kit
    /// is laid out, at most that many.
    /// </summary>
    /// <remarks>
    /// Nothing comes back, and nothing is written, for a recording that cannot be read or has no
    /// hits in it.
    /// </remarks>
    /// <param name="recording">The recording to chop.</param>
    /// <param name="folder">Where the files go, made if it is not there.</param>
    /// <param name="pads">How many drums at most.</param>
    IReadOnlyList<ChoppedDrum> Chop(string recording, string folder, int pads);
}
