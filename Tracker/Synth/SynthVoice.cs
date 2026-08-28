using System;
using JingleBox2.Tracker.Synth.Enums;
using JingleBox2.Tracker.Synth.Interfaces;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// One sounding note. Owns its own copy of the patch, so editing an instrument while it plays
/// changes the next note rather than the one in the air.
/// </summary>
/// <remarks>
/// The generated voice: an oscillator, a filter, an ADSR and a little modulation, which is the
/// chiptune synth the tracker started with. Everything that does not move while the note lasts
/// is worked out in the constructor, on whichever thread started the note, because
/// <see cref="Render"/> then runs on the audio thread and may not do that kind of work.
/// </remarks>
public sealed class SynthVoice : IVoice
{
    /// <summary>Voices not tied to a track, such as an audition, use this.</summary>
    public const int NoTrack = -1;

    /// <summary>Long enough not to click, short enough not to be heard as a note.</summary>
    public const double CutSeconds = 0.004;

    private readonly SynthPatch _patch;
    private readonly SynthEnvelope _envelope;
    private readonly int _sampleRate;

    /// <summary>
    /// What the note sounds at before anything moves it, the instrument's own tuning folded in.
    /// </summary>
    /// <remarks>
    /// Worked out once rather than for every sample: the tuning does not change while the note
    /// lasts, and only the vibrato and the pitch envelope move off this.
    /// </remarks>
    private readonly double _baseFrequency;

    /// <summary>How long the pitch envelope runs for, in seconds rather than the patch's milliseconds.</summary>
    private readonly double _pitchEnvSeconds;

    /// <summary>How hard the wave is pushed into the saturation curve, off the patch.</summary>
    private readonly double _drive;

    /// <summary>
    /// What the drive is levelled out by, so turning it up changes the tone and not the loudness.
    /// </summary>
    /// <remarks>
    /// That is what the level fader is for. Kept per voice because it depends only on the drive
    /// amount, and working it out per sample would be a second hyperbolic tangent on the audio
    /// thread for nothing.
    /// </remarks>
    private readonly double _driveMakeup;

    private readonly ToneFilter _filter;

    /// <summary>This voice's own noise, seeded per voice so two noise hits are not the same noise.</summary>
    private readonly Random _noise;

    private double _phase;

    /// <summary>How far into the note it is, in seconds, which is what the modulation runs on.</summary>
    private double _time;

    /// <summary>When to let go of the note without being asked. Nought when nothing will.</summary>
    private double _holdSeconds;

    /// <summary>
    /// Starts a note on a copy of the patch, at the mixer's rate.
    /// </summary>
    /// <param name="patch">The sound, copied and held so an edit mid note does not reshape it.</param>
    /// <param name="note">What to play.</param>
    /// <param name="track">The strip it sounds on, or <see cref="NoTrack"/> for an audition.</param>
    /// <param name="gain">The volume column and the instrument's own level, together.</param>
    /// <param name="pan">Where it sits, held to -1..1.</param>
    /// <param name="sampleRate">The mixer's rate, which every time in the patch is turned into samples at.</param>
    /// <param name="noiseSeed">A different number per voice, so two noise notes do not agree.</param>
    public SynthVoice(SynthPatch patch, Note note, int track, float gain, float pan, int sampleRate, int noiseSeed)
    {
        _patch = patch.Clone();
        _patch.Clamp();

        _sampleRate = sampleRate <= 0 ? 1 : sampleRate;
        _envelope = new SynthEnvelope(_patch, _sampleRate);
        _baseFrequency = NoteFrequency.Hz(note) * PitchMotion.Ratio(PitchMotion.Tuning(_patch));
        _pitchEnvSeconds = _patch.PitchEnvMs / 1000.0;

        _drive = _patch.Drive;
        _driveMakeup = Saturation.Makeup(_drive);
        _filter = new ToneFilter(_patch.FilterCutoffHz, _patch.FilterResonance, _sampleRate);
        _noise = new Random(noiseSeed);

        Track = track;
        Note = note;
        Gain = gain;
        Pan = Math.Clamp(pan, -1f, 1f);
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
    /// <remarks>The envelope times the volume column, which is where a generated voice's level is.</remarks>
    public float Level { get; private set; }

    /// <inheritdoc/>
    public bool IsFinished => _envelope.IsFinished;

    /// <summary>Letting go of the note has started its release, but it is still sounding.</summary>
    public bool IsReleasing => _envelope.Stage == EnvelopeStage.Release;

    /// <inheritdoc/>
    public string Audition { get; init; } = "";

    /// <inheritdoc/>
    /// <remarks>Read at the top of each frame, so a hold set mid block still lands within it.</remarks>
    public void HoldFor(double seconds) => _holdSeconds = seconds;

    /// <inheritdoc/>
    public void NoteOff() => _envelope.NoteOff();

    /// <inheritdoc/>
    /// <remarks>
    /// A retrigger on the same track fades out in a few milliseconds rather than running its
    /// release: a tracker channel is monophonic, and a full release would overlap the new note.
    /// </remarks>
    public void Cut() => _envelope.NoteOff(CutSeconds);

    /// <inheritdoc/>
    public void Kill() => _envelope.Kill();

    /// <inheritdoc/>
    /// <remarks>
    /// A blank or note-off cell is not a pitch and must not be turned into one, so a voice
    /// holding one kills itself the first time it is asked for audio rather than sounding
    /// whatever the semitone happened to be.
    /// </remarks>
    public void Render(float[] buffer, int frames)
    {
        if (_envelope.IsFinished)
        {
            Level = 0;
            return;
        }

        if (!Note.IsPlayable)
        {
            _envelope.Kill();
            Level = 0;
            return;
        }

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
