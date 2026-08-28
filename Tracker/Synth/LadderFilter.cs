using JingleBox2.Tracker.Synth.Enums;
using JingleBox2.Tracker.Synth.Interfaces;

namespace JingleBox2.Tracker.Synth;

/// <inheritdoc/>
public sealed class LadderFilter : ILadderFilter
{
    private readonly SweepFilter _first;
    private readonly SweepFilter _second;

    /// <summary>Both stages start wide open, so a voice is never born filtered by accident.</summary>
    /// <param name="sampleRate">The rate the voice is rendered at, which both stages are built for.</param>
    public LadderFilter(int sampleRate)
    {
        _first = new SweepFilter(sampleRate);
        _second = new SweepFilter(sampleRate);
    }

    /// <inheritdoc/>
    public void Set(double cutoffHz, double resonance)
    {
        _first.Set(cutoffHz, resonance);
        _second.Set(cutoffHz, 0);
    }

    /// <inheritdoc/>
    public double Process(double input) =>
        _second.Process(_first.Process(input, FilterMode.LowPass), FilterMode.LowPass);

    /// <inheritdoc/>
    public void Reset()
    {
        _first.Reset();
        _second.Reset();
    }
}
