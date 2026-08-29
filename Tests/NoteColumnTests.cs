using System.Linq;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Tracker.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A track playing more than one note at a time.
/// </summary>
/// <remarks>
/// A note column is a whole cell again, with its own note, instrument, volume and effect, and
/// three of them on a track are three voices. Almost everything here is indexed arithmetic over
/// a shape that is no longer a rectangle: tracks are different widths now, so where a cell sits,
/// where it is drawn and where the next press of Tab lands are three walks that have to agree.
/// Indexed things go wrong quietly, which is the whole reason for this file.
///
/// A song with one column a track is every song written before this, so every test here also
/// says what such a song still does.
/// </remarks>
public class NoteColumnTests
{
    /// <summary>Every edit to a pattern, so each one lands in the history.</summary>
    private static readonly IPatternEdit Edits = new PatternEdit();

    /// <summary>A pattern whose first track plays chords and whose others do not.</summary>
    private static Pattern Chords(int columns = 3, int lines = 16, int tracks = 4)
    {
        var pattern = new Pattern(lines, tracks);
        pattern.SetColumns(0, columns);

        return pattern;
    }

    /// <summary>A track has one column until it is given more, which is what every song had.</summary>
    [Fact]
    public void A_track_plays_one_note_until_it_is_told_otherwise()
    {
        var pattern = new Pattern(16, 4);

        Assert.Equal(1, pattern.ColumnsOn(0));
        Assert.Equal(4, pattern.TotalColumns);
    }

    /// <summary>And naming only a track means its first column, everywhere.</summary>
    [Fact]
    public void A_cell_named_by_its_track_is_its_first_column()
    {
        var pattern = Chords();

        pattern[2, 0, 0] = new TrackerCell(new Note(60), 1, TrackerCell.NoVolume, TrackerEffect.None);

        Assert.Equal(new Note(60), pattern[2, 0].Note);
    }

    /// <summary>Each column holds its own cell, and writing one leaves the others alone.</summary>
    [Fact]
    public void Every_column_holds_a_cell_of_its_own()
    {
        var pattern = Chords();

        pattern[0, 0, 0] = new TrackerCell(new Note(60), 0, TrackerCell.NoVolume, TrackerEffect.None);
        pattern[0, 0, 1] = new TrackerCell(new Note(64), 0, TrackerCell.NoVolume, TrackerEffect.None);
        pattern[0, 0, 2] = new TrackerCell(new Note(67), 0, TrackerCell.NoVolume, TrackerEffect.None);

        Assert.Equal(new Note(60), pattern[0, 0, 0].Note);
        Assert.Equal(new Note(64), pattern[0, 0, 1].Note);
        Assert.Equal(new Note(67), pattern[0, 0, 2].Note);
    }

    /// <summary>A column that is not there is outside the pattern, the same as a track that is not.</summary>
    [Fact]
    public void A_column_a_track_has_not_got_is_outside_the_pattern()
    {
        var pattern = Chords(columns: 2);

        Assert.True(pattern.Contains(0, 0, 1));
        Assert.False(pattern.Contains(0, 0, 2));
        Assert.False(pattern.Contains(0, 1, 1));
    }

    /// <summary>Widening a track keeps what it already held, and the new column is empty.</summary>
    [Fact]
    public void Widening_a_track_keeps_what_was_written()
    {
        var pattern = new Pattern(16, 4);
        pattern[0, 0] = new TrackerCell(new Note(60), 0, TrackerCell.NoVolume, TrackerEffect.None);

        pattern.SetColumns(0, 3);

        Assert.Equal(new Note(60), pattern[0, 0, 0].Note);
        Assert.True(pattern[0, 0, 2].IsEmpty);
    }

