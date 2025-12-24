namespace JingleBox2.Midi;

public sealed class MidiMapping
{
    public int PadIndex { get; set; }            // 0..7
    public MidiMessageType Type { get; set; } = MidiMessageType.Note;
    public int Channel { get; set; } = 1;        // 1..16
    public int Value { get; set; }               // Note or CC
}
