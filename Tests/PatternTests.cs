using JingleBox2.Tracker;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A pattern: cells in one array, and the operations that move them about.
/// </summary>
public class PatternTests
{
    private static PatternCursor At(int line, int track = 0) => new() { Line = line, Track = track };

    private static Pattern WithNotes(int lines = 16, int tracks = 4)
    {
        var pattern = new Pattern(lines, tracks);

        for (int line = 0; line < 4; line++)
            PatternEdit.EnterNote(pattern, At(line), new Note(60 + line), 0);

        return pattern;
    }

    [Fact]
    public void A_new_pattern_is_empty()
    {
        var pattern = new Pattern(16, 4);

        for (int line = 0; line < 16; line++)
            for (int track = 0; track < 4; track++)
                Assert.True(pattern[line, track].IsEmpty);
    }

    [Fact]
    public void Its_shape_is_held_between_its_limits()
    {
        Assert.Equal(Pattern.MinLines, new Pattern(0, 4).Lines);
        Assert.Equal(Pattern.MaxLines, new Pattern(9999, 4).Lines);
        Assert.Equal(Song.MaxTrackCount, new Pattern(16, 999).TrackCount);
    }

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

    [Fact]
    public void Resizing_keeps_whatever_still_fits()
    {
        var pattern = WithNotes();

        pattern.Resize(2);

        Assert.Equal(2, pattern.Lines);
        Assert.True(pattern[0, 0].Note.IsPlayable);
        Assert.True(pattern[1, 0].Note.IsPlayable);
    }

    [Fact]
    public void Moving_a_track_slides_the_others_rather_than_swapping()
    {
        var pattern = new Pattern(4, 4);

        for (int track = 0; track < 4; track++)
            PatternEdit.EnterNote(pattern, At(0, track), new Note(60 + track), 0);

        // Dragging the fourth in front of the first should leave the others in order, one place
        // along, which is what somebody dragging a column expects and what a swap does not do.
        pattern.MoveTrack(3, 0);

        Assert.Equal(63, pattern[0, 0].Note.Semitone);
        Assert.Equal(60, pattern[0, 1].Note.Semitone);
        Assert.Equal(61, pattern[0, 2].Note.Semitone);
        Assert.Equal(62, pattern[0, 3].Note.Semitone);
    }

    [Fact]
    public void A_clone_shares_nothing()
    {
        var pattern = WithNotes();
        var copy = pattern.Clone();

        PatternEdit.ClearPattern(pattern);

        Assert.True(copy[0, 0].Note.IsPlayable);
    }

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

        // Once, rather than a change per cell: putting a step back is one thing that happened.
        Assert.Equal(1, said);
    }

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
    private static PatternCursor At(int line, int track = 0) => new() { Line = line, Track = track };

    [Fact]
    public void A_note_writes_the_note_and_the_instrument_and_leaves_the_rest()
    {
        var pattern = new Pattern(16, 4);

        PatternEdit.EnterNote(pattern, At(0), new Note(60), instrument: 3, volume: 40);
        PatternEdit.EnterNote(pattern, At(0), new Note(62), instrument: 5);

        Assert.Equal(62, pattern[0, 0].Note.Semitone);
        Assert.Equal(5, pattern[0, 0].Instrument);

        // No volume given leaves the column as it was, which is what typing a note does.
        Assert.Equal(40, pattern[0, 0].Volume);
    }

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

        // And nothing in the track next to it.
        Assert.Equal(48, pattern[0, 1].Note.Semitone);
    }

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

    [Fact]
    public void Deleting_a_line_pulls_what_is_under_it_up()
    {
        var pattern = new Pattern(4, 1);

        PatternEdit.EnterNote(pattern, At(0), new Note(60), 0);
        PatternEdit.EnterNote(pattern, At(1), new Note(62), 0);

        PatternEdit.DeleteLine(pattern, At(0));

        Assert.Equal(62, pattern[0, 0].Note.Semitone);
    }

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

    [Theory]
    [InlineData(3, 4, 16, 4)]
    [InlineData(1, 4, 16, 0)]
    [InlineData(6, 4, 16, 8)]
    // The nearest grid line is 16, which is off the end of a sixteen line pattern, so it steps
    // back rather than throwing the note away.
    [InlineData(15, 4, 16, 12)]
    // No grid at all is no snapping, and never off the end either.
    [InlineData(9, 1, 16, 9)]
    [InlineData(99, 4, 16, 12)]
    public void A_line_snaps_to_the_nearest_of_the_grid_and_never_off_the_end(
        int line, int grid, int lines, int wanted) =>
        Assert.Equal(wanted, PatternEdit.SnapLine(line, grid, lines));

    [Fact]
    public void An_edit_outside_the_pattern_does_nothing()
    {
        var pattern = new Pattern(4, 1);

        PatternEdit.EnterNote(pattern, At(99), new Note(60), 0);
        PatternEdit.EnterNote(pattern, At(0, 99), new Note(60), 0);

        Assert.True(pattern[0, 0].IsEmpty);
    }
}
