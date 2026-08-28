using System;

namespace JingleBox2.Tracker.Synth;

/// <summary>Which segment of an ADSR a voice is in.</summary>
public enum EnvelopeStage
{
    /// <summary>Rising to full, from wherever the note started.</summary>
    Attack,

    /// <summary>Falling from full towards the sustain level.</summary>
    Decay,

    /// <summary>Holding, until the note is let go of. Skipped when the sustain level is nought.</summary>
    Sustain,

    /// <summary>Falling away after a note off, from wherever the level happened to be.</summary>
    Release,

    /// <summary>Silent and done, which is what the mixer reaps a voice on.</summary>
    Finished
}

/// <summary>
/// A per sample ADSR. Linear segments: at these timescales the shape matters far less than
/// getting the lengths right, and linear keeps the stage boundaries exact.
/// </summary>
/// <remarks>
/// One per voice, and for Zampler two: the loudness and the brightness are not the same shape,
/// so the filter runs a second one of these and only differs in what it does with the answer.
///
/// <see cref="Next"/> is called once per sample per voice on the audio thread. It allocates
/// nothing and takes no lock, because the envelope belongs to the voice being rendered and to
/// nobody else.
/// </remarks>
public sealed class SynthEnvelope
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

    /// <summary>Which segment it is in, which is how a voice knows a key is still being held.</summary>
    public EnvelopeStage Stage { get; private set; }

    /// <summary>Silent and done, so the voice holding this can be dropped.</summary>
    public bool IsFinished => Stage == EnvelopeStage.Finished;

    /// <summary>Where it stands now, without moving it on.</summary>
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
    /// <remarks>
    /// There is nothing to hold at nought, so a patch with no sustain ends on its decay and a
    /// note off that arrives after that finds the envelope already finished and does nothing.
    /// </remarks>
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
