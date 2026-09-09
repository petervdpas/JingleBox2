using System;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;

namespace JingleBox2.SoundDevices.SoundEffects;

/// <summary>
/// A widener: a signal that sits in the middle of your head put across the room instead.
/// </summary>
/// <remarks>
/// **It exists because a mono input is already stereo here and does not sound it.**
/// <see cref="JingleBox2.Audio.StereoFloats"/> reads a one channel capture into both sides by
/// copying, so a microphone arrives as two identical channels and every take is written with
/// two. That is right, since an effect places things in the stereo field and narrowing the
/// answer would throw away half of what it did, but two identical channels are dead centre and
/// no fader can change that: a pan moves the whole thing, it does not open it.
///
/// **It works on the middle and the side rather than on the left and the right, and that is the
/// whole of why it is safe.** What comes in is split into the two: the middle is what both sides
/// share and the side is what they differ by, which for a mono source is nothing at all. A side
/// signal is then made out of the middle, and the two are put back as middle plus side and
/// middle minus side. **Summed to mono the side cancels exactly**, by arithmetic rather than by
/// luck, so a fold-down is the signal that arrived and nothing else.
///
/// That was arrived at by measuring, and the version before it is worth recording. Two delays
/// whose lengths move in opposite directions, read one to a side, is the usual way to do this
/// and it opens the sound perfectly well. Folded to mono it keeps between 45% and 87% of what it
/// was handed depending on where the depth knob happens to sit, because the two arrivals comb
/// and where the notch lands moves with the setting. **A number that swings that far with a
/// control nobody would connect to it is not a tolerance, it is a fault**, and from a chair it
/// is a voice that sounds hollow on one setting and fine on the next with nothing to explain it.
/// The middle and side form measured 1.000 at every one of those settings.
///
/// **The side is made from two taps of the middle whose lengths move in opposite directions**,
/// subtracted from each other. Two copies of one signal a few milliseconds apart are heard as
/// unrelated, and their difference is exactly the part that is not shared, which is what a side
/// signal is.
///
/// **What the two knobs really do is not what their names suggest, and it was measured.**
/// <see cref="Width"/> is the amount and <see cref="Depth"/> is not. Measured on broadband
/// material, the side signal against the input is 0.147, 0.146 and 0.145 as the taps travel one,
/// three and five milliseconds, and 0.147, 0.294 and 0.588 as the width goes a quarter, a half
/// and all the way: flat in one and proportional in the other. Two copies of a signal are
/// unrelated at any separation past a few samples, so the travel has almost nothing to do with
/// how wide it sounds; what it changes is the character of the movement, which shows on
/// sustained material as a comb whose notch moves. **The first version read the amount off the
/// delay length**, which measured as a knob that did almost nothing on music and everything on
/// a sine.
///
/// **Opening a sound costs peak level, and how much is worth knowing rather than discovering.**
/// The two sides are the middle plus and minus the side, so the loudest of them rises as the
/// side grows: measured against the same material, +1.8 dB at a width of a quarter, +3.4 at a
/// half, and +5.9 at the top. That is what widening is rather than a fault in it, and it is
/// said here, on the knob and in the help rather than enforced, since a width of one is a real
/// setting somebody may want and this is not the place to decide they may not have it. For
/// scale, a pair of wholly unrelated channels measures 0.707, so the top of the knob is about
/// five sixths of the way to no relation at all.
///
/// **<see cref="Haas"/> is the one control here that breaks the mono guarantee**, deliberately
/// offered and deliberately off. A plain delay on one side is the strongest placement there is,
/// since the ear decides where a sound is by which side reached it first; folded down it is two
/// arrivals added, which combs. It is applied to the middle of the late side, so a Haas of
/// nothing reads the sample just written and is exact.
///
/// **It does not make an already wide recording narrower.** Every knob adds difference and the
/// side that arrived is carried through untouched, so a stereo source keeps its picture and is
/// opened further. Narrowing is the same arithmetic with the side scaled down and is a different
/// effect; this one is about a source with no side signal at all, which is the case this
/// application actually has.
///
/// Nothing here allocates, takes a lock or blocks. The line is made once, at the longest either
/// delay can be asked for.
/// </remarks>
public sealed class Widen : ISoundEffectEngine
{
    /// <summary>How much side signal is made, which is the amount of width.</summary>
    /// <remarks>
    /// Written out rather than built, so the words this effect and its manifest have to agree on
    /// can be found by looking for them. They are the same strings <c>effect.json</c> names.
    ///
    /// This is the headline control and <see cref="Depth"/> is not, which was settled by
    /// measuring rather than by reasoning: see the remarks on the class.
    /// </remarks>
    public const string Width = "width";

