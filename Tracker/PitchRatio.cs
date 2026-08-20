using System;

namespace JingleBox2.Tracker;

/// <summary>
/// Turns a note into a playback rate. A sample recorded at one pitch is played faster or
/// slower to reach another, the way a tracker has always done it: no synthesis, just resampling.
/// </summary>
public static class PitchRatio
{
    /// <summary>Twelve semitones double the rate, so an octave up plays twice as fast.</summary>
    public const int SemitonesPerOctave = 12;

    /// <summary>
    /// BASS clamps playback rate to a sane band. Past roughly six octaves either way the
    /// result is inaudible or unusably aliased, so refuse it rather than hand BASS junk.
    /// </summary>
    public const int MaxSemitoneShift = 72;

    /// <summary>Rate multiplier to hear <paramref name="note"/> from a sample recorded at <paramref name="baseNote"/>.</summary>
    public static double For(Note note, Note baseNote)
    {
        if (!note.IsPlayable || !baseNote.IsPlayable) return 1.0;

        int shift = Math.Clamp(note.Semitone - baseNote.Semitone, -MaxSemitoneShift, MaxSemitoneShift);
        return Math.Pow(2.0, shift / (double)SemitonesPerOctave);
    }

    /// <summary>The sample rate to hand BASS for that note, given the file's own rate.</summary>
    public static double FrequencyFor(Note note, Note baseNote, int sampleRate) =>
        sampleRate * For(note, baseNote);
}
