using System;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// One sounding note. Owns its own copy of the patch, so editing an instrument while it plays
/// changes the next note rather than the one in the air.
/// </summary>
public sealed class SynthVoice
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
        _baseFrequency = NoteFrequency.Hz(note);
        _pitchEnvSeconds = _patch.PitchEnvMs / 1000.0;
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

    public bool IsFinished => _envelope.IsFinished;

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
        if (_envelope.IsFinished) return;

        // A blank or note-off cell is not a pitch, and must not be turned into one.
        if (!Note.IsPlayable)
        {
            _envelope.Kill();
            return;
        }

        double left = Math.Sqrt((1.0 - Pan) / 2.0);
        double right = Math.Sqrt((1.0 + Pan) / 2.0);
        double step = 1.0 / _sampleRate;

        for (int frame = 0; frame < frames; frame++)
        {
            if (_holdSeconds > 0 && _time >= _holdSeconds)
            {
                _holdSeconds = 0;
                _envelope.NoteOff();
            }

            double level = _envelope.Next();
            if (_envelope.IsFinished) return;

            double frequency = _baseFrequency * Math.Pow(2.0, SemitoneOffsetAt(_time) / 12.0);

            _phase = Oscillator.Wrap(_phase + frequency * step);

            double sample = Oscillator.Sample(_patch.Wave, _phase, _patch.Duty, _noise.NextDouble() * 2.0 - 1.0);
            double value = sample * level * TremoloAt(_time) * Gain;

            int index = frame * 2;
            buffer[index] += (float)(value * left);
            buffer[index + 1] += (float)(value * right);

            _time += step;
        }
    }

    /// <summary>Vibrato and the pitch envelope, both in semitones, at a point in the note.</summary>
    private double SemitoneOffsetAt(double time)
    {
        double offset = 0;

        if (_patch.VibratoDepthCents > 0 && _patch.VibratoRateHz > 0)
            offset += _patch.VibratoDepthCents / 100.0 * Math.Sin(2 * Math.PI * _patch.VibratoRateHz * time);

        if (_patch.PitchEnvSemitones != 0 && _pitchEnvSeconds > 0 && time < _pitchEnvSeconds)
            offset += _patch.PitchEnvSemitones * (1.0 - time / _pitchEnvSeconds);

        return offset;
    }

    /// <summary>Amplitude modulation between full and (1 - depth).</summary>
    private double TremoloAt(double time)
    {
        if (_patch.TremoloDepth <= 0 || _patch.TremoloRateHz <= 0) return 1.0;

        double lfo = 0.5 + 0.5 * Math.Sin(2 * Math.PI * _patch.TremoloRateHz * time);
        return 1.0 - _patch.TremoloDepth * lfo;
    }
}
