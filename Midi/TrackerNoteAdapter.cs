using JingleBox2.Tracker;
using JingleBox2.ViewModels;

namespace JingleBox2.Midi;

/// <summary>
/// Hands keyboard notes to whichever half of the app is in front: the instrument library while
/// you are building a sound, the pattern otherwise.
/// </summary>
public sealed class TrackerNoteAdapter : INoteTrigger
{
    private readonly TrackerViewModel _tracker;
    private readonly InstrumentLibraryViewModel _library;

    public TrackerNoteAdapter(TrackerViewModel tracker, InstrumentLibraryViewModel library)
    {
        _tracker = tracker;
        _library = library;
    }

    public void TriggerNote(Note note, int volume)
    {
        // A song with no instruments has nothing a note could mean, so it goes to the library
        // either way. That also keeps a note audible if the page flag is ever wrong.
        if (_library.IsEditing || !_tracker.HasInstruments) _library.PlayMidiNote(note, volume);
        else _tracker.PlayMidiNote(note, volume);
    }
}
