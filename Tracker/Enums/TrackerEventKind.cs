namespace JingleBox2.Tracker.Enums;

/// <summary>What a step asks of one note column of a track.</summary>
/// <remarks>
/// The first three come straight off a cell, whose columns are independently blank. The last two
/// are only ever made by the pattern commands, which say what happens between one line and the
/// next: see <see cref="JingleBox2.Tracker.Commands.Interfaces.ILineCommands"/>.
/// </remarks>
public enum TrackerEventKind
{
    /// <summary>Start a voice on this track.</summary>
    Trigger,

    /// <summary>Stop whatever this track is playing.</summary>
    Stop,

    /// <summary>Change the running voice without retriggering it.</summary>
    Adjust,

    /// <summary>Silence this column at once, with no release, which is what a note cut asks for.</summary>
    Cut,

    /// <summary>
    /// Move the pitch of what this column is sounding by <see cref="Records.TrackerEvent.Shift"/>
    /// semitones from the note it was played at, without starting it again.
    /// </summary>
    Shift
}
