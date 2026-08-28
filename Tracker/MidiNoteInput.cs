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

    /// <summary>The lowest note number MIDI has.</summary>
    public const int MinMidiNote = 0;

    /// <summary>And the highest, since a note number is seven bits.</summary>
    public const int MaxMidiNote = 127;

    /// <summary>The hardest a key can be struck, which is seven bits as well.</summary>
    public const int MaxVelocity = 127;

    /// <summary>
    /// The tracker note that MIDI note number means, or false when it falls outside the range.
    /// </summary>
    /// <remarks>
    /// Refused rather than clamped, both ends. A note out of range is a keyboard transposed past
    /// what a pattern can hold, and clamping would pile every key beyond the edge onto one note.
    /// </remarks>
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
    /// <remarks>
    /// The two ends are answered exactly rather than by the division, since 127 scaled and
    /// rounded is 64 only because the rounding happens to go that way, and a full-strength key
    /// writing 63 is the kind of thing nobody would ever find.
    /// </remarks>
    public static int VolumeFor(int velocity)
    {
        if (velocity <= 0) return 0;
        if (velocity >= MaxVelocity) return TrackerCell.MaxVolume;

        int volume = (int)Math.Round(velocity * (double)TrackerCell.MaxVolume / MaxVelocity,
            MidpointRounding.AwayFromZero);

        return Math.Clamp(volume, 0, TrackerCell.MaxVolume);
    }
}
