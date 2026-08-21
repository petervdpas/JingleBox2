using System;

namespace JingleBox2.Tracker.Synth;

public enum EnvelopeStage
{
    Attack,
    Decay,
    Sustain,
    Release,
    Finished
}

/// <summary>
/// A per sample ADSR. Linear segments: at these timescales the shape matters far less than
/// getting the lengths right, and linear keeps the stage boundaries exact.
/// </summary>
public sealed class SynthEnvelope
{
    private readonly double _attackPerSample;
    private readonly double _decayPerSample;
    private readonly double _sustain;
    private readonly double _releaseSamples;
    private readonly double _sampleRate;

    private double _releasePerSample;
    private double _level;

    public SynthEnvelope(SynthPatch patch, int sampleRate)
    {
        double rate = sampleRate <= 0 ? 1 : sampleRate;

        double attackSamples = patch.AttackMs / 1000.0 * rate;
        double decaySamples = patch.DecayMs / 1000.0 * rate;

        _sampleRate = rate;
        _sustain = Math.Clamp(patch.Sustain, 0.0, 1.0);
        _releaseSamples = patch.ReleaseMs / 1000.0 * rate;

        // A zero length stage is a jump, not a division by zero.
        _attackPerSample = attackSamples > 0 ? 1.0 / attackSamples : 1.0;
        _decayPerSample = decaySamples > 0 ? (1.0 - _sustain) / decaySamples : 1.0;

        Stage = EnvelopeStage.Attack;
    }

    public EnvelopeStage Stage { get; private set; }

    public bool IsFinished => Stage == EnvelopeStage.Finished;

    public double Level => _level;

    /// <summary>Advances one sample and returns the level to multiply that sample by.</summary>
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
                    // Nothing to hold at zero, so a patch with no sustain ends on its decay.
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

    /// <summary>
    /// Releases from wherever the level happens to be, so a key up never clicks. A shorter
    /// release can be forced, which is how a retrigger cuts the voice it replaces.
    /// </summary>
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

    /// <summary>Cuts the voice dead, for a stop button rather than a note off.</summary>
    public void Kill()
    {
        _level = 0;
        Stage = EnvelopeStage.Finished;
    }
}
