namespace JingleBox2.Audio.Plugins.Interfaces;

/// <summary>
/// Audio work that can be started, left in flight, and come back to.
/// </summary>
/// <remarks>
/// **What a crossing to a plugin's own process costs is waking that process, not carrying the
/// audio.** Measured here at 0.177 milliseconds against 0.008 for the socket itself: the block
/// crosses in shared memory and the message is eight bytes, so almost the whole of it is a
/// thread that has been asleep for the length of a block being woken again.
///
/// Which means the cost is paid once per plugin per block **in series**, because the mixer asks
/// each plugin for its block and waits for it before asking the next. Five plugins at a 512 frame
/// block is 0.89 milliseconds of every 11.6 spent getting there and back, before any plugin does
/// any work, and it grows with the number of plugins rather than with the music.
///
/// So this is the shape that lets them be woken at the same time instead. Everything that has
/// work outstanding is begun, and only then is any of it waited for, so several processes wake at
/// once and the mixer pays one wakeup rather than one each.
///
/// **What may not be overlapped is what depends on what.** A chain is audio flowing through boxes
/// in order, so the second box cannot start until the first has finished; two tracks' chains are
/// independent and can be in flight together. That is why this is driven in rounds rather than
/// started once: each round collects what was outstanding and asks for whatever comes next, so
/// the parallel width is the number of independent runs and never more.
///
/// Not one sample changes. The same plugins are handed the same audio in the same order within
/// each run; what changes is when the asking happens.
/// </remarks>
public interface IOverlappable
{
    /// <summary>
    /// Starts the work and comes back without waiting for it.
    /// </summary>
    /// <remarks>
    /// Anything that needs no waiting is simply done here, which is what an effect of ours in a
    /// chain is: it runs in this process and there is nothing to be in flight.
    /// </remarks>
    /// <param name="buffer">The audio, which must not be touched again until this run is done.</param>
    /// <param name="frames">How many frames are in it.</param>
    /// <returns>
    /// Whether something is now in flight. False means the work is finished and
    /// <see cref="Advance"/> must not be called, which is also the answer when this cannot be
    /// done in parts at all: the caller does the ordinary blocking thing instead.
    /// </returns>
    bool Begin(float[] buffer, int frames);

    /// <summary>
    /// Waits for what is in flight, then starts whatever comes after it.
    /// </summary>
    /// <remarks>
    /// Called only after <see cref="Begin"/> answered true, and then for as long as this keeps
    /// answering true. A run that has gone wrong answers false and leaves nothing outstanding, so
    /// a caller is never left waiting for something that will not come.
    /// </remarks>
    /// <param name="buffer">The same audio the run was begun on.</param>
    /// <param name="frames">The same frame count.</param>
    /// <returns>Whether something is still in flight after this.</returns>
    bool Advance(float[] buffer, int frames);
}
