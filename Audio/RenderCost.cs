using System;
using System.Globalization;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// Two numbers and two counts, added to on the audio thread and read on it as well, so there is
/// no lock: one thread renders at a time, which is the rule <see cref="Tracker.Synth.TrackMixer.Render"/>
/// already keeps, and a reading taken while the other one was halfway through would be off by one
/// block in a measurement about thousands.
///
/// The clock is the caller's. This is arithmetic over durations somebody else measured, which is
/// what lets the whole of it be put a question to with made-up numbers.
/// </remarks>
public sealed class RenderCost : IRenderCost
{
    /// <summary>How long a stretch is, in milliseconds, before a line is worth saying.</summary>
    private const long StretchMs = 5000;

    /// <summary>What counts as over budget: a block that took longer than it had.</summary>
    private const double OverBudget = 1.0;

    /// <summary>When the stretch began, on the clock the caller shares with everything else.</summary>
    private long _began = Environment.TickCount64;

    /// <summary>The fractions added up, for the mean.</summary>
    private double _total;

    /// <inheritdoc/>
    public double Worst { get; private set; }

    /// <inheritdoc/>
    public int Blocks { get; private set; }

    /// <summary>How many of them took longer than the block they were filling.</summary>
    private int _over;

    /// <summary>How long the runtime had spent collecting when the stretch began.</summary>
    private TimeSpan _pausedAt = GC.GetTotalPauseDuration();

    /// <summary>How many collections of each generation there had been when the stretch began.</summary>
    private readonly int[] _collectedAt = new int[Generations];

    /// <summary>How many generations there are to count, which is nought, one and two.</summary>
    private const int Generations = 3;

    /// <inheritdoc/>
    public void Fresh()
    {
        _began = Environment.TickCount64;
        _total = 0;
        Worst = 0;
        Blocks = 0;
        _over = 0;

        _pausedAt = GC.GetTotalPauseDuration();

        for (int generation = 0; generation < Generations; generation++)
            _collectedAt[generation] = GC.CollectionCount(generation);
    }

    /// <inheritdoc/>
    public string? Took(int frames, double milliseconds, int rate)
    {
        if (frames <= 0 || rate <= 0 || milliseconds < 0 || double.IsNaN(milliseconds)) return null;

        double had = frames * 1000.0 / rate;
        double part = milliseconds / had;

        Blocks++;
        _total += part;

        if (part > Worst) Worst = part;
        if (part > OverBudget) _over++;

        if (Environment.TickCount64 - _began < StretchMs) return null;

        string line = Said(frames, had);

        Fresh();

        return line;
    }

    /// <summary>
    /// The stretch in one sentence, in the words somebody would compare with another program.
    /// </summary>
    /// <remarks>
    /// Percentages rather than milliseconds, because the whole point is the ratio: three
    /// milliseconds is nothing in a block of eleven and a dropout in a block of two.
    /// </remarks>
    /// <param name="frames">How big the last block was, which is what the budget is worked out from.</param>
    /// <param name="had">How long that block had, in milliseconds.</param>
    private string Said(int frames, double had) =>
        "render: " + Blocks + " blocks of " + frames + " frames ("
        + had.ToString("0.0", CultureInfo.InvariantCulture) + " ms each), worst "
        + Percent(Worst) + " of the time it had, mean " + Percent(_total / Blocks)
        + (_over == 0 ? ", none over" : ", " + _over + " over") + "; " + Collecting();

    /// <summary>
    /// What the runtime spent collecting over the stretch, which is the other half of the answer.
    /// </summary>
    /// <remarks>
    /// **A block that is cheap on average and occasionally enormous is not slow code, and no
    /// amount of making the mixing faster would touch it.** That shape is a pause: the mixing
    /// thread was ready and was not running. So the collections are counted beside the blocks,
    /// because the two readings together say which of the two faults this machine has and they
    /// want opposite answers.
    ///
    /// The runtime's own totals, which are cumulative, so the stretch is the difference between
    /// two readings rather than anything counted per block. Read here and in
    /// <see cref="Fresh"/> only: this is not asked on the audio thread's way through.
    ///
    /// Paused is every thread that was stopped, not this one in particular, so it is an upper
    /// bound on what the mixing lost rather than a measurement of it. That is the right direction
    /// for the question being asked: no pauses at all rules the whole theory out.
    /// </remarks>
    private string Collecting()
    {
        double paused = (GC.GetTotalPauseDuration() - _pausedAt).TotalMilliseconds;

        int gen0 = GC.CollectionCount(0) - _collectedAt[0];
        int gen1 = GC.CollectionCount(1) - _collectedAt[1];
        int gen2 = GC.CollectionCount(2) - _collectedAt[2];

        return gen0 + gen1 + gen2 == 0
            ? "nothing was collected"
            : gen0 + "/" + gen1 + "/" + gen2 + " collections (gen 0/1/2), "
              + paused.ToString("0.0", CultureInfo.InvariantCulture) + " ms paused";
    }

    /// <summary>A fraction as a whole percentage, which is as fine as this is worth reading.</summary>
    /// <param name="part">The fraction.</param>
    private static string Percent(double part) =>
        Math.Round(part * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
}