    /// <summary>
    /// And narrowing one throws away what was in the columns that go, which is what narrowing
    /// means and is the same rule taking a track off follows.
    /// </summary>
    [Fact]
    public void Narrowing_a_track_takes_its_last_columns_with_it()
    {
        var pattern = Chords();
        pattern[0, 0, 2] = new TrackerCell(new Note(67), 0, TrackerCell.NoVolume, TrackerEffect.None);
        pattern[0, 0, 0] = new TrackerCell(new Note(60), 0, TrackerCell.NoVolume, TrackerEffect.None);

        pattern.SetColumns(0, 1);

        Assert.Equal(1, pattern.ColumnsOn(0));
        Assert.Equal(new Note(60), pattern[0, 0].Note);
    }

    /// <summary>A track's columns travel with it when the song moves the track.</summary>
    /// <remarks>
    /// The block is rebuilt for this, because two tracks need not be the same width any more: a
    /// track of three columns moved in front of a track of one is not a swap of equal pieces.
    /// </remarks>
    [Fact]
    public void Moving_a_track_moves_its_columns()
    {
        var pattern = Chords();

        pattern[0, 0, 2] = new TrackerCell(new Note(67), 0, TrackerCell.NoVolume, TrackerEffect.None);
        pattern[0, 2] = new TrackerCell(new Note(48), 0, TrackerCell.NoVolume, TrackerEffect.None);

        pattern.MoveTrack(0, 2);

        Assert.Equal(3, pattern.ColumnsOn(2));
        Assert.Equal(1, pattern.ColumnsOn(0));
        Assert.Equal(new Note(67), pattern[0, 2, 2].Note);
        Assert.Equal(new Note(48), pattern[0, 1].Note);
    }

    /// <summary>The count is the song's, so it reaches every pattern rather than one of them.</summary>
    /// <remarks>
    /// A pattern left behind would hold cells nothing could reach, and would refuse the next
    /// history step for being the wrong length.
    /// </remarks>
    [Fact]
    public void The_count_is_the_songs_and_reaches_every_pattern()
    {
        var song = Song.CreateDefault();
        song.AddPattern();

        Assert.True(song.SetColumns(1, 4));

        Assert.All(song.Patterns, pattern => Assert.Equal(4, pattern.ColumnsOn(1)));
        Assert.Equal(4, song.ColumnsOn(1));
    }

    /// <summary>A pattern added afterwards is given the counts the song already has.</summary>
    [Fact]
    public void A_new_pattern_is_born_the_songs_width()
    {
        var song = Song.CreateDefault();
        song.SetColumns(0, 3);

        int at = song.AddPattern();

        Assert.Equal(3, song.Patterns[at].ColumnsOn(0));
    }

    /// <summary>A chord written into a song comes back out of its file whole.</summary>
    [Fact]
    public void A_song_carries_a_chord()
    {
        var song = Song.CreateDefault();
        song.SetColumns(0, 3);

        var pattern = song.Patterns[0];
        pattern[4, 0, 0] = new TrackerCell(new Note(60), 1, TrackerCell.NoVolume, TrackerEffect.None);
        pattern[4, 0, 2] = new TrackerCell(new Note(67), 1, TrackerCell.NoVolume, TrackerEffect.None);

        var back = SongStore.Uncopy(SongStore.Copy(song))!;

        Assert.Equal(3, back.ColumnsOn(0));
        Assert.Equal(new Note(60), back.Patterns[0][4, 0, 0].Note);
        Assert.Equal(new Note(67), back.Patterns[0][4, 0, 2].Note);
    }

    /// <summary>
    /// The first column is written the way it always was, so an older copy of the application
    /// can still read the song.
    /// </summary>
    /// <remarks>
    /// A build that predates note columns splits a cell entry into three and reads the third
    /// field as a cell. Writing the column number into every entry would leave it finding every
    /// cell unreadable, so only a column past the first says which one it is.
    /// </remarks>
    [Fact]
    public void A_songs_first_column_is_written_the_way_it_always_was()
    {
        var song = Song.CreateDefault();
        song.SetColumns(0, 2);

        song.Patterns[0][0, 0, 0] = new TrackerCell(new Note(60), 0, TrackerCell.NoVolume, TrackerEffect.None);
        song.Patterns[0][0, 0, 1] = new TrackerCell(new Note(64), 0, TrackerCell.NoVolume, TrackerEffect.None);

        string written = SongStore.Copy(song);

        Assert.Contains("\"0:0:C-5 00 .. ...\"", written);
        Assert.Contains("\"0:0:1:E-5 00 .. ...\"", written);
    }

