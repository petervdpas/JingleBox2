using System;

namespace JingleBox2.Diagnostics.Interfaces;

/// <summary>
/// Holds the terminal quiet while something underneath is expected to complain.
/// </summary>
/// <remarks>
/// **A library written in C writes to the terminal itself, and nothing in this application is in
/// the way of it.** The output devices are opened one at a time before any is offered, because
/// the only way to know whether a device can be had is to have it, and the ones that cannot say
/// so at the top of their voice on the way past: the sound layer's mixing plugin, a server that
/// is not running, a device file that does not exist. Somebody starting the application from a
/// terminal sees half a dozen lines of alarm about things that are working exactly as intended.
///
/// So the stream is pointed at nothing for the length of that pass and put back afterwards. It is
/// the whole stream rather than a filter on the words, since the words belong to three different
/// libraries and are not ours to keep up with.
///
/// **Narrow on purpose.** What is silenced is a stretch where every complaint is expected, and
/// anything this application has to say about the same stretch is in its own log, which is not
/// the terminal. A failure outside it still speaks.
/// </remarks>
public interface ITerminalHush
{
    /// <summary>
    /// Quiets it until the answer is let go of.
    /// </summary>
    /// <remarks>
    /// Something to let go of rather than a pair of calls, so the stream is put back however the
    /// work in between ends. Where there is nothing to quiet, or it could not be done, what comes
    /// back is something that does nothing: a caller never has to ask whether it worked.
    /// </remarks>
    IDisposable Hushed();
}
