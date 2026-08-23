using System;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// One note on Ouroboros: an oscillator blended with noise, through a filter, under an
/// envelope, with two modulation routes deciding what moves.
/// </summary>
/// <remarks>
/// Monophonic by nature and by arrangement. The mixer gives a track one voice and cuts it when
/// the next note arrives, which is what glide needs to mean anything: a note slides from
/// whatever the last one was, so a line phrases instead of stepping.
/// </remarks>
public sealed class OuroborosVoice : IVoice
{
    public const int NoTrack = -1;

    /// <summary>How long a cut takes, so a retrigger is a new note rather than a click.</summary>
    private const double CutSeconds = 0.004;

    private readonly OuroborosPatch _patch;
    private readonly int _sampleRate;
    private readonly SweepFilter _filter;
    private readonly Envelope _envelope;

    private double _phase;
    private double _lfoPhase;
    private uint _noise;

    /// <summary>Where the pitch is now and where it is heading, for the glide between them.</summary>
    private double _hz;
    private readonly double _targetHz;
    private readonly double _glidePerSample;

    public OuroborosVoice(
        OuroborosPatch patch, Note note, int track, float gain, float pan,
        int sampleRate, int noiseSeed, double? fromHz)
    {
        _patch = patch ?? new OuroborosPatch();
        _sampleRate = sampleRate <= 0 ? 44100 : sampleRate;

        Track = track;
        Note = note;
        Gain = gain;
        Pan = Math.Clamp(pan, -1f, 1f);

        _noise = (uint)(noiseSeed == 0 ? 1 : noiseSeed);

        // The note, moved by the machine's own tuning: whole semitones and cents of one.
        double offset = _patch.TuneSemitones + _patch.FineCents / 100.0;
        _targetHz = NoteFrequency.Hz(note) * Math.Pow(2.0, offset / 12.0);

        // Starting from the note before is the whole of glide. With nothing before it, or with
        // glide switched off, the note simply starts where it belongs.
        _hz = _patch.GlideMs > 0 && fromHz is > 0 ? fromHz.Value : _targetHz;

        // A step of the whole distance rather than a share of what is left. A share of the
        // remainder never arrives: after a full glide time it is only two thirds of the way,
        // and the name on the knob says how long it takes, not how fast it gives up.
        double glideSamples = _patch.GlideMs / 1000.0 * _sampleRate;
        _glidePerSample = glideSamples < 1
            ? double.MaxValue
            : Math.Abs(_targetHz - _hz) / glideSamples;

        _filter = new SweepFilter(_sampleRate);
        _envelope = new Envelope(_patch, _sampleRate);
    }

    public int Track { get; }
    public Note Note { get; }
    public float Gain { get; set; }
    public float Pan { get; set; }

    public float Level { get; private set; }

    public bool IsFinished => _envelope.IsFinished;

    /// <summary>What this voice is sounding at, so the next note can slide from it.</summary>
    public double Hz => _hz;

    private double _holdUntil = -1;
    private double _time;

    /// <summary>Which instrument auditioned this, for one that plays one note at a time.</summary>
    public string Audition { get; init; } = "";

    public void HoldFor(double seconds) => _holdUntil = seconds;

    public void NoteOff() => _envelope.NoteOff();

    public void Cut() => _envelope.NoteOff(CutSeconds);

    public void Kill() => _envelope.Kill();

