using Avalonia.Input;

namespace JingleBox2.Views.Interfaces;

/// <summary>
/// What a drag carries, put in the hand at one end and read out at the other.
/// </summary>
/// <remarks>
/// One place that knows the format and how to read it back, so the drag source and the drop
/// target cannot disagree. Two of these exist, one for a track and one for an instrument, and
/// they are separate formats rather than one with a flag on it so that a drop can tell them
/// apart by asking rather than by guessing from what happens to be under the pointer: dragging
/// an instrument onto a track points that track at it, and dragging a track moves the track.
///
/// In-process only. The payload never leaves the app, so there is no reason to expose a number
/// to other programs or to serialise it.
/// </remarks>
public interface IDragPayload
{
    /// <summary>What to put in the hand when a drag of that thing starts.</summary>
    /// <param name="index">Which one: a track's place in the song, or an instrument's in the list.</param>
    IDataTransfer For(int index);

    /// <summary>
    /// What a drag carries, or -1 when it carries something else.
    /// </summary>
    /// <remarks>
    /// Minus one rather than null, and it is the same answer for a drag of the other kind, a
    /// drag from another program, and no drag at all. A drop target has one question, "is this
    /// mine", and three ways of saying no would be three branches at every one of them.
    /// </remarks>
    /// <param name="transfer">Whatever is in the hand, or null.</param>
    int IndexFrom(IDataTransfer? transfer);
}
