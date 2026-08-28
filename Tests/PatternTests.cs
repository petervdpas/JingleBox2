using JingleBox2.Tracker;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A pattern: cells in one array, and the operations that move them about.
/// </summary>
public class PatternTests
{
    /// <summary>
    /// A cursor at a line and track, so the tests read as positions rather than pairs.
    /// </summary>
    private static PatternCursor At(int line, int track = 0) => new() { Line = line, Track = track };

    /// <summary>
    /// A pattern with four notes down the first track, which is enough to tell a resize, a clone
    /// and a restore apart from doing nothing.
    /// </summary>
    private static Pattern WithNotes(int lines = 16, int tracks = 4)
    {
        var pattern = new Pattern(lines, tracks);

        for (int line = 0; line < 4; line++)
            PatternEdit.EnterNote(pattern, At(line), new Note(60 + line), 0);

        return pattern;
    }

    /// <summary>
    /// Every cell starts empty, since the array is made once and never cleared again.
    /// </summary>
    [Fact]
    public void A_new_pattern_is_empty()
    {
        var pattern = new Pattern(16, 4);

        for (int line = 0; line < 16; line++)
            for (int track = 0; track < 4; track++)
                Assert.True(pattern[line, track].IsEmpty);
    }

    /// <summary>
    /// A pattern asked for a size it cannot be is made the nearest size it can, rather than
    /// throwing, because the size arrives from a song file that may have been written by hand.
    /// </summary>
    [Fact]
    public void Its_shape_is_held_between_its_limits()
    {
        Assert.Equal(Pattern.MinLines, new Pattern(0, 4).Lines);
        Assert.Equal(Pattern.MaxLines, new Pattern(9999, 4).Lines);
        Assert.Equal(Song.MaxTrackCount, new Pattern(16, 999).TrackCount);
    }

    /// <summary>
    /// Setting a cell to what it already holds raises nothing, which is what stops a redraw and
    /// an undo step being spent on a keystroke that moved nothing.
    /// </summary>
    [Fact]
    public void Writing_the_same_value_again_says_nothing()
    {
        var pattern = new Pattern(16, 4);
        int said = 0;

        pattern.Changed += (_, _) => said++;

        pattern[0, 0] = new TrackerCell(new Note(60), 0, TrackerCell.NoVolume, default);
        Assert.Equal(1, said);

        pattern[0, 0] = new TrackerCell(new Note(60), 0, TrackerCell.NoVolume, default);
        Assert.Equal(1, said);
    }

    /// <summary>
    /// Shortening a pattern keeps the lines that survive rather than starting the array again,
    /// so shrinking and growing back costs only what fell off the end.
    /// </summary>
    [Fact]
    public void Resizing_keeps_whatever_still_fits()
    {
        var pattern = WithNotes();

        pattern.Resize(2);

        Assert.Equal(2, pattern.Lines);
        Assert.True(pattern[0, 0].Note.IsPlayable);
        Assert.True(pattern[1, 0].Note.IsPlayable);
    }

    /// <summary>
    /// Dragging the fourth track in front of the first should leave the others in order, one place
    /// along, which is what somebody dragging a column expects and what a swap does not do.
    /// </summary>
    [Fact]
    public void Moving_a_track_slides_the_others_rather_than_swapping()
    {
        var pattern = new Pattern(4, 4);

        for (int track = 0; track < 4; track++)
            PatternEdit.EnterNote(pattern, At(0, track), new Note(60 + track), 0);

        pattern.MoveTrack(3, 0);

        Assert.Equal(63, pattern[0, 0].Note.Semitone);
        Assert.Equal(60, pattern[0, 1].Note.Semitone);
        Assert.Equal(61, pattern[0, 2].Note.Semitone);
        Assert.Equal(62, pattern[0, 3].Note.Semitone);
    }

