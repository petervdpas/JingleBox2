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
    public static void EnterNote(Pattern pattern, PatternCursor cursor, Note note, int instrument) =>
        EnterNote(pattern, cursor, note, instrument, TrackerCell.NoVolume);

    /// <summary>
    /// As above, and writes the volume column too. That is how a velocity sensitive keyboard
    /// records: NoVolume leaves the column as it was, which is what typing a note does.
    /// </summary>
    public static void EnterNote(Pattern pattern, PatternCursor cursor, Note note, int instrument, int volume)
    {
        if (!pattern.Contains(cursor.Line, cursor.Track)) return;

        var cell = pattern[cursor.Line, cursor.Track];

        pattern[cursor.Line, cursor.Track] = volume == TrackerCell.NoVolume
            ? cell with { Note = note, Instrument = instrument }
            : cell with { Note = note, Instrument = instrument, Volume = TrackerCell.ClampVolume(volume) };
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

    /// <summary>Empties one track, leaving every other track as it was.</summary>
    public static void ClearTrack(Pattern pattern, int track)
    {
        if (track < 0 || track >= pattern.TrackCount) return;

        for (int line = 0; line < pattern.Lines; line++)
            pattern[line, track] = TrackerCell.Empty;
    }

    /// <summary>Empties the whole pattern.</summary>
    public static void ClearPattern(Pattern pattern)
    {
        for (int track = 0; track < pattern.TrackCount; track++)
            ClearTrack(pattern, track);
    }

    /// <summary>
    /// Snaps a track's cells onto every nth line, which is what a tracker means by quantizing:
    /// notes played in live sit a line or two off the beat, and this pulls them onto it.
    /// Returns how many moved.
    /// </summary>
    /// <remarks>
    /// Nothing is ever lost. Where two cells want the same line, the second keeps the line it
    /// was already on, and if that is taken too it takes the nearest free line to where it was
    /// meant to land. A tidy pattern is worth less than the notes in it.
    /// </remarks>
    public static int Quantize(Pattern pattern, int track, int grid)
    {
        if (track < 0 || track >= pattern.TrackCount || grid <= 1) return 0;

        var placed = new TrackerCell[pattern.Lines];
        for (int line = 0; line < placed.Length; line++) placed[line] = TrackerCell.Empty;

        int moved = 0;

        for (int line = 0; line < pattern.Lines; line++)
        {
            var cell = pattern[line, track];
            if (cell.IsEmpty) continue;

            int target = SnapLine(line, grid, pattern.Lines);

            if (!placed[target].IsEmpty) target = placed[line].IsEmpty ? line : NearestFree(placed, target);
            if (target < 0) continue;

            placed[target] = cell;
            if (target != line) moved++;
        }

        for (int line = 0; line < pattern.Lines; line++)
            pattern[line, track] = placed[line];

        return moved;
    }

    /// <summary>The nearest line that is a multiple of the grid, kept inside the pattern.</summary>
    public static int SnapLine(int line, int grid, int lines)
    {
        if (grid <= 1 || lines <= 0) return Math.Clamp(line, 0, Math.Max(0, lines - 1));

        int snapped = (int)Math.Round(line / (double)grid, MidpointRounding.AwayFromZero) * grid;

        // The last grid line can fall off the end of a pattern whose length is not a multiple
        // of the grid, and a note pushed past the end is a note thrown away.
        while (snapped > lines - 1) snapped -= grid;

        return Math.Max(0, snapped);
    }

    /// <summary>The free line closest to where a cell wanted to go, or -1 when the track is full.</summary>
    private static int NearestFree(TrackerCell[] placed, int wanted)
    {
        for (int distance = 1; distance < placed.Length; distance++)
        {
            int before = wanted - distance;
            if (before >= 0 && placed[before].IsEmpty) return before;

            int after = wanted + distance;
            if (after < placed.Length && placed[after].IsEmpty) return after;
        }

        return -1;
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
