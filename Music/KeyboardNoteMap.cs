using System.Collections.Generic;
using JingleBox2.Music.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Music;

/// <inheritdoc/>
public sealed class KeyboardNoteMap : IKeyboardNoteMap
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
    ///
    /// Static and shared because it is the layout rather than anybody's state: one table for
    /// every hand that ever asks.
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

    /// <inheritdoc cref="IKeyboardNoteMap.NoteOffKey"/>
    public const string NoteOffKey = "Oem3";

    /// <inheritdoc cref="IKeyboardNoteMap.NoteOffDigit"/>
    public const string NoteOffDigit = "D1";

    /// <inheritdoc cref="IKeyboardNoteMap.NoteOffCapsLock"/>
    public const string NoteOffCapsLock = "CapsLock";

    /// <inheritdoc/>
    string IKeyboardNoteMap.NoteOffKey => NoteOffKey;

    /// <inheritdoc/>
    string IKeyboardNoteMap.NoteOffDigit => NoteOffDigit;

    /// <inheritdoc/>
    string IKeyboardNoteMap.NoteOffCapsLock => NoteOffCapsLock;

    /// <inheritdoc/>
    public bool IsNoteOff(string key) => key == NoteOffKey || key == NoteOffCapsLock;

    /// <inheritdoc/>
    public bool IsNoteOffInNotes(string key) => IsNoteOff(key) || key == NoteOffDigit;

    /// <inheritdoc/>
    public Note? NoteFor(string key, int octave)
    {
        if (!Offsets.TryGetValue(key, out int offset)) return null;

        int semitone = octave * 12 + offset;
        if (semitone < Note.MinSemitone || semitone > Note.MaxSemitone) return null;

        return new Note(semitone);
    }

    /// <inheritdoc/>
    public bool IsNoteKey(string key) => Offsets.ContainsKey(key);
}
