using System;
using JingleBox2.Music;
using JingleBox2.Music.Interfaces;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Synth.Enums;
using JingleBox2.Tracker.Synth.Interfaces;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// One note on Lighttower: a wave read round and round at the note's pitch, moving from the
/// drawn beginning to the drawn end as the note goes on.
/// </summary>
/// <remarks>
/// Where along the way the note is comes from how long it has been sounding and the patch's
/// sweep and motion. With the grit on, that position is moved back onto one of the thirty two
/// waves and only taken again when the wave comes round to its start, the way the CMI stepped;
/// the point is read without smoothing and put on the eight bit steps. With it off, the note
/// glides between the lines and between the points.
///
/// **What is heard is centred on nought.** A line drawn by hand is almost never balanced above and
/// below the middle, and whatever it is off by is a direct voltage in the mix: a thump when the
/// note starts and ends, and headroom taken for nothing in between. So the average of the wave
/// being played is taken off it. The picture still shows the line as it was drawn.
///
/// The lines, the sweep, the motion, the grit, the tuning and the level are read once a block, so
/// drawing while a note is held is heard on that note. The envelope's times are taken when the
/// note starts, the way every voice here takes them.
///
/// Everything is made in the constructor, on whichever thread started the note. <see cref="Render"/>
/// runs on the audio thread and does not allocate, take a lock or wait on anything.
/// </remarks>
public sealed class SegmentVoice : IVoice
{
    /// <summary>Concert pitch, so a note becomes a frequency. Shared, since it holds nothing.</summary>
    private static readonly INoteFrequency Pitch = new NoteFrequency();

    /// <summary>The merge the panel draws with. Shared, since it holds nothing.</summary>
    private static readonly IWaveSegments Waves = new WaveSegments();

    /// <summary>A line of silence, standing in for one a damaged patch has not got.</summary>
    private static readonly double[] Silence = new double[WaveSegments.Points];

    /// <summary>A voice not tied to a track, such as an audition, uses this.</summary>
    public const int NoTrack = -1;

    /// <summary>How long a cut takes, so a retrigger is a new note rather than a click.</summary>
    private const double CutSeconds = 0.004;

    /// <summary>The sound, held rather than copied and never written to here.</summary>
    private readonly SegmentPatch _patch;

    /// <summary>The rate everything is worked out at.</summary>
    private readonly int _sampleRate;

    /// <summary>The note's own frequency, before the patch's tuning.</summary>
    private readonly double _noteHz;

    /// <summary>The one envelope.</summary>
    private readonly SynthEnvelope _envelope;

    /// <summary>How far round the wave is, nought to one.</summary>
    private double _phase;

    /// <summary>The position the grit is holding until the wave comes round, or -1 before the first.</summary>
    private double _held = -1;

    /// <summary>Set once the envelope has finished, or the note was killed.</summary>
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
    public SegmentVoice(SegmentPatch patch, Note note, int track, float gain, float pan, int sampleRate)
    {
        _patch = patch ?? new SegmentPatch();
        _sampleRate = sampleRate <= 0 ? 44100 : sampleRate;

        Track = track;
        Note = note;
        Gain = gain;
        Pan = double.IsNaN(pan) ? 0 : Math.Clamp(pan, -1f, 1f);

        _noteHz = Pitch.Hz(note);
        _envelope = new SynthEnvelope(Finite(_patch.AttackMs, 2), Finite(_patch.DecayMs, 400),
            Finite(_patch.Sustain, 0.7), Finite(_patch.ReleaseMs, 300), _sampleRate);
    }

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
    public void NoteOff() => _envelope.NoteOff();

    /// <inheritdoc/>
    public void Cut() => _envelope.NoteOff(CutSeconds);

    /// <inheritdoc/>
    public void Kill()
    {
        _envelope.Kill();

        _finished = true;
    }

    /// <summary>
    /// The balance law every voice here uses: the centre stays at full on both sides.
    /// </summary>
    private float Left => Pan <= 0 ? 1f : 1f - Pan;

    /// <inheritdoc cref="Left"/>
    private float Right => Pan >= 0 ? 1f : 1f + Pan;

