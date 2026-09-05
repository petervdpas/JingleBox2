using System;
using JingleBox2.Tracker.Synth.Enums;
using JingleBox2.Tracker.Synth.Interfaces;
using JingleBox2.Tracker.Records;
using JingleBox2.Music;
using JingleBox2.Music.Interfaces;

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
    /// <summary>The wave shapes, which are the same maths for every voice that draws one.</summary>
    /// <remarks>
    /// Shared rather than one per voice: it holds nothing, and a voice is made every time a
    /// key goes down, which is not somewhere to be allocating.
    /// </remarks>
    private static readonly IOscillator Shapes = new Oscillator();

    /// <summary>Everything that moves a voice off the note it was given.</summary>
    /// <remarks>
    /// Shared rather than one per voice: it holds nothing, and a voice is made every time a
    /// key goes down, which is not somewhere to be allocating.
    /// </remarks>
    private static readonly IPitchMotion Motion = new PitchMotion();

    /// <summary>The drive, applied last on the way out.</summary>
    /// <remarks>
    /// Shared rather than one per voice: it holds nothing, and a voice is made every time a
    /// key goes down, which is not somewhere to be allocating.
    /// </remarks>
    private static readonly ISaturation Shaper = new Saturation();

    /// <summary>Concert pitch, so a note becomes a frequency.</summary>
    /// <remarks>
    /// Shared rather than one per voice: it holds nothing, and a voice is made every time a
    /// key goes down, which is not somewhere to be allocating.
    /// </remarks>
    private static readonly INoteFrequency Pitch = new NoteFrequency();

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

    /// <summary>Whether the filter runs before the drive, off the patch.</summary>
    /// <remarks>
    /// Held rather than read per sample, like the drive above it and for the same reason: this is
    /// the inner loop of the audio thread and the patch cannot change under a voice anyway, since
    /// the voice owns a copy of it.
    /// </remarks>
    private readonly bool _filterFirst;

    /// <summary>
    /// How many points of the wave the loudness-holding makeup is worked out over.
    /// </summary>
    /// <remarks>
    /// One period, sampled evenly. Enough that a pulse at either end of its width is described
    /// rather than missed: the narrowest this machine allows is a twentieth, which is still a
    /// dozen points. It is walked once when a note starts and never again.
    /// </remarks>
    private const int ShapePoints = 256;

    /// <summary>
    /// How many real samples the makeup is worked out over when the filter runs first.
    /// </summary>
    /// <remarks>
    /// A period of the wave is the exact answer while the drive is fed the oscillator, since the
    /// shape of a wave does not depend on how fast it is played. It is the wrong question once the
    /// filter is in front, because what a filter does depends on the note against the cutoff, so
    /// what reaches the drive has to be produced rather than described.
    ///
    /// At the mixer's rate this is about twenty milliseconds, which is a whole cycle of anything
    /// down to the bottom of a bass guitar and several cycles of everything anybody plays above
    /// that. Eight kilobytes of stack, taken and given back inside the constructor.
    /// </remarks>
    private const int FilteredPoints = 1024;

    /// <summary>How many samples the filter is run before any of it is measured.</summary>
    /// <remarks>
    /// A filter starts empty, so its first output is a rise from silence rather than the signal,
    /// and a resonant one rings on its own for a while after being hit with anything. Measuring
    /// through that would read the filter waking up rather than the wave the drive is about to be
    /// handed.
    /// </remarks>
    private const int SettleSamples = 2048;

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
        _baseFrequency = Pitch.Hz(note) * Motion.Ratio(Motion.Tuning(_patch));
        _pitchEnvSeconds = _patch.PitchEnvMs / 1000.0;

        _drive = _patch.Drive;
        _driveMakeup = _patch.EvenDrive ? EvenMakeup(noiseSeed) : Shaper.Makeup(_drive);
        _filterFirst = _patch.FilterFirst;
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
    public int Column { get; init; }

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

            double frequency = _baseFrequency * Motion.Ratio(Motion.MotionAt(_patch, _time));

            _phase = Shapes.Wrap(_phase + frequency * step);

            double sample = Shapes.Sample(_patch.Wave, _phase, _patch.Duty, _noise.NextDouble() * 2.0 - 1.0);
            double value = Shaped(sample) * level * TremoloAt(_time) * Gain;

            int index = frame * 2;
            buffer[index] += (float)(value * left);
            buffer[index + 1] += (float)(value * right);

            _time += step;
        }
    }

    /// <summary>
    /// The drive and the filter, in whichever order the patch asks for.
    /// </summary>
    /// <remarks>
    /// Two different instruments rather than two spellings of one. Drive into filter squares the
    /// wave up and then takes the top off what it made; filter into drive shapes the wave and then
    /// rounds off what is left, which is also what stops a resonant peak being applied to
    /// something already squared off.
    /// </remarks>
    private double Shaped(double sample) =>
        _filterFirst ? Drive(_filter.Process(sample)) : _filter.Process(Drive(sample));

    /// <summary>
    /// Rounds the wave off into itself. Applied before the envelope, so a note keeps its shape
    /// as it decays instead of losing its edge along with its level.
    /// </summary>
    private double Drive(double sample) => Shaper.Apply(sample, _drive, _driveMakeup);

    /// <summary>
    /// The makeup that leaves this patch's own wave as loud as it arrived.
    /// </summary>
    /// <remarks>
    /// The wave is drawn out here rather than guessed at, because a makeup that held the loudness
    /// of a sine would be wrong for a saw and wronger for a narrow pulse. On the stack and gone
    /// before the constructor returns: this runs on whichever thread started the note, never on
    /// the audio thread, so a few hundred hyperbolic tangents is affordable exactly once.
    ///
    /// Noise is the one wave whose shape is not a function of phase, so it is sampled from its own
    /// seed rather than from the voice's, which would take the first two hundred and fifty six
    /// values out of the noise somebody is about to hear.
    /// </remarks>
    /// <param name="noiseSeed">This voice's seed, used for a throwaway sequence of its own.</param>
    private double EvenMakeup(int noiseSeed)
    {
        var noise = _patch.Wave == SynthWave.Noise ? new Random(noiseSeed) : null;

        if (!_patch.FilterFirst)
        {
            Span<double> period = stackalloc double[ShapePoints];

            Shapes.Period(_patch.Wave, _patch.Duty, period, noise);

            return Shaper.Evenly(_drive, period);
        }

        Span<double> filtered = stackalloc double[FilteredPoints];

        Filtered(filtered, noise);

        return Shaper.Evenly(_drive, filtered);
    }

    /// <summary>
    /// Runs the oscillator through a filter of its own until it has settled, then keeps what comes
    /// out next.
    /// </summary>
    /// <remarks>
    /// A filter of its own rather than the voice's, which has not started yet and must be handed
    /// the note with no memory in it: a voice whose filter had already been run for two thousand
    /// samples would begin the note part way into its own attack.
    ///
    /// The note is taken at its base pitch with nothing modulating it. The vibrato and the pitch
    /// envelope move it while it plays, and a makeup that moved with them would be a gain
    /// following the pitch, which is a fault rather than a correction.
    /// </remarks>
    /// <param name="into">Filled with what reaches the drive.</param>
    /// <param name="noise">This voice's throwaway noise, or nothing for the waves that need none.</param>
    private void Filtered(Span<double> into, Random? noise)
    {
        var settling = new ToneFilter(_patch.FilterCutoffHz, _patch.FilterResonance, _sampleRate);

        double phase = 0;
        double step = _baseFrequency / _sampleRate;

        for (int at = 0; at < SettleSamples + into.Length; at++)
        {
            phase = Shapes.Wrap(phase + step);

            double random = noise is null ? 0.0 : noise.NextDouble() * 2.0 - 1.0;
            double sample = settling.Process(Shapes.Sample(_patch.Wave, phase, _patch.Duty, random));

            if (at >= SettleSamples) into[at - SettleSamples] = sample;
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
