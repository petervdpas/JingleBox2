using JingleBox2.Midi;
using System;
using System.Collections.Generic;
using JingleBox2.Midi.Enums;
using JingleBox2.Tracker.Enums;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Tracker.Records;

/// <summary>
/// One point in a lane: when, and how far.
/// </summary>
/// <remarks>
/// The time is in lines and is a double rather than an int, which is the only forward looking
/// decision in this type and costs nothing to make now. Renoise quantises a point to 256 units
/// per line and says what that unit is: "a time of 1.5 means line 1 with a note column delay of
/// 128". There is no delay column here yet, so nothing produces a fraction, but the file writes
/// what it is given and reads back whatever it finds, so the day a fraction appears the format
/// does not have to move.
///
/// The value is normalised, nought to one, and always. A lane does not know whether it is
/// driving hertz or decibels: <see cref="IControlTarget"/> carries the range and converts. That
/// also means a lane survives a machine widening a parameter in a later version.
/// </remarks>
/// <param name="Time">When, in lines from the top of the pattern.</param>
/// <param name="Value">How far, normalised nought to one.</param>
public readonly record struct AutomationPoint(double Time, double Value)
{
    /// <summary>The same point held inside its bounds, for anything arriving from outside.</summary>
    public AutomationPoint Clamped() =>
        new(Math.Max(0, Time), Math.Clamp(Value, 0, 1));
}
