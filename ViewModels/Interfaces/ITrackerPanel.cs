using JingleBox2.Tracker;
using System;
using System.ComponentModel;
using JingleBox2.ViewModels;
using JingleBox2.Tracker.Records;

namespace JingleBox2.ViewModels.Interfaces;

/// <summary>
/// The tracker, as an instrument's front panel sees it.
/// </summary>
/// <remarks>
/// A panel opened from a track wants two things from the tracker and nothing else: where it
/// has got to, and which octave the song is being played at. It asks for those rather than for
/// the tracker itself, because the same panel opens on the rack page where there is no
/// tracker at all, and a panel that knew about songs would not open there.
/// </remarks>
public interface ITrackerPanel : INotifyPropertyChanged
{
    /// <summary>The row being played, or -1 when nothing is playing.</summary>
    int PlayingLine { get; }

    /// <summary>
    /// This panel's window came to the front, so its track is the one being worked on.
    /// </summary>
    /// <remarks>
    /// For a hardware knob pointed at "the track you are on". The cursor answers that while
    /// you are working in the pattern, and stops answering it the moment a panel is open in a
    /// window of its own: two of those and the cursor is on neither. Nothing is applied by
    /// saying this. The mappings are walked per message, so the next thing you touch simply
    /// resolves against a different track.
    /// </remarks>
    void PanelInFront(int track) { }

    /// <summary>And has gone, so the cursor says where you are again.</summary>
    void PanelGone(int track) { }

    /// <summary>How many rows the pattern has, which sets how many pages of eight there are.</summary>
    int PatternLines { get; }

    /// <summary>
    /// The octave the song is typed and auditioned at, shared with the pattern editor.
    /// </summary>
    /// <remarks>
    /// One number for the whole song. Moving it on a machine's panel moves it in the pattern
    /// editor as well, because they are the same octave: a panel with a private one would have
    /// you set it twice and then wonder which one a key press had used.
    /// </remarks>
    int Octave { get; set; }

    /// <summary>
    /// Moves the octave to keep up with a note, which is not the same as being asked to.
    /// </summary>
    /// <remarks>
    /// Setting it is an edit and marks the song unsaved. Following a note the song itself just
    /// played is not: a playback that left the song dirty every time the music went up an
    /// octave would be asking you to save work nobody did.
    /// </remarks>
    void FollowOctave(int octave);

    /// <summary>
    /// Raised for every note that goes to a track, so a panel can light its own keys.
    /// </summary>
    /// <remarks>
    /// Every track's notes, not only the panel's own: which one it belongs to is on the event,
    /// and a panel that filtered nothing would light for the whole song. It can arrive from the
    /// clock thread.
    ///
    /// Seconds is how long that note will sound, or zero when nobody knows: a pattern's note
    /// lasts until the next one, an auditioned recording lasts exactly as long as the recording.
    /// </remarks>
    event EventHandler<(int Track, Note Note, double Seconds)>? NotePlayed;
}
