using System;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;

namespace JingleBox2.SoundDevices.SoundEffects;

/// <summary>
/// A ring modulator: the signal multiplied by a tone, which is the voice of a machine.
/// </summary>
/// <remarks>
/// **Multiplication rather than mixing, and that difference is the whole sound.** Adding a tone
/// to a voice leaves the voice with a tone under it; multiplying them leaves neither, and what
/// comes out is the sum and difference of every partial in one with the tone in the other. The
/// pitch of the original is gone, since none of those sums are a harmonic series any more, and a
/// sound with no pitch and all of its rhythm is what everybody recognises as a robot.
///
/// It is the oldest trick in this book and it is still the one that works. A vocoder is the other
/// way to the same place, and it is a different effect: a bank of filters and a carrier to play
/// through them, which needs a second signal and a page of controls. This needs a knob.
///
/// **The carrier is a tone or a square and they are two different machines.** A sine gives the
/// clean two-sideband sound; a square is a sine plus every odd harmonic of it, so it gives that
/// sound several times over at once, which is harsher and reads as older hardware.
///
/// **Under about twenty a second it stops being ring modulation to the ear.** The sidebands are
/// too close to the original to hear as separate, and what is left is the level going up and down,
/// which is a tremolo that passes through nought and comes out the other side. That is not a
/// second mode and there is nothing here that switches: it is the same arithmetic heard slowly,
/// and it is worth knowing about because it is most of what the bottom of the knob is for.
///
/// The bit depth is the other half of a machine's voice and costs one rounding: what a converter
/// does with the bits it has not got. It is after the modulation rather than before it, so what
/// gets stepped is the sound you are keeping.
///
/// Nothing here allocates, takes a lock or blocks. There is no line and no memory beyond where
/// the two carriers have got to.
/// </remarks>
public sealed class Ring : ISoundEffectEngine
{
    /// <summary>What the signal is multiplied by, in cycles a second.</summary>
    /// <remarks>
    /// Written out rather than built, so the words this effect and its manifest have to agree on
    /// can be found by looking for them. They are the same strings <c>effect.json</c> names.
    /// </remarks>
    public const string Carrier = "carrier";

    /// <summary>Whether that is a tone or a square.</summary>
    public const string Square = "square";

    /// <summary>How far apart the two sides are carried, in cycles a second.</summary>
    /// <remarks>
    /// One carrier a side, a little apart, which is the difference between a sound sitting in the
    /// middle of the head and one standing across the room. Nought is one carrier and dead centre.
    /// </remarks>
    public const string Spread = "spread";

    /// <summary>How many bits are taken away afterwards.</summary>
    public const string Crush = "crush";

    /// <summary>How much of what comes out has been modulated.</summary>
    public const string Mix = "mix";

    /// <summary>The slowest carrier, which is a tremolo rather than a modulation.</summary>
    public const double LeastCarrier = 1;

    /// <summary>The fastest, which is past where anything of the original is left.</summary>
    public const double MostCarrier = 2000;

    /// <summary>How far apart the two sides can be carried, either way.</summary>
    public const double MostSpread = 40;

    /// <summary>How many bits the crush knob takes away at its top.</summary>
    /// <remarks>
    /// Down to two, which is where a signal is four levels and every consonant is a click. The
    /// knob is not in bits: it is nought to one, because what somebody is choosing is how broken
    /// it sounds rather than a number a converter would recognise.
    /// </remarks>
    public const double Bits = 14;

    /// <summary>What a fresh one is set to: a plain robot, dead centre, whole.</summary>
    public const double CarrierThen = 200;

    /// <inheritdoc cref="CarrierThen"/>
    public const double SquareThen = 0;

    /// <inheritdoc cref="CarrierThen"/>
    public const double SpreadThen = 0;

    /// <inheritdoc cref="CarrierThen"/>
    public const double CrushThen = 0;

    /// <inheritdoc cref="CarrierThen"/>
    public const double MixThen = 1;