    /// <summary>And a song written before note columns existed opens as one column a track.</summary>
    [Fact]
    public void A_song_that_never_heard_of_columns_reads_as_one_apiece()
    {
        var song = Song.CreateDefault();
        song.Patterns[0][0, 0] = new TrackerCell(new Note(60), 0, TrackerCell.NoVolume, TrackerEffect.None);

        string written = SongStore.Copy(song).Replace("\"NoteColumns\": [", "\"Ignored\": [");

        var back = SongStore.Uncopy(written)!;

        Assert.Equal(1, back.ColumnsOn(0));
        Assert.Equal(new Note(60), back.Patterns[0][0, 0].Note);
    }

    /// <summary>Every column of a line becomes an event of its own.</summary>
    [Fact]
    public void The_sequencer_plays_every_column()
    {
        var song = Song.CreateDefault();
        song.SetColumns(0, 3);

        song.Patterns[0][0, 0, 0] = new TrackerCell(new Note(60), 0, TrackerCell.NoVolume, TrackerEffect.None);
        song.Patterns[0][0, 0, 2] = new TrackerCell(new Note(67), 0, TrackerCell.NoVolume, TrackerEffect.None);

        var events = new TrackerSequencer(song.TrackCount)
            .EventsFor(song, new TrackerPosition(0, 0))
            .Where(one => one.Track == 0)
            .ToList();

        Assert.Equal(2, events.Count);
        Assert.Equal(0, events[0].Column);
        Assert.Equal(2, events[1].Column);
        Assert.Equal(new Note(67), events[1].Note);
    }

    /// <summary>
    /// An OFF names the column it was written in, or it would take the whole chord down.
    /// </summary>
    [Fact]
    public void An_off_names_its_own_column()
    {
        var song = Song.CreateDefault();
        song.SetColumns(0, 2);

        song.Patterns[0][0, 0, 1] = new TrackerCell(Note.Off, 0, TrackerCell.NoVolume, TrackerEffect.None);

        var stop = new TrackerSequencer(song.TrackCount)
            .EventsFor(song, new TrackerPosition(0, 0))
            .Single();

        Assert.Equal(TrackerEventKind.Stop, stop.Kind);
        Assert.Equal(1, stop.Column);
    }

    /// <summary>
    /// A blank instrument column means the last one that column played, not the last one the
    /// track played.
    /// </summary>
    /// <remarks>
    /// Remembered per track, a chord whose third column names a different instrument would
    /// leave the next note in the first column playing the third's.
    /// </remarks>
    [Fact]
    public void Each_column_remembers_its_own_instrument()
    {
        var song = Song.CreateDefault();
        song.SetColumns(0, 2);

        var pattern = song.Patterns[0];
        pattern[0, 0, 0] = new TrackerCell(new Note(60), 0, TrackerCell.NoVolume, TrackerEffect.None);
        pattern[0, 0, 1] = new TrackerCell(new Note(64), 1, TrackerCell.NoVolume, TrackerEffect.None);
        pattern[1, 0, 0] = new TrackerCell(new Note(62), TrackerCell.NoInstrument, TrackerCell.NoVolume,
            TrackerEffect.None);

        var sequencer = new TrackerSequencer(song.TrackCount);
        sequencer.EventsFor(song, new TrackerPosition(0, 0));

        var next = sequencer.EventsFor(song, new TrackerPosition(0, 1)).Single();

        Assert.Equal(0, next.Instrument);
    }

