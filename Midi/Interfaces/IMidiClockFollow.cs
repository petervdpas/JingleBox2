using System.Threading;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// Somebody else's clock, arriving, and the transport waiting on it.
/// </summary>
/// <remarks>
/// **Two threads meet here and they meet on purpose.** Clock bytes arrive on the port's own
/// thread and the transport waits on the thread that keeps time, so this is the one object
/// between them: the arriving side only ever counts, and the waiting side only ever reads. What
/// is shared is a count and a flag, which is a value rather than a shape, so a lock is the right
/// answer and there is nothing to swap whole.
///
/// **When the clock stops without saying so, the transport holds where it is.** That is the
/// decision this type exists to encode, and it costs no code at all: waiting for a tick that
/// never comes is holding. The pass stays on its line, whatever is sounding goes on sounding and
/// decays as it would, and when ticks start again it carries on from where it was. The
/// alternative — deciding after some length of silence that the master has gone and stopping —
/// would mean picking that length, and any length is wrong: a master paused for a bar and a
/// master unplugged look identical on the wire and only one of them wants the music to stop.
///
/// So the only things that stop a follower are a stop message and the transport being stopped
/// here. Silence is not one of them.
///
/// **Nothing here decides what a tick means.** How many of them make a line is
/// <see cref="IMidiClockGrid"/>'s, and it has to be, or the two ends of a sync would each have
/// their own idea of it.
/// </remarks>
public interface IMidiClockFollow
{
    /// <summary>True while this is the clock the transport is running on.</summary>
    /// <remarks>
    /// What the clock thread asks before it waits on anything, so a machine on its own clock
    /// takes the ordinary stopwatch path and pays one comparison a line.
    /// </remarks>
    bool IsFollowing { get; }

    /// <summary>How many ticks have arrived since the pass began.</summary>
    long Ticks { get; }

    /// <summary>Where the last position pointer said to be, in sixteenth notes.</summary>
    /// <remarks>
    /// Kept rather than acted on, because a pointer arrives before the continue that means it:
    /// the standard's order is the position and then the go, so this is read when the go turns
    /// up rather than when the pointer does.
    /// </remarks>
    int Pointer { get; }

    /// <summary>Says whether this is the clock to follow, and forgets whatever had arrived.</summary>
    /// <param name="following">True to follow, false to go back to the machine's own time.</param>
    void Follow(bool following);

    /// <summary>One tick arrived. Called from the port's thread.</summary>
    void Tick();

    /// <summary>
    /// A start arrived, which means from the top.
    /// </summary>
    /// <remarks>
    /// The count goes back to nought here rather than at the first tick, since a master sends go
    /// and then ticks and the first of those ticks is the first of the pass.
    /// </remarks>
    void Start();

    /// <summary>A continue arrived, which means from wherever the pointer last said.</summary>
    void Resume();

    /// <summary>A stop arrived.</summary>
    void Cease();

    /// <summary>A position pointer arrived, in sixteenth notes from the top of the song.</summary>
    /// <param name="pointer">Sixteenths from the top.</param>
    void Placed(int pointer);

    /// <summary>
    /// Waits until that many ticks have arrived, or the transport is stopped.
    /// </summary>
    /// <remarks>
    /// **This is where holding happens.** A clock that has gone quiet leaves this waiting, and
    /// waiting is exactly what holding the transport where it is looks like from the inside.
    /// It wakes now and then anyway rather than sleeping on a pulse alone, so a stop is answered
    /// promptly however it arrives.
    /// </remarks>
    /// <param name="until">The tick count to wait for.</param>
    /// <param name="token">Cancelled when the transport stops.</param>
    /// <returns>
    /// True when the count was reached. False when the transport was stopped, the master said
    /// stop, or this is no longer following, all of which mean the pass is over rather than late.
    /// </returns>
    bool WaitFor(double until, CancellationToken token);

    /// <summary>Raised when a start or a continue arrives, so the transport can begin.</summary>
    /// <remarks>
    /// Raised on the port's thread, since that is where the message landed. Whoever listens has
    /// to get itself back to wherever it needs to be.
    /// </remarks>
    event System.Action<bool>? Began;

    /// <summary>And when a stop does.</summary>
    /// <inheritdoc cref="Began" path="/remarks"/>
    event System.Action? Ended;
}
