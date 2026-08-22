using JingleBox2.Tracker;

namespace JingleBox2.Midi;

public interface INoteTrigger
{
    void TriggerNote(Note note, int volume);

    /// <summary>A key coming up. Whether that writes anything is not this end's business.</summary>
    void ReleaseNote(Note note);
}

/// <summary>
/// Turns note messages from a keyboard into tracker notes.
/// </summary>
/// <remarks>
/// Both halves of a key are passed on. What a release turns into is decided further along,
/// where the setting for it lives: writing a note-off for every key that comes up fills a
/// pattern quickly at a step of one, so it is something to ask for rather than the default.
/// </remarks>
public sealed class MidiNoteRouter
{
    private readonly INoteTrigger _notes;

    public MidiNoteRouter(INoteTrigger notes)
    {
        _notes = notes;
    }

    public void Handle(MidiMessage msg)
    {
        if (msg is null || msg.Type != MidiMessageType.Note) return;
        if (!MidiNoteInput.TryNote(msg.Value, out var note)) return;

        if (msg.IsOn) _notes.TriggerNote(note, MidiNoteInput.VolumeFor(msg.Data));
        else _notes.ReleaseNote(note);
    }
}