    /// <summary>
    /// A chord played into a track that shows one column widens it rather than landing on
    /// itself.
    /// </summary>
    /// <remarks>
    /// The whole feature is unreachable without this. A track shows one column until somebody
    /// says otherwise, so a chord recorded into a fresh one used to put its second note on top
    /// of its first and the only thing kept was whichever finger was last down, which reads as
    /// polyphony not working at all.
    /// </remarks>
    [Fact]
    public void A_chord_widens_the_track_it_is_played_into()
    {
        var song = Song.CreateDefault();

        Assert.Equal(1, song.ColumnsOn(0));

        Assert.Equal(1, song.RoomForChord(0, 0));
        Assert.Equal(2, song.ColumnsOn(0));

        Assert.Equal(2, song.RoomForChord(0, 1));
        Assert.Equal(3, song.ColumnsOn(0));
    }

    /// <summary>It stops at the widest a track can be, and the note lands in the last column.</summary>
    [Fact]
    public void A_chord_stops_widening_at_the_last_column()
    {
        var song = Song.CreateDefault();
        song.SetColumns(0, Song.MaxNoteColumns);

        Assert.Equal(Song.MaxNoteColumns - 1, song.RoomForChord(0, Song.MaxNoteColumns - 1));
        Assert.Equal(Song.MaxNoteColumns, song.ColumnsOn(0));
    }

    /// <summary>A track already wide enough is left alone.</summary>
    [Fact]
    public void A_chord_that_fits_widens_nothing()
    {
        var song = Song.CreateDefault();
        song.SetColumns(0, 4);

        Assert.Equal(1, song.RoomForChord(0, 0));
        Assert.Equal(4, song.ColumnsOn(0));
    }

    /// <summary>A chord is kept in pitch order however the fingers landed.</summary>
    /// <remarks>
    /// The thing this exists for: playing E, then B, then G records the same shape as playing
    /// E, then G, then B, so the same chord looks the same every time it is played.
    /// </remarks>
    [Fact]
    public void A_chord_is_written_in_pitch_order()
    {
        var pattern = Chords();
        var at = new PatternCursor(0, 0, CellColumn.Note);

        Edits.EnterNote(pattern, at, new Note(64), 0);
        Edits.EnterChordNote(pattern, at, 1, new Note(71), 0, TrackerCell.NoVolume);
        Edits.EnterChordNote(pattern, at, 2, new Note(67), 0, TrackerCell.NoVolume);

        Assert.Equal(new Note(64), pattern[0, 0, 0].Note);
        Assert.Equal(new Note(67), pattern[0, 0, 1].Note);
        Assert.Equal(new Note(71), pattern[0, 0, 2].Note);
    }

    /// <summary>A note below everything already down pushes the whole chord along.</summary>
    [Fact]
    public void A_note_under_the_chord_pushes_it_along()
    {
        var pattern = Chords();
        var at = new PatternCursor(0, 0, CellColumn.Note);

        Edits.EnterNote(pattern, at, new Note(67), 0);
        Edits.EnterChordNote(pattern, at, 1, new Note(71), 0, TrackerCell.NoVolume);

        Assert.Equal(0, Edits.EnterChordNote(pattern, at, 2, new Note(60), 0, TrackerCell.NoVolume));

        Assert.Equal(new Note(60), pattern[0, 0, 0].Note);
        Assert.Equal(new Note(67), pattern[0, 0, 1].Note);
        Assert.Equal(new Note(71), pattern[0, 0, 2].Note);
    }

    /// <summary>What a note is played at travels with it when the chord shuffles along.</summary>
    [Fact]
    public void A_shifted_note_keeps_its_own_volume()
    {
        var pattern = Chords();
        var at = new PatternCursor(0, 0, CellColumn.Note);

        Edits.EnterNote(pattern, at, new Note(67), 0, 0x30);
        Edits.EnterChordNote(pattern, at, 1, new Note(60), 0, 0x18);

        Assert.Equal(0x18, pattern[0, 0, 0].Volume);
        Assert.Equal(0x30, pattern[0, 0, 1].Volume);
    }

