using System;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// The edit operations a pattern grid performs, as plain functions on a pattern and a cursor.
/// Keeping them here means the key handling in the view is a lookup table, not logic.
/// </summary>
/// <remarks>
/// It is also the only way a pattern is edited, and that is what makes the undo history
/// possible: <see cref="Watching"/> is rung inside every method here rather than at the call
/// sites, so an edit added later is recorded without anybody remembering to say so, and an edit
/// made from somewhere new is recorded too.
///
/// The one edit that does not come through here is a paste, which writes a whole block at once
/// and rings the same bell on its own way in.
/// </remarks>
public interface IPatternEdit
{
    /// <summary>
    /// Told before every edit, so something can keep what is about to be replaced.
    /// </summary>
    /// <remarks>
    /// Here rather than at the call sites, which is the whole reason this is the only way a
    /// pattern is edited: an edit added later is recorded without anybody remembering to say so,
    /// and an edit made from somewhere new is recorded too.
    ///
    /// A hook rather than a second interface because there is one of it, it is the application's
    /// own history, and a pattern knows nothing about songs or views. Nothing at all is set in a
    /// test or a tool, which is the other reason it is allowed to be nothing.
    ///
    /// It hangs off the editor rather than off the type, so whoever pointed it somewhere and
    /// whoever does the editing have to be holding the same editor. That is the one thing a
    /// static field got for nothing, and it is worth giving up: two songs open at once would
    /// have shared a hook.
    /// </remarks>
    Action<Pattern, string>? Watching { get; set; }

    /// <summary>Writes a note and the current instrument, leaving the other columns alone.</summary>
    /// <param name="pattern">The pattern being edited.</param>
    /// <param name="cursor">Where in it the note goes.</param>
    /// <param name="note">The note to write.</param>
    /// <param name="instrument">Which instrument the note names.</param>
    void EnterNote(Pattern pattern, PatternCursor cursor, Note note, int instrument);

    /// <summary>
    /// As above, and writes the volume column too. That is how a velocity sensitive keyboard
    /// records: NoVolume leaves the column as it was, which is what typing a note does.
    /// </summary>
    /// <param name="pattern">The pattern being edited.</param>
    /// <param name="cursor">Where in it the note goes.</param>
    /// <param name="note">The note to write.</param>
    /// <param name="instrument">Which instrument the note names.</param>
    /// <param name="volume">
    /// How hard it was played, or <see cref="TrackerCell.NoVolume"/> to leave the column alone.
    /// </param>
    void EnterNote(Pattern pattern, PatternCursor cursor, Note note, int instrument, int volume);

    /// <summary>
    /// Puts another note into the chord already being played onto one line, in pitch order.
    /// </summary>
    /// <remarks>
    /// A chord is not three simultaneous events. It is three events a few milliseconds apart in
    /// whatever order the fingers landed, so appending each one to the next free column records
    /// the same shape differently every time you play it: E G B on one take and E B G on the
    /// next. This puts each note where its pitch belongs and pushes the ones above it along, so
    /// the lowest voice is always the first column.
    ///
    /// That is more than tidiness once the new note action is anything but cut. A column is a
    /// voice and it carries across chords, so a column that is the bass in one chord and the
    /// top of the next has a voice leaping about inside it, releasing and sustaining across the
    /// leap. In pitch order each column stays the voice it was.
    ///
    /// A chord with nowhere left to go drops its highest note, which is the one that falls off
    /// the end when the rest are pushed along. Eight columns is as wide as a track goes, so this
    /// is reached by a ninth finger.
    ///
    /// The cell is written clean rather than merged into whatever was there. Every column this
    /// touches was either empty or holds a note this same chord put there a moment ago, so there
    /// is nothing of anybody's to keep, and merging would have a shifted note's effect column
    /// follow it into its new home.
    /// </remarks>
    /// <param name="pattern">The pattern being edited.</param>
    /// <param name="from">
    /// The line and track being played onto, with the note column the chord started in.
    /// </param>
    /// <param name="filled">How many of the chord's columns are already written.</param>
    /// <param name="note">The note to write.</param>
    /// <param name="instrument">Which instrument the note names.</param>
    /// <param name="volume">
    /// How hard it was played, or <see cref="TrackerCell.NoVolume"/> for no volume column.
    /// </param>
    /// <returns>Which note column it went into.</returns>
    int EnterChordNote(Pattern pattern, PatternCursor from, int filled, Note note, int instrument,
                       int volume);

    /// <summary>Writes a note-off, which stops the track without starting anything.</summary>
    /// <remarks>
    /// Written through the cell rather than over it. The note and the instrument are what a
    /// note-off replaces; the volume and the effect are not, and a line that stops a note is a
    /// perfectly ordinary place to also ride the volume down or run an effect. Emptying the
    /// cell first threw those away without saying so.
    /// </remarks>
    /// <param name="pattern">The pattern being edited.</param>
    /// <param name="cursor">Where the note-off goes.</param>
    void EnterNoteOff(Pattern pattern, PatternCursor cursor);

