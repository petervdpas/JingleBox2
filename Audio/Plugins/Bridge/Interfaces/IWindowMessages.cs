using System.Threading;

namespace JingleBox2.Audio.Plugins.Bridge.Interfaces;

/// <summary>
/// The window server's own errands, on the thread a plugin's interface lives on.
/// </summary>
/// <remarks>
/// A plugin's process has one thread the plugin can see, and that thread has two masters. The
/// bridge knocks on it when the parent has asked for something, and the window server knocks on
/// it when somebody has clicked, dragged or uncovered the plugin's interface. Waiting on only the
/// first is what makes a plugin's window a picture that never draws.
///
/// The two platforms differ here and only here, which is why this is a seam rather than a branch.
/// Windows gives every thread a message queue, and a window whose queue nobody drains gets no
/// paint, no timer and no mouse: the pump is compulsory and it is what this delivers. X11 has no
/// per-thread queue at all, so a plugin hands the host a run loop's worth of timers and file
/// descriptors instead, and <see cref="JingleBox2.Audio.Plugins.PluginRunLoop"/> is the thing
/// that answers it. There is nothing left for this to do there but wait.
///
/// This is the half that was missing on Windows. Nothing in the application drained the child
/// process's queue, and <c>PluginRunLoop</c>'s own remarks said Windows "has a message pump
/// already running before the plugin arrives", which is true of a window's process and false of
/// a plugin's: a plugin host process never builds a toolkit, so nothing was ever going to start
/// one for it.
/// </remarks>
public interface IWindowMessages
{
    /// <summary>
    /// Waits until there is something to do, doing whatever the window server wants doing.
    /// </summary>
    /// <remarks>
    /// Comes back on any of three things: the knock, a message worth dispatching, or the time
    /// running out. The caller loops, so coming back early costs a turn and nothing else, and
    /// coming back for a message that turned out to be nothing is the ordinary case.
    ///
    /// Everything waiting is dispatched before this returns, not one message a turn: a drag
    /// makes messages faster than a caller with other work to do comes round, and leaving them
    /// on the queue is how an interface falls behind the mouse.
    /// </remarks>
    /// <param name="knock">Set when the bridge has something for the plugin's thread.</param>
    /// <param name="milliseconds">The longest to wait when nothing at all happens.</param>
    void Wait(WaitHandle knock, int milliseconds);
}
