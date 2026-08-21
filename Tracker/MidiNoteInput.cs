using System;

namespace JingleBox2.Tracker;

/// <summary>
/// Turns MIDI note numbers and velocities into tracker values. Pure, so the mapping can be
/// checked without a keyboard plugged in.
/// </summary>
public static class MidiNoteInput
{
    /// <summary>MIDI note 60 is middle C, which is C-4 here, and semitones count from C-0.</summary>
    public const int SemitoneOffset = -12;

    public const int MinMidiNote = 0;
    public const int MaxMidiNote = 127;
    public const int MaxVelocity = 127;

    public static bool TryNote(int midiNote, out Note note)
    {
        note = Note.Empty;
        if (midiNote < MinMidiNote || midiNote > MaxMidiNote) return false;

        int semitone = midiNote + SemitoneOffset;
        if (semitone < Note.MinSemitone || semitone > Note.MaxSemitone) return false;

        note = new Note(semitone);
        return true;
    }

    /// <summary>Velocity on the cell's 0..64 scale. A keyboard that sends no velocity plays full.</summary>
    public static int VolumeFor(int velocity)
    {
        if (velocity <= 0) return 0;
        if (velocity >= MaxVelocity) return TrackerCell.MaxVolume;

        int volume = (int)Math.Round(velocity * (double)TrackerCell.MaxVolume / MaxVelocity,
            MidpointRounding.AwayFromZero);

        return Math.Clamp(volume, 0, TrackerCell.MaxVolume);
    }
}