    /// <summary>How far the two taps travel, which is the character of the movement.</summary>
    /// <remarks>
    /// Not the amount. On anything broadband the side signal is the same size whatever this is
    /// set to, since two copies of a signal are unrelated at any separation past a few samples.
    /// What it changes is where the moving comb sits on sustained material.
    /// </remarks>
    public const string Depth = "depth";

    /// <summary>How quickly those two lengths move, in cycles a second.</summary>
    public const string Rate = "rate";

    /// <summary>The plain delay on one side, in milliseconds.</summary>
    public const string Haas = "haas";

    /// <summary>Which side that delay goes on.</summary>
    public const string Side = "side";

    /// <summary>How much of what comes out has been opened.</summary>
    public const string Mix = "mix";

    /// <summary>
    /// How much side signal a width of one makes, against the middle.
    /// </summary>
    /// <remarks>
    /// A half, and it is a headroom decision rather than a taste one. The two sides are the
    /// middle plus and minus the side, so a side as large as the middle would put one of them at
    /// twice what arrived. At a half, a width of one measures a side 0.588 of the input and a
    /// peak 5.9 dB above it, against the 0.707 a pair of wholly unrelated channels would give:
    /// so the top of the knob reaches most of the way to no relation at all, and stops short of
    /// the setting where one channel is the other one inverted.
    /// </remarks>
    public const double SideMost = 0.5;

    /// <summary>The furthest the moving pair will travel, in milliseconds.</summary>
    /// <remarks>
    /// Small on purpose. Past about five milliseconds the movement stops being a picture and
    /// starts being a chorus, which is a different effect somebody should choose rather than
    /// arrive at by turning this one up.
    /// </remarks>
    public const double MostDepthMs = 5;

    /// <summary>The slowest the pair moves, which is a picture that drifts rather than wobbles.</summary>
    public const double LeastRate = 0.05;

    /// <summary>And the fastest, which is where it is heard as movement rather than as width.</summary>
    public const double MostRate = 2;

    /// <summary>
    /// The longest plain delay one side can be given, in milliseconds.
    /// </summary>
    /// <remarks>
    /// Thirty five, which is about where the ear stops hearing two arrivals as one sound and
    /// begins hearing the second as a thing of its own. Past it this would be a slapback rather
    /// than a placement, and EchoBox is the effect for that.
    /// </remarks>
    public const double MostHaasMs = 35;

    /// <summary>How long a line has to be to hold the longest of both together, with room to read.</summary>
    private const double MostMs = MostHaasMs + MostDepthMs;

    /// <summary>What a fresh one is set to: half open, and no plain delay at all.</summary>
    /// <remarks>
    /// The middle and side half applied and the Haas off. That setting opens a mono source and
    /// folds down to exactly what arrived, so a fresh one can be dropped on a chain by somebody
    /// who has read nothing about it. The control that can quietly ruin a mono fold-down is the
    /// one they have to reach for, which is the right way round.
    /// </remarks>
    public const double WidthThen = 0.5;

    /// <inheritdoc cref="WidthThen"/>
    public const double DepthThen = 3;

    /// <inheritdoc cref="WidthThen"/>
    public const double RateThen = 0.35;

    /// <inheritdoc cref="WidthThen"/>
    public const double HaasThen = 0;

    /// <inheritdoc cref="WidthThen"/>
    public const double SideThen = 0;

    /// <inheritdoc cref="WidthThen"/>
    public const double MixThen = 1;

    /// <summary>Frames a second, which is what every time here is turned into.</summary>
    private readonly double _rate;

    /// <summary>How many frames the line holds: the longest ask, with room to read between.</summary>
    private readonly int _room;

    /// <summary>
    /// The middle of what came in, kept so the taps and the Haas can read behind it.
    /// </summary>
    /// <remarks>
    /// The middle alone and not the two sides. Everything read out of here goes into making a
    /// side signal or delaying a middle, and the side that arrived is carried straight through
    /// without ever being stored, so a second line would hold something nothing reads.
    /// </remarks>
    private readonly float[] _line;

    /// <summary>Where the next frame is written.</summary>
    private int _write;

    /// <summary>How far round the pair's own movement has got, in radians.</summary>
    private double _phase;

    /// <summary>The knobs, as single words so a thread never reads half of one.</summary>
    private float _width = (float)WidthThen;

    /// <inheritdoc cref="_width"/>
    private float _depth = (float)DepthThen;

    /// <inheritdoc cref="_width"/>
    private float _sweep = (float)RateThen;

    /// <inheritdoc cref="_width"/>
    private float _haas = (float)HaasThen;

    /// <inheritdoc cref="_width"/>
    private float _side = (float)SideThen;

    /// <inheritdoc cref="_width"/>
    private float _mix = (float)MixThen;

