using System;

namespace JingleBox2.Tracker.Synth;

/// <summary>Concert pitch: tracker semitones to Hz, with C-4 landing on middle C.</summary>
public static class NoteFrequency
{
    public const double A4Hz = 440.0;

    /// <summary>A-4 is semitone 57 here, the same note MIDI calls 69.</summary>
    public const int A4Semitone = 57;

    public static double Hz(Note note) => Hz(note.Semitone);

    public static double Hz(int semitone) => A4Hz * Math.Pow(2.0, (semitone - A4Semitone) / 12.0);

}
