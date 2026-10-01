using System;
using System.Collections.Generic;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// The buffer follows the sound server and not real time: a 512 frame buffer at 44.1 kHz is
/// 11.6 ms, and with PipeWire moving 1024 frames at 48 kHz, a cycle of 21.3 ms, it was measured
/// running dry, the sound card taking a twentieth less audio than it played. Two cycles is 2048
/// frames there, which is what played clean. What real time does make smaller is the cushion, 40 ms
/// against 120, since that is the mixing's own lead and the mixing is what runs in real time.
/// </remarks>
public sealed class AudioChoices : IAudioChoices
{
    /// <summary>The buffer sizes everybody is offered.</summary>
    private static readonly int[] Buffers = { 64, 128, 256, 512, 1024, 2048, 4096, 8192 };

    /// <summary>The buffer sizes offered with real time: one smaller on top.</summary>
    private static readonly int[] RealtimeBuffers = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };

    /// <summary>The top-ups everybody is offered.</summary>
    private static readonly int[] Periods = { 5, 10, 20 };

    /// <summary>The top-ups offered with real time: two quicker on top.</summary>
    private static readonly int[] RealtimePeriods = { 1, 2, 5, 10, 20 };

    /// <summary>The cushions everybody is offered.</summary>
    private static readonly int[] Aheads = { 0, 10, 20, 40, 80, 120, 160, 200 };

    /// <summary>The cushions offered with real time: one smaller on top.</summary>
    private static readonly int[] RealtimeAheads = { 0, 5, 10, 20, 40, 80, 120, 160, 200 };

    /// <inheritdoc/>
    public IReadOnlyList<int> BufferFrames(bool realtime) => realtime ? RealtimeBuffers : Buffers;

    /// <inheritdoc/>
    public IReadOnlyList<int> UpdatePeriods(bool realtime) => realtime ? RealtimePeriods : Periods;

    /// <inheritdoc/>
    public IReadOnlyList<int> Cushions(bool realtime) => realtime ? RealtimeAheads : Aheads;

    /// <summary>What is recommended where the sound server could not be asked.</summary>
    private const int UnknownBuffer = 2048;

    /// <summary>How many of the server's cycles the buffer has to hold.</summary>
    private const int Cycles = 2;

    /// <summary>The rates offered.</summary>
    private static readonly int[] Offered = { 44100, 48000, 96000 };

    /// <summary>What is recommended where the server's rate is not offered or not known.</summary>
    private const int UnknownRate = 44100;

    /// <inheritdoc/>
    public AudioSizes Recommended(bool realtime, ServerClock? clock, int rate) =>
        new(BufferFor(clock, rate), 10, 1);

    /// <summary>The smallest buffer offered that holds two of the server's cycles at this rate.</summary>
    private static int BufferFor(ServerClock? clock, int rate)
    {
        if (clock is not { Rate: > 0, Quantum: > 0 } cycle || rate <= 0) return UnknownBuffer;

        double needed = Cycles * (double)cycle.Quantum * rate / cycle.Rate;

        foreach (int frames in Buffers)
        {
            if (frames >= needed - 0.5) return frames;
        }

        return Buffers[^1];
    }

    /// <inheritdoc/>
    public IReadOnlyList<int> Rates => Offered;

    /// <inheritdoc/>
    public int RecommendedRate(ServerClock? clock) =>
        clock is { } cycle && Array.IndexOf(Offered, cycle.Rate) >= 0 ? cycle.Rate : UnknownRate;

    /// <inheritdoc/>
    public int RecommendedCushion(bool realtime) => realtime ? 40 : 120;
}
