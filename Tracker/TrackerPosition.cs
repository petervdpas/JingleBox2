using System;

namespace JingleBox2.Tracker;

/// <summary>Where the player is: which entry in the order list, and which step inside it.</summary>
/// <remarks>
/// The order index rather than the pattern, because the same pattern can be in a song twice and
/// "which pattern" would then be an ambiguous answer to "where are we". What follows a slot is a
/// different question each time it is asked, and only the order knows it.
/// </remarks>
/// <param name="OrderIndex">Which entry of <see cref="Song.Order"/>, counting from zero.</param>
/// <param name="Line">Which step inside that entry's pattern, counting from zero.</param>
public readonly record struct TrackerPosition(int OrderIndex, int Line)
{
    /// <summary>The top of the song, which is where a stopped transport goes back to.</summary>
    public static readonly TrackerPosition Start = new(0, 0);

    /// <summary>Order and line, both two digits, as the status line shows it.</summary>
    public override string ToString() => $"{OrderIndex:00}:{Line:00}";
}

/// <summary>What a step asks of a track. Three, because a cell's columns are independently blank.</summary>
public enum TrackerEventKind
{
    /// <summary>Start a voice on this track.</summary>
    Trigger,

    /// <summary>Stop whatever this track is playing.</summary>
    Stop,

    /// <summary>Change the running voice without retriggering it.</summary>
    Adjust
}

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
