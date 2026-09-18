using System;
using System.Diagnostics;

namespace JingleBox2.Audio.Plugins.Bridge;

/// <summary>
/// When the mixing thread's cushion runs dry, which is how long it can afford to wait for a
/// plugin.
/// </summary>
/// <remarks>
/// A plugin runs in a process of its own, and now and then the system does not wake that process
/// for a tenth of a second or more. Opening a plugin's window is when it happens, and it is not
/// the plugin whose window is opening that is held up but whichever one the system passed over.
/// Waiting for it as long as it took held up the whole mix behind one plugin, the cushion ran
/// dry, and everything crackled at once.
///
/// So the mixing thread says, before each chunk, when the audio it has already made runs out,
/// and a plugin is waited for until a little before then. One that is later than that is played
/// as silence for that block, which is one instrument skipping for a moment rather than the whole
/// song dropping out. A bigger cushion is a later deadline, so on a machine with room to spare
/// this never happens at all.
///
/// Kept per thread, because the mixing thread is the only one with a cushion. A pad or an effect
/// played from anywhere else sees no deadline and waits the way it always did.
/// </remarks>
public static class MixDeadline
{
    /// <summary>
    /// What the rest of a chunk still needs after the plugins, taken off the deadline so the chunk
    /// is finished before the cushion runs out rather than at the moment it does.
    /// </summary>
    private const double MarginMs = 3;

    /// <summary>When the cushion runs dry, in stopwatch ticks, and nought while there is none.</summary>
    [ThreadStatic] private static long _dryAt;

    /// <summary>The rate the audio is made at, which turns a block's frames into time.</summary>
    [ThreadStatic] private static int _rate;

    /// <summary>Says when the audio already made runs out, for the chunk about to be mixed.</summary>
    /// <param name="dryAt">When that is, in stopwatch ticks.</param>
    /// <param name="rate">The rate the audio is made at.</param>
    public static void Set(long dryAt, int rate)
    {
        _dryAt = dryAt;
        _rate = rate;
    }

    /// <summary>Takes the deadline away, for a thread that stops mixing ahead.</summary>
    public static void Clear() => _dryAt = 0;

    /// <summary>
    /// When a block asked for at that moment has to be back by, or nought where there is no
    /// deadline on this thread.
    /// </summary>
    /// <remarks>
    /// Never earlier than the block's own length after it was asked for. A cushion nearly empty
    /// already would otherwise give up on a plugin working at its ordinary speed, and silencing
    /// an instrument that was on time is the one outcome worse than a late block.
    /// </remarks>
    /// <param name="asked">When the block was asked for, in stopwatch ticks.</param>
    /// <param name="frames">How many frames it carries.</param>
    public static long Due(long asked, int frames)
    {
        if (_dryAt == 0 || _rate <= 0) return 0;

        long margin = (long)(MarginMs * Stopwatch.Frequency / 1000);
        long own = (long)frames * Stopwatch.Frequency / _rate;

        return Math.Max(_dryAt - margin, asked + own);
    }
}
