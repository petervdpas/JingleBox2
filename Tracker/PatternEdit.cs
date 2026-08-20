using System;
using System.Globalization;

namespace JingleBox2.Tracker;

/// <summary>
/// The edit operations a pattern grid performs, as plain functions on a pattern and a cursor.
/// Keeping them here means the key handling in the view is a lookup table, not logic.
/// </summary>
public static class PatternEdit
{
    /// <summary>Writes a note and the current instrument, leaving the other columns alone.</summary>
    public static void EnterNote(Pattern pattern, PatternCursor cursor, Note note, int instrument)
    {
        if (!pattern.Contains(cursor.Line, cursor.Track)) return;

        var cell = pattern[cursor.Line, cursor.Track];
        pattern[cursor.Line, cursor.Track] = cell with { Note = note, Instrument = instrument };
    }

    /// <summary>Writes a note-off, which stops the track without starting anything.</summary>
    public static void EnterNoteOff(Pattern pattern, PatternCursor cursor)
    {
        if (!pattern.Contains(cursor.Line, cursor.Track)) return;

        pattern[cursor.Line, cursor.Track] = TrackerCell.Empty with { Note = Note.Off };
    }

    /// <summary>
    /// Types one hex digit into the column under the cursor, shifting the existing value
    /// left the way a tracker's two-digit fields work. Does nothing on the note column.
    /// </summary>
    public static bool EnterHexDigit(Pattern pattern, PatternCursor cursor, char digit)
    {
        if (!pattern.Contains(cursor.Line, cursor.Track)) return false;
        if (!TryHexValue(digit, out int value)) return false;

        var cell = pattern[cursor.Line, cursor.Track];

        switch (cursor.Column)
        {
            case CellColumn.Instrument:
                int instrument = cell.Instrument == TrackerCell.NoInstrument ? 0 : cell.Instrument;
                pattern[cursor.Line, cursor.Track] = cell with { Instrument = ShiftIn(instrument, value) };
                return true;

            case CellColumn.Volume:
                int volume = cell.Volume == TrackerCell.NoVolume ? 0 : cell.Volume;
                pattern[cursor.Line, cursor.Track] =
                    cell with { Volume = TrackerCell.ClampVolume(ShiftIn(volume, value)) };
                return true;

            case CellColumn.Effect:
                var effect = cell.Effect.IsNone
                    ? new TrackerEffect(TrackerEffect.SetVolume, 0)
                    : cell.Effect;
                pattern[cursor.Line, cursor.Track] =
                    cell with { Effect = effect with { Parameter = ShiftIn(effect.Parameter, value) } };
                return true;

            default:
                return false;
        }
    }

    /// <summary>Sets the effect letter under the cursor, keeping the parameter.</summary>
    public static bool EnterEffectCommand(Pattern pattern, PatternCursor cursor, char command)
    {
        if (!pattern.Contains(cursor.Line, cursor.Track)) return false;
        if (cursor.Column != CellColumn.Effect) return false;
        if (!char.IsLetter(command)) return false;

        var cell = pattern[cursor.Line, cursor.Track];
        pattern[cursor.Line, cursor.Track] =
            cell with { Effect = cell.Effect with { Command = char.ToUpperInvariant(command) } };
        return true;
    }

    /// <summary>Clears the column under the cursor. On the note column, clears the whole cell.</summary>
    public static void ClearAtCursor(Pattern pattern, PatternCursor cursor)
    {
        if (!pattern.Contains(cursor.Line, cursor.Track)) return;

        var cell = pattern[cursor.Line, cursor.Track];

        pattern[cursor.Line, cursor.Track] = cursor.Column switch
        {
            CellColumn.Note => TrackerCell.Empty,
            CellColumn.Instrument => cell with { Instrument = TrackerCell.NoInstrument },
            CellColumn.Volume => cell with { Volume = TrackerCell.NoVolume },
            CellColumn.Effect => cell with { Effect = TrackerEffect.None },
            _ => cell
        };
    }

    /// <summary>Moves every note on a track by semitones. Empty cells and note-offs are left alone.</summary>
    public static void TransposeTrack(Pattern pattern, int track, int semitones)
    {
        if (track < 0 || track >= pattern.TrackCount) return;

        for (int line = 0; line < pattern.Lines; line++)
        {
            var cell = pattern[line, track];
            if (!cell.Note.IsPlayable) continue;

            pattern[line, track] = cell with { Note = cell.Note.Transpose(semitones) };
        }
    }

    /// <summary>
    /// Pushes every cell on a track down one line from the cursor, dropping the last one.
    /// The insert-line edit every tracker has.
    /// </summary>
    public static void InsertLine(Pattern pattern, PatternCursor cursor)
    {
        if (!pattern.Contains(cursor.Line, cursor.Track)) return;

        for (int line = pattern.Lines - 1; line > cursor.Line; line--)
            pattern[line, cursor.Track] = pattern[line - 1, cursor.Track];

        pattern[cursor.Line, cursor.Track] = TrackerCell.Empty;
    }

    /// <summary>Pulls every cell on a track up one line into the cursor, blanking the last.</summary>
    public static void DeleteLine(Pattern pattern, PatternCursor cursor)
    {
        if (!pattern.Contains(cursor.Line, cursor.Track)) return;

        for (int line = cursor.Line; line < pattern.Lines - 1; line++)
            pattern[line, cursor.Track] = pattern[line + 1, cursor.Track];

        pattern[pattern.Lines - 1, cursor.Track] = TrackerCell.Empty;
    }

    private static int ShiftIn(int current, int digit) => (current * 16 + digit) & 0xFF;

    private static bool TryHexValue(char digit, out int value) =>
        int.TryParse(digit.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
}