    /// <summary>
    /// How far from the beginning to the end a note is after that long, by the patch's motion.
    /// </summary>
    /// <param name="seconds">How long the note has been sounding.</param>
    /// <param name="sweepSeconds">How long the way from one end to the other takes.</param>
    /// <param name="motion">What happens at the end.</param>
    public static double Along(double seconds, double sweepSeconds, SegmentMotion motion)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) return 0;

        double laps = seconds / Math.Max(SegmentPatch.LeastSweepMs / 1000, sweepSeconds);

        return motion switch
        {
            SegmentMotion.Loop => laps - Math.Floor(laps),
            SegmentMotion.Bounce => (laps % 2) <= 1 ? laps % 2 : 2 - (laps % 2),
            _ => Math.Min(1, laps),
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Two waves are read at the one point and weighed by the position, rather than the wave
    /// being worked out whole and then read: it is the same number and it is one sample's work
    /// rather than a hundred and twenty eight.
    /// </remarks>
    public void Render(float[] buffer, int frames)
    {
        if (_finished || buffer is null)
        {
            Level = 0;
            return;
        }

        var begin = Line(_patch.Begin);
        var end = Line(_patch.End);
        bool grit = _patch.Grit;
        var motion = _patch.Motion;
        double sweep = Finite(_patch.SweepMs, 2000) / 1000;
        double volume = Finite(_patch.Volume, 0.25);
        double offset = Finite(_patch.TuneSemitones, 0) + (Finite(_patch.FineCents, 0) / 100.0) + Bend;
        double step = _noteHz * Math.Pow(2.0, offset / 12.0) / _sampleRate;
        double beginMiddle = Middle(begin);
        double endMiddle = Middle(end);
        double tick = 1.0 / _sampleRate;
        int samples = Math.Min(frames * 2, buffer.Length);
        float loudest = 0;

        if (!double.IsFinite(step) || step < 0) step = 0;

        for (int index = 0; index + 1 < samples; index += 2)
        {
            if (_holdUntil >= 0 && _time >= _holdUntil)
            {
                _holdUntil = -1;
                NoteOff();
            }

            double along = Along(_time, sweep, motion);

            if (grit)
            {
                if (_held < 0) _held = Waves.Stepped(along);

                along = _held;
            }

            double wave = grit ? Stepped(begin, end, along) : Smooth(begin, end, along);

            wave -= beginMiddle + ((endMiddle - beginMiddle) * along);

            double envelope = _envelope.Next();

            if (_envelope.IsFinished)
            {
                _finished = true;
                break;
            }

            float mono = (float)(wave * envelope * volume * Gain);
            float magnitude = Math.Abs(mono);

            if (magnitude > loudest) loudest = magnitude;

            buffer[index] += mono * Left;
            buffer[index + 1] += mono * Right;

            _phase += step;

            if (_phase >= 1)
            {
                _phase -= Math.Floor(_phase);

                if (grit) _held = Waves.Stepped(Along(_time + tick, sweep, motion));
            }

            _time += tick;
        }

        Level = Math.Min(1f, loudest);
    }

    /// <summary>The point under the phase, unsmoothed and on the eight bit steps.</summary>
    private double Stepped(double[] begin, double[] end, double along)
    {
        int point = Math.Min(WaveSegments.Points - 1, (int)(_phase * WaveSegments.Points));
        double value = begin[point] + ((end[point] - begin[point]) * along);

        return Math.Round(value * WaveSegments.Steps, MidpointRounding.AwayFromZero) / WaveSegments.Steps;
    }

    /// <summary>The wave between the two points either side of the phase.</summary>
    private double Smooth(double[] begin, double[] end, double along)
    {
        double place = _phase * WaveSegments.Points;
        int below = Math.Min(WaveSegments.Points - 1, (int)place);
        int above = (below + 1) % WaveSegments.Points;
        double past = place - below;

        double low = begin[below] + ((end[below] - begin[below]) * along);
        double high = begin[above] + ((end[above] - begin[above]) * along);

        return low + ((high - low) * past);
    }

    /// <summary>The average of a line, which is how far it sits off the middle.</summary>
    private static double Middle(double[] line)
    {
        double sum = 0;

        for (int point = 0; point < line.Length; point++) sum += line[point];

        return sum / line.Length;
    }

    /// <summary>
    /// That line where it is whole and fit to play, or silence.
    /// </summary>
    /// <remarks>
    /// Asked rather than trusted, since the patch is the panel's and a voice may not write to it
    /// to put it right; a patch read off disc is brought into range where it is read. A line with
    /// one bad point in it is a line nobody meant, and playing the rest of it would put the bad
    /// point into the buffer.
    /// </remarks>
    private static double[] Line(double[]? line)
    {
        if (line is null || line.Length != WaveSegments.Points) return Silence;

        for (int point = 0; point < line.Length; point++)
            if (!double.IsFinite(line[point]) || Math.Abs(line[point]) > 1) return Silence;

        return line;
    }

    /// <summary>That number, or the fallback where it is not one.</summary>
    private static double Finite(double value, double fallback) => double.IsFinite(value) ? value : fallback;
}
