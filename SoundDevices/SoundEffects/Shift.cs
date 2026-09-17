using System;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;

namespace JingleBox2.SoundDevices.SoundEffects;

/// <summary>
/// A pitch shifter: what went past comes back at another pitch, at the same speed.
/// </summary>
/// <remarks>
/// **Two taps running through a delay line at the wrong speed, crossfaded.** A read that moves
/// faster than the write comes out higher and a read that moves slower comes out lower, and
/// either way it eventually runs into the write or off the end of the line; the whole of a
/// shifter like this is what you do at that moment. Here there are two taps half a window apart
/// and a raised cosine over each, so whichever tap is about to wrap is already at nothing when
/// it does. The other is at full and nobody hears the join.
///
/// The two windows sum to exactly one, which is arithmetic rather than luck: the second tap's
/// window is the first's turned over, since a raised cosine half a turn on is one minus the same
/// cosine. So it is one cosine a sample rather than two, and there is no ripple in the level
/// however the taps happen to fall.
///
/// **This is the cheap kind and it says so.** It knows nothing about the sound it is given, so a
/// window that is short enough to follow a voice is short enough to hear as a flutter on a held
/// note, and a window long enough to hold a note smears a drum. That trade is the Grain knob and
/// it is the only honest way to offer it: a phase vocoder is a different effect with a different
/// cost, and pretending a delay line is one would be a knob that does not do what it says.
///
/// **No shift at all is no effect at all.** With the taps standing still, what comes out is a
/// copy of what went in a fraction of a window late, which is a comb filter over the dry signal
/// and is not what anybody means by nought. So nought is passed straight through, with the line
/// still being written, so moving off it starts from audio that is already there.
///
/// **Two things make it more than an interval, and both are off until they are turned.** Detune
/// moves the left side down and the right side up by the same few cents, on top of whatever the
/// interval is, so a sound with no interval at all comes out as two copies pulling apart across
/// the room: the classic doubler. Feedback sends what comes out back into the line, so what was
/// moved is moved again, and again: an octave up with feedback is a note climbing out of the top
/// of itself, which is what everybody calls shimmer. What goes back is bent through the drives'
/// curve on the way, so a cascade that piles up is held rather than allowed to run away. A sample
/// that is not a number is not sent round, so it costs a window rather than the rest of the session.
///
/// Nothing here allocates, takes a lock or blocks. The line is made once, long enough for the
/// longest window this effect offers.
/// </remarks>
public sealed class Shift : ISoundEffectEngine
{
    /// <summary>How far to move it, in semitones.</summary>
    /// <remarks>
    /// Written out rather than built, so the words this effect and its manifest have to agree on
    /// can be found by looking for them. They are the same strings <c>effect.json</c> names.
    /// </remarks>
    public const string Steps = "steps";

    /// <summary>How far apart the two sides are pulled, in cents: the left down and the right up by that much.</summary>
    public const string Detune = "detune";

    /// <summary>How much of what comes out goes back in, so what was moved is moved again.</summary>
    public const string Feedback = "feedback";

    /// <summary>The furthest the sides can be pulled apart.</summary>
    public const double MostDetune = 50;

    /// <summary>The most that goes back in.</summary>
    public const double MostFeedback = 0.9;

    /// <summary>And the part of a semitone, in cents.</summary>
    /// <remarks>
    /// Its own knob rather than a finer step on the other, because the two are used differently:
    /// semitones are a musical interval somebody chooses and cents are a detune somebody dials in
    /// by ear. A hundred either way, so the fine knob alone reaches the next semitone.
    /// </remarks>
    public const string Cents = "cents";

    /// <summary>How long each window is, in milliseconds.</summary>
    public const string Window = "window";

    /// <summary>How much of what comes out has been moved.</summary>
    public const string Mix = "mix";

    /// <summary>How far it will go either way, which is two octaves.</summary>
    public const double MostSteps = 24;

    /// <summary>And how far the fine knob goes, which is a whole semitone either way.</summary>
    public const double MostCents = 100;

