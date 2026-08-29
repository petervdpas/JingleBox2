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

    /// <summary>Velocity as the volume column holds it, which is the same number.</summary>
    /// <remarks>
    /// Nothing is scaled. The column runs to <see cref="TrackerCell.MaxVolume"/>, which is 128,
    /// and MIDI has 128 velocities, so a hit written into the pattern is the number the keyboard
    /// sent and can be read back against whatever the keyboard says it sent. That is the whole
    /// point of the column being 128 wide: on the old 64 scale this was a division, two keys
    /// struck a little apart landed on one number, and there was no way to tell a rounding from
    /// a hand.
    ///
    /// Which leaves 0x80 above anything a key can produce. It is not a gap: it is the level a
    /// person types in when they want a note louder than they can play, and a key at full
    /// velocity is 0x7F, a fifteenth of a decibel under it.
    /// </remarks>
    /// <param name="velocity">How hard the key was struck, 0 to 127.</param>
    int VolumeFor(int velocity);
}
