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

    private readonly int[] _seen = new int[Enough];
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

    private ControlPickup Decide()
    {
        int first = _seen[0];

        bool same = true, only = true, low = false, high = false;

        for (int at = 0; at < Enough; at++)
        {
            int value = _seen[at];

            if (value != first) same = false;
            if (value != 0 && value != 127) only = false;
            if (value == 0) low = true;
            if (value == 127) high = true;
        }

        // The same number again and again while a hand is turning is not a position, it is a
        // count of notches: one notch, one notch, one notch. Asked first, because the number an
        // encoder repeats is often 127, which read the other way round is a button held down.
        if (same)
        {
            // Counting from the middle of the range. Not the middle itself, which is the
            // number one of these sends when it is standing still.
            if (first != Still && Math.Abs(first - Still) <= Near)
            {
                Turn = ControlTurn.Offset;
                return ControlPickup.Relative;
            }

            // Counting from either end. Not nought, which is this convention's standing still.
            if (first != 0 && (first <= Edge || first >= 127 - Edge))
            {
                Turn = ControlTurn.Twos;
                return ControlPickup.Relative;
            }
        }

        // Two positions and nothing between them, and both of them seen. Following it is the
        // whole of the job; there is no position to pick up from.
        if (only && low && high) return ControlPickup.Jump;

        // Numbers that walk. It is saying where it is, so it has to be picked up rather than
        // jumped to, or touching it drags the parameter to wherever your hand happens to rest.
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
