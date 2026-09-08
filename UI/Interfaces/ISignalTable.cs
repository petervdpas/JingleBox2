using System;
using JingleBox2.UI.Records;

namespace JingleBox2.UI.Interfaces;

/// <summary>
/// What every point on the routing table is carrying, read once and told to whoever draws it.
/// </summary>
/// <remarks>
/// **There is one routing table and every meter is a listener on a point of it.** Before this
/// there were as many readings as there were meters, each measuring wherever its own page
/// happened to reach, so two views of one block could disagree on screen. That is the fault this
/// codebase keeps naming, two spellings of one fact, and the answer to it is always the same: one
/// place says it.
///
/// **Told rather than asked**, which is the other half. A meter that pulled would only move while
/// its own page's clock was running, and the clocks here are not the same clock.
///
/// Only what somebody is looking at is measured. A point with no listener is not read, so a page
/// nobody has opened costs nothing, and the page in front costs one reading per point on it
/// however many meters are drawn from it.
///
/// The drawing thread's, all of it: the readings end up in properties a window is bound to.
/// </remarks>
public interface ISignalTable
{
    /// <summary>What that point was carrying when it was last read.</summary>
    /// <remarks>
    /// Silence and unknown for a point nothing has read yet, which is the honest answer: nobody
    /// has looked. A caller that wants a number now watches the point instead, since watching is
    /// what makes it read at all.
    /// </remarks>
    /// <param name="point">The place on the table.</param>
    PatchLevel At(SignalPoint point);

    /// <summary>Asks to be told what one point is carrying, from the next reading on.</summary>
    /// <remarks>
    /// Told only where the reading moved, so a still page stays still and a meter already at
    /// nought is not written to twenty times a second.
    ///
    /// **What comes back is how the watching stops.** A listener that reaches for whatever it is
    /// writing into when it is told needs no stopping at all, which is what the mixer's own do,
    /// since the strips are rebuilt whenever the mix moves and anything holding one would be
    /// holding the last. One that belongs to something that comes and goes lets go of what it had.
    /// </remarks>
    /// <param name="point">The place on the table to follow.</param>
    /// <param name="told">Given the new reading, on the drawing thread.</param>
    /// <returns>What to let go of when the listener is done.</returns>
    IDisposable Watch(SignalPoint point, Action<PatchLevel> told);

    /// <summary>
    /// Takes one reading of every point somebody is listening to, and tells them where it moved.
    /// </summary>
    /// <remarks>
    /// Called by whichever clock is running. Reading twice in one moment is harmless, since a
    /// reading is a question rather than a change, and it is what lets two clocks be independent.
    /// </remarks>
    void Read();
}
