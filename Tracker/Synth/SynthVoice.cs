using System;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// One sounding note. Owns its own copy of the patch, so editing an instrument while it plays
/// changes the next note rather than the one in the air.
/// </summary>
public sealed class SynthVoice : IVoice
{
    /// <summary>Voices not tied to a track, such as an audition, use this.</summary>
    public const int NoTrack = -1;

    /// <summary>Long enough not to click, short enough not to be heard as a note.</summary>
    public const double CutSeconds = 0.004;

    private readonly SynthPatch _patch;
    private readonly SynthEnvelope _envelope;
    private readonly int _sampleRate;
    private readonly double _baseFrequency;
    private readonly double _pitchEnvSeconds;
    private readonly double _drive;
    private readonly double _driveMakeup;
    private readonly ToneFilter _filter;
    private readonly Random _noise;

    private double _phase;
    private double _time;
    private double _holdSeconds;

    public SynthVoice(SynthPatch patch, Note note, int track, float gain, float pan, int sampleRate, int noiseSeed)
    {
        _patch = patch.Clone();
        _patch.Clamp();

        _sampleRate = sampleRate <= 0 ? 1 : sampleRate;
        _envelope = new SynthEnvelope(_patch, _sampleRate);
        // The instrument's own tuning is folded in once here rather than being worked out for
        // every sample: it does not change while the note lasts.
        _baseFrequency = NoteFrequency.Hz(note) * PitchMotion.Ratio(PitchMotion.Tuning(_patch));
        _pitchEnvSeconds = _patch.PitchEnvMs / 1000.0;

        // The makeup keeps the level where it was, so turning drive up changes the tone rather
        // than the loudness. That is what the level fader is for.
        _drive = _patch.Drive;
        _driveMakeup = Saturation.Makeup(_drive);
        _filter = new ToneFilter(_patch.FilterCutoffHz, _patch.FilterResonance, _sampleRate);
        _noise = new Random(noiseSeed);

        Track = track;
        Note = note;
        Gain = gain;
        Pan = Math.Clamp(pan, -1f, 1f);
    }

    public int Track { get; }

    public Note Note { get; }

    /// <summary>Level from the cell and the instrument, changeable while the note holds.</summary>
    public float Gain { get; set; }

    public float Pan { get; set; }

    /// <summary>How loud this voice is right now, for metering. Zero once it has finished.</summary>
    public float Level { get; private set; }

    public bool IsFinished => _envelope.IsFinished;

    /// <summary>Letting go of the note has started its release, but it is still sounding.</summary>
    public bool IsReleasing => _envelope.Stage == EnvelopeStage.Release;

    /// <summary>
    /// Releases on its own after this many seconds. Used for auditioning, where there is no
    /// key to let go of.
    /// </summary>
    public void HoldFor(double seconds) => _holdSeconds = seconds;

    public void NoteOff() => _envelope.NoteOff();

    /// <summary>
    /// A retrigger on the same track fades out in a few milliseconds rather than running its
    /// release: a tracker channel is monophonic, and a full release would overlap the new note.
    /// </summary>
    public void Cut() => _envelope.NoteOff(CutSeconds);

    public void Kill() => _envelope.Kill();

    /// <summary>
    /// Adds this voice into an interleaved stereo buffer. Additive rather than overwriting:
    /// the mixer sums every voice into the same buffer.
    /// </summary>
    public void Render(float[] buffer, int frames)
    {
        if (_envelope.IsFinished)
        {
            Level = 0;
            return;
        }

        // A blank or note-off cell is not a pitch, and must not be turned into one.
        if (!Note.IsPlayable)
        {
            _envelope.Kill();
            Level = 0;
            return;
        }

        // A balance control, not an equal-power pan: centre stays at full level on both sides,
        // which is what BASS does for the sampled instruments, so the two match in the mix.
        double left = Pan <= 0 ? 1.0 : 1.0 - Pan;
        double right = Pan >= 0 ? 1.0 : 1.0 + Pan;
        double step = 1.0 / _sampleRate;

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

            Level = (float)(level * Gain);

            double frequency = _baseFrequency * PitchMotion.Ratio(PitchMotion.MotionAt(_patch, _time));

            _phase = Oscillator.Wrap(_phase + frequency * step);

            double sample = Oscillator.Sample(_patch.Wave, _phase, _patch.Duty, _noise.NextDouble() * 2.0 - 1.0);
            double value = _filter.Process(Drive(sample)) * level * TremoloAt(_time) * Gain;

            int index = frame * 2;
            buffer[index] += (float)(value * left);
            buffer[index + 1] += (float)(value * right);

            _time += step;
        }
    }

    /// <summary>
    /// Rounds the wave off into itself. Applied before the envelope, so a note keeps its shape
    /// as it decays instead of losing its edge along with its level.
    /// </summary>
    private double Drive(double sample) => Saturation.Apply(sample, _drive, _driveMakeup);

    /// <summary>Amplitude modulation between full and (1 - depth).</summary>
    private double TremoloAt(double time)
    {
        if (_patch.TremoloDepth <= 0 || _patch.TremoloRateHz <= 0) return 1.0;

        double lfo = 0.5 + 0.5 * Math.Sin(2 * Math.PI * _patch.TremoloRateHz * time);
        return 1.0 - _patch.TremoloDepth * lfo;
    }
}
