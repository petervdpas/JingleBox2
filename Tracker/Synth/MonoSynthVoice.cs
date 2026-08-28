using System;
using JingleBox2.Tracker.Synth.Enums;
using JingleBox2.Tracker.Synth.Interfaces;
using JingleBox2.Tracker.Records;
using JingleBox2.Music;
using JingleBox2.Music.Interfaces;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// One note on Ouroboros: an oscillator blended with noise, through a filter, under an
/// envelope, with two modulation routes deciding what moves.
/// </summary>
/// <remarks>
/// Monophonic by nature and by arrangement. The mixer gives a track one voice and cuts it when
/// the next note arrives, which is what glide needs to mean anything: a note slides from
/// whatever the last one was, so a line phrases instead of stepping.
///
/// Everything that does not move while the note lasts is worked out in the constructor, on
/// whichever thread started the note. <see cref="Render"/> then runs on the audio thread and
/// may not allocate, take a lock or wait on anything.
/// </remarks>
public sealed class MonoSynthVoice : IVoice
{
    /// <summary>Concert pitch, so a note becomes a frequency.</summary>
    /// <remarks>
    /// Shared rather than one per voice: it holds nothing, and a voice is made every time a
    /// key goes down, which is not somewhere to be allocating.
    /// </remarks>
    private static readonly INoteFrequency Pitch = new NoteFrequency();

    /// <summary>Voices not tied to a track, such as an audition, use this.</summary>
    public const int NoTrack = -1;

    /// <summary>How long a cut takes, so a retrigger is a new note rather than a click.</summary>
    private const double CutSeconds = 0.004;

    private readonly MonoSynthPatch _patch;
    private readonly int _sampleRate;

    /// <summary>Swept rather than fixed, since the whole machine is one modulation route into it.</summary>
    private readonly SweepFilter _filter;

    private readonly Envelope _envelope;

    private double _phase;
    private double _lfoPhase;

    /// <summary>The noise generator's state, which is also its output. Never nought, or it stays there.</summary>
    private uint _noise;

    /// <summary>Where the pitch is now, which is where the next note glides from.</summary>
    private double _hz;

    /// <summary>Where it is heading: the note itself, with the machine's tuning folded in.</summary>
    private readonly double _targetHz;

    /// <summary>
    /// How far the pitch moves per sample while it is sliding.
    /// </summary>
    /// <remarks>
    /// A step of the whole distance divided by the time rather than a share of what is left. A
    /// share of the remainder never arrives: after a full glide time it is only two thirds of
    /// the way, and the name on the knob says how long it takes, not how fast it gives up.
    ///
    /// The largest double there is stands for no glide, so the first sample covers whatever
    /// the distance was and the note simply starts where it belongs.
    /// </remarks>
    private readonly double _glidePerSample;

    /// <summary>
    /// Starts a note, sliding from the pitch the track was already sounding.
    /// </summary>
    /// <param name="patch">The sound. Held rather than copied, and never written to here.</param>
    /// <param name="note">What to play.</param>
    /// <param name="track">The strip it sounds on, or <see cref="NoTrack"/> for an audition.</param>
    /// <param name="gain">The volume column and the instrument's own level, together.</param>
    /// <param name="pan">Where it sits, held to -1..1.</param>
    /// <param name="sampleRate">The mixer's rate, which every time in the patch is turned into samples at.</param>
    /// <param name="noiseSeed">A different number per voice, so two noise notes do not agree.</param>
    /// <param name="fromHz">
    /// What the track was sounding, for the glide to start from. Nothing for an audition, which
    /// has no note before it, and ignored when glide is switched off.
    /// </param>
    public MonoSynthVoice(
        MonoSynthPatch patch, Note note, int track, float gain, float pan,
        int sampleRate, int noiseSeed, double? fromHz)
    {
        _patch = patch ?? new MonoSynthPatch();
        _sampleRate = sampleRate <= 0 ? 44100 : sampleRate;

        Track = track;
        Note = note;
        Gain = gain;
        Pan = Math.Clamp(pan, -1f, 1f);

        _noise = (uint)(noiseSeed == 0 ? 1 : noiseSeed);

        double offset = _patch.TuneSemitones + _patch.FineCents / 100.0;
        _targetHz = Pitch.Hz(note) * Math.Pow(2.0, offset / 12.0);

        _hz = _patch.GlideMs > 0 && fromHz is > 0 ? fromHz.Value : _targetHz;

        double glideSamples = _patch.GlideMs / 1000.0 * _sampleRate;
        _glidePerSample = glideSamples < 1
            ? double.MaxValue
            : Math.Abs(_targetHz - _hz) / glideSamples;

        _filter = new SweepFilter(_sampleRate);
        _envelope = new Envelope(_patch, _sampleRate);
    }