    /// <summary>A chord with nowhere left to go drops its highest note.</summary>
    [Fact]
    public void A_full_chord_drops_its_highest_note()
    {
        var pattern = Chords(columns: 2);
        var at = new PatternCursor(0, 0, CellColumn.Note);

        Edits.EnterNote(pattern, at, new Note(64), 0);
        Edits.EnterChordNote(pattern, at, 1, new Note(71), 0, TrackerCell.NoVolume);
        Edits.EnterChordNote(pattern, at, 2, new Note(60), 0, TrackerCell.NoVolume);

        Assert.Equal(new Note(60), pattern[0, 0, 0].Note);
        Assert.Equal(new Note(64), pattern[0, 0, 1].Note);
    }

    /// <summary>A chord begun in the second column stays there rather than sliding to the first.</summary>
    [Fact]
    public void A_chord_stays_where_the_cursor_started_it()
    {
        var pattern = Chords();
        var at = new PatternCursor(0, 0, CellColumn.Note, 1);

        Edits.EnterNote(pattern, at, new Note(67), 0);
        Edits.EnterChordNote(pattern, at, 1, new Note(60), 0, TrackerCell.NoVolume);

        Assert.True(pattern[0, 0, 0].IsEmpty);
        Assert.Equal(new Note(60), pattern[0, 0, 1].Note);
        Assert.Equal(new Note(67), pattern[0, 0, 2].Note);
    }

    /// <summary>A track with nothing in its extra columns does not need them.</summary>
    /// <remarks>
    /// What clearing a track asks: the room a chord took is given back once the chord is gone.
    /// </remarks>
    [Fact]
    public void An_empty_track_needs_one_column()
    {
        var song = Song.CreateDefault();
        song.SetColumns(0, 4);

        Assert.Equal(1, song.ColumnsUsed(0));
    }

    /// <summary>It needs as far as the widest column anything is written in.</summary>
    [Fact]
    public void A_track_needs_as_far_as_its_widest_note()
    {
        var song = Song.CreateDefault();
        song.SetColumns(0, 5);

        song.Patterns[0][0, 0, 2] = new TrackerCell(new Note(60), 0, TrackerCell.NoVolume, TrackerEffect.None);

        Assert.Equal(3, song.ColumnsUsed(0));
    }

    /// <summary>
    /// And across every pattern, since the count is the song's and one pattern's emptiness is
    /// not the song's.
    /// </summary>
    /// <remarks>
    /// Narrowing by what the pattern in front happens to use would throw another pattern's
    /// chords away, and a song may not lose music because a track was cleared somewhere else.
    /// </remarks>
    [Fact]
    public void A_track_needs_what_every_pattern_uses()
    {
        var song = Song.CreateDefault();
        song.AddPattern();
        song.SetColumns(0, 4);

        song.Patterns[1][0, 0, 3] = new TrackerCell(new Note(60), 0, TrackerCell.NoVolume, TrackerEffect.None);

        Assert.Equal(4, song.ColumnsUsed(0));
    }

    /// <summary>Tab walks the fields of a column, then the columns of a track, then the tracks.</summary>
    [Fact]
    public void The_cursor_walks_the_columns_it_really_has()
    {
        var columns = new NoteColumns(new[] { 2, 1, 1, 1 });
        var cursor = new PatternCursor(0, 0, CellColumn.Effect);

        var moved = cursor.MoveColumn(1, 4, columns);

        Assert.Equal(0, moved.Track);
        Assert.Equal(1, moved.NoteColumn);
        Assert.Equal(CellColumn.Note, moved.Column);
    }

    /// <summary>And falls off the last column of a track into the next track's first.</summary>
    [Fact]
    public void The_cursor_leaves_a_track_after_its_last_column()
    {
        var columns = new NoteColumns(new[] { 2, 1, 1, 1 });
        var cursor = new PatternCursor(0, 0, CellColumn.Effect, 1);

        var moved = cursor.MoveColumn(1, 4, columns);

        Assert.Equal(1, moved.Track);
        Assert.Equal(0, moved.NoteColumn);
    }

