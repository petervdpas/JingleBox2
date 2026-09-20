using System;
using JingleBox2.Music;
using JingleBox2.Music.Interfaces;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Synth.Interfaces;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// One note on Operetta: four sines, each with its own envelope, wired together the way the
/// patch's algorithm says.
/// </summary>
/// <remarks>
/// Every sample the operators are worked out from the fourth down to the first, since a modulator
/// is always numbered higher than what it bends: by the time an operator is reached, everything
/// bending it has its value for this sample. What an operator adds to another is its output
/// times <see cref="FmPatch.Depth"/>, added to the other's phase, which is phase modulation and is
/// what every FM instrument since the DX7 really does under that name.
///
/// The note ends when every operator that is heard has finished its envelope. A modulator still
/// ringing under a carrier that has gone is bending nothing anybody can hear.
///
/// The envelopes' times are taken when the note starts, the way every voice here takes them. The
/// ratios, the levels, the algorithm and the feedback are read once a block, so a knob turned
/// while a note is held is heard on that note.
///
/// Everything is made in the constructor, on whichever thread started the note. <see cref="Render"/>
/// runs on the audio thread and does not allocate, take a lock or wait on anything.
/// </remarks>
public sealed class FmVoice : IVoice
{
    /// <summary>Concert pitch, so a note becomes a frequency. Shared, since it holds nothing.</summary>
    private static readonly INoteFrequency Pitch = new NoteFrequency();

    /// <summary>A voice not tied to a track, such as an audition, uses this.</summary>
    public const int NoTrack = -1;

    /// <summary>
    /// How far the fourth operator bends itself at full feedback, in radians of phase.
    /// </summary>
    /// <remarks>
    /// Half a turn, averaged over the last two samples as the DX7 does, which is what stops a
    /// sine fed back into itself from flipping between two values every sample: most of the knob
    /// is a sine turning into a saw, and the top of it is where that starts to hiss.
    /// </remarks>
    public const double FeedbackDepth = Math.PI;

    /// <summary>How long a cut takes, so a retrigger is a new note rather than a click.</summary>
    private const double CutSeconds = 0.004;

    /// <summary>The sound, held rather than copied and never written to here.</summary>
    private readonly FmPatch _patch;

    /// <summary>The rate everything is worked out at.</summary>
    private readonly int _sampleRate;

    /// <summary>The note's own frequency with the patch's tuning in it.</summary>
    private readonly double _hz;

    /// <summary>One envelope to an operator, first to fourth.</summary>
    private readonly SynthEnvelope[] _envelopes = new SynthEnvelope[FmPatch.Operators];

    /// <summary>How far round each operator is, nought to one.</summary>
    private readonly double[] _phases = new double[FmPatch.Operators];

    /// <summary>What each operator put out this sample.</summary>
    private readonly double[] _outputs = new double[FmPatch.Operators];

    /// <summary>How far each operator moves per sample, and how loud it is, for this block.</summary>
    private readonly double[] _steps = new double[FmPatch.Operators];

    /// <inheritdoc cref="_steps"/>
    private readonly double[] _levels = new double[FmPatch.Operators];

    /// <summary>The fourth operator's last two outputs, which is what it is fed back from.</summary>
    private double _fedOne;

    /// <inheritdoc cref="_fedOne"/>
    private double _fedTwo;

    /// <summary>Set once every heard operator is silent, or the note was killed.</summary>
    private bool _finished;

    /// <summary>How far into the note it lets go of itself, or -1 when nothing will.</summary>
    private double _holdUntil = -1;

    /// <summary>How far into the note it is, in seconds.</summary>
    private double _time;

    /// <summary>
    /// Starts a note.
    /// </summary>
    /// <param name="patch">The sound. Held rather than copied, and never written to here.</param>
    /// <param name="note">What to play.</param>
    /// <param name="track">The strip it sounds on, or <see cref="NoTrack"/> for an audition.</param>
    /// <param name="gain">The volume column and the instrument's own level, together.</param>
    /// <param name="pan">Where it sits, held to -1..1.</param>
    /// <param name="sampleRate">The mixer's rate.</param>
    public FmVoice(FmPatch patch, Note note, int track, float gain, float pan, int sampleRate)
    {
        _patch = patch ?? new FmPatch();
        _sampleRate = sampleRate <= 0 ? 44100 : sampleRate;

        Track = track;
        Note = note;
        Gain = gain;
        Pan = Math.Clamp(pan, -1f, 1f);

        double offset = _patch.TuneSemitones + (_patch.FineCents / 100.0);

        _hz = Pitch.Hz(note) * Math.Pow(2.0, offset / 12.0);

        for (int at = 0; at < FmPatch.Operators; at++)
        {
            var one = Operator(at);

            _envelopes[at] = new SynthEnvelope(one.AttackMs, one.DecayMs, one.Sustain, one.ReleaseMs, _sampleRate);
        }
    }

    /// <summary>A plain operator, standing in for one a damaged patch has not got.</summary>
    private static readonly FmOperator Missing = new();

    /// <summary>
    /// That operator of the patch, or a silent one where the patch holds fewer than four.
    /// </summary>
    /// <remarks>
    /// Asked rather than trusted, since the patch is the panel's and a voice may not write to it
    /// to put it right; a patch read off disc is brought into range where it is read.
    /// </remarks>
    /// <param name="at">Which, from nought.</param>
    private FmOperator Operator(int at) =>
        _patch.Stack is { } stack && at < stack.Count && stack[at] is { } one ? one : Missing;