    /// <inheritdoc/>
    public int Track { get; }

    /// <inheritdoc/>
    public Note Note { get; }

    /// <inheritdoc/>
    public float Gain { get; set; }

    /// <inheritdoc/>
    public float Pan { get; set; }

    /// <inheritdoc/>
    /// <remarks>
    /// The loudest sample of the last block rather than where the envelope stands, because the
    /// filter and the mixer both sit between the envelope and what comes out.
    /// </remarks>
    public float Level { get; private set; }

    /// <inheritdoc/>
    public bool IsFinished => _envelope.IsFinished;

    /// <summary>What this voice is sounding at, so the next note can slide from it.</summary>
    public double Hz => _hz;

    /// <summary>How far into the note it lets go of itself, or -1 when nothing will.</summary>
    private double _holdUntil = -1;

    /// <summary>How far into the note it is, in seconds, which is what the hold is measured against.</summary>
    private double _time;

    /// <inheritdoc/>
    public string Audition { get; init; } = "";

    /// <inheritdoc/>
    public void HoldFor(double seconds) => _holdUntil = seconds;

    /// <inheritdoc/>
    public void NoteOff() => _envelope.NoteOff();

    /// <inheritdoc/>
    public void Cut() => _envelope.NoteOff(CutSeconds);

    /// <inheritdoc/>
    public void Kill() => _envelope.Kill();

    /// <inheritdoc/>
    /// <remarks>
    /// The whole machine in one loop, in the order the signal runs: the two modulation sources
    /// worked out once, the pitch with its glide, the oscillator blended with the noise, the
    /// filter swept by its own route, and the amplifier opened by the envelope or held open.
    /// </remarks>
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

