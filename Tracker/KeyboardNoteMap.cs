using System.Collections.Generic;

namespace JingleBox2.Tracker;

/// <summary>
/// The two-row piano layout trackers have used since the eighties: the lower letter row is
/// one octave, the upper row the next, with the black keys where they look right.
/// Keys are named as Avalonia reports them, so the view does no translation.
/// </summary>
public static class KeyboardNoteMap
{
    /// <summary>Semitone offset from the base octave, or null when the key is not a note key.</summary>
    private static readonly Dictionary<string, int> Offsets = new()
    {
        // Lower row: Z is C of the current octave.
        ["Z"] = 0,  ["S"] = 1,  ["X"] = 2,  ["D"] = 3,  ["C"] = 4,
        ["V"] = 5,  ["G"] = 6,  ["B"] = 7,  ["H"] = 8,  ["N"] = 9,
        ["J"] = 10, ["M"] = 11,
        ["OemComma"] = 12, ["L"] = 13, ["OemPeriod"] = 14, ["OemSemicolon"] = 15, ["Oem2"] = 16,

        // Upper row: Q is C one octave above.
        ["Q"] = 12, ["D2"] = 13, ["W"] = 14, ["D3"] = 15, ["E"] = 16,
        ["R"] = 17, ["D5"] = 18, ["T"] = 19, ["D6"] = 20, ["Y"] = 21,
        ["D7"] = 22, ["U"] = 23,
        ["I"] = 24, ["D9"] = 25, ["O"] = 26, ["D0"] = 27, ["P"] = 28
    };

    /// <summary>The key that writes a note-off into the cell.</summary>
    public const string NoteOffKey = "Oem3";

    public static bool IsNoteOff(string key) => key == NoteOffKey;

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

    public static bool IsNoteKey(string key) => Offsets.ContainsKey(key);
}
