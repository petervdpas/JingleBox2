namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Asks the operating system to schedule the calling thread as an audio thread.
/// </summary>
/// <remarks>
/// This application never asked, and that is most of why its buffer had to be so large. The
/// threads that must not be late, the one mixing ahead and the tracker's clock, were made with
/// <c>ThreadPriority.AboveNormal</c>, which on Linux is very nearly nothing: the runtime maps it
/// to a nice value and a nice value is a hint about how to share time between ordinary threads.
/// So the thread with a ten millisecond deadline was queued behind a browser laying out a page,
/// and the answer to being late was a bigger buffer.
///
/// Every serious audio application on this platform runs that thread under the real-time
/// scheduler instead, where it runs ahead of every ordinary thread on the machine whatever they
/// are doing. It costs nothing when there is nothing to do, because a thread that is asleep is
/// asleep at any priority.
///
/// **It is asked for and it may be refused, and a refusal is ordinary.** It needs permission the
/// system gives per user, and a machine that has not been set up for audio does not give it. So
/// this answers whether it got it rather than throwing, the caller carries on either way, and
/// what it got is written in the log, because "the buffer has to be enormous here" and "this
/// machine will not grant real-time scheduling" are the same fact and only one of them is
/// findable.
///
/// **A real-time thread that never sleeps holds a core against everything else**, so only a
/// thread that waits belongs here. Both of the ones that ask do: the mixer sleeps on a full
/// queue and the clock sleeps until its line is nearly due.
/// </remarks>
public interface IRealtimeThread
{
    /// <summary>
    /// Puts the calling thread on the real-time scheduler, if the system allows it.
    /// </summary>
    /// <remarks>
    /// Deliberately modest. Anything at all under this scheduler runs ahead of every ordinary
    /// thread, so there is nothing to win by asking for a high number and something real to lose:
    /// the sound server itself runs here, and a client that outranks the server it feeds is a
    /// client that can starve the thing it is talking to.
    /// </remarks>
    /// <returns>Whether it was granted.</returns>
    bool Take();

    /// <summary>
    /// Whether this platform has an answer for this at all.
    /// </summary>
    /// <remarks>
    /// Asked with the platform rather than reading it, the same rule the audio defaults keep: a
    /// machine running Linux can then be asked what Windows would have said, and the settings
    /// page can be checked on either. Windows has its own way of saying a thread is for audio and
    /// it is not written here yet, so the honest answer there is no, and a switch that cannot do
    /// anything should say so rather than sit there being ticked.
    /// </remarks>
    /// <param name="linux">True on Linux, false elsewhere.</param>
    bool PossibleOn(bool linux);

    /// <summary>Whether this machine has an answer for it.</summary>
    bool Possible { get; }

    /// <summary>What the calling thread is actually scheduled as, for the log to say.</summary>
    /// <remarks>
    /// Read back from the system rather than remembered from the request, because the request is
    /// the half that can be quietly ignored.
    /// </remarks>
    string Said();
}