            double lfo = Lfo();
            double vco = _patch.VcoModSource == ModSource.Lfo ? lfo : level;
            double vcf = _patch.VcfModSource == ModSource.Lfo ? lfo : level;

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
                    double semitones = vco * _patch.VcoModAmount * MonoSynthPatch.PitchModSemitones;
                    hz *= Math.Pow(2, semitones / 12.0);
                }
                else
                {
                    width = Math.Clamp(width + vco * _patch.VcoModAmount * 0.48, 0.02, 0.98);
                }
            }

            _phase += hz * step;
            if (_phase >= 1) _phase -= Math.Floor(_phase);

            double tone = _patch.Wave == MonoSynthWave.Pulse
                ? (_phase < width ? 1.0 : -1.0)
                : _phase * 2.0 - 1.0;

            double sample = tone * (1 - _patch.NoiseMix) + Noise() * _patch.NoiseMix;

            double cutoff = _patch.CutoffHz;

            if (_patch.VcfModAmount > 0)
            {
                double octaves = vcf * _patch.VcfModAmount * 6.0;
                cutoff *= Math.Pow(2, _patch.VcfModInverted ? -octaves : octaves);
            }

            _filter.Set(cutoff, _patch.Resonance);
            sample = _filter.Process(sample, _patch.FilterMode);

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

    /// <summary>
    /// The balance law, which is the same one every voice here uses.
    /// </summary>
    /// <remarks>
    /// A balance and not an equal-power pan: the centre stays at full on both sides, which is
    /// what BASS does for the pads, so a voice and a pad sit together in the mix.
    /// </remarks>
    private float Left => Pan <= 0 ? 1f : 1f - Pan;

    /// <summary>The other side of the same balance.</summary>
    private float Right => Pan >= 0 ? 1f : 1f + Pan;

    /// <summary>
    /// The low frequency oscillator, minus one to one.
    /// </summary>
    /// <remarks>
    /// Run once per sample whether or not either route is pointed at it, so switching a route
    /// over mid note does not start it from a phase it never had. A triangle rises for the
    /// first half of the cycle and falls for the second.
    /// </remarks>
    private double Lfo()
    {
        _lfoPhase += _patch.LfoRateHz / _sampleRate;
        if (_lfoPhase >= 1) _lfoPhase -= Math.Floor(_lfoPhase);

        if (_patch.LfoWave == LfoWave.Square) return _lfoPhase < 0.5 ? 1.0 : -1.0;

        return _lfoPhase < 0.5 ? _lfoPhase * 4.0 - 1.0 : 3.0 - _lfoPhase * 4.0;
    }

    /// <summary>
    /// White noise from a counter, so two voices are not the same noise.
    /// </summary>
    /// <remarks>
    /// A three shift exclusive-or generator: no allocation, no table and no call out of the
    /// class, which is what it has to be to run once per sample per voice on the audio thread.
    /// </remarks>
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

        /// <summary>Whether the note holds at full, or decays away whether or not a key is down.</summary>
        private readonly bool _sustain;

        private double _level;
        private bool _released;

        /// <summary>
        /// How much comes off the level per sample once the note has been let go of.
        /// </summary>
        /// <remarks>
        /// Starts as the decay, which is what a note off does with nothing else asked for: this
        /// machine has one time for both, so a tail is the same shape whether the note ran out
        /// or was released. A cut overrides it with a shorter one.
        /// </remarks>
        private double _releasePerSample;

        private readonly double _rate;

        /// <summary>Turns the patch's two times into per sample steps, at the voice's rate.</summary>
        public Envelope(MonoSynthPatch patch, int sampleRate)
        {
            double rate = sampleRate <= 0 ? 44100 : sampleRate;

            _rate = rate;
            _attackPerSample = Rate(patch.AttackMs, rate);
            _decayPerSample = Rate(patch.DecayMs, rate);
            _sustain = patch.Sustain;
            _releasePerSample = _decayPerSample;
        }

        /// <summary>Silent and done, which is what the voice reports to the mixer.</summary>
        public bool IsFinished { get; private set; }

        /// <summary>Advances one sample and returns the level to multiply that sample by.</summary>
        /// <remarks>Held open while sustain is on; otherwise it decays away and is done.</remarks>
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

            if (_sustain) return _level;

            _level -= _decayPerSample;
            if (_level <= 0) { _level = 0; IsFinished = true; }

            return _level;
        }

        /// <summary>Still in the attack. Declared after its use, which the compiler allows for a field.</summary>
        private bool _rising = true;

        /// <summary>
        /// Lets go of the note, optionally in less time than the decay would take.
        /// </summary>
        /// <param name="seconds">
        /// A shorter fall, for a retrigger that must not overlap the note replacing it. Nothing
        /// leaves the decay in place, which is what a key coming up should sound like.
        /// </param>
        public void NoteOff(double? seconds = null)
        {
            if (_released) return;

            _released = true;

            if (seconds is > 0) _releasePerSample = _level / Math.Max(1, seconds.Value * _rate);
        }

        /// <summary>Silent at once, for a transport stop rather than a note ending.</summary>
        public void Kill()
        {
            _level = 0;
            IsFinished = true;
        }

        /// <summary>A stage's length in milliseconds as a share of full scale per sample.</summary>
        /// <remarks>At least one sample, so a stage of no length is a jump and not a division by zero.</remarks>
        private static double Rate(double milliseconds, double sampleRate)
        {
            double samples = Math.Max(1, milliseconds / 1000.0 * sampleRate);
            return 1.0 / samples;
        }
    }
}
