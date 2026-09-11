using System;
using JingleBox2.Audio.Enums;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class TakeSteps : ITakeSteps
{
    /// <summary>What actually reads and writes the take.</summary>
    private readonly IWaveformService _waveforms;

    /// <summary>Names what does the work.</summary>
    /// <param name="waveforms">The take editing, defaulted to the real one.</param>
    public TakeSteps(IWaveformService? waveforms = null) => _waveforms = waveforms ?? new WaveformService();

    /// <inheritdoc/>
    /// <remarks>
    /// A normalize is the whole take by its nature, so the step's region says nothing there and
    /// is not consulted: what it carries instead is the peak it was asked for, which is why a
    /// peak changed in the box afterwards does not rewrite what an old step did.
    /// </remarks>
    public bool Run(TakeStep step, string path)
    {
        switch (step.Kind)
        {
            case TakeEditKind.Trim:
                _waveforms.TrimFile(path, step.From, step.To);
                return true;

            case TakeEditKind.Silence:
                _waveforms.SilenceFile(path, step.From, step.To);
                return true;

            case TakeEditKind.Reverse:
                _waveforms.ReverseFile(path, step.From, step.To);
                return true;

            case TakeEditKind.FadeIn:
                _waveforms.FadeFile(path, step.From, step.To, rising: true);
                return true;

            case TakeEditKind.FadeOut:
                _waveforms.FadeFile(path, step.From, step.To, rising: false);
                return true;

            case TakeEditKind.Normalize:
                return Math.Abs(_waveforms.NormalizeFile(path, step.Peak)) >= 0.001;

            default:
                return false;
        }
    }
}
