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
        if (Rack) _rack.PlayMidiNote(note, volume);
        else _tracker.PlayMidiNote(note, volume);
    }

    /// <summary>Whether a key belongs to the machine being built rather than to the pattern.</summary>
    /// <remarks>
    /// Asked of the page that is up, not of the page that exists. The machines page lives
    /// inside the tracker and is hidden rather than taken away when the pattern is in front,
    /// so a flag set when it was put together stays set for the rest of the session and every
    /// note goes to the rack.
    ///
    /// A song with no instruments has nothing a note could mean, so it goes to the rack either
    /// way rather than sounding nothing.
    /// </remarks>
    private bool Rack => _tracker.ShowsMachines || !_tracker.HasInstruments;

    /// <summary>
    /// A key coming up. Only the pattern has anywhere to put one: the rack is auditioning a
    /// sound, and there is nothing there for a note-off to be written into.
    /// </summary>
    public void ReleaseNote(Note note)
    {
        if (Rack) return;

        _tracker.ReleaseMidiNote(note);
    }
}
