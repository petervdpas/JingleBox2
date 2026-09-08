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

    /// <summary>The knobs, as single words so a thread never reads half of one.</summary>
    private float _steps = (float)StepsThen;

    /// <inheritdoc cref="_steps"/>
    private float _cents = (float)CentsThen;

    /// <inheritdoc cref="_steps"/>
    private float _window = (float)WindowThen;

    /// <inheritdoc cref="_steps"/>
    private float _mix = (float)MixThen;

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
    public System.Collections.Generic.IReadOnlyList<string> Keys { get; } =
        new[] { Steps, Cents, Window, Mix };

    /// <inheritdoc/>
    public double ValueOf(string? key) => key switch
    {
        Steps => _steps,
        Cents => _cents,
        Window => _window,
        Mix => _mix,
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

            case Mix:
                _mix = (float)Math.Clamp(value, 0, 1);
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
        double step = (1 - ratio) / window;
        double mix = _mix;
        bool moving = semitones != 0;

        for (int at = 0; at < block; at++)
        {
            float wasLeft = buffer[at * 2];
            float wasRight = buffer[(at * 2) + 1];

            _line[_write * 2] = wasLeft;
            _line[(_write * 2) + 1] = wasRight;

            if (moving)
            {
                _phase += step;
                _phase -= Math.Floor(_phase);

                double gain = 0.5 - (0.5 * Math.Cos(2 * Math.PI * _phase));

                double other = _phase + 0.5;

                if (other >= 1) other -= 1;

                double near = Behind + (_phase * window);
                double far = Behind + (other * window);

                double left = (Read(near, 0) * gain) + (Read(far, 0) * (1 - gain));
                double right = (Read(near, 1) * gain) + (Read(far, 1) * (1 - gain));

                buffer[at * 2] = (float)((wasLeft * (1 - mix)) + (left * mix));
                buffer[(at * 2) + 1] = (float)((wasRight * (1 - mix)) + (right * mix));
            }

            _write = _write + 1 >= _room ? 0 : _write + 1;
        }
    }
}
