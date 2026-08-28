using JingleBox2.Tracker;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// Writes what the lanes say, one line at a time.
/// </summary>
/// <remarks>
/// The other half of remote control, and deliberately the same half. A knob turned from CC 74
/// and the clock arriving at line 32 are one act against one interface, so this reaches a
/// parameter through <see cref="Midi.Interfaces.IControlTargets"/> exactly as
/// <see cref="Midi.MidiControlRouter"/> does, and everything that made a link resolve correctly
/// makes a lane resolve correctly for free: a machine only answering on a track that plays it,
/// an insert found by what it is rather than where it sits, a strip written through the fader on
/// the screen.
///
/// It knows nothing about the clock beyond being called with a position, which is what makes it
/// testable with no audio and no window. It also means nothing here is on a timer: the player
/// asks once per line and asks for nothing in between.
/// </remarks>
public interface IAutomationPlayer
{
    /// <summary>
    /// Forgets what was written and what was resolved.
    /// </summary>
    /// <remarks>
    /// Called when playback starts and when the song changes. Both matter: the parameters have
    /// been moved by hand since the last pass, so the remembered value is a lie and would stop
    /// the first line writing anything at all, and holding lanes from a song that has been
    /// closed would keep it alive for as long as this lives.
    /// </remarks>
    void Reset();

    /// <summary>Puts every lane on this line where it should be. Silent when there are none.</summary>
    /// <remarks>
    /// A value that has not moved is not written again, and the comparison is made here rather
    /// than left to the target to notice. A machine's setting is a field and would take the write
    /// happily; a plugin's parameter is a message to another process. A lane holding still
    /// between two points would otherwise post the same number thirty times a second.
    ///
    /// A lane that resolves to nothing is passed over rather than reported as a fault, and it is
    /// said in the log once per lane per pass. A lane naming a machine the track is not playing
    /// is an ordinary state, not thirty faults a second.
    /// </remarks>
    void Play(Song? song, TrackerPosition position);
}
