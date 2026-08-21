using JingleBox2.Tracker;
using JingleBox2.ViewModels;

namespace JingleBox2.Midi;

/// <summary>Hands keyboard notes to the tracker, the same shape as PadTriggerAdapter.</summary>
public sealed class TrackerNoteAdapter : INoteTrigger
{
    private readonly TrackerViewModel _tracker;

    public TrackerNoteAdapter(TrackerViewModel tracker)
    {
        _tracker = tracker;
    }

    public void TriggerNote(Note note, int volume) => _tracker.PlayMidiNote(note, volume);
}
