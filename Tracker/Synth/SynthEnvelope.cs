using System;
using JingleBox2.Tracker.Synth.Enums;
using JingleBox2.Tracker.Synth.Interfaces;

namespace JingleBox2.Tracker.Synth;

/// <inheritdoc/>
public sealed class SynthEnvelope : ISynthEnvelope
{
    private readonly double _attackPerSample;
    private readonly double _decayPerSample;
    private readonly double _sustain;

    /// <summary>The patch's own release length, in samples, for a note off that asks for no other.</summary>
    private readonly double _releaseSamples;

    private readonly double _sampleRate;

    /// <summary>
    /// How much comes off the level per sample while releasing.
    /// </summary>
    /// <remarks>
    /// Worked out at the note off rather than up front, because a release starts from wherever
    /// the level happens to be: a key let go of during the attack has less to fall than one let
    /// go of at full, and both have to take the same time.
    /// </remarks>
    private double _releasePerSample;

    private double _level;

    /// <summary>
    /// Takes the four times off a patch and turns them into per sample steps.
    /// </summary>
    /// <remarks>
    /// A zero length stage is a jump, not a division by zero: an attack of no length arrives at
    /// full on the first sample, which is what a drum patch asks for.
    /// </remarks>
    /// <param name="patch">The instrument whose four times the envelope is built from.</param>
    /// <param name="sampleRate">The rate the voice is rendered at, which the times are turned into steps against.</param>
    public SynthEnvelope(SynthPatch patch, int sampleRate)
    {
        double rate = sampleRate <= 0 ? 1 : sampleRate;

        double attackSamples = patch.AttackMs / 1000.0 * rate;
        double decaySamples = patch.DecayMs / 1000.0 * rate;

        _sampleRate = rate;
        _sustain = Math.Clamp(patch.Sustain, 0.0, 1.0);
        _releaseSamples = patch.ReleaseMs / 1000.0 * rate;

        _attackPerSample = attackSamples > 0 ? 1.0 / attackSamples : 1.0;
        _decayPerSample = decaySamples > 0 ? (1.0 - _sustain) / decaySamples : 1.0;

        Stage = EnvelopeStage.Attack;
    }

    /// <inheritdoc/>
    public EnvelopeStage Stage { get; private set; }

    /// <inheritdoc/>
    public bool IsFinished => Stage == EnvelopeStage.Finished;

    /// <inheritdoc/>
    public double Level => _level;

    /// <inheritdoc/>
    public double Next()
    {
        switch (Stage)
        {
            case EnvelopeStage.Attack:
                _level += _attackPerSample;
                if (_level >= 1.0)
                {
                    _level = 1.0;
                    Stage = EnvelopeStage.Decay;
                }
                break;

            case EnvelopeStage.Decay:
                _level -= _decayPerSample;
                if (_level <= _sustain)
                {
                    _level = _sustain;
                    Stage = _sustain > 0 ? EnvelopeStage.Sustain : EnvelopeStage.Finished;
                }
                break;

            case EnvelopeStage.Sustain:
                _level = _sustain;
                break;

            case EnvelopeStage.Release:
                _level -= _releasePerSample;
                if (_level <= 0)
                {
                    _level = 0;
                    Stage = EnvelopeStage.Finished;
                }
                break;

            case EnvelopeStage.Finished:
                _level = 0;
                break;
        }

        return _level;
    }

    /// <inheritdoc/>
    public void NoteOff(double? releaseSeconds = null)
    {
        if (Stage == EnvelopeStage.Release || Stage == EnvelopeStage.Finished) return;

        double samples = releaseSeconds is null
            ? _releaseSamples
            : releaseSeconds.Value * _sampleRate;

        if (samples <= 0)
        {
            _level = 0;
            Stage = EnvelopeStage.Finished;
            return;
        }

        _releasePerSample = _level / samples;
        Stage = EnvelopeStage.Release;
    }

    /// <inheritdoc/>
    public void Kill()
    {
        _level = 0;
        Stage = EnvelopeStage.Finished;
    }
}
