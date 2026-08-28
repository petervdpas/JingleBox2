using System.Collections.Generic;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// Where a recording gets cut into pieces. Nothing here touches the file: a cut is a position
/// in the sound, and the sound stays whole.
/// </summary>
/// <remarks>
/// Slice points are fractions of the recording, and there is always one more of them than there
/// are slices, because the two ends are points as well. That is what makes them editable: the
/// first point is where the sliced region begins, the last is where it ends, and every point in
/// between is a boundary two slices share. Move one and two slices change. Take one away and
/// the two either side become one.
///
/// Which is also why the head is a point rather than a pinned zero. A take almost always opens
/// with a moment of nothing before the first hit, and that silence has to belong to no slice
/// rather than to the first one.
///
/// It knows nothing about what takes the pieces. A kit has sixteen pads and a map thirty-two
/// zones, and both clamp again on the way in.
/// </remarks>
public interface ISampleSlicer
{
    /// <summary>
    /// The most pieces this will ever cut a recording into. Whoever takes the pieces holds
    /// fewer than this and clamps again: a kit has sixteen pads, a map thirty-two zones.
    /// </summary>
    int MaxSlices { get; }

    /// <summary>
    /// The shortest a slice is allowed to be. A drum hit is several rising moments, not one,
    /// and without a floor under the spacing the loudest of them are all found separately.
    /// </summary>
    double MinSliceSeconds { get; }

    /// <summary>Evenly spaced points, for a loop with nothing in it to find.</summary>
    /// <param name="slices">How many pieces to cut it into.</param>
    List<double> Even(int slices);

    /// <summary>
    /// Points at the attacks, up to the number asked for, falling back to an even division when
    /// the recording has too few to work with.
    /// </summary>
    /// <param name="peaks">Loudest moment per bucket across the whole recording, 0 to 1.</param>
    /// <param name="lengthSeconds">How long the recording is, for the spacing rules.</param>
    /// <param name="slices">How many pieces to aim for. Fewer come back when fewer are there.</param>
    /// <remarks>
    /// Each loud moment is walked back to where its sound began before the spacing rule is
    /// applied rather than after, so two loud moments inside one hit fall back onto the same
    /// start and the second is then dropped for being on top of the first.
    ///
    /// One attack is not a slicing, it is a recording, so fewer than two falls back to an even
    /// division: that says more than a single cut in an arbitrary place does.
    /// </remarks>
    List<double> Transients(IReadOnlyList<float>? peaks, double lengthSeconds, int slices);

    /// <summary>
    /// Points at the silences, for a recording that is things separated by nothing rather than
    /// things struck.
    /// </summary>
    /// <remarks>
    /// What a spoken take needs, and what looking for attacks cannot give it. A word is several
    /// attacks, one to a syllable, and the quietest moment inside a word is louder than the pause
    /// after it, so a detector that ranks by loudness cuts up the words and runs the pauses
    /// together. Ten spoken numbers have nine silences in them and no reliable attacks at all.
    ///
    /// The cut goes at the end of each silence rather than the start, because what is being found
    /// is where the next thing begins.
    ///
    /// A silence at the very front is the lead-in and one at the very back is what is left after
    /// the last thing. Neither divides one piece from another, but both say where the sliced
    /// region begins and ends, which is why the head and the tail are read off them and the rest
    /// are looked for in between.
    ///
    /// Candidates are scored by what starts after the gap rather than by how long the gap is. A
    /// word decaying away can be quiet for longer than the pause before the next word, so length
    /// picks the middle of a word over the space between two; what marks a gap as real is that
    /// something loud begins on the other side of it. They are then kept apart for the same
    /// reason the attacks are, since two silences a moment apart both hear the same word start
    /// after them and both would be taken.
    /// </remarks>
    /// <param name="peaks">Loudest moment per bucket across the whole recording, 0 to 1.</param>
    /// <param name="lengthSeconds">How long the recording is, for the spacing rules.</param>
    /// <param name="slices">How many pieces to aim for. Fewer come back when fewer are there.</param>
    List<double> Gaps(IReadOnlyList<float>? peaks, double lengthSeconds, int slices);

    /// <summary>
    /// Puts a hand-edited list back in order: inside the recording, rising, and never so close
    /// together that a slice has nothing in it.
    /// </summary>
    /// <remarks>
    /// Everything landing in one place leaves no slicing to describe, so the whole recording
    /// comes back as one piece rather than as a list nobody can draw.
    /// </remarks>
    /// <param name="points">The cuts as somebody left them, in any order.</param>
    /// <param name="lengthSeconds">How long the recording is, which is what the spacing is measured in.</param>
    List<double> Clean(IEnumerable<double> points, double lengthSeconds);
}
