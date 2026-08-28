using System.Collections.Generic;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// The parts of holding a sliced recording that a kit and a map do identically.
/// </summary>
/// <remarks>
/// A kit and a map disagree about where a piece goes: one puts it on a fixed key, the other
/// hands it a stretch of keyboard. They agree about everything else, and this is that
/// everything else. Reading the cuts back off the pieces matters most, because it is what
/// keeps the cuts from being stored twice: the pieces are where they live, and there is no
/// second copy to fall out of step with them.
///
/// Whether two pieces name the same recording is a question about the disc rather than about
/// the strings, which is why this is handed an <see cref="JingleBox2.Files.Interfaces.IFilePaths"/>
/// rather than deciding for itself: a chop stops being a chop the moment two of its pieces are
/// spelled differently, and on Windows two spellings are one file.
/// </remarks>
public interface ISlices
{
    /// <summary>
    /// Where the recording was cut, read off the windows of the pieces. One more point than
    /// there are pieces: the first is where the sliced region starts, the last where it ends.
    /// </summary>
    /// <param name="windows">The window each piece plays, in the order the pieces are held.</param>
    IReadOnlyList<double> PointsFrom(IReadOnlyList<SampleShape?> windows);

    /// <summary>
    /// The one recording a set of pieces came from, or empty when they do not agree on one.
    /// </summary>
    /// <param name="paths">What each piece plays, in any order.</param>
    string OneFile(IEnumerable<string> paths);

    /// <summary>How many pieces those points describe, never more than there is room for.</summary>
    /// <param name="points">The cuts, which is one more than the number of pieces.</param>
    /// <param name="room">How many pieces the thing holding them has: sixteen pads, thirty-two zones.</param>
    int CountFor(IReadOnlyList<double>? points, int room);

    /// <summary>
    /// True when that name is one the app gave the piece rather than one somebody typed.
    /// </summary>
    /// <remarks>
    /// Either the recording's own name, or the recording's name and which piece of it this is,
    /// which is what a chop calls its pieces. Both are the app talking to itself, and both
    /// should be replaced when another take lands. Anything else is yours and is kept.
    ///
    /// A piece's name is the take's name, a space, and a number: "Countdown 3". That shape is
    /// what the tail is measured against, so a take called "Countdown" and a piece somebody
    /// renamed "Countdown intro" are told apart.
    /// </remarks>
    /// <param name="name">What the piece is called now.</param>
    /// <param name="wasCalled">The recording the piece came off.</param>
    bool Auto(string name, string wasCalled);

    /// <summary>What a piece is called: the recording's name and which piece of it this is.</summary>
    /// <param name="filePath">The recording being cut up.</param>
    /// <param name="index">Which piece, counted from nought and shown counted from one.</param>
    string NameFor(string filePath, int index);
}
