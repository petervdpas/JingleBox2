using JingleBox2.Diagnostics;
using JingleBox2.Tracker;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Midi;

/// <summary>
/// Turns note messages from a keyboard into tracker notes.
/// </summary>
/// <remarks>
/// The second router, and the same shape as the first: this one knows the wire and nothing about
/// the application, and an adapter on the far side of <see cref="INoteTrigger"/> knows where a
/// note goes. See <see cref="MidiRouter"/> for pads and <see cref="MidiControlRouter"/> for
/// knobs.
///
/// Both halves of a key are passed on. What a release turns into is decided further along,
/// where the setting for it lives: writing a note-off for every key that comes up fills a
/// pattern quickly at a step of one, so it is something to ask for rather than the default.
/// </remarks>
public sealed class MidiNoteRouter
{
    private readonly INoteTrigger _notes;

    /// <param name="notes">Where a note goes. Not a view model, so this can be tested.</param>
    public MidiNoteRouter(INoteTrigger notes)
    {
        _notes = notes;
    }

    /// <summary>
    /// Plays or releases the note this message names, and says which in the log.
    /// </summary>
    /// <remarks>
    /// Both halves are said out loud, and that is the second half of a fault rather than a
    /// convenience. The two routers that read buttons and ignore notes wrote a line per message;
    /// this one, which is the only one that plays notes, wrote nothing at all. So a log taken
    /// while a key was hanging showed every press and had no way whatever of showing that its
    /// release never arrived, which is exactly the fact anybody reading such a log needs.
    ///
    /// Asked before the line is built rather than after. A key press is tens of messages a
    /// minute and not thousands, so this could be written unconditionally without anybody
    /// noticing; it is guarded because the guard inside <c>Log.Write</c> is checked after
    /// the caller has already allocated the closure, and this file is the model the noisier
    /// paths copy.
    ///
    /// Aftertouch shares a note's shape and is not a key coming up. It never reaches here,
    /// because <c>MidiService.Read</c> does not produce a <see cref="MidiMessageType.Note"/>
    /// for it, and <c>Tests/NotePathTests.cs</c> says so on purpose.
    /// </remarks>
    public void Handle(MidiMessage msg)
    {
        if (msg is null || msg.Type != MidiMessageType.Note) return;
        if (!MidiNoteInput.TryNote(msg.Value, out var note)) return;

        if (Log.On(LogArea.Midi))
            Log.Write(LogArea.Midi, () =>
                "note: '" + msg.Device + "' " + (msg.IsOn ? "down " : "up ") + note
                + " (" + msg.Value + ") velocity " + msg.Data);

        if (msg.IsOn) _notes.TriggerNote(note, MidiNoteInput.VolumeFor(msg.Data));
        else _notes.ReleaseNote(note);
    }
}
