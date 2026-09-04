using System;
using System.Runtime.InteropServices;
using System.Threading;
using JingleBox2.Audio.Plugins.Bridge.Interfaces;

namespace JingleBox2.Audio.Plugins.Bridge;

/// <summary>
/// The Win32 message queue, drained on the thread a plugin's interface lives on.
/// </summary>
/// <remarks>
/// <inheritdoc cref="IWindowMessages" path="/remarks"/>
///
/// The wait is <c>MsgWaitForMultipleObjectsEx</c> rather than a wait on the knock followed by a
/// look at the queue. Those are not the same thing: between looking at an empty queue and going
/// back to waiting there is a window in which a message can arrive, and a thread that has just
/// gone to sleep on an event alone will not be woken by it. The plugin's interface would then
/// stop until something else happened to knock, which from a chair is an interface that answers
/// every other click.
///
/// <c>MWMO_INPUTAVAILABLE</c> is what closes the other half of that race. Without it the wait
/// answers only messages that arrive <em>while</em> it is waiting, so anything already on the
/// queue when the wait begins is slept straight past.
///
/// A message is dispatched and its result thrown away, which is what a pump does: the window it
/// belongs to is the plugin's, and what the plugin makes of it is the plugin's business.
/// <c>WM_QUIT</c> is dispatched like anything else rather than ending anything, because this
/// process stops when the parent says so and not when a plugin's toolkit decides it has had
/// enough.
/// </remarks>
public sealed class WindowsMessages : IWindowMessages
{
    /// <summary>The Win32 window library, by the name the loader knows it under.</summary>
    private const string Library = "user32.dll";

    /// <summary>A message, in the shape Win32 fills one in.</summary>
    /// <remarks>
    /// Written out in full because the layout has to match the C header. Nothing here is read:
    /// the message is taken off the queue and handed straight back, and only its size matters.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        /// <summary>The window it is for.</summary>
        public nint Window;

        /// <summary>Which message.</summary>
        public uint What;

        /// <summary>Its first parameter, whose meaning depends on the message.</summary>
        public nint First;

        /// <summary>Its second.</summary>
        public nint Second;

        /// <summary>When it was posted.</summary>
        public uint Time;

        /// <summary>Where the pointer was, in screen coordinates.</summary>
        public int X;

        /// <summary>And the other half of that.</summary>
        public int Y;
    }

    /// <summary>Takes a message off the queue if there is one, without waiting.</summary>
    [DllImport(Library)]
    private static extern int PeekMessageW(out Message message, nint window, uint first, uint last, uint remove);

    /// <summary>Turns key presses into the character messages a toolkit expects.</summary>
    [DllImport(Library)]
    private static extern int TranslateMessage(ref Message message);

    /// <summary>Hands a message to the window procedure it belongs to.</summary>
    [DllImport(Library)]
    private static extern nint DispatchMessageW(ref Message message);

    /// <summary>Waits for a handle, a message, or the time; whichever comes first.</summary>
    [DllImport(Library)]
    private static extern uint MsgWaitForMultipleObjectsEx(
        uint count, nint[] handles, uint milliseconds, uint wake, uint flags);

    /// <summary>Take the message off the queue rather than leaving it there.</summary>
    private const uint Remove = 1;

    /// <summary>Wake for anything at all: input, posted messages, sent messages, timers, paint.</summary>
    private const uint AllInput = 0x04FF;

    /// <summary>
    /// Answer at once for a message that was already waiting when the wait began, rather than
    /// only for one that arrives during it.
    /// </summary>
    private const uint InputAvailable = 0x0004;

    /// <summary>
    /// The most messages to dispatch in one turn, so a queue being filled faster than it drains
    /// cannot keep the bridge waiting for ever.
    /// </summary>
    /// <remarks>
    /// A plugin repainting itself under a drag makes messages continuously, and a pump with no
    /// bound would go round for as long as that lasted, which is the whole time somebody has hold
    /// of a knob. The parent would be told nothing for the length of the gesture. Coming back to
    /// the bridge after a few hundred and picking the rest up on the next turn costs nothing:
    /// the next turn is immediately, since a queue with anything on it answers at once.
    /// </remarks>
    private const int MostPerTurn = 512;

    /// <summary>The wait's one handle, kept rather than made per turn.</summary>
    /// <remarks>
    /// This runs sixty times a second and more while anybody is dragging anything, and the array
    /// is the only thing on the path that would otherwise be allocated. The handle in it is
    /// filled in on each wait, since the caller owns it and is entitled to hand over a different
    /// one.
    /// </remarks>
    private readonly nint[] _handles = new nint[1];

    /// <inheritdoc/>
    /// <remarks>
    /// The wait comes first and the draining second, which is the order that lets a turn cost
    /// nothing when nothing has happened. Both are done whatever the wait answered: the answer
    /// says what woke it and not what is waiting, and a knock and a message arriving together is
    /// the ordinary case rather than a corner of it.
    /// </remarks>
    public void Wait(WaitHandle knock, int milliseconds)
    {
        if (knock == null) return;

        _handles[0] = knock.SafeWaitHandle.DangerousGetHandle();

        MsgWaitForMultipleObjectsEx(
            1, _handles, (uint)Math.Max(0, milliseconds), AllInput, InputAvailable);

        Turn();
    }

    /// <summary>Dispatches everything waiting, up to <see cref="MostPerTurn"/>.</summary>
    /// <remarks>
    /// A plugin's window procedure is somebody else's code and is entitled to throw, and a throw
    /// out of here would end the pump and leave the interface dead while the plugin went on
    /// answering the bridge. It is written down and the turn ends; the next turn starts again.
    /// </remarks>
    private static void Turn()
    {
        try
        {
            for (int done = 0; done < MostPerTurn; done++)
            {
                if (PeekMessageW(out var message, 0, 0, 0, Remove) == 0) return;

                TranslateMessage(ref message);
                DispatchMessageW(ref message);
            }
        }
        catch (Exception error)
        {
            Diagnostics.Log.Fault(Diagnostics.Enums.LogArea.Plugins,
                "the plugin's window threw while being given a message", error);
        }
    }
}
