using System;

namespace JingleBox2.Midi;

/// <summary>
/// Works out what kind of control is sending, from the values it sends.
/// </summary>
/// <remarks>
/// A MIDI message carries a controller number and a value between nought and a hundred and
/// twenty seven, and says nothing at all about the thing that sent it. A button, a fader and an
/// endless encoder are the same three bytes. There is no way to ask, and no device that
/// volunteers it.
///
/// What they do differ in is the values, and unmistakably:
/// <code>
/// only ever 0 and 127                a button
/// the same number over and over      an encoder, counting notches rather than saying where it is
/// numbers that walk                  a knob or a fader, saying where it is
/// </code>
///
/// An encoder is the one that matters, because read as a position it does not merely feel wrong,
/// it is wrong: every anticlockwise notch of one convention reads as very nearly full scale, so
/// the parameter slams to the top on the way down. And which convention a given encoder uses is
/// itself unstandardised, which the resting value gives away: one counts from the middle of the
/// range and the other from either end.
///
/// Three messages settles it, which is about thirty milliseconds of a hand moving, and nothing
/// is applied until it has. Held back rather than guessed at, because a guess that is wrong for
/// an encoder is a parameter thrown to one end of its range in front of you.
/// </remarks>
public sealed class ControlSense
{
    /// <summary>How many values it takes. Three of anything is a pattern; two is a coincidence.</summary>
    private const int Enough = 3;

    /// <summary>How far from the middle of the range still counts as the middle.</summary>
    private const int Near = 4;

    /// <summary>How far from either end still counts as the end.</summary>
    private const int Edge = 4;

    /// <summary>The top of a continuous controller's range, and what a button's press reads as.</summary>
    private const int Top = 127;

    /// <summary>The values so far. Never more than <see cref="Enough"/>: after that it has decided.</summary>
    private readonly int[] _seen = new int[Enough];

    /// <summary>How many of them have arrived.</summary>
    private int _count;

    /// <summary>What it decided, or nothing while it is still listening.</summary>
    public ControlPickup? Pickup { get; private set; }

    /// <summary>Which way the encoder counts, when it decided there is one.</summary>
    public ControlTurn Turn { get; private set; } = ControlTurn.Offset;

    /// <summary>
    /// Another value arrived.
    /// </summary>
    /// <returns>True once it knows, which is also the moment it stops paying attention.</returns>
    public bool Saw(int value)
    {
        if (Pickup is not null) return true;

        _seen[_count++] = value;

        if (_count < Enough) return false;

        Pickup = Decide();

        return true;
    }

    /// <summary>
    /// What those three values say about the thing that sent them.
    /// </summary>
    /// <remarks>
    /// Asked in this order, and the order is the whole of it.
    ///
    /// The same number again and again while a hand is turning is not a position, it is a count
    /// of notches: one notch, one notch, one notch. That is asked first, because the number an
    /// encoder repeats is very often 127, and read the other way round 127 three times is a
    /// button being held down.
    ///
    /// Which convention the encoder counts in is given away by the number it repeats.
    /// <see cref="ControlTurn.Offset"/> counts from the middle of the range, so it rests near
    /// <see cref="Still"/> and never on it, since the middle itself is what it sends when it is
    /// standing still. <see cref="ControlTurn.Twos"/> counts from either end, and nought is that
    /// convention's standing still, so nought is excluded for the same reason.
    ///
    /// Two positions with nothing between them, and both of them seen, is a button. Following it
    /// is the whole of the job and there is no position to pick up from.
    ///
    /// Numbers that walk is anything else. It is saying where it is, so it has to be picked up
    /// rather than jumped to, or touching it drags the parameter to wherever your hand happens
    /// to be resting.
    /// </remarks>
    private ControlPickup Decide()
    {
        int first = _seen[0];

        bool same = true, only = true, low = false, high = false;

        for (int at = 0; at < Enough; at++)
        {
            int value = _seen[at];

            if (value != first) same = false;
            if (value != 0 && value != Top) only = false;
            if (value == 0) low = true;
            if (value == Top) high = true;
        }

        if (same)
        {
            if (first != Still && Math.Abs(first - Still) <= Near)
            {
                Turn = ControlTurn.Offset;
                return ControlPickup.Relative;
            }

            if (first != 0 && (first <= Edge || first >= Top - Edge))
            {
                Turn = ControlTurn.Twos;
                return ControlPickup.Relative;
            }
        }

        if (only && low && high) return ControlPickup.Jump;

        return ControlPickup.Takeover;
    }

    /// <summary>The number either convention sends for standing still.</summary>
    private const int Still = 64;

    /// <summary>What it decided, in words, for the list in SETTINGS.</summary>
    public static string Describe(ControlPickup pickup, ControlTurn turn) => pickup switch
    {
        ControlPickup.Jump => "jumps",
        ControlPickup.Relative => turn == ControlTurn.Twos ? "encoder" : "encoder, from centre",
        ControlPickup.Takeover => "picks up",
        ControlPickup.Endless => "endless knob",
        _ => "listening"
    };
}