    /// <summary>
    /// A clone is a copy of the cells and not a second view of them, which is what a history step
    /// depends on: clearing the original must leave the copy holding the notes.
    /// </summary>
    [Fact]
    public void A_clone_shares_nothing()
    {
        var pattern = WithNotes();
        var copy = pattern.Clone();

        PatternEdit.ClearPattern(pattern);

        Assert.True(copy[0, 0].Note.IsPlayable);
    }

    /// <summary>
    /// The round trip a history step is made of: keep the cells and the lanes, edit, ask whether
    /// the pattern still holds them, and put them back.
    /// </summary>
    /// <remarks>
    /// Putting a step back says so once, rather than a change per cell: putting a step back is one
    /// thing that happened, and a change per cell would redraw the grid several thousand times for
    /// one press of undo.
    /// </remarks>
    [Fact]
    public void Cells_can_be_kept_and_put_back_whole()
    {
        var pattern = WithNotes();

        var kept = pattern.Cells();
        var lanes = pattern.LaneCopy();
        int lines = pattern.Lines, tracks = pattern.TrackCount;

        Assert.True(pattern.Holds(kept, lines, tracks, lanes));

        PatternEdit.ClearPattern(pattern);
        Assert.False(pattern.Holds(kept, lines, tracks, lanes));

        int said = 0;
        pattern.Changed += (_, _) => said++;

        pattern.Restore(kept, lines, tracks, lanes);

        Assert.True(pattern[0, 0].Note.IsPlayable);

        Assert.Equal(1, said);
    }

    /// <summary>
    /// A copy that does not match the pattern it is being poured into is refused rather than
    /// read past its end, since a step can outlive a resize.
    /// </summary>
    [Fact]
    public void Putting_back_a_copy_of_the_wrong_size_does_nothing()
    {
        var pattern = WithNotes();

        pattern.Restore(new TrackerCell[3], 16, 4, null);

        Assert.True(pattern[0, 0].Note.IsPlayable);
    }
}

/// <summary>The edits themselves, which are the only door a pattern is changed through.</summary>
public class PatternEditTests
{
    /// <summary>
    /// A cursor at a line and track, so the tests read as positions rather than pairs.
    /// </summary>
    private static PatternCursor At(int line, int track = 0) => new() { Line = line, Track = track };

    /// <summary>
    /// Typing over a note replaces the note and the instrument and nothing else.
    /// </summary>
    /// <remarks>
    /// No volume given leaves the column as it was, which is what typing a note does: the volume
    /// column is edited on its own, and a note retyped over a quiet one must not shout.
    /// </remarks>
    [Fact]
    public void A_note_writes_the_note_and_the_instrument_and_leaves_the_rest()
    {
        var pattern = new Pattern(16, 4);

        PatternEdit.EnterNote(pattern, At(0), new Note(60), instrument: 3, volume: 40);
        PatternEdit.EnterNote(pattern, At(0), new Note(62), instrument: 5);

        Assert.Equal(62, pattern[0, 0].Note.Semitone);
        Assert.Equal(5, pattern[0, 0].Instrument);

        Assert.Equal(40, pattern[0, 0].Volume);
    }

    /// <summary>
    /// Transposing works down one track and touches nothing in the track next to it, which is
    /// the part that goes wrong quietly when an index is worked out the wrong way round.
    /// </summary>
    [Fact]
    public void Transposing_a_track_moves_every_note_in_it()
    {
        var pattern = new Pattern(4, 2);

        PatternEdit.EnterNote(pattern, At(0), new Note(60), 0);
        PatternEdit.EnterNote(pattern, At(1), new Note(62), 0);
        PatternEdit.EnterNote(pattern, At(0, 1), new Note(48), 0);

        PatternEdit.TransposeTrack(pattern, 0, 12);

        Assert.Equal(72, pattern[0, 0].Note.Semitone);
        Assert.Equal(74, pattern[1, 0].Note.Semitone);

        Assert.Equal(48, pattern[0, 1].Note.Semitone);
    }

