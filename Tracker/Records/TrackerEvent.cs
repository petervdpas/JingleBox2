using JingleBox2.Tracker.Enums;

namespace JingleBox2.Tracker.Records;

/// <summary>One thing to do to one track on one step.</summary>
/// <param name="Track">Which track, counting from zero.</param>
/// <param name="Kind">Whether this starts a voice, stops one, or only moves one.</param>
/// <param name="Note">The note to play, or <see cref="Note.Off"/> for a stop.</param>
/// <param name="Instrument">
/// Which instrument to play it on, or <see cref="TrackerCell.NoInstrument"/>, which the sequencer
/// reads as "whatever this track last played".
/// </param>
/// <param name="Gain">The volume column as a 0 to 1 gain, or null when the column is blank.</param>
/// <param name="Effect">The effect command on this cell, or <see cref="TrackerEffect.None"/>.</param>
public readonly record struct TrackerEvent(
    int Track,
    TrackerEventKind Kind,
    Note Note,
    int Instrument,
    float? Gain,
    TrackerEffect Effect)
{
    /// <summary>The event that silences a track, with every other column saying nothing.</summary>
    public static TrackerEvent Stop(int track) =>
        new(track, TrackerEventKind.Stop, Note.Off, TrackerCell.NoInstrument, null, TrackerEffect.None);
}
