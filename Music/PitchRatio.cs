using System;
using JingleBox2.Music.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Music;

/// <inheritdoc/>
public sealed class PitchRatio : IPitchRatio
{
    /// <inheritdoc cref="IPitchRatio.SemitonesPerOctave"/>
    public const int SemitonesPerOctave = 12;

    /// <inheritdoc cref="IPitchRatio.MaxSemitoneShift"/>
    public const int MaxSemitoneShift = 72;

    /// <inheritdoc/>
    int IPitchRatio.SemitonesPerOctave => SemitonesPerOctave;

    /// <inheritdoc/>
    int IPitchRatio.MaxSemitoneShift => MaxSemitoneShift;

    /// <inheritdoc/>
    public double For(Note note, Note baseNote)
    {
        if (!note.IsPlayable || !baseNote.IsPlayable) return 1.0;

        int shift = Math.Clamp(note.Semitone - baseNote.Semitone, -MaxSemitoneShift, MaxSemitoneShift);
        return Math.Pow(2.0, shift / (double)SemitonesPerOctave);
    }

    /// <inheritdoc/>
    public double FrequencyFor(Note note, Note baseNote, int sampleRate) =>
        sampleRate * For(note, baseNote);
}
