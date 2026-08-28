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
///
/// Two shapes in one class, decided by whether a <see cref="SamplerPatch"/> was handed in.
/// Without one it is the plain path every recording-based instrument uses: drive, then a fixed
/// tone filter. With one it is Zampler: two four pole filters and a second envelope for the
/// brightness. They are one class because everything before that point, the window, the loop,
/// the direction and the read speed, is identical and would otherwise be written twice.
///
/// Everything that does not move while the note lasts is worked out in the constructor, on
/// whichever thread started the note. <see cref="Render"/> then runs on the audio thread and
/// may not allocate, take a lock or wait on anything.
/// </remarks>
public sealed class SampleVoice : IVoice
{
    /// <summary>The take, shared with every other voice playing it and never written to.</summary>
    private readonly SampleData _sample;

    private readonly SynthPatch _patch;
    private readonly SynthEnvelope _envelope;

    /// <summary>Where in the file to read and how to repeat, worked out once when the note started.</summary>
    private readonly SampleWindow _window;

    /// <summary>The plain path's filter, one per side. Unused when Zampler's ladder is in play.</summary>
    private readonly ToneFilter _left;
    private readonly ToneFilter _right;

    /// <summary>
    /// Zampler's own shaping, when this voice is one of its. Null on every other machine, and
    /// the plain filter above is used instead.
    /// </summary>
    private readonly SamplerPatch? _zampler;

    /// <summary>Zampler's four pole filters, one per side. Null on every other machine.</summary>
    private readonly LadderFilter? _ladderLeft;
    private readonly LadderFilter? _ladderRight;

    /// <summary>The brightness's own envelope, which is the second of Zampler's two.</summary>
    private readonly SynthEnvelope? _filterEnvelope;

    /// <summary>The note the zone or the pad calls its own, which the key follow is measured from.</summary>
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

    /// <summary>Samples left before the swept cutoff is worked out again.</summary>
    private int _sinceSweep;

    private readonly int _sampleRate;

    /// <summary>
    /// The file's own rate against the engine's.
    /// </summary>
    /// <remarks>
    /// A file recorded at one rate played out at another is already a resample, before the note
    /// is taken into account.
    /// </remarks>
    private readonly double _rateRatio;

    /// <summary>How much faster the note asks for than the recording's own, tuning folded in.</summary>
    private readonly double _noteRatio;

    /// <summary>How hard the sample is pushed into the saturation curve. The plain path only.</summary>
    private readonly double _drive;

    /// <summary>What the drive is levelled out by, worked out once since it only depends on the drive.</summary>
    private readonly double _driveMakeup;

    /// <summary>
    /// Which choke group this voice belongs to, or nought for none.
    /// </summary>
    /// <remarks>
    /// Only a kit uses it. Two pads in the same group cannot sound at once, which is what a
    /// closed hihat does to an open one: the same piece of metal cannot be doing both.
    /// </remarks>
    public int Choke { get; init; }

    /// <summary>
    /// Where the read head is, in fractional frames.
    /// </summary>
    /// <remarks>
    /// Written on the audio thread and read from the drawing one through
    /// <see cref="Progress"/>, deliberately without a lock. See that property for why.
    /// </remarks>
    private double _position;

    /// <summary>Which way the head is moving, turned round by a ping-pong loop.</summary>
    private int _direction;

    /// <summary>How far into the note it is, in seconds, which is what the modulation runs on.</summary>
    private double _time;

    /// <summary>When to let go of the note without being asked. Nought when nothing will.</summary>
    private double _holdSeconds;

    /// <summary>The head has run off the window. The envelope still finishes the tail.</summary>
    private bool _ended;

