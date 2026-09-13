using System;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;

namespace JingleBox2.SoundDevices.SoundEffects;

/// <summary>
/// A phaser: the signal run through a row of all-pass stages that a slow sine sweeps up and down,
/// and added back to itself.
/// </summary>
/// <remarks>
/// **An all-pass stage changes nothing you can hear on its own.** It lets every frequency through
/// at the level it arrived at and delays each by a different amount, so a stage is a phase shift
/// that turns over at one frequency. What makes the sound is adding that back to the original:
/// wherever the row has shifted a frequency by half a turn the two cancel, and that is a notch.
/// Four stages make two notches, twelve make six, and the sine moving the stages up and down is
/// the notches sweeping through the sound, which is the swoosh.
///
/// That is why **Mix** starts at a half. At a half the notches are as deep as they go, since the
/// shifted copy and the original are the same size; at one there is only the shifted copy, which
/// has no notches in it at all and sounds almost exactly like what went in.
///
/// **Feedback is a resonance, and it is paid for so it does not get louder.** What comes out of
/// the last stage is fed back into the first, which sharpens the notches and puts peaks between
/// them. Fed back without anything else, a peak at nine tenths would be ten times what went in;
/// so the input is scaled down by as much as the feedback adds, which leaves a peak at the level
/// it arrived at and makes the dips between them deeper instead. Negative feedback puts the peaks
/// where positive puts the notches, which is a different vowel rather than a different amount.
///
/// **Spread** reads the sine a fraction of a turn apart for the two sides, so the notches pass
/// through the left and then the right, which is what makes a phaser move across the room. Nought
/// is both sides together and is the mono pedal.
///
/// The centre is followed rather than jumped to, over a few tens of milliseconds, since a stage
/// whose coefficient steps is a click; the rate and the depth are read on every sample and move the
/// sine from where it is. What the stages hold is put back to nought at the end of a block where it
/// is not a number or is too small to hear, so a NaN handed in costs one block rather than the rest
/// of the session, and a tail dying away never reaches the numbers a processor is slow at.
///
/// Nothing here allocates, takes a lock or blocks.
/// </remarks>
public sealed class Phase : ISoundEffectEngine
{
    /// <summary>How fast the sine sweeps, in turns a second.</summary>
    /// <remarks>
    /// Written out rather than built, so the words this effect and its manifest have to agree on
    /// can be found by looking for them. They are the same strings <c>effect.json</c> names.
    /// </remarks>
    public const string Rate = "rate";

    /// <summary>How far it sweeps, nought to one, which is up to two octaves either way.</summary>
    public const string Depth = "depth";

    /// <summary>Where the sweep is centred, in hertz.</summary>
    public const string Centre = "centre";

    /// <summary>How much of what comes out goes back in, minus to plus.</summary>
    public const string Feedback = "feedback";

    /// <summary>Which of the four rows of stages, by its place in <see cref="Rows"/>.</summary>
    public const string Stages = "stages";

    /// <summary>How far apart the two sides read the sine, in degrees.</summary>
    public const string Spread = "spread";

    /// <summary>How much of what comes out has been through the stages.</summary>
    public const string Mix = "mix";

    /// <summary>The slowest sweep, which takes fifty seconds to go round.</summary>
    public const double LeastRate = 0.02;

    /// <summary>The fastest, which is a warble rather than a sweep.</summary>
    public const double MostRate = 10;

    /// <summary>How many octaves the sweep reaches either side of the centre at full depth.</summary>
    public const double Octaves = 2;

    /// <summary>The lowest centre.</summary>
    public const double LeastCentre = 100;

    /// <summary>The highest centre.</summary>
    public const double MostCentre = 4000;

    /// <summary>How far the feedback goes either way.</summary>
    /// <remarks>Short of one, since a loop that gives back everything it is handed never decays.</remarks>
    public const double MostFeedback = 0.9;

    /// <summary>How far apart the sides can read the sine, which is opposite.</summary>
    public const double MostSpread = 180;