    /// <summary>The shortest window, under which a voice turns to metal.</summary>
    public const double LeastWindowMs = 10;

    /// <summary>The longest, which is what the line is made to hold.</summary>
    public const double MostWindowMs = 120;

    /// <summary>What a fresh one is set to, which is no shift at all.</summary>
    public const double StepsThen = 0;

    /// <inheritdoc cref="StepsThen"/>
    public const double CentsThen = 0;

    /// <inheritdoc cref="StepsThen"/>
    public const double WindowThen = 50;

    /// <inheritdoc cref="StepsThen"/>
    public const double MixThen = 1;

    /// <summary>How far behind the write the nearest tap ever gets.</summary>
    /// <remarks>
    /// A few frames rather than a window, so that at the top of its travel a tap is reading what
    /// was written a moment ago rather than a tenth of a second ago. What it has to leave room
    /// for is the sample after the one being read, since the read lands between two.
    /// </remarks>
    private const int Behind = 4;

    /// <summary>What the line is written into and read out of, interleaved stereo.</summary>
    private readonly float[] _line;

    /// <summary>How many frames it holds.</summary>
    private readonly int _room;

    /// <summary>Frames a second, which is what a window in milliseconds is turned into.</summary>
    private readonly double _rate;

    /// <summary>Where the next sample is written.</summary>
    private int _write;

    /// <summary>Where the first tap is inside its window, from nought to one.</summary>
    private double _phase;

    /// <summary>The same for the right side, which only parts from the left while there is detune.</summary>
    private double _phaseRight;

    /// <summary>What came out of each side last, before the mix, for the feedback.</summary>
    private double _backLeft;

    /// <inheritdoc cref="_backLeft"/>
    private double _backRight;

    /// <inheritdoc cref="_steps"/>
    private float _detune;

    /// <inheritdoc cref="_steps"/>
    private float _feedback;

    /// <summary>The knobs, as single words so a thread never reads half of one.</summary>
    private float _steps = (float)StepsThen;

    /// <inheritdoc cref="_steps"/>
    private float _cents = (float)CentsThen;

    /// <inheritdoc cref="_steps"/>
    private float _window = (float)WindowThen;

    /// <inheritdoc cref="_steps"/>
    private float _mix = (float)MixThen;

    /// <summary>The volume the block is handed back at, the last thing it goes through.</summary>
    private readonly IEffectLevel _level = new EffectLevel();

