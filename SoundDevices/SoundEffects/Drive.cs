using System;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;

namespace JingleBox2.SoundDevices.SoundEffects;

/// <summary>
/// Drive: the signal pushed until it stops being polite.
/// </summary>
/// <remarks>
/// The third engine. What it does is one line of arithmetic and everything around that line is
/// the reason it is worth having as an effect rather than as a knob: a curve on its own is a
/// fuzz box, and what makes drive usable on a whole track is what is done before and after it.
///
/// **Before it is a tilt.** Distortion is loudest where the signal is, so driving a full mix
/// turns the bass into mud and takes the top with it. A tilt in front lets you choose what gets
/// bitten: leaning it up drives the top and leaves the weight alone, which is the sound of a
/// mix through a desk, and leaning it down drives the bottom, which is the sound of an amplifier.
/// One control rather than a filter each way, because it is a lean and not a crossover.
///
/// **After it is the level it cost.** A curve that flattens the peaks makes everything quieter,
/// so a drive without a makeup is a drive nobody can compare with the sound before it, and
/// deciding whether you like an effect while it is also six decibels down is not a comparison at
/// all. The makeup is worked out from the curve rather than measured, so it is exact and free:
/// what a tanh does to full scale is known.
///
/// The fade at the bottom is a trap this codebase has already paid for once, recorded in the
/// synth's own drive. The makeup levels the curve at full scale and nowhere else, so leaving the
/// minimum it steps by 1.6 decibels the moment the knob comes off its stop, which reads as the
/// effect switching on rather than as a knob being turned. It is faded in over the first unit of
/// the range, so a drive of two and above is exactly what it always was.
///
/// **The bias is what makes it a character rather than a curve.** A symmetrical curve adds odd
/// harmonics only, which is the sound of a transistor; leaning the signal off centre before it
/// is bitten adds the even ones, which is what a valve does and is most of why people like them.
/// It is taken back out afterwards, or the effect would put a step in the output that every
/// speaker in the building would try to reproduce.
///
/// Nothing here allocates, takes a lock or blocks, which is what <see cref="ISoundEffectEngine"/>
/// asks of anything on the audio path.
/// </remarks>
public sealed class Drive : ISoundEffectEngine
{
    /// <summary>How hard the signal is pushed into the curve.</summary>
    /// <remarks>
    /// Written out rather than built, so the words this effect and its manifest have to agree on
    /// can be found by looking for them. They are the same strings <c>effect.json</c> names.
    /// </remarks>
    public const string Amount = "amount";

    /// <summary>Which end of the signal gets bitten: down is weight, up is bite.</summary>
    public const string Tilt = "tilt";

    /// <summary>How far the signal leans off centre before it is bitten.</summary>
    public const string Bias = "bias";

    /// <summary>What comes out, after the curve has been paid for.</summary>
    public const string Level = "level";

    /// <summary>How much of what comes out is driven rather than what went in.</summary>
    public const string Mix = "mix";

    /// <summary>Whether the curve is paid for by its peak or by its loudness.</summary>
    public const string Even = "even";

    /// <summary>No drive at all, which is what one means.</summary>
    public const double LeastAmount = 1;

    /// <summary>And as far as it goes, which is well past useful and is the point.</summary>
    public const double MostAmount = 24;

    /// <summary>How far the tilt leans either way.</summary>
    public const double MostTilt = 1;

    /// <summary>How far off centre the bias goes, which is not far before it is a fault.</summary>
    public const double MostBias = 0.5;

    /// <summary>The most the output can be lifted, in decibels.</summary>
    public const double MostLevelDb = 12;

    /// <summary>And the most it can be taken down.</summary>
    public const double LeastLevelDb = -24;

    /// <summary>
    /// Over how much of the range the makeup is faded in.
    /// </summary>
    /// <remarks>
    /// The same rule and the same number as the synth's drive, for the same reason: the makeup
    /// levels the curve at full scale and nowhere else, so without this the knob steps as it
    /// leaves its stop.
    /// </remarks>
    private const double FadeIn = 1.0;

    /// <summary>Where the tilt turns over, which is about where a mix stops being weight.</summary>
    private const double TiltHz = 700;


    /// <inheritdoc/>
    public string Id { get; }

    /// <summary>Every parameter, in the order a face reads them.</summary>
    private static readonly string[] Words = { Amount, Tilt, Bias, Level, Mix, Even };

    /// <inheritdoc/>
    public System.Collections.Generic.IReadOnlyList<string> Keys => Words;

