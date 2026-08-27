namespace JingleBox2.Midi;

public enum MidiMessageType
{
    Note = 0,
    ControlChange = 1,

    /// <summary>
    /// The wheel, the strip, or a motorised fader on a control surface.
    /// </summary>
    /// <remarks>
    /// The odd one out in three ways, all of which come from it being the only fourteen bit
    /// message MIDI has. It carries no controller number, because the channel is the number:
    /// there is one bend per channel and no way to have two. Its range is 0 to 16383 and its
    /// centre is 8192 rather than nought. And it is not really about pitch any more. Mackie
    /// Control sends a surface's faders this way, one per channel, precisely because 128
    /// positions is not enough for a hand on a hundred millimetre fader, which is why this is
    /// read at all.
    ///
    /// Last, so a number stored anywhere before this existed still reads as what it was.
    /// </remarks>
    PitchBend = 2
}

public sealed class MidiMessage
{
    /// <summary>Which controller sent it, so routing can tell a pad box from a keyboard.</summary>
    public string Device { get; init; } = "";

    public MidiMessageType Type { get; init; }

    /// <summary>1 to 16. For a bend this is the whole of its address.</summary>
    public int Channel { get; init; }

    /// <summary>Note or CC number. Nought for a bend, which has none.</summary>
    public int Value { get; init; }

    /// <summary>
    /// Velocity, or a CC value, 0 to 127. For a bend, 0 to 16383 with 8192 in the middle.
    /// </summary>
    public int Data { get; init; }

    /// <summary>A note on, or a CC above nought. Always false for a bend, which is neither.</summary>
    public bool IsOn { get; init; }
}
