using System.Collections.Generic;

namespace JingleBox2.Tracker;

/// <summary>
/// The two-row piano layout trackers have used since the eighties: the lower letter row is
/// one octave, the upper row the next, with the black keys where they look right.
/// Keys are named as Avalonia reports them, so the view does no translation.
/// </summary>
public static class KeyboardNoteMap
{
    /// <summary>
    /// Semitone offset from the base octave, or null when the key is not a note key.
    /// </summary>
    /// <remarks>
    /// Two rows of the letter keyboard, each a keyboard of its own. On the lower row Z is C of
    /// the current octave, with S and D standing in for the black keys above it; on the upper
    /// row Q is C an octave higher, with the digit row doing the same job. The rows overlap by
    /// five keys at the join, which is deliberate and is what every tracker does: the notes
    /// under a hand resting on the middle of the keyboard can be reached from either row.
    /// </remarks>
    private static readonly Dictionary<string, int> Offsets = new()
    {
        ["Z"] = 0,  ["S"] = 1,  ["X"] = 2,  ["D"] = 3,  ["C"] = 4,
        ["V"] = 5,  ["G"] = 6,  ["B"] = 7,  ["H"] = 8,  ["N"] = 9,
        ["J"] = 10, ["M"] = 11,
        ["OemComma"] = 12, ["L"] = 13, ["OemPeriod"] = 14, ["OemSemicolon"] = 15, ["Oem2"] = 16,

        ["Q"] = 12, ["D2"] = 13, ["W"] = 14, ["D3"] = 15, ["E"] = 16,
        ["R"] = 17, ["D5"] = 18, ["T"] = 19, ["D6"] = 20, ["Y"] = 21,
        ["D7"] = 22, ["U"] = 23,
        ["I"] = 24, ["D9"] = 25, ["O"] = 26, ["D0"] = 27, ["P"] = 28
    };

    /// <summary>The key that writes a note-off wherever the cursor is.</summary>
    public const string NoteOffKey = "Oem3";

    /// <summary>
    /// The other key that writes one, and only while the cursor is on the note column.
    /// </summary>
    /// <remarks>
    /// The digit row is how the instrument and volume columns are typed, so a 1 that always
    /// meant note-off would be a 1 that could never be typed into a volume. On the note column
    /// there is no such thing as typing a digit, so there it is free to mean what it means in
    /// every other tracker.
    /// </remarks>
    public const string NoteOffDigit = "D1";

    /// <summary>
    /// Caps lock, which is where Renoise puts a note-off and where a hand coming from Renoise
    /// will reach for it. Works from any column, as it does there.
    /// </summary>
    /// <remarks>
    /// It goes on being caps lock as well: pressing it still turns the light on and off and
    /// still shifts what the letter keys type, because that happens in the X server long
    /// before anything here is told about it. Nothing can be done about that from this side,
    /// and Renoise on the same machine behaves the same way.
    /// </remarks>
    public const string NoteOffCapsLock = "CapsLock";

    /// <summary>True for the keys that write a note-off from any column.</summary>
    public static bool IsNoteOff(string key) => key == NoteOffKey || key == NoteOffCapsLock;

    /// <summary>True for any of them, for use when the cursor is on the note column.</summary>
    public static bool IsNoteOffInNotes(string key) => IsNoteOff(key) || key == NoteOffDigit;

    /// <summary>
    /// The note this key plays at the given octave, or null if the key is not part of the
    /// layout. Notes past the top of the range are refused rather than clamped, so holding
    /// a high octave does not pile every key onto B-9.
    /// </summary>
    public static Note? NoteFor(string key, int octave)
    {
        if (!Offsets.TryGetValue(key, out int offset)) return null;

        int semitone = octave * 12 + offset;
        if (semitone < Note.MinSemitone || semitone > Note.MaxSemitone) return null;

        return new Note(semitone);
    }

    /// <summary>
    /// True for a key that is part of the layout at all.
    /// </summary>
    /// <remarks>
    /// Asked separately from <see cref="NoteFor"/> because they answer different questions: this
    /// one says whether a key press belongs to the keyboard, and that one says what it plays.
    /// A key that is on the layout but out of range at this octave is still the keyboard's, and
    /// must not fall through to whatever the letter would otherwise have done.
    /// </remarks>
    public static bool IsNoteKey(string key) => Offsets.ContainsKey(key);
}
