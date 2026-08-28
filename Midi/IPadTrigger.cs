namespace JingleBox2.Midi;

/// <summary>What a pad is being asked to do.</summary>
public enum PadTriggerAction
{
    /// <summary>Play it if it is stopped, stop it if it is playing.</summary>
    /// <remarks>
    /// What a pad box wants, and the default: one button per pad and one press does both jobs,
    /// because there is no second button to stop with.
    /// </remarks>
    Toggle,

    /// <summary>Play it from the beginning, whatever it was doing.</summary>
    /// <remarks>
    /// What a jingle wants: hit it again and it starts again, which is the whole point of a
    /// stinger. Toggle would stop it instead, half a second into a two second sting.
    /// </remarks>
    Start,

    /// <summary>Stop it, and do nothing if it was already stopped.</summary>
    Stop
}

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
