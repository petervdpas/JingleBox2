using System;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// One note played from a recording. The file is read at whatever speed the note asks for,
/// through the same envelope, drive and filter a generated voice runs through.
/// </summary>
/// <remarks>
/// This is what makes a sample an instrument rather than a file being triggered: the window,
/// the loop and the direction belong to the instrument, and the note only decides how fast to
/// read. Everything happens here rather than in the audio library, which is the point: BASS
/// can pitch a channel and nothing else.
/// </remarks>
public sealed class SampleVoice : IVoice
{
    private readonly SampleData _sample;
    private readonly SynthPatch _patch;
    private readonly SynthEnvelope _envelope;
    private readonly SampleWindow _window;
    private readonly ToneFilter _left;
    private readonly ToneFilter _right;

    /// <summary>
    /// Zampler's own shaping, when this voice is one of its. Null on every other machine, and
    /// the plain filter above is used instead.
    /// </summary>
    private readonly ZamplerPatch? _zampler;

    private readonly LadderFilter? _ladderLeft;
    private readonly LadderFilter? _ladderRight;
    private readonly SynthEnvelope? _filterEnvelope;
    private readonly int _root;

    /// <summary>
    /// How often the swept cutoff is worked out again, in samples.
    /// </summary>
    /// <remarks>
    /// Recomputing a filter's coefficients every sample costs more than the filter does. Every
    /// sixteenth is under a millisecond at any rate worth using, which is faster than a sweep
    /// can be heard to step.
    /// </remarks>
    private const int SweepEvery = 16;

    private int _sinceSweep;

    private readonly int _sampleRate;
    private readonly double _rateRatio;
    private readonly double _noteRatio;
    private readonly double _drive;
    private readonly double _driveMakeup;

    /// <summary>
    /// Which choke group this voice belongs to, or nought for none.
    /// </summary>
    /// <remarks>
    /// Only a kit uses it. Two pads in the same group cannot sound at once, which is what a
    /// closed hihat does to an open one: the same piece of metal cannot be doing both.
    /// </remarks>
    public int Choke { get; init; }

    private double _position;
    private int _direction;
    private double _time;
    private double _holdSeconds;
    private bool _ended;

    public SampleVoice(
        SampleData sample,
        SynthPatch patch,
        SampleShape? shape,
        Note note,
        Note baseNote,
        int track,
        float gain,
        float pan,
        int sampleRate,
        ZamplerPatch? zampler = null)
    {
        _sample = sample;
        _patch = patch.Clone();
        _patch.Clamp();

        _sampleRate = sampleRate <= 0 ? 1 : sampleRate;
        _envelope = new SynthEnvelope(_patch, _sampleRate);

        _window = SamplePlayback.WindowFor(shape, sample.FrameCount);
        _position = _window.Entry;
        _direction = _window.Direction;

        // A file recorded at one rate played out at another is already a resample, before the
        // note is taken into account.
        _rateRatio = (double)sample.SampleRate / _sampleRate;
        _noteRatio = PitchRatio.For(note, baseNote) * PitchMotion.Ratio(PitchMotion.Tuning(_patch));

        _drive = _patch.Drive;
        _driveMakeup = Saturation.Makeup(_drive);

        _left = new ToneFilter(_patch.FilterCutoffHz, _patch.FilterResonance, _sampleRate);
        _right = new ToneFilter(_patch.FilterCutoffHz, _patch.FilterResonance, _sampleRate);

        _root = baseNote.Semitone;

        // How long one pass through the window takes at the speed this note reads it. The file's
        // own rate and the note's ratio are the whole of it: a window of N frames read at ratio r
        // out of a file recorded at R takes N / (R * r) seconds, whatever rate the engine runs at.
        WindowSeconds = _window.IsLooping || sample.SampleRate <= 0 || _noteRatio <= 0
            ? 0
            : Math.Max(0, _window.End - _window.Start) / (sample.SampleRate * _noteRatio);

        if (zampler != null)
        {
            _zampler = zampler.Clone();
            _zampler.Clamp();

            // Two four pole filters and a second envelope, which is the machine this is named
            // for: the loudness and the brightness are not the same shape.
            _ladderLeft = new LadderFilter(_sampleRate);
            _ladderRight = new LadderFilter(_sampleRate);

            _envelope = new SynthEnvelope(Shaped(
                _zampler.AttackMs, _zampler.DecayMs, _zampler.Sustain, _zampler.ReleaseMs), _sampleRate);

            _filterEnvelope = new SynthEnvelope(Shaped(
                _zampler.FilterAttackMs, _zampler.FilterDecayMs,
                _zampler.FilterSustain, _zampler.FilterReleaseMs), _sampleRate);

            Sweep(0);
        }

        Track = track;
        Note = note;
        Gain = gain;
        Pan = Math.Clamp(pan, -1f, 1f);
    }

    public int Track { get; }

    public Note Note { get; }

    public float Gain { get; set; }

    public float Pan { get; set; }

    public float Level { get; private set; }

    /// <summary>
    /// A patch carrying nothing but one envelope's times, so the shared envelope can run it.
    /// </summary>
    /// <remarks>
    /// One envelope in the codebase rather than two that have to be kept sounding alike. The
    /// filter and the amplifier ask it the same question and only differ in what they do with
    /// the answer.
    /// </remarks>
    private static SynthPatch Shaped(double attack, double decay, double sustain, double release) =>
        new()
        {
            AttackMs = attack,
            DecayMs = decay,
            Sustain = sustain,
            ReleaseMs = release
        };

