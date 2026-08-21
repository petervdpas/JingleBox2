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

    private readonly int _sampleRate;
    private readonly double _rateRatio;
    private readonly double _noteRatio;
    private readonly double _drive;
    private readonly double _driveMakeup;

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
        int sampleRate)
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

    /// <summary>Finished when the envelope closes, or when a one-shot runs off its end.</summary>
    public bool IsFinished => _ended || _envelope.IsFinished;

    public void HoldFor(double seconds) => _holdSeconds = seconds;

    public void NoteOff() => _envelope.NoteOff();

    public void Cut() => _envelope.NoteOff(SynthVoice.CutSeconds);

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

            double shapedLeft = _left.Process(Saturation.Apply(a, _drive, _driveMakeup)) * shared;
            double shapedRight = _right.Process(Saturation.Apply(b, _drive, _driveMakeup)) * shared;

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