    public void Render(float[] buffer, int frames)
    {
        if (_envelope.IsFinished)
        {
            Level = 0;
            return;
        }

        int samples = Math.Min(frames * 2, buffer.Length);
        double step = 1.0 / _sampleRate;
        float loudest = 0;

        for (int index = 0; index + 1 < samples; index += 2)
        {
            if (_holdUntil >= 0 && _time >= _holdUntil)
            {
                _holdUntil = -1;
                _envelope.NoteOff();
            }

            double level = _envelope.Next();

            if (_envelope.IsFinished)
            {
                Level = 0;
                return;
            }

            // The two sources of modulation, worked out once and pointed wherever the patch
            // says. This is the whole of the modulation section: two numbers and four switches.
            double lfo = Lfo();
            double vco = _patch.VcoModSource == ModSource.Lfo ? lfo : level;
            double vcf = _patch.VcfModSource == ModSource.Lfo ? lfo : level;

            // Pitch: glide first, then whatever the oscillator's modulation adds to it.
            if (_hz != _targetHz)
            {
                double left = _targetHz - _hz;

                _hz = Math.Abs(left) <= _glidePerSample
                    ? _targetHz
                    : _hz + Math.Sign(left) * _glidePerSample;
            }

            double hz = _hz;
            double width = _patch.PulseWidth;

            if (_patch.VcoModAmount > 0)
            {
                if (_patch.VcoModTarget == VcoModTarget.Frequency)
                {
                    double semitones = vco * _patch.VcoModAmount * OuroborosPatch.PitchModSemitones;
                    hz *= Math.Pow(2, semitones / 12.0);
                }
                else
                {
                    width = Math.Clamp(width + vco * _patch.VcoModAmount * 0.48, 0.02, 0.98);
                }
            }

            _phase += hz * step;
            if (_phase >= 1) _phase -= Math.Floor(_phase);

            // The mixer: the oscillator and the noise, both, in whatever proportion.
            double tone = _patch.Wave == OuroborosWave.Pulse
                ? (_phase < width ? 1.0 : -1.0)
                : _phase * 2.0 - 1.0;

            double sample = tone * (1 - _patch.NoiseMix) + Noise() * _patch.NoiseMix;

            // The filter, swept by its own route, opening or closing by the polarity switch.
            double cutoff = _patch.CutoffHz;

            if (_patch.VcfModAmount > 0)
            {
                double octaves = vcf * _patch.VcfModAmount * 6.0;
                cutoff *= Math.Pow(2, _patch.VcfModInverted ? -octaves : octaves);
            }

            _filter.Set(cutoff, _patch.Resonance);
            sample = _filter.Process(sample, _patch.FilterMode);

            // The amplifier, opened by the envelope or simply held open.
            double amp = _patch.EnvelopeToAmp ? level : 1.0;
            double value = sample * amp * _patch.Volume * Gain;

            float mono = (float)value;
            float magnitude = Math.Abs(mono);
            if (magnitude > loudest) loudest = magnitude;

            buffer[index] += mono * Left;
            buffer[index + 1] += mono * Right;

            _time += step;
        }

        Level = Math.Min(1f, loudest);
    }

    private float Left => Pan <= 0 ? 1f : 1f - Pan;
    private float Right => Pan >= 0 ? 1f : 1f + Pan;

    /// <summary>The low frequency oscillator, minus one to one.</summary>
    private double Lfo()
    {
        _lfoPhase += _patch.LfoRateHz / _sampleRate;
        if (_lfoPhase >= 1) _lfoPhase -= Math.Floor(_lfoPhase);

        if (_patch.LfoWave == LfoWave.Square) return _lfoPhase < 0.5 ? 1.0 : -1.0;

        // Triangle: up for the first half, down for the second.
        return _lfoPhase < 0.5 ? _lfoPhase * 4.0 - 1.0 : 3.0 - _lfoPhase * 4.0;
    }

    /// <summary>White noise from a counter, so two voices are not the same noise.</summary>
    private double Noise()
    {
        _noise ^= _noise << 13;
        _noise ^= _noise >> 17;
        _noise ^= _noise << 5;

        return _noise / (double)uint.MaxValue * 2.0 - 1.0;
    }

    /// <summary>
    /// Attack, decay, and a sustain that is on or off rather than a level.
    /// </summary>
    /// <remarks>
    /// The machine this follows has a switch there, not a knob: off is a drum or a pluck that
    /// decays and stays gone, on is anything held. Two answers cover more than a sustain level
    /// suggests, and there is nothing to set wrong.
    /// </remarks>
    private sealed class Envelope
    {
        private readonly double _attackPerSample;
        private readonly double _decayPerSample;
        private readonly bool _sustain;

        private double _level;
        private bool _released;
        private double _releasePerSample;

        private readonly double _rate;

        public Envelope(OuroborosPatch patch, int sampleRate)
        {
            double rate = sampleRate <= 0 ? 44100 : sampleRate;

            _rate = rate;
            _attackPerSample = Rate(patch.AttackMs, rate);
            _decayPerSample = Rate(patch.DecayMs, rate);
            _sustain = patch.Sustain;
            _releasePerSample = _decayPerSample;
        }

        public bool IsFinished { get; private set; }

        public double Next()
        {
            if (IsFinished) return 0;

            if (_released)
            {
                _level -= _releasePerSample;
                if (_level <= 0) { _level = 0; IsFinished = true; }
                return _level;
            }

            if (_rising)
            {
                _level += _attackPerSample;
                if (_level >= 1.0) { _level = 1.0; _rising = false; }
                return _level;
            }

            // Held open while sustain is on; otherwise it decays away and is done.
            if (_sustain) return _level;

            _level -= _decayPerSample;
            if (_level <= 0) { _level = 0; IsFinished = true; }

            return _level;
        }

        private bool _rising = true;

        public void NoteOff(double? seconds = null)
        {
            if (_released) return;

            _released = true;

            if (seconds is > 0) _releasePerSample = _level / Math.Max(1, seconds.Value * _rate);
        }

        public void Kill()
        {
            _level = 0;
            IsFinished = true;
        }

        private static double Rate(double milliseconds, double sampleRate)
        {
            double samples = Math.Max(1, milliseconds / 1000.0 * sampleRate);
            return 1.0 / samples;
        }
    }
}
