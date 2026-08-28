using System;
using System.Globalization;

namespace JingleBox2.Tracker;

/// <summary>
/// One note cell. Semitones count from C-0, so C-4 is 48. Two values are not notes:
/// <see cref="Empty"/> means the cell is blank and <see cref="Off"/> means "stop the voice".
/// </summary>
/// <param name="Semitone">
/// Semitones above C-0, or one of the two values that are not notes. Held as a plain number so a
/// note is a value type with nothing to allocate, which is what makes a pattern one array.
/// </param>
public readonly record struct Note(int Semitone)
{
    /// <summary>The semitone number a blank cell carries.</summary>
    public const int EmptyValue = -1;

    /// <summary>And the one a note-off carries. Below <see cref="EmptyValue"/>, so neither is playable.</summary>
    public const int OffValue = -2;

    /// <summary>C-0, the bottom of the range.</summary>
    public const int MinSemitone = 0;

    /// <summary>B-9, the top of it, which is as high as a three character column can name.</summary>
    public const int MaxSemitone = 119;

    /// <summary>How a blank cell is written and shown.</summary>
    public const string EmptyText = "---";

    /// <summary>How a note-off is written and shown. Three characters, as every column here is.</summary>
    public const string OffText = "OFF";

    /// <summary>
    /// What a note-off used to be written as. Read, never written.
    /// </summary>
    /// <remarks>
    /// The note column is what goes into a song file, so changing what a note-off looks like
    /// changes the file as well. Songs written before this still say the old thing and still
    /// have to open, so it stays understood; only what comes out is new.
    /// </remarks>
    public const string OldOffText = "===";

    /// <summary>A blank cell.</summary>
    public static readonly Note Empty = new(EmptyValue);

    /// <summary>A note-off, which silences the track without starting anything.</summary>
    public static readonly Note Off = new(OffValue);

    /// <summary>Middle C in tracker terms, the default base note for a sample.</summary>
    public static readonly Note C4 = new(48);

    /// <summary>
    /// The twelve names, each two characters wide.
    /// </summary>
    /// <remarks>
    /// Sharps only, with a hyphen padding the naturals, because a column three characters wide
    /// has no room for both spellings and every tracker has settled on this one.
    /// </remarks>
    private static readonly string[] NoteNames =
        { "C-", "C#", "D-", "D#", "E-", "F-", "F#", "G-", "G#", "A-", "A#", "B-" };

    /// <summary>True for a blank cell.</summary>
    public bool IsEmpty => Semitone == EmptyValue;

    /// <summary>True for a note-off.</summary>
    public bool IsOff => Semitone == OffValue;

    /// <summary>True for a note that would actually sound, which is neither of the other two.</summary>
    public bool IsPlayable => Semitone >= MinSemitone && Semitone <= MaxSemitone;

    /// <summary>Which octave it is in, or -1 when it is not a note.</summary>
    public int Octave => IsPlayable ? Semitone / 12 : -1;

    /// <summary>A note from where it sits in an octave and which octave that is.</summary>
    public static Note FromOctave(int noteInOctave, int octave) => new(octave * 12 + noteInOctave);

    /// <summary>Moves by semitones, clamped to the playable range. Empty and off do not move.</summary>
    public Note Transpose(int semitones)
    {
        if (!IsPlayable) return this;
        return new Note(Math.Clamp(Semitone + semitones, MinSemitone, MaxSemitone));
    }

    /// <summary>Three characters, always: "C-4", "OFF" or "---".</summary>
    public override string ToString()
    {
        if (IsOff) return OffText;
        if (!IsPlayable) return EmptyText;
        return NoteNames[Semitone % 12] + (Semitone / 12).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Parses "C-4", "C#4", "---", "OFF" and the older "===". Anything else is false.</summary>
    /// <remarks>
    /// Blank text is an empty note rather than a failure, since a cell the file did not store
    /// arrives here as nothing at all.
    /// </remarks>
    public static bool TryParse(string? text, out Note note)
    {
        note = Empty;
        if (string.IsNullOrWhiteSpace(text)) return true;

        string s = text.Trim();
        if (s == EmptyText) { note = Empty; return true; }
        if (s == OffText || s == OldOffText) { note = Off; return true; }
        if (s.Length != 3) return false;

        string name = s[..2].ToUpperInvariant();
        int index = Array.IndexOf(NoteNames, name);
        if (index < 0) return false;

        if (!int.TryParse(s[2..], NumberStyles.None, CultureInfo.InvariantCulture, out int octave)) return false;
        if (octave is < 0 or > 9) return false;

        note = FromOctave(index, octave);
        return true;
    }
}
