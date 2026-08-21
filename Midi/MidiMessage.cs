namespace JingleBox2.Midi;

public enum MidiMessageType
{
    Note = 0,
    ControlChange = 1
}

public sealed class MidiMessage
{
    /// <summary>Which controller sent it, so routing can tell a pad box from a keyboard.</summary>
    public string Device { get; init; } = "";

    public MidiMessageType Type { get; init; }
    public int Channel { get; init; }   // 1..16
    public int Value { get; init; }     // Note or CC number
    public int Data { get; init; }      // Velocity / CC value
    public bool IsOn { get; init; }     // NoteOn or CC > 0
}