    /// <summary>Frames a second, which is what a carrier in cycles a second is turned into.</summary>
    private readonly double _rate;

    /// <summary>Where each carrier has got to, in radians.</summary>
    private double _left;

    /// <inheritdoc cref="_left"/>
    private double _right;

    /// <summary>The knobs, as single words so a thread never reads half of one.</summary>
    private float _carrier = (float)CarrierThen;

    /// <inheritdoc cref="_carrier"/>
    private float _square = (float)SquareThen;

    /// <inheritdoc cref="_carrier"/>
    private float _spread = (float)SpreadThen;

    /// <inheritdoc cref="_carrier"/>
    private float _crush = (float)CrushThen;

    /// <inheritdoc cref="_carrier"/>
    private float _mix = (float)MixThen;

    /// <summary>Takes the rate the mix runs at.</summary>
    /// <param name="sampleRate">What the mix is running at.</param>
    /// <param name="id">Which effect this one is standing for, or nothing outside the application.</param>
    public Ring(int sampleRate, string? id = null)
    {
        Id = id ?? "";

        _rate = sampleRate > 0 ? sampleRate : 48000;
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public System.Collections.Generic.IReadOnlyList<string> Keys { get; } =
        new[] { Carrier, Square, Spread, Crush, Mix };

    /// <inheritdoc/>
    public double ValueOf(string? key) => key switch
    {
        Carrier => _carrier,
        Square => _square,
        Spread => _spread,
        Crush => _crush,
        Mix => _mix,
        _ => 0
    };

    /// <inheritdoc/>
    public void SetValue(string? key, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return;

        switch (key)
        {
            case Carrier:
                _carrier = (float)Math.Clamp(value, LeastCarrier, MostCarrier);
                break;

            case Square:
                _square = (float)Math.Clamp(value, 0, 1);
                break;

            case Spread:
                _spread = (float)Math.Clamp(value, -MostSpread, MostSpread);
                break;

            case Crush:
                _crush = (float)Math.Clamp(value, 0, 1);
                break;

            case Mix:
                _mix = (float)Math.Clamp(value, 0, 1);
                break;
        }
    }

    /// <summary>Where one carrier stands, as a tone or as a square.</summary>
    /// <param name="phase">How far round it is, in radians.</param>
    /// <param name="square">Whether it is a square rather than a tone.</param>
    private static double Carried(double phase, bool square) =>
        square ? (phase < Math.PI ? 1 : -1) : Math.Sin(phase);

    /// <summary>That sample with bits taken off it, or as it is where none are.</summary>
    /// <param name="value">The sample.</param>
    /// <param name="levels">How many steps there are either side of nought, or nought for all of them.</param>
    private static double Stepped(double value, double levels) =>
        levels <= 0 ? value : Math.Round(value * levels) / levels;

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

        bool square = _square >= 0.5f;
        double mix = _mix;
        double half = _spread * 0.5;
        double stepLeft = 2 * Math.PI * Math.Max(0, _carrier - half) / _rate;
        double stepRight = 2 * Math.PI * Math.Max(0, _carrier + half) / _rate;
        double levels = _crush <= 0 ? 0 : Math.Pow(2, (16 - (Bits * _crush)) - 1);

        for (int at = 0; at < block; at++)
        {
            _left += stepLeft;
            _right += stepRight;

            if (_left >= 2 * Math.PI) _left -= 2 * Math.PI;

            if (_right >= 2 * Math.PI) _right -= 2 * Math.PI;

            float wasLeft = buffer[at * 2];
            float wasRight = buffer[(at * 2) + 1];

            double left = Stepped(wasLeft * Carried(_left, square), levels);
            double right = Stepped(wasRight * Carried(_right, square), levels);

            buffer[at * 2] = (float)((wasLeft * (1 - mix)) + (left * mix));
            buffer[(at * 2) + 1] = (float)((wasRight * (1 - mix)) + (right * mix));
        }
    }
}