    /// <summary>
    /// Makes the line at the longest window this effect offers.
    /// </summary>
    /// <remarks>
    /// Once, and never again: growing it later would mean allocating on the audio thread, which
    /// is the one place nothing may allocate.
    /// </remarks>
    /// <param name="sampleRate">What the mix is running at, since a window is in milliseconds.</param>
    /// <param name="id">Which effect this one is standing for, or nothing outside the application.</param>
    public Shift(int sampleRate, string? id = null)
    {
        Id = id ?? "";

        _rate = sampleRate > 0 ? sampleRate : 48000;
        _room = (int)(MostWindowMs * 0.001 * _rate) + Behind + 4;
        _line = new float[_room * 2];
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public string? Preset { get; set; }

    /// <inheritdoc/>
    public System.Collections.Generic.IReadOnlyList<string> Keys { get; } =
        new[] { Steps, Cents, Window, Mix, Detune, Feedback, IEffectLevel.Key };

    /// <inheritdoc/>
    public double ValueOf(string? key) => key switch
    {
        Steps => _steps,
        Cents => _cents,
        Window => _window,
        Mix => _mix,
        Detune => _detune,
        Feedback => _feedback,
        IEffectLevel.Key => _level.Db,
        _ => 0
    };

    /// <inheritdoc/>
    public void SetValue(string? key, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return;

        switch (key)
        {
            case Steps:
                _steps = (float)Math.Clamp(value, -MostSteps, MostSteps);
                break;

            case Cents:
                _cents = (float)Math.Clamp(value, -MostCents, MostCents);
                break;

            case Window:
                _window = (float)Math.Clamp(value, LeastWindowMs, MostWindowMs);
                break;

            case IEffectLevel.Key:
                _level.Set(value);
                break;

            case Mix:
                _mix = (float)Math.Clamp(value, 0, 1);
                break;

            case Detune:
                _detune = (float)Math.Clamp(value, 0, MostDetune);
                break;

            case Feedback:
                _feedback = (float)Math.Clamp(value, 0, MostFeedback);
                break;
        }
    }

    /// <summary>How much faster the read runs than the write.</summary>
    /// <param name="semitones">The whole interval, semitones and cents together.</param>
    private static double Ratio(double semitones) => Math.Pow(2, semitones / 12);

    /// <summary>One sample of one side, read between two frames of the line.</summary>
    /// <param name="from">How far behind the write to read, in frames.</param>
    /// <param name="side">Nought for the left, one for the right.</param>
    private double Read(double from, int side)
    {
        double at = _write - from;

        if (at < 0) at += _room;

        if (at >= _room) at -= _room;

        int first = (int)at;
        double along = at - first;
        int second = first + 1 >= _room ? 0 : first + 1;

        return _line[(first * 2) + side] * (1 - along) + _line[(second * 2) + side] * along;
    }

    /// <summary>One side read through both taps, each under its half of the raised cosine.</summary>
    /// <param name="phase">Where the first tap is inside its window, from nought to one.</param>
    /// <param name="window">How long the window is, in frames.</param>
    /// <param name="side">Nought for the left, one for the right.</param>
    private double Tapped(double phase, double window, int side)
    {
        double gain = 0.5 - (0.5 * Math.Cos(2 * Math.PI * phase));

        double other = phase + 0.5;

        if (other >= 1) other -= 1;

        double near = Behind + (phase * window);
        double far = Behind + (other * window);

        return (Read(near, side) * gain) + (Read(far, side) * (1 - gain));
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

        double semitones = _steps + (_cents / 100.0);
        double ratio = Ratio(semitones);
        double window = Math.Clamp(_window * 0.001 * _rate, 1, _room - Behind - 2);
        double detune = _detune / 100.0;
        double step = (1 - ratio) / window;
        double stepRight = detune == 0 ? step : (1 - Ratio(semitones + detune)) / window;
        double stepLeft = detune == 0 ? step : (1 - Ratio(semitones - detune)) / window;
        double mix = _mix;
        double feedback = _feedback;
        bool moving = semitones != 0 || detune != 0;

        for (int at = 0; at < block; at++)
        {
            float wasLeft = buffer[at * 2];
            float wasRight = buffer[(at * 2) + 1];

            if (moving && feedback > 0)
            {
                _line[_write * 2] = (float)(wasLeft + Audio.TangentSwitch.Now.Of(_backLeft * feedback));
                _line[(_write * 2) + 1] = (float)(wasRight + Audio.TangentSwitch.Now.Of(_backRight * feedback));
            }
            else
            {
                _line[_write * 2] = wasLeft;
                _line[(_write * 2) + 1] = wasRight;
            }

            if (moving)
            {
                _phase += stepLeft;
                _phase -= Math.Floor(_phase);

                if (detune == 0)
                {
                    _phaseRight = _phase;
                }
                else
                {
                    _phaseRight += stepRight;
                    _phaseRight -= Math.Floor(_phaseRight);
                }

                double left = Tapped(_phase, window, 0);
                double right = Tapped(_phaseRight, window, 1);

                _backLeft = double.IsFinite(left) ? left : 0;
                _backRight = double.IsFinite(right) ? right : 0;

                buffer[at * 2] = (float)((wasLeft * (1 - mix)) + (left * mix));
                buffer[(at * 2) + 1] = (float)((wasRight * (1 - mix)) + (right * mix));
            }
            else
            {
                _backLeft = 0;
                _backRight = 0;
            }

            _write = _write + 1 >= _room ? 0 : _write + 1;
        }

        _level.Apply(buffer, block);
    }
}
