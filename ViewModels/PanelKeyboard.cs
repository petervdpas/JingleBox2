using System;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels.Interfaces;

namespace JingleBox2.ViewModels;

/// <inheritdoc/>
public sealed class PanelKeyboard : IPanelKeyboard
{
    /// <inheritdoc cref="IPanelKeyboard.Keys"/>
    public const int Keys = IPanelKeyboard.Keys;

    /// <inheritdoc cref="IPanelKeyboard.TopOctave"/>
    public const int TopOctave = IPanelKeyboard.TopOctave;



    /// <inheritdoc/>
    public int Reveal(Note note, int octave, int keys = Keys)
    {
        if (!note.IsPlayable) return octave;

        int first = octave * 12;
        int last = first + keys - 1;

        if (note.Semitone >= first && note.Semitone <= last) return octave;

        int played = note.Semitone / 12;

        int wanted = note.Semitone < first ? played : played - (keys - 1) / 12 + 1;

        return Math.Clamp(wanted, 0, TopOctave);
    }
}
