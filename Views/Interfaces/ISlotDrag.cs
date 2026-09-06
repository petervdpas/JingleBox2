using Avalonia.Input;

namespace JingleBox2.Views.Interfaces;

/// <summary>
/// A device being dragged along the chain it is already on.
/// </summary>
/// <remarks>
/// The same idea as <see cref="IDragPayload"/> with one thing added, and the thing is what makes
/// it a separate contract rather than a third format beside the other two: **a place in a chain
/// means nothing without the chain it is a place in**. The same strip is drawn over a track's
/// chain, the master's, a pad's and the recording input's, so "slot 1" is four different devices
/// on four different chains, and two strips can be on the screen at once. Dropped onto the wrong
/// one, a number alone would reorder a chain nobody was dragging.
///
/// So the chain travels with the number and a drop asks whether what is in the hand is its own.
/// It is refused rather than carried across, since moving a device from one chain to another
/// would mean loading a plugin somewhere else, which is what the plus is for.
///
/// In-process only. The payload never leaves the application, so there is nothing to serialise
/// and no reason to show another program which effect is being moved.
/// </remarks>
public interface ISlotDrag
{
    /// <summary>What to put in the hand when a device is picked up.</summary>
    /// <param name="chain">The chain it is on, which is what a drop compares against.</param>
    /// <param name="index">Where it sits on that chain now, before anything is moved.</param>
    IDataTransfer For(object chain, int index);

    /// <summary>
    /// Where the device in the hand came from, or -1 when the hand holds something else.
    /// </summary>
    /// <remarks>
    /// Minus one is the same answer for a drag of another kind, a drag from another program, a
    /// drag from a different chain, and no drag at all, for the reason <see cref="IDragPayload"/>
    /// gives: a drop has one question, and four ways of saying no would be four branches at
    /// every one of them.
    /// </remarks>
    /// <param name="transfer">Whatever is in the hand, or null.</param>
    /// <param name="chain">The chain asking, which has to be the one it was picked up from.</param>
    int IndexFrom(IDataTransfer? transfer, object chain);
}