    /// <summary>Clearing one column empties that column and no other.</summary>
    [Fact]
    public void Clearing_a_track_leaves_the_others()
    {
        var pattern = new Pattern(4, 2);

        PatternEdit.EnterNote(pattern, At(0), new Note(60), 0);
        PatternEdit.EnterNote(pattern, At(0, 1), new Note(62), 0);

        PatternEdit.ClearTrack(pattern, 0);

        Assert.True(pattern[0, 0].IsEmpty);
        Assert.False(pattern[0, 1].IsEmpty);
    }

    /// <summary>
    /// An inserted line leaves an empty row at the cursor and everything below it one line later.
    /// </summary>
    [Fact]
    public void Inserting_a_line_pushes_what_is_under_it_down()
    {
        var pattern = new Pattern(4, 1);

        PatternEdit.EnterNote(pattern, At(0), new Note(60), 0);
        PatternEdit.EnterNote(pattern, At(1), new Note(62), 0);

        PatternEdit.InsertLine(pattern, At(0));

        Assert.True(pattern[0, 0].IsEmpty);
        Assert.Equal(60, pattern[1, 0].Note.Semitone);
        Assert.Equal(62, pattern[2, 0].Note.Semitone);
    }

    /// <summary>The other direction: the row under the cursor closes the gap.</summary>
    [Fact]
    public void Deleting_a_line_pulls_what_is_under_it_up()
    {
        var pattern = new Pattern(4, 1);

        PatternEdit.EnterNote(pattern, At(0), new Note(60), 0);
        PatternEdit.EnterNote(pattern, At(1), new Note(62), 0);

        PatternEdit.DeleteLine(pattern, At(0));

        Assert.Equal(62, pattern[0, 0].Note.Semitone);
    }

    /// <summary>
    /// A note off the grid is moved onto it, and the count that comes back is what the status
    /// line reports, so it has to be the number of notes that actually moved.
    /// </summary>
    [Fact]
    public void Quantising_moves_notes_onto_the_grid()
    {
        var pattern = new Pattern(16, 1);

        PatternEdit.EnterNote(pattern, At(3), new Note(60), 0);

        int moved = PatternEdit.Quantize(pattern, 0, grid: 4);

        Assert.Equal(1, moved);
        Assert.True(pattern[3, 0].IsEmpty);
        Assert.True(pattern[4, 0].Note.IsPlayable);
    }

    /// <summary>
    /// Snapping on its own, over the cases where rounding to the nearest grid line is not the
    /// whole answer.
    /// </summary>
    /// <remarks>
    /// Line 15 of a sixteen line pattern on a grid of 4: the nearest grid line is 16, which is off
    /// the end of a sixteen line pattern, so it steps back rather than throwing the note away.
    /// Line 9 on a grid of 1: no grid at all is no snapping, and never off the end either. The
    /// last case is a line outside the pattern altogether, which still lands inside it.
    /// </remarks>
    [Theory]
    [InlineData(3, 4, 16, 4)]
    [InlineData(1, 4, 16, 0)]
    [InlineData(6, 4, 16, 8)]
    [InlineData(15, 4, 16, 12)]
    [InlineData(9, 1, 16, 9)]
    [InlineData(99, 4, 16, 12)]
    public void A_line_snaps_to_the_nearest_of_the_grid_and_never_off_the_end(
        int line, int grid, int lines, int wanted) =>
        Assert.Equal(wanted, PatternEdit.SnapLine(line, grid, lines));

    /// <summary>
    /// A cursor off the end of the pattern is refused rather than throwing, because a resize can
    /// leave one there and the keyboard would then fault on the next keystroke.
    /// </summary>
    [Fact]
    public void An_edit_outside_the_pattern_does_nothing()
    {
        var pattern = new Pattern(4, 1);

        PatternEdit.EnterNote(pattern, At(99), new Note(60), 0);
        PatternEdit.EnterNote(pattern, At(0, 99), new Note(60), 0);

        Assert.True(pattern[0, 0].IsEmpty);
    }
}