    /// <summary>Puts the four pole filters where the envelope and the keyboard say they go.</summary>
    private void Sweep(double envelope)
    {
        if (_zampler == null) return;

        double cutoff = _zampler.CutoffFor(envelope, Note.Semitone, _root);

        _ladderLeft!.Set(cutoff, _zampler.Resonance);
        _ladderRight!.Set(cutoff, _zampler.Resonance);
    }

    /// <summary>Finished when the envelope closes, or when a one-shot runs off its end.</summary>
    public bool IsFinished => _ended || _envelope.IsFinished;

    /// <summary>
    /// Where the read head is, as a fraction of the whole recording, or -1 once it has stopped.
    /// </summary>
    /// <remarks>
    /// Read from the drawing thread while the audio thread is writing it, and deliberately not
    /// locked. A double is written whole on the runtimes this ships on, so the worst that can
    /// happen is a cursor a fortieth of a second behind, which is what a cursor is anyway.
    /// Locking the audio thread to draw a line would be the actual mistake.
    /// </remarks>
    public double Progress =>
        IsFinished || _sample.FrameCount <= 0
            ? -1
            : Math.Clamp(_position / _sample.FrameCount, 0, 1);

    /// <summary>
    /// How long this voice takes to read its window once, or zero when it loops and so never
    /// finishes on its own.
    /// </summary>
    /// <remarks>
    /// What an audition of a one-shot should hold for. A recording cut off part way through is
    /// not the sound the instrument makes, and a fixed hold cuts every recording longer than it.
    /// </remarks>
    public double WindowSeconds { get; }

    /// <summary>Which instrument auditioned this, for one that plays one note at a time.</summary>
    public string Audition { get; init; } = "";

    public void HoldFor(double seconds) => _holdSeconds = seconds;

    public void NoteOff()
    {
        _envelope.NoteOff();
        _filterEnvelope?.NoteOff();
    }

    public void Cut()
    {
        _envelope.NoteOff(SynthVoice.CutSeconds);
        _filterEnvelope?.NoteOff(SynthVoice.CutSeconds);
    }

    public void Kill()
    {
        _envelope.Kill();
        _ended = true;
    }

    public void Render(float[] buffer, int frames)
    {
        if (IsFinished || _sample.IsEmpty || !Note.IsPlayable)
        {
            Level = 0;
            if (!Note.IsPlayable) Kill();
            return;
        }

        // The same balance law the generated voices use, so the two sit together in a mix.
        double left = Pan <= 0 ? 1.0 : 1.0 - Pan;
        double right = Pan >= 0 ? 1.0 : 1.0 + Pan;
        double step = 1.0 / _sampleRate;

        bool stereo = _sample.Channels >= 2;

        // The meter reads what the file is actually doing, not just where the envelope is: a
        // quiet recording should not light up a meter the way a full scale one does.
        Level = 0;

        for (int frame = 0; frame < frames; frame++)
        {
            if (_holdSeconds > 0 && _time >= _holdSeconds)
            {
                _holdSeconds = 0;
                _envelope.NoteOff();
                _filterEnvelope?.NoteOff();
            }

            // The filter's envelope runs whether or not it is doing anything, so that turning
            // the amount up mid note does not start it late.
            if (_filterEnvelope != null)
            {
                double brightness = _filterEnvelope.Next();

                if (--_sinceSweep <= 0)
                {
                    Sweep(brightness);
                    _sinceSweep = SweepEvery;
                }
            }

            double level = _envelope.Next();
            if (_envelope.IsFinished)
            {
                Level = 0;
                return;
            }

            double a = _sample.Between(_position, 0);
            double b = stereo ? _sample.Between(_position, 1) : a;

            double shared = level * TremoloAt(_time) * Gain;

            double shapedLeft = _zampler == null
                ? _left.Process(Saturation.Apply(a, _drive, _driveMakeup)) * shared
                : _ladderLeft!.Process(a) * shared * _zampler.Volume;

            double shapedRight = _zampler == null
                ? _right.Process(Saturation.Apply(b, _drive, _driveMakeup)) * shared
                : _ladderRight!.Process(b) * shared * _zampler.Volume;

            float loudest = (float)Math.Max(Math.Abs(shapedLeft), Math.Abs(shapedRight));
            if (loudest > Level) Level = loudest;

            double outLeft = shapedLeft * left;
            double outRight = shapedRight * right;

            int index = frame * 2;
            buffer[index] += (float)outLeft;
            buffer[index + 1] += (float)outRight;

            // Vibrato and the pitch envelope move the read speed rather than a frequency, which
            // is the same thing for a sample: faster is higher.
            double speed = _rateRatio * _noteRatio * PitchMotion.Ratio(PitchMotion.MotionAt(_patch, _time));

            if (!SamplePlayback.Advance(ref _position, ref _direction, speed, _window))
            {
                // Out of audio: let the envelope finish the tail rather than cutting it dead.
                _ended = true;
                Level = 0;
                return;
            }

            _time += step;
        }
    }

    /// <summary>Amplitude modulation between full and (1 - depth).</summary>
    private double TremoloAt(double time)
    {
        if (_patch.TremoloDepth <= 0 || _patch.TremoloRateHz <= 0) return 1.0;

        double lfo = 0.5 + 0.5 * Math.Sin(2 * Math.PI * _patch.TremoloRateHz * time);
        return 1.0 - _patch.TremoloDepth * lfo;
    }
}
