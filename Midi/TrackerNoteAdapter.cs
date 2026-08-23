using JingleBox2.Tracker;
using JingleBox2.ViewModels;

namespace JingleBox2.Midi;

/// <summary>
/// Hands keyboard notes to whichever half of the app is in front: the rack while you are
/// building a sound, the pattern otherwise.
/// </summary>
public sealed class TrackerNoteAdapter : INoteTrigger
{
    private readonly TrackerViewModel _tracker;
    private readonly MachineRackViewModel _rack;

    public TrackerNoteAdapter(TrackerViewModel tracker, MachineRackViewModel rack)
    {
        _tracker = tracker;
        _rack = rack;
    }

    public void TriggerNote(Note note, int volume)
    {
        // A song with no instruments has nothing a note could mean, so it goes to the rack
        // either way. That also keeps a note audible if the page flag is ever wrong.
        if (_rack.IsEditing || !_tracker.HasInstruments) _rack.PlayMidiNote(note, volume);
        else _tracker.PlayMidiNote(note, volume);
    }

    /// <summary>
    /// A key coming up. Only the pattern has anywhere to put one: the rack is auditioning a
    /// sound, and there is nothing there for a note-off to be written into.
    /// </summary>
    public void ReleaseNote(Note note)
    {
        if (_rack.IsEditing || !_tracker.HasInstruments) return;

        _tracker.ReleaseMidiNote(note);
    }
}
