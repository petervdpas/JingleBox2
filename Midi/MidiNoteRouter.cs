using JingleBox2.Diagnostics;
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

        // Both halves, said out loud. The two routers that read buttons and ignore notes were
        // the noisy ones and the one that plays them said nothing at all, so a log taken while
        // a key hung showed every press and had no way of showing that its release never
        // arrived. A key press is tens of messages a minute, not thousands, so this is asked
        // rather than counted.
        if (Log.On(LogArea.Midi))
            Log.Write(LogArea.Midi, () =>
                "note: '" + msg.Device + "' " + (msg.IsOn ? "down " : "up ") + note
                + " (" + msg.Value + ") velocity " + msg.Data);

        if (msg.IsOn) _notes.TriggerNote(note, MidiNoteInput.VolumeFor(msg.Data));
        else _notes.ReleaseNote(note);
    }
}