    /// <summary>A cursor left in a column its track no longer has is pulled back inside it.</summary>
    [Fact]
    public void A_cursor_is_pulled_out_of_a_column_that_went()
    {
        var cursor = new PatternCursor(0, 0, CellColumn.Note, 2);

        var held = cursor.Clamp(16, 4, new NoteColumns(new[] { 1, 1, 1, 1 }));

        Assert.Equal(0, held.NoteColumn);
    }

    /// <summary>A click lands on the column under it, whatever the tracks either side are doing.</summary>
    /// <remarks>
    /// The one that goes wrong quietly: tracks are different widths now, so where a track begins
    /// is a walk from the left rather than a multiplication, and a picture drawn one way and hit
    /// tested the other lands a click on the wrong cell.
    /// </remarks>
    [Fact]
    public void A_click_lands_where_it_looks_like_it_landed()
    {
        var metrics = new PatternMetrics(8, 16, 4, 0, 0, new NoteColumns(new[] { 3, 1, 1, 1 }));

        double x = metrics.ColumnX(1, CellColumn.Volume) + 4;

        Assert.Equal(1, metrics.TrackAt(x));
        Assert.Equal(CellColumn.Volume, metrics.ColumnAt(x, 1));
        Assert.Equal(0, metrics.NoteColumnAt(x, 1));
    }

    /// <summary>Including a click on a later column of a wide track.</summary>
    [Fact]
    public void A_click_lands_on_the_column_of_a_chord_it_is_over()
    {
        var metrics = new PatternMetrics(8, 16, 4, 0, 0, new NoteColumns(new[] { 3, 1, 1, 1 }));

        double x = metrics.ColumnX(0, CellColumn.Instrument, 2) + 4;
        var cursor = metrics.CursorAt(x, 0, 16);

        Assert.Equal(0, cursor.Track);
        Assert.Equal(2, cursor.NoteColumn);
        Assert.Equal(CellColumn.Instrument, cursor.Column);
    }

    /// <summary>A wide track makes the pattern wider, which is what the scroll bar is measured on.</summary>
    [Fact]
    public void A_wide_track_makes_the_content_wider()
    {
        var plain = new PatternMetrics(8, 16, 4);
        var wide = new PatternMetrics(8, 16, 4, 0, 0, new NoteColumns(new[] { 3, 1, 1, 1 }));

        Assert.Equal(plain.ContentWidth + 2 * plain.NoteColumnWidth, wide.ContentWidth);
    }

    /// <summary>A copied chord is put back whole.</summary>
    [Fact]
    public void Copy_and_paste_carry_every_column()
    {
        var pattern = Chords();

        pattern[0, 0, 0] = new TrackerCell(new Note(60), 0, TrackerCell.NoVolume, TrackerEffect.None);
        pattern[0, 0, 2] = new TrackerCell(new Note(67), 0, TrackerCell.NoVolume, TrackerEffect.None);

        var block = PatternBlock.Copy(pattern, new PatternSelection(0, 0, 0, 0))!;

        block.Paste(Edits, pattern, new PatternCursor(8, 0, CellColumn.Note));

        Assert.Equal(new Note(60), pattern[8, 0, 0].Note);
        Assert.Equal(new Note(67), pattern[8, 0, 2].Note);
    }

    /// <summary>And what will not fit on a narrower track is clipped rather than refused.</summary>
    [Fact]
    public void A_chord_pasted_onto_a_narrower_track_is_clipped()
    {
        var pattern = Chords();

        pattern[0, 0, 0] = new TrackerCell(new Note(60), 0, TrackerCell.NoVolume, TrackerEffect.None);
        pattern[0, 0, 1] = new TrackerCell(new Note(64), 0, TrackerCell.NoVolume, TrackerEffect.None);

        var block = PatternBlock.Copy(pattern, new PatternSelection(0, 0, 0, 0))!;

        block.Paste(Edits, pattern, new PatternCursor(0, 1, CellColumn.Note));

        Assert.Equal(new Note(60), pattern[0, 1].Note);
        Assert.Equal(1, pattern.ColumnsOn(1));
    }

