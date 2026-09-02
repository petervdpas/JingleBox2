using System;
using JingleBox2.Devices.SoundEffects.Interfaces;

namespace JingleBox2.Devices.SoundEffects;

/// <summary>
/// A delay: what went past comes back later, quieter each time.
/// </summary>
/// <remarks>
/// The plainest effect there is and the one worth building first, because everything about
/// having an effect at all is visible in it: a line of audio kept, a knob that moves where it is
/// read from, and a path back into itself.
///
/// The time glides rather than jumps. A delay line read from a different place on the next
/// sample is a click, and every hardware delay ever made either crossfades or slides; sliding is
/// the one that sounds like something rather than like a fault, and it is what a hand turning
/// the knob expects. A time set before anything has been rendered is where it starts rather than
/// somewhere to glide from, so a song opening does not smear its first repeats.
///
/// The repeats darken, which is what <c>damp</c> is: one pole on what comes out of the line,
/// which is both what you hear and what goes back in, so each pass round loses a little more of
/// the top. In the loop rather than only on the way out, because that is the difference between
/// repeats that fall away into the dark and repeats that stay bright until they stop. Not a
/// calibrated frequency and it does not pretend to be one.
///
/// The read wraps at both ends. A position a hair below nought is one whole line short, and
/// adding the line back to it lands on the length itself once the arithmetic rounds, which is one
/// frame past the end: it took an eight thousand frame block to find, and it is the sort of thing
/// that shows up as a crash on somebody else's buffer size.
///
/// Nothing here allocates, takes a lock or blocks, which is what <see cref="IEffectEngine"/>
/// asks of anything on the audio path. The line is made once, at the longest time the effect
/// offers.
/// </remarks>
public sealed class Delay : IEffectEngine
{
    /// <summary>How long the repeat is, in milliseconds.</summary>
    /// <remarks>
    /// Written out rather than built, so the words this effect and its manifest have to agree on
    /// can be found by looking for them. They are the same strings <c>effect.json</c> names.
    /// </remarks>
    public const string Time = "time";

    /// <summary>How much of the repeat goes back in, which is how many repeats there are.</summary>
    public const string Feedback = "feedback";

    /// <summary>How much of what comes out is the repeat rather than what went in.</summary>
    public const string Mix = "mix";

    /// <summary>How much of the top each repeat loses.</summary>
    public const string Damp = "damp";

    /// <summary>The shortest repeat, under which it stops being a delay and starts being a tone.</summary>
    public const double LeastMs = 10;

    /// <summary>The longest, which is what the line is made to hold.</summary>
    public const double MostMs = 2000;

    /// <summary>
    /// Past this and the repeats grow instead of falling away.
    /// </summary>
    /// <remarks>
    /// Deliberately under one. A delay at unity never stops, and one over it is a fault that
    /// arrives as a rising howl at whatever level the mix is at. Somebody who wants that has a
    /// mixer.
    /// </remarks>
    public const double MostFeedback = 0.95;

    /// <summary>What a fresh one is set to: a plain quarter-ish repeat, a third of the way in.</summary>
    public const double TimeThen = 375;

    /// <inheritdoc cref="TimeThen"/>
    public const double FeedbackThen = 0.35;

    /// <inheritdoc cref="TimeThen"/>
    public const double MixThen = 0.3;

    /// <inheritdoc cref="TimeThen"/>
    public const double DampThen = 0.3;

    /// <summary>How quickly the time follows the knob, as a fraction of the way per sample.</summary>
    /// <remarks>
    /// Worked out from the rate so it is the same glide on every sound card: about thirty
    /// milliseconds, which is long enough not to click and short enough that the knob feels
    /// attached to what you hear.
    /// </remarks>
    private const double GlideMs = 30;

    /// <summary>What the line is written into and read out of, interleaved stereo.</summary>
    private readonly float[] _line;

    /// <summary>How many frames it holds, which is the longest repeat there can be.</summary>
    private readonly int _room;

    /// <summary>Frames a second, which is what a time in milliseconds is turned into.</summary>
    private readonly double _rate;

    /// <summary>How far the read follows the knob each sample.</summary>
    private readonly double _glide;

    /// <summary>Where the next sample is written.</summary>
    private int _write;

    /// <summary>Where the read is, in frames behind the write, and where it is going.</summary>
    private double _now;

    /// <inheritdoc cref="_now"/>
    private double _want = TimeThen;

