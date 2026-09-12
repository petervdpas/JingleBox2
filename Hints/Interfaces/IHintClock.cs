using System;

namespace JingleBox2.Hints.Interfaces;

/// <summary>
/// The one clock every hint in the application is driven by.
/// </summary>
/// <remarks>
/// **One rather than one per place, which is the whole point of it.** Five timers were doing this
/// before, each started and stopped by hand at its own rate, and a sixth would have been written
/// the next time somebody needed the same thing. What they share is not the rate, it is the rule:
/// gather what is said, answer once it settles, and answer anyway if the saying never stops.
///
/// **An answer runs on this clock's own thread and never on the drawing thread.** A caller whose
/// work belongs on the drawing thread says so itself, in the one line that gets it there, which
/// is better than this deciding for everybody: writing a settings file has no business on the
/// thread drawing a window, and reading a plugin chain's patches has no business anywhere else.
///
/// Anything thrown by an answer is written down and costs that answer. This runs unattended with
/// nobody watching, and a hint is never the thing an application should be lost over.
/// </remarks>
public interface IHintClock : IDisposable
{
    /// <summary>
    /// Makes a hint: what to do, how long the saying has to stop for, and how long it may be put
    /// off in all.
    /// </summary>
    /// <param name="name">What it is about, for a log line when the answer throws.</param>
    /// <param name="settles">
    /// How long since the last <see cref="IHint.Moved"/> before the answer happens. Long enough
    /// to outlast a gesture and short enough that letting go and closing the application is not a
    /// race.
    /// </param>
    /// <param name="atMost">
    /// How long an owed answer may be held back in all, however long the saying goes on. Without
    /// it a hand resting on a fader for a minute holds the work back for a minute.
    /// </param>
    /// <param name="answer">The work, run on this clock's thread.</param>
    IHint Gathered(string name, TimeSpan settles, TimeSpan atMost, Action answer);

    /// <summary>
    /// Work that happens every so often whether or not anybody says anything moved.
    /// </summary>
    /// <remarks>
    /// **The net under the saying, for work that can tell for itself whether it is needed.** The
    /// settings file is the one of these: it compares what it would write with what it wrote, so
    /// a round where nothing moved costs no disc at all, and a change nobody remembered to
    /// mention is written anyway. Deferred work that cannot tell has no business here and should
    /// be said rather than looked for.
    ///
    /// Slower than a gathered hint by a long way, since nothing is waiting on it.
    /// </remarks>
    /// <param name="name">What it is about, for a log line when the answer throws.</param>
    /// <param name="every">How long between answers.</param>
    /// <param name="answer">The work, run on this clock's thread.</param>
    IHint Often(string name, TimeSpan every, Action answer);
}
