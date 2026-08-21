using JingleBox2.Tracker;

namespace JingleBox2.ViewModels;

/// <summary>
/// Sounds a single note on an instrument. The tracker owns the audio engine, so the library
/// borrows it through this rather than opening a second one.
/// </summary>
public interface IInstrumentAudition
{
    void Audition(TrackerInstrument instrument, Note note, int volume);
}