    /// <summary>The one pole in each channel's feedback path.</summary>
    private double _dampedLeft;

    /// <inheritdoc cref="_dampedLeft"/>
    private double _dampedRight;

    /// <summary>The knobs, as single words so a thread never reads half of one.</summary>
    private float _feedback = (float)FeedbackThen;

    /// <inheritdoc cref="_feedback"/>
    private float _mix = (float)MixThen;

    /// <inheritdoc cref="_feedback"/>
    private float _damp = (float)DampThen;

    /// <summary>True once a block has been worked on, which is what makes the time glide.</summary>
    private volatile bool _running;

    /// <summary>
    /// Makes the line at the longest repeat this effect offers.
    /// </summary>
    /// <remarks>
    /// Once, and never again: growing it later would mean allocating on the audio thread, which
    /// is the one place nothing may allocate. Two seconds at any rate anybody runs is a few
    /// hundred kilobytes.
    /// </remarks>
    /// <param name="sampleRate">What the mix is running at, since a time is in milliseconds.</param>
    /// <param name="id">Which effect this one is standing for, or nothing outside the application.</param>
    public Delay(int sampleRate, string? id = null)
    {
        Id = id ?? "";

        _rate = sampleRate > 0 ? sampleRate : 48000;
        _room = (int)(MostMs * 0.001 * _rate) + 2;
        _line = new float[_room * 2];
        _now = Frames(TimeThen);
        _glide = 1.0 / Math.Max(1, GlideMs * 0.001 * _rate);
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public System.Collections.Generic.IReadOnlyList<string> Keys { get; } =
        new[] { Time, Feedback, Damp, Mix };

    /// <summary>That many milliseconds as frames, held inside the line.</summary>
    /// <param name="ms">The time in milliseconds.</param>
    private double Frames(double ms) => Math.Clamp(ms * 0.001 * _rate, 1, _room - 2);

    /// <inheritdoc/>
    public double ValueOf(string? key) => key switch
    {
        Time => _want,
        Feedback => _feedback,
        Mix => _mix,
        Damp => _damp,
        _ => 0
    };

    /// <inheritdoc/>
    public void SetValue(string? key, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return;

        switch (key)
        {
            case Time:
                _want = Math.Clamp(value, LeastMs, MostMs);
                if (!_running) _now = Frames(_want);
                break;

            case Feedback:
                _feedback = (float)Math.Clamp(value, 0, MostFeedback);
                break;

            case Mix:
                _mix = (float)Math.Clamp(value, 0, 1);
                break;

            case Damp:
                _damp = (float)Math.Clamp(value, 0, 1);
                break;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The block is held to what the buffer can really take and rounded down to whole frames,
    /// because a count that is a promise rather than a measurement is how this application has
    /// crashed on the audio thread before. Nothing is asked of the caller beyond a buffer.
    /// </remarks>
    public void Process(float[] buffer, int frames)
    {
        if (buffer is null) return;

        _running = true;

        int block = Math.Min(frames, buffer.Length / 2);

        if (block <= 0) return;

        double target = Frames(_want);
        double feedback = _feedback;
        double mix = _mix;
        double keep = 1 - _damp * 0.9;

        for (int at = 0; at < block; at++)
        {
            _now += (target - _now) * _glide;

            double back = Math.Clamp(_now, 1, _room - 2);
            double from = _write - back;

            if (from < 0) from += _room;

            if (from >= _room) from -= _room;

            int first = (int)from;
            double along = from - first;
            int second = first + 1 >= _room ? 0 : first + 1;

            double left = _line[first * 2] * (1 - along) + _line[second * 2] * along;
            double right = _line[first * 2 + 1] * (1 - along) + _line[second * 2 + 1] * along;

            _dampedLeft += (left - _dampedLeft) * keep;
            _dampedRight += (right - _dampedRight) * keep;

            float wasLeft = buffer[at * 2];
            float wasRight = buffer[at * 2 + 1];

            _line[_write * 2] = (float)(wasLeft + _dampedLeft * feedback);
            _line[_write * 2 + 1] = (float)(wasRight + _dampedRight * feedback);

            _write = _write + 1 >= _room ? 0 : _write + 1;

            buffer[at * 2] = (float)(wasLeft * (1 - mix) + _dampedLeft * mix);
            buffer[at * 2 + 1] = (float)(wasRight * (1 - mix) + _dampedRight * mix);
        }
    }
}
