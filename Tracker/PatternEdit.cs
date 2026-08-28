using System;
using System.Globalization;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker;

/// <summary>
/// The edit operations a pattern grid performs, as plain functions on a pattern and a cursor.
/// Keeping them here means the key handling in the view is a lookup table, not logic.
/// </summary>
public static class PatternEdit
{
    /// <summary>
    /// Told before every edit, so something can keep what is about to be replaced.
    /// </summary>
    /// <remarks>
    /// Here rather than at the call sites, which is the whole reason this class is the only way
    /// a pattern is edited: an edit added later is recorded without anybody remembering to say
    /// so, and an edit made from somewhere new is recorded too.
    ///
    /// A hook and not an interface because there is one of it, it is the application's own
    /// history, and a pattern knows nothing about songs or views. Nothing at all is set in a
    /// test or a tool, which is the other reason it is allowed to be nothing.
    /// </remarks>
    public static Action<Pattern, string>? Watching;

    /// <summary>
    /// Rings <see cref="Watching"/> for one edit, and does nothing when there is no pattern.
    /// </summary>
    /// <remarks>
    /// Every method here calls this first, before it has decided whether it will change
    /// anything. A step for an edit that turned out to do nothing costs nothing: the history
    /// notices that the pattern still holds what the last step kept and reuses it.
    /// </remarks>
    private static void Taking(Pattern? pattern, string what)
    {
        if (pattern is not null) Watching?.Invoke(pattern, what);
    }

    /// <summary>Writes a note and the current instrument, leaving the other columns alone.</summary>
    public static void EnterNote(Pattern pattern, PatternCursor cursor, Note note, int instrument) =>
        EnterNote(pattern, cursor, note, instrument, TrackerCell.NoVolume);

    /// <summary>
    /// As above, and writes the volume column too. That is how a velocity sensitive keyboard
    /// records: NoVolume leaves the column as it was, which is what typing a note does.
    /// </summary>
    public static void EnterNote(Pattern pattern, PatternCursor cursor, Note note, int instrument, int volume)
    {
        Taking(pattern, "a note");

        if (!pattern.Contains(cursor.Line, cursor.Track)) return;

        var cell = pattern[cursor.Line, cursor.Track];

        pattern[cursor.Line, cursor.Track] = volume == TrackerCell.NoVolume
            ? cell with { Note = note, Instrument = instrument }
            : cell with { Note = note, Instrument = instrument, Volume = TrackerCell.ClampVolume(volume) };
    }

    /// <summary>Writes a note-off, which stops the track without starting anything.</summary>
    /// <remarks>
    /// Written through the cell rather than over it. The note and the instrument are what a
    /// note-off replaces; the volume and the effect are not, and a line that stops a note is a
    /// perfectly ordinary place to also ride the volume down or run an effect. Emptying the
    /// cell first threw those away without saying so.
    /// </remarks>
    public static void EnterNoteOff(Pattern pattern, PatternCursor cursor)
    {
        Taking(pattern, "a note off");

        if (!pattern.Contains(cursor.Line, cursor.Track)) return;

        var cell = pattern[cursor.Line, cursor.Track];

        pattern[cursor.Line, cursor.Track] = cell with
        {
            Note = Note.Off,
            Instrument = TrackerCell.NoInstrument
        };
    }

