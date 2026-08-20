using System;
using System.Globalization;

namespace JingleBox2.Tracker;

/// <summary>
/// One note cell. Semitones count from C-0, so C-4 is 48. Two values are not notes:
/// <see cref="Empty"/> means the cell is blank and <see cref="Off"/> means "stop the voice".
/// </summary>
public readonly record struct Note(int Semitone)
{
    public const int EmptyValue = -1;
    public const int OffValue = -2;

    public const int MinSemitone = 0;    // C-0
    public const int MaxSemitone = 119;  // B-9

    public const string EmptyText = "---";
    public const string OffText = "===";

    public static readonly Note Empty = new(EmptyValue);
    public static readonly Note Off = new(OffValue);

    /// <summary>Middle C in tracker terms, the default base note for a sample.</summary>
    public static readonly Note C4 = new(48);

    private static readonly string[] NoteNames =
        { "C-", "C#", "D-", "D#", "E-", "F-", "F#", "G-", "G#", "A-", "A#", "B-" };

    public bool IsEmpty => Semitone == EmptyValue;
    public bool IsOff => Semitone == OffValue;
    public bool IsPlayable => Semitone >= MinSemitone && Semitone <= MaxSemitone;

    public int Octave => IsPlayable ? Semitone / 12 : -1;

    public static Note FromOctave(int noteInOctave, int octave) => new(octave * 12 + noteInOctave);

    /// <summary>Moves by semitones, clamped to the playable range. Empty and off do not move.</summary>
    public Note Transpose(int semitones)
    {
        if (!IsPlayable) return this;
        return new Note(Math.Clamp(Semitone + semitones, MinSemitone, MaxSemitone));
    }

    public override string ToString()
    {
        if (IsOff) return OffText;
        if (!IsPlayable) return EmptyText;
        return NoteNames[Semitone % 12] + (Semitone / 12).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Parses "C-4", "C#4", "---" and "===". Returns false for anything else.</summary>
    public static bool TryParse(string? text, out Note note)
    {
        note = Empty;
        if (string.IsNullOrWhiteSpace(text)) return true;

        string s = text.Trim();
        if (s == EmptyText) { note = Empty; return true; }
        if (s == OffText) { note = Off; return true; }
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