    /// <inheritdoc/>
    public int Track { get; }

    /// <inheritdoc/>
    public int Column { get; init; }

    /// <inheritdoc/>
    public string Audition { get; init; } = "";

    /// <inheritdoc/>
    public Note Note { get; }

    /// <inheritdoc/>
    public float Gain { get; set; }

    /// <inheritdoc/>
    public float Pan { get; set; }

    /// <inheritdoc/>
    /// <remarks>
    /// Read once at the top of a block rather than per sample. A wheel moves tens of times a
    /// second and a block is a few milliseconds, so reading it again inside the loop buys
    /// nothing anybody can hear and costs the one thing the loop cannot afford; and a bend that
    /// changed halfway through a block would be applied to part of it, which is a step in the
    /// pitch rather than a slide.
    /// </remarks>
    public float Bend { get; set; }

    /// <inheritdoc/>
    public float Level { get; private set; }

    /// <inheritdoc/>
    public bool IsFinished => _finished;

    /// <inheritdoc/>
    public void HoldFor(double seconds) => _holdUntil = seconds;

    /// <inheritdoc/>
    public void NoteOff()
    {
        foreach (var envelope in _envelopes) envelope.NoteOff();
    }

    /// <inheritdoc/>
    public void Cut()
    {
        foreach (var envelope in _envelopes) envelope.NoteOff(CutSeconds);
    }

    /// <inheritdoc/>
    public void Kill()
    {
        foreach (var envelope in _envelopes) envelope.Kill();

        _finished = true;
    }

    /// <summary>
    /// The balance law every voice here uses: the centre stays at full on both sides.
    /// </summary>
    private float Left => Pan <= 0 ? 1f : 1f - Pan;

    /// <inheritdoc cref="Left"/>
    private float Right => Pan >= 0 ? 1f : 1f + Pan;

    /// <inheritdoc/>
    /// <remarks>
    /// A level knob is squared on its way in, so the knob moves loudness and brightness evenly
    /// rather than doing everything in its bottom quarter. What is heard is shared out between
    /// however many operators are heard, so an algorithm with four carriers is not four times as
    /// loud as one with one.
    ///
    /// The sine is the dear part, a call into the system's maths library per operator per sample,
    /// so an operator at no level skips it and puts out nought. Its envelope and its phase still
    /// move, so turning its level up mid note starts it where it would have been.
    /// </remarks>
    public void Render(float[] buffer, int frames)
    {
        if (_finished || buffer is null)
        {
            Level = 0;
            return;
        }

        int samples = Math.Min(frames * 2, buffer.Length);
        int algorithm = Math.Clamp(_patch.Algorithm, 1, FmPatch.Algorithms) - 1;
        int[] bentBy = FmPatch.ModulatedBy[algorithm];
        int heard = FmPatch.Heard[algorithm];
        int carriers = System.Numerics.BitOperations.PopCount((uint)heard);
        double share = _patch.Volume / carriers;
        double feedback = _patch.Feedback * FeedbackDepth * 0.5;
        double step = 1.0 / _sampleRate;
        double wheel = Bend == 0 ? 1.0 : Math.Pow(2.0, Bend / 12.0);
        float loudest = 0;

        for (int at = 0; at < FmPatch.Operators; at++)
        {
            var one = Operator(at);

            _steps[at] = _hz * wheel * one.Ratio * Math.Pow(2.0, one.FineCents / 1200.0) / _sampleRate;
            _levels[at] = one.Level * one.Level;
        }

        for (int index = 0; index + 1 < samples; index += 2)
        {
            if (_holdUntil >= 0 && _time >= _holdUntil)
            {
                _holdUntil = -1;
                NoteOff();
            }

            double sum = 0;
            bool sounding = false;

            for (int at = FmPatch.Operators - 1; at >= 0; at--)
            {
                double envelope = _envelopes[at].Next();
                double bend = 0;
                int from = bentBy[at];

                for (int other = at + 1; other < FmPatch.Operators; other++)
                    if ((from & (1 << other)) != 0) bend += _outputs[other];

                bend *= FmPatch.Depth;

                if (at == FmPatch.Operators - 1) bend += (_fedOne + _fedTwo) * feedback;

                double output = _levels[at] > 0
                    ? Math.Sin((2 * Math.PI * _phases[at]) + bend) * _levels[at] * envelope
                    : 0;

                _outputs[at] = output;

                _phases[at] += _steps[at];
                if (_phases[at] >= 1) _phases[at] -= Math.Floor(_phases[at]);

                if ((heard & (1 << at)) == 0) continue;

                sum += output;
                sounding |= !_envelopes[at].IsFinished;
            }

            _fedTwo = _fedOne;
            _fedOne = _outputs[FmPatch.Operators - 1];

            if (!sounding)
            {
                _finished = true;
                break;
            }

            float mono = (float)(sum * share * Gain);
            float magnitude = Math.Abs(mono);

            if (magnitude > loudest) loudest = magnitude;

            buffer[index] += mono * Left;
            buffer[index + 1] += mono * Right;

            _time += step;
        }

        Level = Math.Min(1f, loudest);
    }
}
