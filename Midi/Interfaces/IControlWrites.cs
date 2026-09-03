using System;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// What a controller has done, on its way from the port's thread to the drawing thread.
/// </summary>
/// <remarks>
/// A knob and a button arrive the same way and must not be carried the same way, and that is the
/// whole of what this is for.
///
/// **A position coalesces and a press may not.** A hand sweeping a knob sends a hundred messages
/// a second and only where it ended up matters, so the ones in between are dropped on purpose:
/// the sound is the same and the panel is drawn once. A press is a thing somebody did, and two of
/// them are two of them. Carried the same way as a knob, a pad hit twice inside one trip to the
/// drawing thread arrives once, so a pad in toggle mode is left playing when it was told to stop
/// and the light on the screen disagrees with the hand that played it. Measured on a real device:
/// two note ons in the same millisecond, one toggle.
///
/// Nothing here knows what a mapping is pointed at. It is handed what to do and told which of the
/// two kinds it is, which is what lets it be put a question to without a window, a port or a
/// drawing thread.
/// </remarks>
public interface IControlWrites
{
    /// <summary>
    /// A value on its way to a parameter, replacing any that has not landed yet.
    /// </summary>
    /// <param name="mapping">Which link it came from, since one link has one value in flight.</param>
    /// <param name="write">Where the value goes, run on the drawing thread.</param>
    /// <param name="value">What it should be.</param>
    void Moved(ControlMapping mapping, Action<double> write, double value);

    /// <summary>
    /// A press, kept beside every other press in the order they were made.
    /// </summary>
    /// <param name="write">What the press does, run on the drawing thread.</param>
    /// <param name="value">What to hand it, which for a press is only ever the one number.</param>
    void Pressed(Action<double> write, double value);

    /// <summary>
    /// The value on its way to that link's parameter, or nothing when none is.
    /// </summary>
    /// <remarks>
    /// What the parameter will hold a moment from now, which is the honest answer to where it
    /// stands for anything working out its next value from this one. A press has no such
    /// question: it is not a value and nothing picks it up.
    /// </remarks>
    /// <param name="mapping">The link being asked about.</param>
    double? Waiting(ControlMapping mapping);
}
