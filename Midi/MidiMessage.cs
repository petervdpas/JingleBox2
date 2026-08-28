using JingleBox2.Midi.Enums;

namespace JingleBox2.Midi;

/// <summary>
/// One message off the wire, in the terms the rest of the program uses.
/// </summary>
/// <remarks>
/// Deliberately one shape for five kinds rather than five types, because almost everything that
/// reads these begins by asking two questions, what kind and is it a press, and a hierarchy
/// would turn that into a cast. The fields mean slightly different things per kind and each says
/// so on itself.
///
/// Immutable, and made in exactly one place, <c>MidiService.Read</c>. That is what lets
/// the whole routing half of the application be tested by handing it messages nobody's hardware
/// sent.
/// </remarks>
public sealed class MidiMessage
{
    /// <summary>Which controller sent it, so routing can tell a pad box from a keyboard.</summary>
    public string Device { get; init; } = "";

    /// <summary>Which of the five kinds this is. Everything that reads one asks this first.</summary>
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
    /// <remarks>
    /// False for a realtime byte and for a system exclusive message as well, and deliberately:
    /// every other router in this application begins by asking for a press, so leaving it false
    /// is what keeps a transport byte out of the pads without a line being added to any of them.
    /// </remarks>
    public bool IsOn { get; init; }

    /// <summary>
    /// The whole message, for a system exclusive one, opening 0xF0 and closing 0xF7 included.
    /// </summary>
    /// <remarks>
    /// Null for every other kind. An allocation per message would be worth avoiding on a knob
    /// being turned, which is three hundred a second; one of these is a button press.
    /// </remarks>
    public byte[]? Bytes { get; init; }
}