    /// <summary>How many stages each choice on the face is, in its order.</summary>
    /// <remarks>
    /// Even counts only. An odd row leaves the shifted copy inverted at the bottom of the range, so
    /// adding it back thins the bass out whatever the sweep is doing, and that is a different
    /// effect nobody asked this one for.
    /// </remarks>
    public static readonly int[] Rows = { 4, 6, 8, 12 };

    /// <summary>What a fresh one is set to: a slow, deep, gently fed back four stage sweep.</summary>
    public const double RateThen = 0.5;

    /// <inheritdoc cref="RateThen"/>
    public const double DepthThen = 0.7;

    /// <inheritdoc cref="RateThen"/>
    public const double CentreThen = 800;

    /// <inheritdoc cref="RateThen"/>
    public const double FeedbackThen = 0.3;

    /// <inheritdoc cref="RateThen"/>
    public const double StagesThen = 0;

    /// <inheritdoc cref="RateThen"/>
    public const double SpreadThen = 60;

    /// <inheritdoc cref="RateThen"/>
    public const double MixThen = 0.5;

    /// <summary>How long the centre takes to follow its knob, in milliseconds.</summary>
    private const double FollowMs = 30;

    /// <summary>Anything smaller than this in a stage is nought, since nobody can hear it.</summary>
    private const double Faint = 1e-15;

    /// <summary>Frames a second.</summary>
    private readonly double _rate;

    /// <summary>How much of the way to its knob the centre moves each sample.</summary>
    private readonly double _follow;

    /// <summary>The sine the stages are swept by.</summary>
    private readonly ISlowOscillator _sweep;

    /// <summary>What each stage holds, the left side's first and then the right's.</summary>
    private readonly double[] _held = new double[Rows[^1] * 2];

    /// <summary>What came out of the last stage on each side, for the feedback.</summary>
    private readonly double[] _out = new double[2];

    /// <summary>The volume the block is handed back at, the last thing it goes through.</summary>
    private readonly IEffectLevel _level = new EffectLevel();

    /// <summary>Where the centre has got to, or nought before it has been placed.</summary>
    private double _centreAt;

    /// <summary>The knobs, as single words so a thread never reads half of one.</summary>
    private float _speed = (float)RateThen;

    /// <inheritdoc cref="_speed"/>
    private float _depth = (float)DepthThen;

    /// <inheritdoc cref="_speed"/>
    private float _centre = (float)CentreThen;

    /// <inheritdoc cref="_speed"/>
    private float _feedback = (float)FeedbackThen;

    /// <inheritdoc cref="_speed"/>
    private float _stages = (float)StagesThen;

    /// <inheritdoc cref="_speed"/>
    private float _spread = (float)SpreadThen;

    /// <inheritdoc cref="_speed"/>
    private float _mix = (float)MixThen;