    /// <summary>How much of the tilt filter one sample carries over, worked out once.</summary>
    private readonly double _lean;

    /// <summary>What the tilt filter is holding, per side.</summary>
    private readonly double[] _low = new double[2];

    /// <summary>What went into the centring filter last, per side.</summary>
    private readonly double[] _was = new double[2];

    /// <summary>And what came out of it, per side.</summary>
    private readonly double[] _centred = new double[2];

    /// <summary>
    /// How much of the offset one sample carries over, which is what makes it a slow filter.
    /// </summary>
    /// <remarks>
    /// Near one, so it takes out only what does not move at all and leaves the lowest note
    /// anybody plays alone. Lower and it would be a high pass with an opinion about the bass.
    /// </remarks>
    private const double Settling = 0.9995;

    /// <summary>How hard it is pushed.</summary>
    private volatile float _amount = 1;

    /// <summary>Which end gets it.</summary>
    private volatile float _tilt;

    /// <summary>How far off centre.</summary>
    private volatile float _bias;

    /// <summary>What comes out, in decibels.</summary>
    private volatile float _level;

    /// <summary>How much of it is the driven signal.</summary>
    private volatile float _mix = 1;

    /// <summary>Whether the curve is paid for by its loudness rather than by its peak.</summary>
    /// <remarks>
    /// False is what this effect has always done and is the default, so a chain written before
    /// this switch existed sounds exactly as it did.
    /// </remarks>
    private volatile bool _even;

    /// <summary>What the curve is costing in loudness, measured off what has gone past.</summary>
    /// <remarks>
    /// Kept whether or not the switch is on, since it holds two numbers and no work is done in it
    /// until somebody asks. Fed only while the switch is on, which is the part that costs
    /// anything on the audio thread.
    /// </remarks>
    private readonly ILoudnessMakeup _loudness;

    /// <summary>Builds one at the rate it is about to be handed audio at.</summary>
    /// <param name="sampleRate">What the host is running at.</param>
    /// <param name="id">Which effect this is standing for, or nothing for one built by hand.</param>
    public Drive(int sampleRate, string? id = null)
    {
        double rate = sampleRate <= 0 ? 44100 : sampleRate;

        _lean = 1.0 - Math.Exp(-2.0 * Math.PI * TiltHz / rate);
        _loudness = new LoudnessMakeup(sampleRate);

        Id = id ?? "";
    }

    /// <inheritdoc/>
    public double ValueOf(string? key) => key switch
    {
        Amount => _amount,
        Tilt => _tilt,
        Bias => _bias,
        Level => _level,
        Mix => _mix,
        Even => _even ? 1 : 0,
        _ => 0
    };