    /// <summary>
    /// Types one hex digit into the column under the cursor, shifting the existing value
    /// left the way a tracker's two-digit fields work. Does nothing on the note column.
    /// </summary>
    public static bool EnterHexDigit(Pattern pattern, PatternCursor cursor, char digit)
    {
        Taking(pattern, "a digit");

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
        Taking(pattern, "an effect");

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
        Taking(pattern, "clearing a cell");

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

    /// <summary>
    /// Gives every note on a track the same volume, or takes the volume column off them
    /// entirely with <see cref="TrackerCell.NoVolume"/>, which leaves the instrument's own
    /// level to decide. What a velocity sensitive keyboard needs after a take: a kick that
    /// came out a little different every time becomes one kick again. Returns how many changed.
    /// </summary>
    /// <remarks>
    /// Only cells that sound a note are touched. A note-off with a level would be a
    /// contradiction, and a level on an empty cell is invisible: nothing would ever play it and
    /// nothing on the screen would say it was there.
    /// </remarks>
    public static int SetTrackVolume(Pattern pattern, int track, int volume)
    {
        Taking(pattern, "a track's volume");

        if (track < 0 || track >= pattern.TrackCount) return 0;

        int wanted = volume == TrackerCell.NoVolume
            ? TrackerCell.NoVolume
            : Math.Clamp(volume, 0, TrackerCell.MaxVolume);

        int changed = 0;

        for (int line = 0; line < pattern.Lines; line++)
        {
            var cell = pattern[line, track];

            if (!cell.Note.IsPlayable || cell.Volume == wanted) continue;

            pattern[line, track] = cell with { Volume = wanted };
            changed++;
        }

        return changed;
    }

    /// <summary>Moves every note on a track by semitones. Empty cells and note-offs are left alone.</summary>
    public static void TransposeTrack(Pattern pattern, int track, int semitones)
    {
        Taking(pattern, "transposing a track");

        if (track < 0 || track >= pattern.TrackCount) return;

        for (int line = 0; line < pattern.Lines; line++)
        {
            var cell = pattern[line, track];
            if (!cell.Note.IsPlayable) continue;

            pattern[line, track] = cell with { Note = cell.Note.Transpose(semitones) };
        }
    }

    /// <summary>
    /// Empties every cell in a block. What Delete does with a selection up, and the only way
    /// to take out a phrase rather than a note at a time.
    /// </summary>
    public static int ClearRegion(Pattern pattern, PatternSelection selection)
    {
        Taking(pattern, "clearing a selection");

        return Apply(pattern, selection, cell => TrackerCell.Empty);
    }

    /// <summary>Moves every note in a block, leaving note-offs and empty cells alone.</summary>
    public static int TransposeRegion(Pattern pattern, PatternSelection selection, int semitones)
    {
        Taking(pattern, "transposing a selection");

        return Apply(pattern, selection, cell =>
            cell.Note.IsPlayable ? cell with { Note = cell.Note.Transpose(semitones) } : cell);
    }

    /// <summary>Gives every note in a block the same volume, or takes the column off them.</summary>
    public static int SetRegionVolume(Pattern pattern, PatternSelection selection, int volume)
    {
        Taking(pattern, "a selection's volume");

        int wanted = volume == TrackerCell.NoVolume
            ? TrackerCell.NoVolume
            : Math.Clamp(volume, 0, TrackerCell.MaxVolume);

        return Apply(pattern, selection, cell =>
            cell.Note.IsPlayable ? cell with { Volume = wanted } : cell);
    }

    /// <summary>
    /// Runs an edit over every cell of a block and reports how many it actually changed.
    /// Cells the edit leaves alone are not counted, so "3 notes moved" means three.
    /// </summary>
    private static int Apply(Pattern pattern, PatternSelection selection, Func<TrackerCell, TrackerCell> edit)
    {
        if (selection.IsEmpty) return 0;

        var block = selection.Clamp(pattern.Lines, pattern.TrackCount);
        if (block.IsEmpty) return 0;

        int changed = 0;

        for (int line = block.FirstLine; line <= block.LastLine; line++)
        {
            for (int track = block.FirstTrack; track <= block.LastTrack; track++)
            {
                var cell = pattern[line, track];
                var edited = edit(cell);

                if (edited == cell) continue;

                pattern[line, track] = edited;
                changed++;
            }
        }

        return changed;
    }

    /// <summary>Empties one track, leaving every other track as it was.</summary>
    public static void ClearTrack(Pattern pattern, int track)
    {
        Taking(pattern, "clearing a track");

        if (track < 0 || track >= pattern.TrackCount) return;

        for (int line = 0; line < pattern.Lines; line++)
            pattern[line, track] = TrackerCell.Empty;
    }

    /// <summary>Empties the whole pattern.</summary>
    public static void ClearPattern(Pattern pattern)
    {
        Taking(pattern, "clearing the pattern");

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
        Taking(pattern, "quantising");

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
    /// <remarks>
    /// The last grid line can fall off the end of a pattern whose length is not a multiple of
    /// the grid, so the answer walks back a grid at a time until it is inside. A note pushed
    /// past the end is a note thrown away, and quantising is not allowed to cost anybody a note.
    /// </remarks>
    public static int SnapLine(int line, int grid, int lines)
    {
        if (grid <= 1 || lines <= 0) return Math.Clamp(line, 0, Math.Max(0, lines - 1));

        int snapped = (int)Math.Round(line / (double)grid, MidpointRounding.AwayFromZero) * grid;

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
        Taking(pattern, "inserting a line");

        if (!pattern.Contains(cursor.Line, cursor.Track)) return;

        for (int line = pattern.Lines - 1; line > cursor.Line; line--)
            pattern[line, cursor.Track] = pattern[line - 1, cursor.Track];

        pattern[cursor.Line, cursor.Track] = TrackerCell.Empty;
    }

    /// <summary>Pulls every cell on a track up one line into the cursor, blanking the last.</summary>
    public static void DeleteLine(Pattern pattern, PatternCursor cursor)
    {
        Taking(pattern, "deleting a line");

        if (!pattern.Contains(cursor.Line, cursor.Track)) return;

        for (int line = cursor.Line; line < pattern.Lines - 1; line++)
            pattern[line, cursor.Track] = pattern[line + 1, cursor.Track];

        pattern[pattern.Lines - 1, cursor.Track] = TrackerCell.Empty;
    }

    /// <summary>
    /// Shifts a hex digit into the low nibble of a two-digit field, dropping what falls off the
    /// top, which is how a tracker's two-digit columns are typed.
    /// </summary>
    private static int ShiftIn(int current, int digit) => (current * 16 + digit) & 0xFF;

    /// <summary>What a hex digit is worth, or false for a key that is not one.</summary>
    private static bool TryHexValue(char digit, out int value) =>
        int.TryParse(digit.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
}