    /// <summary>Clearing a track clears every column of it.</summary>
    [Fact]
    public void Clearing_a_track_clears_its_chords_too()
    {
        var pattern = Chords();
        pattern[0, 0, 2] = new TrackerCell(new Note(67), 0, TrackerCell.NoVolume, TrackerEffect.None);

        Edits.ClearTrack(pattern, 0);

        Assert.True(pattern[0, 0, 2].IsEmpty);
    }

    /// <summary>And a line inserted into a track opens a hole in every column of it.</summary>
    /// <remarks>
    /// A chord is written across the columns of one line, so a hole in one of them would leave
    /// the notes of that chord on different lines.
    /// </remarks>
    [Fact]
    public void Inserting_a_line_keeps_a_chord_together()
    {
        var pattern = Chords();

        pattern[0, 0, 0] = new TrackerCell(new Note(60), 0, TrackerCell.NoVolume, TrackerEffect.None);
        pattern[0, 0, 2] = new TrackerCell(new Note(67), 0, TrackerCell.NoVolume, TrackerEffect.None);

        Edits.InsertLine(pattern, new PatternCursor(0, 0, CellColumn.Note, 2));

        Assert.Equal(new Note(60), pattern[1, 0, 0].Note);
        Assert.Equal(new Note(67), pattern[1, 0, 2].Note);
    }

    /// <summary>
    /// A song step is a whole song, so undoing a narrowing brings back what it threw away.
    /// </summary>
    /// <remarks>
    /// The counts belong to the song, so narrowing a track is a song edit and not a pattern
    /// edit: it takes cells out of every pattern at once, and nothing smaller than the document
    /// would put them back.
    /// </remarks>
    [Fact]
    public void Undoing_a_narrowing_brings_the_chord_back()
    {
        var song = Song.CreateDefault();
        song.SetColumns(0, 3);
        song.Patterns[0][0, 0, 2] = new TrackerCell(new Note(67), 0, TrackerCell.NoVolume, TrackerEffect.None);

        var step = SongStore.Uncopy(SongStore.Copy(song))!;

        song.SetColumns(0, 1);
        Assert.Equal(1, song.ColumnsOn(0));

        song.TakeFrom(step);

        Assert.Equal(3, song.ColumnsOn(0));
        Assert.Equal(3, song.Patterns[0].ColumnsOn(0));
        Assert.Equal(new Note(67), song.Patterns[0][0, 0, 2].Note);
    }

    /// <summary>
    /// A history step carries the column counts, so an undo across a change of them lands.
    /// </summary>
    /// <remarks>
    /// Without the counts a step would hold cells of the wrong length, be refused and say
    /// nothing. This codebase has had that bug twice and both times it survived because doing
    /// nothing looks like working.
    /// </remarks>
    [Fact]
    public void A_step_carries_the_column_counts()
    {
        var pattern = Chords();
        pattern[0, 0, 2] = new TrackerCell(new Note(67), 0, TrackerCell.NoVolume, TrackerEffect.None);

        var kept = pattern.Cells();
        var columns = pattern.ColumnCounts();
        var lanes = pattern.LaneCopy();

        Assert.True(pattern.Holds(kept, pattern.Lines, pattern.TrackCount, columns, lanes));

        pattern.SetColumns(0, 1);

        Assert.False(pattern.Holds(kept, pattern.Lines, pattern.TrackCount, columns, lanes));

        pattern.Restore(kept, 16, 4, columns, lanes);

        Assert.Equal(3, pattern.ColumnsOn(0));
        Assert.Equal(new Note(67), pattern[0, 0, 2].Note);
    }
}