    /// <inheritdoc/>
    public void SetValue(string? key, double value)
    {
        if (double.IsNaN(value)) return;

        switch (key)
        {
            case Amount:
                _amount = (float)Math.Clamp(value, LeastAmount, MostAmount);
                break;

            case Tilt:
                _tilt = (float)Math.Clamp(value, -MostTilt, MostTilt);
                break;

            case Bias:
                _bias = (float)Math.Clamp(value, -MostBias, MostBias);
                break;

            case Level:
                _level = (float)Math.Clamp(value, LeastLevelDb, MostLevelDb);
                break;

            case Mix:
                _mix = (float)Math.Clamp(value, 0, 1);
                break;

            case Even:
                _even = value >= 0.5;
                break;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The block is held to what the buffer can really take and rounded down to whole frames,
    /// the same rule the mixer keeps.
    ///
    /// **It runs whatever its knobs say, including when they say nothing.** The fast path that
    /// was here was a tick waiting to happen: a block skipped is a block the tilt and the
    /// centring filter did not see, so their memory is from whenever the effect was last doing
    /// something, and the first block after a knob crosses the threshold starts from that rather
    /// than from the signal. A knob leaving its stop is then a jump rather than a fade.
    ///
    /// It costs nothing to be right here, because at no drive and no tilt the arithmetic is the
    /// identity: the fade makes the curve contribute nothing, the tilt's two halves add back up
    /// to what went in, and what comes out is the sample that went in, exactly.
    /// </remarks>
    /// <param name="buffer">Interleaved stereo, worked on in place.</param>
    /// <param name="frames">How many frames of it are real.</param>
    public void Process(float[] buffer, int frames)
    {
        if (buffer == null || frames <= 0) return;

        int count = Math.Min(frames, buffer.Length / 2);

        if (count <= 0) return;

        double amount = _amount;
        double mix = _mix;
        double level = Math.Pow(10, _level / 20.0);

        bool even = _even;
        double makeup = even ? 1 : Makeup(amount);
        double tilt = _tilt;
        double bias = _bias;

        for (int frame = 0; frame < count; frame++)
        {
            int at = frame * 2;

            if (even) makeup = _loudness.Makeup;

            buffer[at] = (float)One(buffer[at], 0, amount, makeup, tilt, bias, level, mix, even);
            buffer[at + 1] = (float)One(buffer[at + 1], 1, amount, makeup, tilt, bias, level, mix, even);
        }
    }

    /// <summary>
    /// What the curve costs in level, undone.
    /// </summary>
    /// <remarks>
    /// Worked out from the curve rather than measured, since what a tanh does to full scale is
    /// known exactly and measuring it would be arithmetic with a delay on it.
    /// </remarks>
    /// <param name="amount">How hard it is being pushed.</param>
    public static double Makeup(double amount) =>
        amount <= LeastAmount ? 1 : 1.0 / Math.Tanh(amount);

    /// <summary>
    /// How much of the driven signal is used, at the bottom of the range.
    /// </summary>
    /// <remarks>
    /// **The fade is on the whole curve and not on the makeup**, which is where it was first and
    /// is why the knob still stepped: the makeup levels the curve at full scale and nowhere
    /// else, so fading the makeup in leaves the curve itself arriving at full strength. At a
    /// drive of 1.05 that is a fifth of the level gone the moment the knob leaves its stop,
    /// measured, which reads as the effect switching on rather than as a knob being turned.
    ///
    /// The synth's own drive had this right and it is the same rule here: blend between what
    /// went in and what came out over the first unit of the range.
    /// </remarks>
    /// <param name="amount">How hard it is being pushed.</param>
    public static double Faded(double amount) =>
        Math.Clamp((amount - LeastAmount) / FadeIn, 0, 1);

    /// <summary>
    /// Takes the offset back out of one side.
    /// </summary>
    /// <remarks>
    /// **A filter rather than arithmetic**, and the arithmetic is why: leaning the signal by a
    /// known amount does not put a known offset on the output, because the curve is not linear.
    /// Driven hard, a signal leaned by 0.4 spends most of its time against the top of the curve,
    /// so what comes out is nearly a constant and what a subtraction of <c>tanh(bias * amount)</c>
    /// leaves is a step three quarters of full scale, which is what the first version did and
    /// what the measurement caught.
    ///
    /// What is left when a signal is taken away from itself a moment ago is whatever moved, and
    /// an offset is precisely the part that does not. It costs two numbers a side.
    /// </remarks>
    /// <param name="side">Which side's memory to use.</param>
    /// <param name="sample">What came off the curve.</param>
    private double Centre(int side, double sample)
    {
        double answer = sample - _was[side] + Settling * _centred[side];

        _was[side] = sample;
        _centred[side] = answer;

        return answer;
    }

    /// <summary>
    /// One sample of one side: leaned, biased, bitten, centred, levelled and mixed back.
    /// </summary>
    /// <param name="sample">What went in.</param>
    /// <param name="side">Which side's tilt memory to use.</param>
    /// <param name="amount">How hard it is pushed.</param>
    /// <param name="makeup">What that costs in level, undone.</param>
    /// <param name="tilt">Which end gets it.</param>
    /// <param name="bias">How far off centre it leans first.</param>
    /// <param name="level">What comes out, as a multiplier.</param>
    /// <param name="mix">How much of the result comes out.</param>
    /// <param name="even">Whether the followers are being kept, which only the loudness makeup needs.</param>
    private double One(double sample, int side, double amount, double makeup,
                       double tilt, double bias, double level, double mix, bool even)
    {
        _low[side] += (sample - _low[side]) * _lean;

        double high = sample - _low[side];

        double leaned = tilt >= 0
            ? _low[side] * (1 - tilt * 0.75) + high * (1 + tilt)
            : _low[side] * (1 - tilt) + high * (1 + tilt * 0.75);

        double bitten = Math.Tanh((leaned + bias) * amount);

        double centred = Centre(side, bitten);

        if (even) _loudness.Saw(leaned, centred);

        double faded = Faded(amount);

        double driven = leaned + (centred * makeup - leaned) * faded;

        double wet = driven * level;

        return mix >= 1 ? wet : sample + (wet - sample) * mix;
    }
}
