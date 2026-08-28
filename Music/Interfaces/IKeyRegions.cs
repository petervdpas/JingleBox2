using System.Collections.Generic;

namespace JingleBox2.Music.Interfaces;

/// <summary>
/// Sharing a stretch of keyboard out among a number of things that each want a piece of it.
/// </summary>
/// <remarks>
/// Two places want this and would otherwise each have their own arithmetic: dropping a handful
/// of recordings on a map and laying them out, and cutting one recording into slices. They are
/// the same question, and it has one right answer: contiguous stretches, none empty, no two
/// differing by more than a key, and the last one reaching the top so nothing above it is
/// silent.
/// </remarks>
public interface IKeyRegions
{
    /// <summary>The keys of a piano, A0 to C8. What a sliced recording is laid across.</summary>
    int PianoLow { get; }

    /// <summary>The top of that same stretch.</summary>
    int PianoHigh { get; }

    /// <summary>The whole of what a note column can say.</summary>
    int LowestKey { get; }

    /// <summary>And the top of that.</summary>
    int HighestKey { get; }

    /// <summary>
    /// One stretch per piece, in order, covering everything from <paramref name="low"/> to
    /// <paramref name="high"/> with no gaps between them.
    /// </summary>
    /// <remarks>
    /// The last stretch takes whatever the division left over rather than stopping short, so
    /// the top of the range is always reached. Stopping short would leave a handful of keys at
    /// the top answering to nothing, which reads as a broken instrument rather than as
    /// arithmetic.
    /// </remarks>
    /// <param name="low">The bottom of the stretch to share out.</param>
    /// <param name="high">The top of it.</param>
    /// <param name="count">How many pieces want a share. Nought or fewer is no pieces at all.</param>
    IReadOnlyList<(int Low, int High)> Split(int low, int high, int count);

    /// <summary>The key in the middle of a stretch, which is where a piece of it is rooted.</summary>
    /// <remarks>
    /// Rooting a zone in its own middle keeps the worst transposition down to half its width
    /// either way, which is the best any even split can do.
    /// </remarks>
    /// <param name="low">The bottom of the stretch.</param>
    /// <param name="high">The top of it.</param>
    int Middle(int low, int high);
}
