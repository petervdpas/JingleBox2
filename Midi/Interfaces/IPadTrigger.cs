using JingleBox2.Midi.Enums;
using JingleBox2.Midi;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// Where a mapped button on a controller comes out, as a pad and something to do to it.
/// </summary>
/// <remarks>
/// The seam between the wire and the pads, and it is the same shape as
/// <see cref="INoteTrigger"/> for the same reason: <see cref="MidiRouter"/> knows the mappings
/// and nothing about the application, and the adapter on the other side knows the pads and
/// nothing about MIDI. Neither of them needs a window to be put a question to.
///
/// A pad number outside the range is an ordinary state rather than a fault: pads are made and
/// unmade in SETTINGS while the rest of the program is running, and a mapping made against an
/// eight pad grid outlives being cut down to four.
/// </remarks>
public interface IPadTrigger
{
    /// <summary>Does that to that pad, or nothing at all when there is no such pad.</summary>
    void TriggerPad(int padIndex, PadTriggerAction action);
}
