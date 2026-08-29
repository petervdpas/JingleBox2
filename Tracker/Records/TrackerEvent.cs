using JingleBox2.Tracker.Enums;

namespace JingleBox2.Tracker.Records;

/// <summary>One thing to do to one note column of one track on one step.</summary>
/// <param name="Track">Which track, counting from zero.</param>
/// <param name="Column">
/// Which of that track's note columns, counting from zero.
/// </param>
/// <param name="Kind">Whether this starts a voice, stops one, or only moves one.</param>
/// <param name="Note">The note to play, or <see cref="Note.Off"/> for a stop.</param>
/// <param name="Instrument">
/// Which instrument to play it on, or <see cref="TrackerCell.NoInstrument"/>, which the sequencer
/// reads as "whatever this column last played".
/// </param>
/// <param name="Gain">The volume column as a 0 to 1 gain, or null when the column is blank.</param>
/// <param name="Effect">The effect command on this cell, or <see cref="TrackerEffect.None"/>.</param>
/// <remarks>
/// The column is beside the track because the two together are what names a voice. A stop that
/// named only its track would take a whole chord down to end one note of it, which is the same
/// mistake a host makes when it can only say all notes off.
/// </remarks>
public readonly record struct TrackerEvent(
    int Track,
    int Column,
    TrackerEventKind Kind,
    Note Note,
    int Instrument,
    float? Gain,
    TrackerEffect Effect)
{
    /// <summary>The event that silences one column, with every other column saying nothing.</summary>
    public static TrackerEvent Stop(int track, int column = 0) =>
        new(track, column, TrackerEventKind.Stop, Note.Off, TrackerCell.NoInstrument, null,
            TrackerEffect.None);
}
