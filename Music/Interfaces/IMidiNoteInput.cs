using JingleBox2.Tracker.Records;

namespace JingleBox2.Music.Interfaces;

/// <summary>
/// Turns MIDI note numbers and velocities into tracker values. Pure, so the mapping can be
/// checked without a keyboard plugged in.
/// </summary>
public interface IMidiNoteInput
{
    /// <summary>MIDI note 60 is middle C, which is C-4 here, and semitones count from C-0.</summary>
    int SemitoneOffset { get; }

    /// <summary>The lowest note number MIDI has.</summary>
    int MinMidiNote { get; }

    /// <summary>And the highest, since a note number is seven bits.</summary>
    int MaxMidiNote { get; }

    /// <summary>The hardest a key can be struck, which is seven bits as well.</summary>
    int MaxVelocity { get; }

    /// <summary>
    /// The tracker note that MIDI note number means, or false when it falls outside the range.
    /// </summary>
    /// <remarks>
    /// Refused rather than clamped, both ends. A note out of range is a keyboard transposed past
    /// what a pattern can hold, and clamping would pile every key beyond the edge onto one note.
    /// </remarks>
    /// <param name="midiNote">The note number off the wire.</param>
    /// <param name="note">The tracker note it means, or <c>Note.Empty</c> when there is none.</param>
    bool TryNote(int midiNote, out Note note);

    /// <summary>Velocity on the cell's 0..64 scale. A keyboard that sends no velocity plays full.</summary>
    /// <remarks>
    /// The two ends are answered exactly rather than by the division, since 127 scaled and
    /// rounded is 64 only because the rounding happens to go that way, and a full-strength key
    /// writing 63 is the kind of thing nobody would ever find.
    /// </remarks>
    /// <param name="velocity">How hard the key was struck, 0 to 127.</param>
    int VolumeFor(int velocity);
}
