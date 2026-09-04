using System.Threading;
using JingleBox2.Audio.Plugins.Bridge.Interfaces;

namespace JingleBox2.Audio.Plugins.Bridge;

/// <summary>
/// The wait on a platform whose windows do not arrive as messages on a thread.
/// </summary>
/// <remarks>
/// <inheritdoc cref="IWindowMessages" path="/remarks"/>
///
/// X11 is that platform. A toolkit drawing into an X11 window reads its own connection rather
/// than a queue the system keeps for the thread, so there is no queue here to drain: what the
/// plugin needs instead is somebody to come back to its timers and its file descriptors, and
/// <see cref="JingleBox2.Audio.Plugins.PluginRunLoop"/> is already that somebody. So this waits
/// for the knock and nothing else, which is exactly what the pump did before there was a seam
/// here at all.
/// </remarks>
public sealed class NoWindowMessages : IWindowMessages
{
    /// <inheritdoc/>
    public void Wait(WaitHandle knock, int milliseconds) => knock?.WaitOne(milliseconds);
}
