using JingleBox2.Tracker;
using System;

namespace JingleBox2.ViewModels;

/// <summary>
/// How wide a panel's keyboard is, and where it has to be to show a note.
/// </summary>
/// <remarks>
/// Eighty-eight keys shows everything and is a metre of wood on a panel this size; one octave
/// fits and hides most of what a track plays. Three octaves is the trade: wide enough that a
/// bass line and its own upper reaches are on screen together, small enough to sit under the
/// knobs, and it moves when the music goes somewhere it is not.
///
/// It moves in whole octaves, and the OCTAVE lamps are where it starts. That is the point of
/// keeping the two the same number: the lamps are not a separate setting that has to be kept
/// in step with the keyboard, they are the keyboard's own position.
/// </remarks>
public static class PanelKeyboard
{
    /// <summary>Three octaves and the C on top, which is what a panel has room for.</summary>
    public const int Keys = 37;

    /// <summary>The highest octave the lamps count to.</summary>
    public const int TopOctave = 9;

    /// <summary>
    /// Which octave the keyboard has to start at for a note to be on it, moving as little as
    /// it can. A note already showing moves nothing.
    /// </summary>
    public static int Reveal(Note note, int octave, int keys = Keys)
    {
        if (!note.IsPlayable) return octave;

        int first = octave * 12;
        int last = first + keys - 1;

        if (note.Semitone >= first && note.Semitone <= last) return octave;

        int played = note.Semitone / 12;

        // Below, the note's own octave becomes the leftmost. Above, it becomes the rightmost
        // whole one, so the keyboard travels the least distance that puts the note on it.
        int wanted = note.Semitone < first ? played : played - (keys - 1) / 12 + 1;

        return Math.Clamp(wanted, 0, TopOctave);
    }
}