    /// <summary>Builds one at the rate it is about to be handed audio at.</summary>
    /// <param name="sampleRate">What the mix runs at.</param>
    /// <param name="id">Which effect this one is standing for, or nothing outside the application.</param>
    public Phase(int sampleRate, string? id = null)
    {
        Id = id ?? "";

        _rate = sampleRate > 0 ? sampleRate : 48000;
        _follow = 1.0 / Math.Max(1, FollowMs * 0.001 * _rate);
        _sweep = new SlowOscillator((int)_rate);
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public System.Collections.Generic.IReadOnlyList<string> Keys { get; } =
        new[] { Rate, Depth, Centre, Feedback, Stages, Spread, Mix, IEffectLevel.Key };

    /// <inheritdoc/>
    public double ValueOf(string? key) => key switch
    {
        Rate => _speed,
        Depth => _depth,
        Centre => _centre,
        Feedback => _feedback,
        Stages => _stages,
        Spread => _spread,
        Mix => _mix,
        IEffectLevel.Key => _level.Db,
        _ => 0
    };

    /// <inheritdoc/>
    /// <remarks>
    /// A centre set before anything has been rendered is where the sweep starts rather than
    /// somewhere to follow from, so a song opening does not slide into its own setting.
    /// </remarks>
    public void SetValue(string? key, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return;

        switch (key)
        {
            case Rate:
                _speed = (float)Math.Clamp(value, LeastRate, MostRate);
                break;

            case Depth:
                _depth = (float)Math.Clamp(value, 0, 1);
                break;

            case Centre:
                _centre = (float)Math.Clamp(value, LeastCentre, MostCentre);
                if (_centreAt <= 0) _centreAt = _centre;
                break;

            case Feedback:
                _feedback = (float)Math.Clamp(value, -MostFeedback, MostFeedback);
                break;

            case Stages:
                _stages = (float)Math.Clamp(Math.Round(value), 0, Rows.Length - 1);
                break;

            case Spread:
                _spread = (float)Math.Clamp(value, 0, MostSpread);
                break;

            case Mix:
                _mix = (float)Math.Clamp(value, 0, 1);
                break;

            case IEffectLevel.Key:
                _level.Set(value);
                break;
        }
    }

    /// <summary>The coefficient of a stage turning over at that frequency.</summary>
    /// <param name="hertz">Where the stage shifts a quarter of a turn.</param>
    private double Coefficient(double hertz)
    {
        double tangent = Math.Tan(Math.PI * Math.Clamp(hertz, 20, _rate * 0.45) / _rate);

        return (tangent - 1) / (tangent + 1);
    }

    /// <summary>One sample of one side through the row, fed back and mixed.</summary>
    /// <param name="sample">What went in.</param>
    /// <param name="side">Nought for the left, one for the right.</param>
    /// <param name="coefficient">What every stage in the row is set to for this sample.</param>
    /// <param name="stages">How many stages the row is.</param>
    /// <param name="feedback">How much of the last output goes back in.</param>
    /// <param name="mix">How much of what comes out has been through the row.</param>
    private double One(double sample, int side, double coefficient, int stages, double feedback, double mix)
    {
        double into = (sample * (1 - Math.Abs(feedback))) + (_out[side] * feedback);
        int first = side * Rows[^1];

        for (int stage = 0; stage < stages; stage++)
        {
            int at = first + stage;
            double shifted = (coefficient * into) + _held[at];

            _held[at] = into - (coefficient * shifted);
            into = shifted;
        }

        _out[side] = into;

        return (sample * (1 - mix)) + (into * mix);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The block is held to what the buffer can really take and rounded down to whole frames,
    /// because a count that is a promise rather than a measurement is how this application has
    /// crashed on the audio thread before.
    /// </remarks>
    public void Process(float[] buffer, int frames)
    {
        if (buffer is null) return;

        int block = Math.Min(frames, buffer.Length / 2);

        if (block <= 0) return;

        if (_centreAt <= 0) _centreAt = _centre;

        double speed = _speed;
        double reach = _depth * Octaves;
        double centre = _centre;
        double feedback = _feedback;
        int stages = Rows[(int)_stages];
        double apart = _spread / 360.0;
        double mix = _mix;

        for (int at = 0; at < block; at++)
        {
            _centreAt += (centre - _centreAt) * _follow;

            double left = _sweep.Step(speed);
            double right = apart <= 0 ? left : _sweep.Beside(apart);

            double leftCoefficient = Coefficient(_centreAt * Math.Pow(2, reach * left));
            double rightCoefficient = apart <= 0 ? leftCoefficient : Coefficient(_centreAt * Math.Pow(2, reach * right));

            buffer[at * 2] = (float)One(buffer[at * 2], 0, leftCoefficient, stages, feedback, mix);
            buffer[(at * 2) + 1] = (float)One(buffer[(at * 2) + 1], 1, rightCoefficient, stages, feedback, mix);
        }

        Settle(_held);
        Settle(_out);

        _level.Apply(buffer, block);
    }

    /// <summary>Puts back to nought whatever in there is not a number or is too small to hear.</summary>
    /// <param name="memory">What a row or its feedback holds.</param>
    private static void Settle(double[] memory)
    {
        for (int at = 0; at < memory.Length; at++)
            if (!double.IsFinite(memory[at]) || Math.Abs(memory[at]) < Faint) memory[at] = 0;
    }
}