    /// <summary>
    /// Makes the line at the longest either delay can ask for.
    /// </summary>
    /// <remarks>
    /// Once, and never again: growing it later would mean allocating on the audio thread, which
    /// is the one place nothing may allocate. Forty milliseconds at any rate anybody runs is a
    /// few kilobytes.
    /// </remarks>
    /// <param name="sampleRate">What the mix runs at, since every time here is in milliseconds.</param>
    /// <param name="id">Which effect this one stands for, or nothing outside the application.</param>
    public Widen(int sampleRate, string? id = null)
    {
        Id = id ?? "";

        _rate = sampleRate > 0 ? sampleRate : 48000;
        _room = (int)(MostMs * 0.001 * _rate) + 2;
        _line = new float[_room];
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public System.Collections.Generic.IReadOnlyList<string> Keys { get; } =
        new[] { Width, Depth, Rate, Haas, Side, Mix };

    /// <inheritdoc/>
    public double ValueOf(string? key) => key switch
    {
        Width => _width,
        Depth => _depth,
        Rate => _sweep,
        Haas => _haas,
        Side => _side,
        Mix => _mix,
        _ => 0
    };

    /// <inheritdoc/>
    public void SetValue(string? key, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return;

        switch (key)
        {
            case Width:
                _width = (float)Math.Clamp(value, 0, 1);
                break;

            case Depth:
                _depth = (float)Math.Clamp(value, 0, MostDepthMs);
                break;

            case Rate:
                _sweep = (float)Math.Clamp(value, LeastRate, MostRate);
                break;

            case Haas:
                _haas = (float)Math.Clamp(value, 0, MostHaasMs);
                break;

            case Side:
                _side = (float)Math.Clamp(value, 0, 1);
                break;

            case Mix:
                _mix = (float)Math.Clamp(value, 0, 1);
                break;
        }
    }

    /// <summary>That many milliseconds as frames.</summary>
    private double Frames(double milliseconds) => milliseconds * 0.001 * _rate;

    /// <summary>
    /// The middle, that many frames behind the write, read between the two frames the position
    /// falls between.
    /// </summary>
    /// <remarks>
    /// Between the entries rather than at one, since the taps travel continuously and a read
    /// that jumped from one frame to the next would be a click on every step.
    ///
    /// Wrapped at both ends. A position a hair below nought lands on the length itself once the
    /// arithmetic rounds, which is one frame past the array and an index outside it on the audio
    /// thread; that is the fault this application has already paid for once in
    /// <see cref="Delay"/>, where it took an eight thousand frame block to find.
    ///
    /// Nothing behind the write is exact: asked for nought it reads the sample just written, so a
    /// Haas of nothing hands back the middle itself rather than an interpolation of it.
    /// </remarks>
    /// <param name="back">How many frames behind the write to read.</param>
    private float Taken(double back)
    {
        double at = _write - back;

        while (at < 0) at += _room;
        while (at >= _room) at -= _room;

        int first = (int)at;
        double into = at - first;
        int second = first + 1;

        if (second >= _room) second -= _room;

        float one = _line[first];
        float two = _line[second];

        return (float)(one + ((two - one) * into));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The block is held to what the buffer can really take and rounded down to whole frames,
    /// because a count that is a promise rather than a measurement is how this application has
    /// crashed on the audio thread before.
    ///
    /// Everything that cannot move inside a block is worked out before the loop, which is the
    /// rule the voices already keep: the knobs are read once, and what is left per sample is the
    /// movement and two readings.
    ///
    /// What the mix knob puts back is the signal as it was handed in rather than as it was
    /// written into the line, so a mix short of the top returns exactly what arrived even while
    /// the lines are still filling.
    /// </remarks>
    public void Process(float[] buffer, int frames)
    {
        if (buffer is null) return;

        int block = Math.Min(frames, buffer.Length / 2);

        if (block <= 0) return;

        double mix = _mix;
        double gain = _width * SideMost;
        double half = Frames(_depth) * 0.5;
        double haas = Frames(_haas);
        double step = 2 * Math.PI * _sweep / _rate;
        bool onLeft = _side >= 0.5f;

        for (int at = 0; at < block; at++)
        {
            float wasLeft = buffer[at * 2];
            float wasRight = buffer[(at * 2) + 1];

            double middle = (wasLeft + wasRight) * 0.5;
            double side = (wasLeft - wasRight) * 0.5;

            _line[_write] = (float)middle;

            _phase += step;

            if (_phase >= 2 * Math.PI) _phase -= 2 * Math.PI;

            double swing = Math.Sin(_phase);

            side += (Taken(half * (1 + swing)) - Taken(half * (1 - swing))) * gain;

            double late = Taken(haas);

            double left = (onLeft ? late : middle) + side;
            double right = (onLeft ? middle : late) - side;

            buffer[at * 2] = (float)((wasLeft * (1 - mix)) + (left * mix));
            buffer[(at * 2) + 1] = (float)((wasRight * (1 - mix)) + (right * mix));

            _write++;

            if (_write >= _room) _write = 0;
        }
    }
}
