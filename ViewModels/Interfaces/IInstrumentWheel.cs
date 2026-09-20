using System.Collections.Generic;

namespace JingleBox2.ViewModels.Interfaces;

/// <summary>
/// What one instrument's modulation wheel may turn, and what it does turn.
/// </summary>
/// <remarks>
/// The list a device's Menu offers and the one place an instrument's choice is written. It
/// exists because there was no way to change what the wheel does without a keyboard: a link is
/// filled in from a MIDI message, so a drawn wheel, which sends none, could never be pointed at
/// anything, and the machine's own declaration was the only answer anybody had.
///
/// A list and an index, which is the shape every picker here keeps: what a row means, where the
/// choice is written down and what happens to the sound when it moves all belong to whoever owns
/// the device, and whatever draws it says which row and nothing else.
///
/// What the first row means is the implementer's business rather than this contract's: a list
/// that wants a row standing for "whatever the device says" puts one there and answers for it.
/// </remarks>
public interface IInstrumentWheel
{
    /// <summary>What is offered, in the order it is offered.</summary>
    IReadOnlyList<string> Names { get; }

    /// <summary>Which one the wheel turns, or -1 for none of them.</summary>
    /// <remarks>
    /// Minus one is what a device naming a control its face has since lost looks like: the wheel
    /// reaches nothing, and nothing is marked rather than the first row being marked, which
    /// would be a lie about what the wheel is doing.
    /// </remarks>
    int Picked { get; set; }
}