    /// <summary>
    /// Starts a note on a recording, at whatever speed the note against the root asks for.
    /// </summary>
    /// <param name="sample">The take, already decoded. The mixer never reads a file.</param>
    /// <param name="patch">The plain path's shaping, copied and held.</param>
    /// <param name="shape">Which part of the file sounds and how it repeats. Nothing is the whole file.</param>
    /// <param name="note">What was played.</param>
    /// <param name="baseNote">
    /// What the recording itself sounds at. Passing the played note here makes the ratio one and
    /// nothing is resampled, which is what a kit does: a key chooses which recording sounds
    /// rather than how fast to read one.
    /// </param>
    /// <param name="track">The strip it sounds on, or <see cref="SynthVoice.NoTrack"/> for an audition.</param>
    /// <param name="gain">The volume column and the instrument's own level, together.</param>
    /// <param name="pan">Where it sits, held to -1..1.</param>
    /// <param name="sampleRate">The mixer's rate, which is half of how fast the file is read.</param>
    /// <param name="zampler">
    /// Zampler's own shaping, which swaps the plain filter for two four pole ones and adds a
    /// second envelope for the brightness. Null on every other machine.
    /// </param>
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
        SamplerPatch? zampler = null)
    {
        _sample = sample;
        _patch = patch.Clone();
        _patch.Clamp();

        _sampleRate = sampleRate <= 0 ? 1 : sampleRate;
        _envelope = new SynthEnvelope(_patch, _sampleRate);

        _window = SamplePlayback.WindowFor(shape, sample.FrameCount);
        _position = _window.Entry;
        _direction = _window.Direction;

        _rateRatio = (double)sample.SampleRate / _sampleRate;
        _noteRatio = PitchRatio.For(note, baseNote) * PitchMotion.Ratio(PitchMotion.Tuning(_patch));

        _drive = _patch.Drive;
        _driveMakeup = Saturation.Makeup(_drive);

        _left = new ToneFilter(_patch.FilterCutoffHz, _patch.FilterResonance, _sampleRate);
        _right = new ToneFilter(_patch.FilterCutoffHz, _patch.FilterResonance, _sampleRate);

        _root = baseNote.Semitone;

        WindowSeconds = _window.IsLooping || sample.SampleRate <= 0 || _noteRatio <= 0
            ? 0
            : Math.Max(0, _window.End - _window.Start) / (sample.SampleRate * _noteRatio);

        if (zampler != null)
        {
            _zampler = zampler.Clone();
            _zampler.Clamp();

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
    /// The loudest sample the file actually produced, not just where the envelope is: a quiet
    /// recording should not light up a meter the way a full scale one does.
    /// </remarks>
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

    /// <summary>
    /// Puts the four pole filters where the envelope and the keyboard say they go.
    /// </summary>
    /// <remarks>
    /// The filter's envelope runs whether or not the amount knob is doing anything, so turning
    /// it up part way through a note does not start the envelope late.
    /// </remarks>
    private void Sweep(double envelope)
    {
        if (_zampler == null) return;

        double cutoff = _zampler.CutoffFor(envelope, Note.Semitone, _root);

        _ladderLeft!.Set(cutoff, _zampler.Resonance);
        _ladderRight!.Set(cutoff, _zampler.Resonance);
    }

    /// <inheritdoc/>
    /// <remarks>Finished when the envelope closes, or when a one-shot runs off its end.</remarks>
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
    ///
    /// The file's own rate and the note's ratio are the whole of it: a window of N frames read
    /// at ratio r out of a file recorded at R takes N / (R * r) seconds, whatever rate the
    /// engine happens to be running at.
    /// </remarks>
    public double WindowSeconds { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// The same fact <see cref="WindowSeconds"/> holds, asked as the question a hand letting go
    /// needs answered. A looping window has no end and answers no.
    /// </remarks>
    public bool OneShot => WindowSeconds > 0;

    /// <inheritdoc/>
    public string Audition { get; init; } = "";

    /// <inheritdoc/>
    public void HoldFor(double seconds) => _holdSeconds = seconds;

    /// <inheritdoc/>
    /// <remarks>Both envelopes, or Zampler's filter would stay where the note left it.</remarks>
    public void NoteOff()
    {
        _envelope.NoteOff();
        _filterEnvelope?.NoteOff();
    }

    /// <inheritdoc/>
    /// <remarks>The same few milliseconds a generated voice uses, so a retrigger sounds alike.</remarks>
    public void Cut()
    {
        _envelope.NoteOff(SynthVoice.CutSeconds);
        _filterEnvelope?.NoteOff(SynthVoice.CutSeconds);
    }

    /// <inheritdoc/>
    public void Kill()
    {
        _envelope.Kill();
        _ended = true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A blank or note-off cell is not a pitch and must not be turned into one, so a voice
    /// holding one kills itself the first time it is asked for audio.
    ///
    /// Running off the end of the window is not the end of the voice: the head stops and the
    /// envelope is left to finish the tail rather than the sound being cut dead.
    /// </remarks>
    public void Render(float[] buffer, int frames)
    {
        if (IsFinished || _sample.IsEmpty || !Note.IsPlayable)
        {
            Level = 0;
            if (!Note.IsPlayable) Kill();
            return;
        }

        double left = Pan <= 0 ? 1.0 : 1.0 - Pan;
        double right = Pan >= 0 ? 1.0 : 1.0 + Pan;
        double step = 1.0 / _sampleRate;

        bool stereo = _sample.Channels >= 2;

        Level = 0;

        for (int frame = 0; frame < frames; frame++)
        {
            if (_holdSeconds > 0 && _time >= _holdSeconds)
            {
                _holdSeconds = 0;
                _envelope.NoteOff();
                _filterEnvelope?.NoteOff();
            }

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

            double speed = _rateRatio * _noteRatio * PitchMotion.Ratio(PitchMotion.MotionAt(_patch, _time));

            if (!SamplePlayback.Advance(ref _position, ref _direction, speed, _window))
            {
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
