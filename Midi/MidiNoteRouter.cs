using JingleBox2.Tracker;

namespace JingleBox2.Midi;

public interface INoteTrigger
{
    void TriggerNote(Note note, int volume);
}

/// <summary>
/// Turns note messages from a keyboard into tracker notes.
///
/// Key releases are dropped on purpose: writing a note-off every time a key comes up would
/// fill the pattern. The note-off key on the computer keyboard stays the way to write one.
/// </summary>
public sealed class MidiNoteRouter
{
    private readonly INoteTrigger _notes;

    public MidiNoteRouter(INoteTrigger notes)
    {
        _notes = notes;
    }

    public void Handle(MidiMessage msg)
    {
        if (msg is null || msg.Type != MidiMessageType.Note || !msg.IsOn) return;
        if (!MidiNoteInput.TryNote(msg.Value, out var note)) return;

        _notes.TriggerNote(note, MidiNoteInput.VolumeFor(msg.Data));
    }
}