    /// <summary>
    /// Types one hex digit into the column under the cursor, shifting the existing value
    /// left the way a tracker's two-digit fields work. Does nothing on the note column.
    /// </summary>
    /// <param name="pattern">The pattern being edited.</param>
    /// <param name="cursor">Which cell and which column the digit is typed into.</param>
    /// <param name="digit">The key that was pressed.</param>
    /// <returns>True when the digit went in, so the caller knows to step down a line.</returns>
    bool EnterHexDigit(Pattern pattern, PatternCursor cursor, char digit);

    /// <summary>Sets the effect letter under the cursor, keeping the parameter.</summary>
    /// <param name="pattern">The pattern being edited.</param>
    /// <param name="cursor">Which cell, which has to be on the effect column.</param>
    /// <param name="command">The letter that was pressed.</param>
    /// <returns>True when the letter went in.</returns>
    bool EnterEffectCommand(Pattern pattern, PatternCursor cursor, char command);

    /// <summary>Clears the column under the cursor. On the note column, clears the whole cell.</summary>
    /// <param name="pattern">The pattern being edited.</param>
    /// <param name="cursor">Which cell and which column to clear.</param>
    void ClearAtCursor(Pattern pattern, PatternCursor cursor);

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
    /// <param name="pattern">The pattern being edited.</param>
    /// <param name="track">Which track's notes to level.</param>
    /// <param name="volume">The level to write, or <see cref="TrackerCell.NoVolume"/> to clear it.</param>
    int SetTrackVolume(Pattern pattern, int track, int volume);

    /// <summary>Moves every note on a track by semitones. Empty cells and note-offs are left alone.</summary>
    /// <param name="pattern">The pattern being edited.</param>
    /// <param name="track">Which track to move.</param>
    /// <param name="semitones">How far, up or down.</param>
    void TransposeTrack(Pattern pattern, int track, int semitones);

    /// <summary>
    /// Empties every cell in a block. What Delete does with a selection up, and the only way
    /// to take out a phrase rather than a note at a time.
    /// </summary>
    /// <param name="pattern">The pattern being edited.</param>
    /// <param name="selection">The block to empty.</param>
    /// <returns>How many cells actually changed.</returns>
    int ClearRegion(Pattern pattern, PatternSelection selection);

    /// <summary>Moves every note in a block, leaving note-offs and empty cells alone.</summary>
    /// <param name="pattern">The pattern being edited.</param>
    /// <param name="selection">The block to move.</param>
    /// <param name="semitones">How far, up or down.</param>
    /// <returns>How many cells actually changed.</returns>
    int TransposeRegion(Pattern pattern, PatternSelection selection, int semitones);

    /// <summary>Gives every note in a block the same volume, or takes the column off them.</summary>
    /// <param name="pattern">The pattern being edited.</param>
    /// <param name="selection">The block to level.</param>
    /// <param name="volume">The level to write, or <see cref="TrackerCell.NoVolume"/> to clear it.</param>
    /// <returns>How many cells actually changed.</returns>
    int SetRegionVolume(Pattern pattern, PatternSelection selection, int volume);

    /// <summary>Empties one track, leaving every other track as it was.</summary>
    /// <param name="pattern">The pattern being edited.</param>
    /// <param name="track">Which track to empty.</param>
    void ClearTrack(Pattern pattern, int track);

    /// <summary>Empties the whole pattern.</summary>
    /// <param name="pattern">The pattern to empty.</param>
    void ClearPattern(Pattern pattern);

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
    /// <param name="pattern">The pattern being edited.</param>
    /// <param name="track">Which track to pull onto the grid.</param>
    /// <param name="grid">Every nth line. One or less is no grid at all and does nothing.</param>
    int Quantize(Pattern pattern, int track, int grid);

    /// <summary>The nearest line that is a multiple of the grid, kept inside the pattern.</summary>
    /// <remarks>
    /// The last grid line can fall off the end of a pattern whose length is not a multiple of
    /// the grid, so the answer walks back a grid at a time until it is inside. A note pushed
    /// past the end is a note thrown away, and quantising is not allowed to cost anybody a note.
    /// </remarks>
    /// <param name="line">Where the cell is now.</param>
    /// <param name="grid">Every nth line.</param>
    /// <param name="lines">How long the pattern is, so the answer stays inside it.</param>
    int SnapLine(int line, int grid, int lines);

    /// <summary>
    /// Pushes every cell on a track down one line from the cursor, dropping the last one.
    /// The insert-line edit every tracker has.
    /// </summary>
    /// <remarks>
    /// Every note column of the track and not only the one the cursor is in. A chord is written
    /// across the columns of one line, so opening a hole in one column of it would leave the
    /// notes of that chord on different lines, which is not a thing anybody can have meant by
    /// pressing insert.
    /// </remarks>
    /// <param name="pattern">The pattern being edited.</param>
    /// <param name="cursor">Which track, and the line the hole opens at.</param>
    void InsertLine(Pattern pattern, PatternCursor cursor);

    /// <summary>Pulls every cell on a track up one line into the cursor, blanking the last.</summary>
    /// <remarks>Every note column of it, for the reason the insert covers them all.</remarks>
    /// <param name="pattern">The pattern being edited.</param>
    /// <param name="cursor">Which track, and the line that is taken out.</param>
    void DeleteLine(Pattern pattern, PatternCursor cursor);
}
