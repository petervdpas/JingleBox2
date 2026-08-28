namespace JingleBox2.Midi;

/// <summary>The kinds of message this application reads. Everything else is dropped at the wire.</summary>
public enum MidiMessageType
{
    /// <summary>
    /// A key going down or coming up.
    /// </summary>
    /// <remarks>
    /// Both spellings of a release are this: a note off, and a note on at nothing, which is what
    /// most keyboards actually send. <see cref="MidiMessage.IsOn"/> is the difference, and
    /// reading the second as a press is the classic way to hang every note on a keyboard.
    /// </remarks>
    Note = 0,

    /// <summary>
    /// A knob, a fader, a button, or an encoder counting notches.
    /// </summary>
    /// <remarks>
    /// All four are the same three bytes and nothing in the message says which sent it, which is
    /// the whole reason <see cref="ControlSense"/> exists.
    /// </remarks>
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
    PitchBend = 2,

    /// <summary>
    /// Start, continue and stop, which are one byte each and belong to no channel.
    /// </summary>
    /// <remarks>
    /// The transport as the specification has had it since 1983, and the one dialect of it that
    /// every sequencer ever built can speak. A byte with no channel, no data and no note off:
    /// 0xFA start, 0xFB continue, 0xFC stop. Its siblings 0xF8 clock and 0xFE active sensing
    /// arrive dozens of times a second and are dropped at the wire.
    ///
    /// <c>Value</c> carries the status byte itself, since there is nothing else to carry.
    /// </remarks>
    Realtime = 3,

    /// <summary>
    /// A system exclusive message, whole, in <c>Bytes</c>.
    /// </summary>
    /// <remarks>
    /// The only variable length message MIDI has, and so the only one that can arrive in pieces.
    /// Three separate things wanted it and none of them could be built without it: MIDI Machine
    /// Control, which is what a KeyStep Pro's transport buttons send; the universal identity
    /// reply, which is the one name a device has that does not change with the operating system;
    /// and Arturia's own settings protocol, which is how a controller says what its own knobs
    /// are set to.
    /// </remarks>
    SystemExclusive = 4
}

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
