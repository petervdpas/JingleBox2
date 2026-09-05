using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Enums;

namespace JingleBox2.Tracker;

/// <summary>
/// Patterns, the order they play in, and the instruments they reference.
/// The order list holds pattern indexes, so the same pattern can appear more than once.
/// </summary>
public sealed class Song
{
    /// <summary>A song of one track, which is as narrow as one can be.</summary>
    public const int MinTrackCount = 1;

    /// <summary>Two digits is as wide as the track badges and headers are built for.</summary>
    public const int MaxTrackCount = 32;

    /// <summary>What a new song opens with, which is enough to start and not a wall of empty columns.</summary>
    public const int DefaultTrackCount = 4;

    /// <summary>A track has at least one note column, or it would have nowhere to put a note.</summary>
    public const int MinNoteColumns = 1;

    /// <summary>
    /// Eight, which is as many notes as a track can play at once.
    /// </summary>
    /// <remarks>
    /// Renoise allows twelve. Eight because nothing widens until it is asked for, and because
    /// every column is width on the screen whether or not anything is written in it: a pattern
    /// where one track has twelve is a pattern where you can see two tracks.
    /// </remarks>
    public const int MaxNoteColumns = 8;

    /// <summary>One, so a song opens as every song before note columns existed played.</summary>
    public const int DefaultNoteColumns = 1;

    /// <summary>What the song is called, which is also what its file is called.</summary>
    public string Name { get; set; } = "untitled";

    /// <summary>
    /// What this song is, in the writer's own words. Empty unless somebody says otherwise.
    /// </summary>
    /// <remarks>
    /// For picking one out of a list months later, which a file name written in a hurry is bad
    /// at: "Gruber" says nothing about the bed being 30 seconds at 98bpm with the sting at the
    /// end. It travels with the song rather than sitting in a settings file, because it is part
    /// of the work and has to survive the song being copied to another machine.
    /// </remarks>
    public string Description { get; set; } = "";

    /// <summary>
    /// Which kind of machine this song was last written on, in one word.
    /// </summary>
    /// <remarks>
    /// **Written on rather than made on**, and the difference is the whole of what it is for: it
    /// is set on every save rather than carried from where the song began, since what anything
    /// wants to know is whether the paths **in this file** could mean anything here, and those
    /// were written by whoever saved it last. A song begun on Linux and saved on Windows has
    /// Windows paths in it and says so.
    ///
    /// Empty in every song written before it existed, which reads back as unknown and behaves
    /// exactly as before. See <see cref="Interfaces.IMachineWord"/> for what the word is for.
    /// </remarks>
    public string WrittenOn { get; set; } = "";

    /// <summary>Beats a minute. Held to its range by <see cref="Normalize"/> on the way in.</summary>
    public double Bpm { get; set; } = TrackerTiming.DefaultBpm;

    /// <summary>Steps to a beat, which is how finely the beat can be written rather than a tempo.</summary>
    public int LinesPerBeat { get; set; } = TrackerTiming.DefaultLinesPerBeat;

    /// <summary>
    /// Whether this song plays the one pattern or works through the order.
    /// </summary>
    /// <remarks>
    /// Part of the song rather than a preference, and it took being told twice to get right. It
    /// looks like a thing about the desk, which is how it was first written; it is not. A song
    /// that is finished plays as a song and a song being worked on loops the pattern in hand,
    /// and which of those it is, is a fact about where the work has got to. Opening it tomorrow
    /// should find it where it was left, and changing it should make the song want saving.
    ///
    /// Pattern by default, which is what a new song has always started as and what somebody
    /// writing a first pattern wants.
    ///
    /// The loop switch beside it is the other way round and stays in the settings: whether the
    /// thing you are listening to comes round again is about how you are working at this
    /// moment, and a song handed to somebody else has no business setting it.
    /// </remarks>
    public TrackerPlayMode PlayMode { get; set; } = TrackerPlayMode.Pattern;

    /// <summary>
    /// Which octave notes are typed and auditioned at, for this song.
    /// </summary>
    /// <remarks>
    /// One octave for the whole song rather than one per instrument or one for the machine.
    /// The pattern editor and every instrument's own panel read the same number, so moving the
    /// octave anywhere moves it everywhere, and a song reopens where it was left. A bass part
    /// written two octaves down is a property of the work, not of the bass.
    /// </remarks>
    public int KeyboardOctave { get; set; } = 4;

    /// <summary>
    /// How many tracks every pattern in the song has.
    /// </summary>
    /// <remarks>
    /// One number for the whole song rather than one per pattern, because the tracks are what
    /// the mixer, the instruments and the order are all indexed by, and a pattern with a width
    /// of its own would make every one of those an answer that depends on where the playhead is.
    /// </remarks>
    public int TrackCount { get; set; } = DefaultTrackCount;

    /// <summary>The patterns themselves, which the order list points into by index.</summary>
    public List<Pattern> Patterns { get; set; } = new();

    /// <summary>
    /// What a pattern at that place in the song is called.
    /// </summary>
    /// <remarks>
    /// Its own index, counted from nought, so a pattern's name and its place are the same
    /// number. They were one apart: the order counts slots from 00 and patterns were named from
    /// 01, so a fresh song read "slot 00 plays pattern 01" and the two columns of the order list
    /// were permanently out of step for no reason anybody had chosen. Songs written on the old
    /// naming are renumbered on the way in.
    ///
    /// It stays true because a pattern is never taken out of the list: removing a slot removes
    /// the slot, and the patterns only ever grow, so no index ever shifts under a name.
    /// </remarks>
    /// <param name="index">Where the pattern sits in <see cref="Patterns"/>.</param>
    public static string Named(int index) =>
        Math.Max(0, index).ToString("00", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Indexes into <see cref="Patterns"/>, in playing order.</summary>
    public List<int> Order { get; set; } = new();

    /// <summary>The first slot of the loop range, or <see cref="NoLoop"/> for none.</summary>
    /// <remarks>
    /// A range over the order rather than over the patterns: it is slots that are looped, so a
    /// pattern that appears twice can be inside the range once and outside it once.
    ///
    /// Part of the song rather than a preference, unlike the loop switch beside the mode picker.
    /// The switch is about how you are working; a range is about a piece of the music, the eight
    /// bars you are going round while you write the solo, and it is worth still being there when
    /// the song is opened again. Renoise keeps it in the song for the same reason.
    /// </remarks>
    public int LoopFrom { get; set; } = NoLoop;

    /// <summary>The last slot of the loop range, or <see cref="NoLoop"/> for none.</summary>
    public int LoopTo { get; set; } = NoLoop;

    /// <summary>What either end of the loop range holds when there is no range at all.</summary>
    public const int NoLoop = -1;

    /// <summary>The most slots one press of Play it again is allowed to add.</summary>
    /// <remarks>
    /// Sixteen, which is four four-bar phrases and past anything anybody chooses from a menu.
    /// A bound rather than a policy: it is there so a number arriving from somewhere unexpected
    /// cannot fill an order list with thousands of rows.
    /// </remarks>
    public const int MaxRepeats = 16;

    /// <summary>True when the order has a range marked on it.</summary>
    /// <remarks>
    /// Both ends have to be inside the order, so an order that has shrunk under a range leaves
    /// no range rather than one pointing at slots that are not there. Asked rather than repaired
    /// on the way in, since a range is cheap to check and a repair would be a write from
    /// whatever happened to look first.
    /// </remarks>
    public bool HasLoop =>
        LoopFrom >= 0 && LoopTo >= 0 &&
        LoopFrom < Order.Count && LoopTo < Order.Count;

    /// <summary>The first slot of the range, whichever end it was drawn from.</summary>
    public int LoopFirst => Math.Min(LoopFrom, LoopTo);

    /// <summary>And the last, so nothing above has to know which way somebody dragged.</summary>
    public int LoopLast => Math.Max(LoopFrom, LoopTo);

    /// <summary>Whether that slot is inside the range.</summary>
    /// <remarks>
    /// Named apart from <see cref="Looping"/> on purpose: one asks whether a particular slot is
    /// marked, the other whether the song comes round at all, and two names a letter apart
    /// would be read for each other for ever.
    /// </remarks>
    /// <param name="slot">Where in the order.</param>
    public bool InLoop(int slot) => HasLoop && slot >= LoopFirst && slot <= LoopLast;

    /// <summary>
    /// Whether the song comes round again when it reaches the end of what it is playing.
    /// </summary>
    /// <remarks>
    /// What "the end" is depends on <see cref="PlayMode"/>: the end of the pattern when a
    /// pattern is being looped, the end of the order when the song is playing. The two are one
    /// question and belong in one place, which is why this is here rather than in the settings
    /// where it started. A jingle that plays once and a part you go round while writing it are
    /// different songs, not the same song on different days.
    ///
    /// True by default, and true in every song written before it was part of one: that is what
    /// the transport did when nothing could set it.
    ///
    /// The loop range is a third thing again and answers before this does: marking a range is
    /// saying "go round these" in as many words. See <see cref="SetLoop"/>.
    /// </remarks>
    public bool Looping { get; set; } = true;

    /// <summary>Marks a range over the order, or takes one off with <see cref="NoLoop"/>.</summary>
    /// <remarks>
    /// Either end may be given first, since it is drawn by dragging and a drag goes both ways.
    /// Held inside the order rather than refused, so a range drawn past the last row is a range
    /// to the last row, which is what the hand doing it meant.
    /// </remarks>
    /// <param name="from">One end, or <see cref="NoLoop"/> to clear the range.</param>
    /// <param name="to">The other end. Ignored when the first is <see cref="NoLoop"/>.</param>
    public void SetLoop(int from, int to)
    {
        if (from < 0 || Order.Count == 0)
        {
            LoopFrom = NoLoop;
            LoopTo = NoLoop;
            return;
        }

        LoopFrom = Math.Clamp(from, 0, Order.Count - 1);
        LoopTo = Math.Clamp(to < 0 ? from : to, 0, Order.Count - 1);
    }

    /// <summary>
    /// The song's own instruments, which are its copies and not the rack's.
    /// </summary>
    /// <remarks>
    /// A song owns what it plays: your name, your settings, its own id, stored here. Two of them
    /// can come off one machine, and improving one in a song changes that song and nothing else.
    /// Cells point into this list by index, which is why <see cref="RemoveInstrumentAt"/> has to
    /// renumber every one of them.
    /// </remarks>
    public List<TrackerInstrument> Instruments { get; set; } = new();

    /// <summary>
    /// Which instrument each track holds, by index, or -1 for none. The mapping is one to one
    /// in both directions: an instrument sits on a single track, and a track holds a single
    /// instrument. To play one sample on two tracks, add the recording twice.
    /// </summary>
    public List<int> TrackInstruments { get; set; } = new();

    /// <summary>One strip per track: level, placement, mute and solo.</summary>
    public List<TrackMix> Mix { get; set; } = new();

    /// <summary>
    /// How many note columns each track shows, one entry per track.
    /// </summary>
    /// <remarks>
    /// The song's and not the pattern's, which is Renoise's arrangement and right for the same
    /// reason its track count is the song's: a part is played on so many voices whatever
    /// pattern it is in, and counts that varied per pattern would make copying a track between
    /// patterns a question with no good answer.
    ///
    /// A song written before note columns existed has nothing here and reads back as one
    /// column a track, which is exactly what it played.
    /// </remarks>
    public List<int> NoteColumns { get; set; } = new();

    /// <summary>
    /// The whole mix, after every track: a level, a place and one effect the song goes through.
    /// </summary>
    /// <remarks>
    /// A <see cref="TrackMix"/> because it is the same handful of settings and there is nothing
    /// to gain by writing them out twice, but it is not a track and is not in the list of them:
    /// nothing plays through it, nothing is keyed off it, and it does not move when the tracks
    /// are reordered. Its ducking is left where it starts and nothing reads it.
    ///
    /// Never null. A song written before this existed gets a fresh one on the way in, which is
    /// unity and no effect, so an old song sounds exactly as it did.
    /// </remarks>
    public TrackMix Master { get; set; } = new();

    /// <summary>
    /// What this song's own controller layout is, over the top of the one in the settings.
    /// </summary>
    /// <remarks>
    /// Two layers, because a controller is two things at once. Some of what you wire up is
    /// about the desk and true of everything you ever open: these faders are the track levels,
    /// that knob is the filter on whatever machine is in front of me. That belongs in the
    /// settings, where the hardware lives, and it is there.
    ///
    /// The rest is about one piece of music. This song's third track is the lead and its filter
    /// is the one your hand should fall on, and next week's song will have the lead somewhere
    /// else. That cannot live with the hardware, because it is not about the hardware. It
    /// travels with the song, and a song handed to somebody else arrives with its own layout on
    /// it.
    ///
    /// The song's win where the two name the same control, which is what makes them overrides
    /// rather than a second list: the desk is what a control does unless this song has
    /// something to say about it.
    /// </remarks>
    public List<Midi.ControlMapping> Controls { get; set; } = new();

    /// <summary>
    /// Takes everything from another song, without becoming a different object.
    /// </summary>
    /// <remarks>
    /// For a history putting a step back. The player, the mixer, every panel and the view model
    /// all hold the song they were opened on, so what comes back has to be the contents rather
    /// than a replacement.
    ///
    /// The patterns keep their identity too, and that is not a nicety. A history's cheap steps
    /// are a pattern and its cells, held by reference; replacing the list would leave every one
    /// of them pointing at an object no longer in the song, and undoing a note after undoing an
    /// instrument would appear to do nothing at all. So a pattern that already exists is filled
    /// rather than swapped, and only the count changing adds or drops one.
    ///
    /// Everything else is found rather than listed, because a list written out here would be
    /// right the day it was written and wrong the first time a field is added.
    /// </remarks>
    public void TakeFrom(Song? was)
    {
        if (was is null) return;

        foreach (var property in typeof(Song).GetProperties(
                     System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite) continue;
            if (property.Name == nameof(Patterns)) continue;

            property.SetValue(this, property.GetValue(was));
        }

        while (Patterns.Count > was.Patterns.Count) Patterns.RemoveAt(Patterns.Count - 1);

        for (int at = 0; at < was.Patterns.Count; at++)
        {
            var wanted = was.Patterns[at];

            if (at < Patterns.Count)
            {
                Patterns[at].Name = wanted.Name;
                Patterns[at].Restore(
                    wanted.Cells(), wanted.Lines, wanted.TrackCount,
                    wanted.ColumnCounts(), wanted.LaneCopy());
            }
            else
            {
                Patterns.Add(wanted);
            }
        }
    }

    /// <summary>The tempo and the resolution as one thing, for anything working out lengths.</summary>
    /// <remarks>
    /// Not written to the file, since both halves of it already are and a third copy would be a
    /// third thing that could disagree.
    /// </remarks>
    [JsonIgnore]
    public TrackerTiming Timing => new(Bpm, LinesPerBeat);

    /// <summary>A new song: one pattern, once in the order, and nothing else.</summary>
    public static Song CreateDefault()
    {
        var song = new Song();
        song.Patterns.Add(new Pattern(Pattern.DefaultLines, song.TrackCount) { Name = Named(0) });
        song.Order.Add(0);
        return song;
    }

    /// <summary>
    /// The pattern that order slot plays, or null when the slot or what it names is not there.
    /// </summary>
    /// <remarks>
    /// Asked by the slot rather than by the pattern, because the same pattern can be in a song
    /// twice and where the playhead is has to be one answer.
    /// </remarks>
    public Pattern? PatternAt(int orderIndex)
    {
        if (orderIndex < 0 || orderIndex >= Order.Count) return null;

        int patternIndex = Order[orderIndex];
        return patternIndex >= 0 && patternIndex < Patterns.Count ? Patterns[patternIndex] : null;
    }

    /// <summary>One instrument by index, or null for anything outside the list.</summary>
    public TrackerInstrument? InstrumentAt(int index) =>
        index >= 0 && index < Instruments.Count ? Instruments[index] : null;

    /// <summary>The instrument a track defaults to, or <see cref="TrackerCell.NoInstrument"/>.</summary>
    public int GetTrackInstrument(int track)
    {
        if (track < 0 || track >= TrackInstruments.Count) return TrackerCell.NoInstrument;

        int index = TrackInstruments[track];
        return index >= 0 && index < Instruments.Count ? index : TrackerCell.NoInstrument;
    }

    /// <summary>The track an instrument sits on, or -1 when it is not on one.</summary>
    public int GetInstrumentTrack(int instrument)
    {
        if (instrument < 0 || instrument >= Instruments.Count) return -1;

        for (int track = 0; track < TrackInstruments.Count && track < TrackCount; track++)
            if (TrackInstruments[track] == instrument) return track;

        return -1;
    }

    /// <summary>
    /// Puts an instrument on a track. Because the mapping is one to one, this moves the
    /// instrument off whatever track it was on and displaces whatever that track was holding.
    /// </summary>
    public void SetTrackInstrument(int track, int instrument)
    {
        if (track < 0 || track >= TrackCount) return;

        EnsureTrackInstruments();

        int value = instrument >= 0 && instrument < Instruments.Count
            ? instrument
            : TrackerCell.NoInstrument;

        if (value != TrackerCell.NoInstrument) ClearInstrumentFromTracks(value);

        TrackInstruments[track] = value;
    }

    /// <summary>How many note columns a track has. One for a track nothing has said anything about.</summary>
    public int ColumnsOn(int track)
    {
        if (track < 0 || track >= TrackCount) return 0;

        return track < NoteColumns.Count
            ? Math.Clamp(NoteColumns[track], MinNoteColumns, MaxNoteColumns)
            : DefaultNoteColumns;
    }

    /// <summary>
    /// Gives a track that many note columns, here and in every pattern.
    /// </summary>
    /// <remarks>
    /// Every pattern, because the count is the song's: a track two columns wide is two columns
    /// wide throughout, and a pattern left behind would hold cells nothing could reach and
    /// would refuse the next history step for being the wrong length.
    ///
    /// Narrowing throws away what was in the columns that go, which is what narrowing means and
    /// is the same rule taking a track off follows. It leaves an undo step like any other edit,
    /// because whoever asked for it went through the editor.
    /// </remarks>
    public bool SetColumns(int track, int count)
    {
        if (track < 0 || track >= TrackCount) return false;

        int wanted = Math.Clamp(count, MinNoteColumns, MaxNoteColumns);
        if (wanted == ColumnsOn(track)) return false;

        EnsureNoteColumns();
        NoteColumns[track] = wanted;

        foreach (var pattern in Patterns) pattern.SetColumns(NoteColumns);

        return true;
    }

    /// <summary>
    /// How many note columns a track really needs: as far as the widest one anything is
    /// written in, across every pattern in the song.
    /// </summary>
    /// <remarks>
    /// Every pattern, because the count is the song's. Narrowing a track by what one pattern
    /// happens to use would throw away the chords another pattern has on it, and a song is not
    /// allowed to lose music because somebody cleared a track somewhere else.
    ///
    /// Never less than one, since a track with nothing on it anywhere still has to have
    /// somewhere to put a note.
    /// </remarks>
    public int ColumnsUsed(int track)
    {
        int used = MinNoteColumns;

        foreach (var pattern in Patterns)
        {
            if (track < 0 || track >= pattern.TrackCount) continue;

            for (int column = pattern.ColumnsOn(track) - 1; column >= used; column--)
            {
                if (!Written(pattern, track, column)) continue;

                used = column + 1;
                break;
            }
        }

        return Math.Clamp(used, MinNoteColumns, MaxNoteColumns);
    }

    /// <summary>Whether anything at all is written in one note column of one track.</summary>
    private static bool Written(Pattern pattern, int track, int column)
    {
        for (int line = 0; line < pattern.Lines; line++)
            if (!pattern[line, track, column].IsEmpty) return true;

        return false;
    }

    /// <summary>
    /// Makes room on a track for the next note of a chord, widening it if it has none, and
    /// answers the note column that note goes into.
    /// </summary>
    /// <remarks>
    /// A track shows one note column until somebody says otherwise, so without this a chord
    /// played into a fresh track has nowhere to go: the second note lands on the first and the
    /// only thing recorded is whichever finger was last down. Somebody playing a chord has
    /// already said what they want, and making them find a menu before they are allowed to
    /// record what they are playing is the wrong way round.
    ///
    /// One column at a time, stopping at <see cref="MaxNoteColumns"/>, where a further note
    /// lands in the last column over the note that was there. Dropping it instead is the other
    /// defensible answer and is what Renoise does; neither is obviously right, and landing it
    /// at least leaves the note somewhere a hand can find it.
    /// </remarks>
    /// <param name="track">The track being played into.</param>
    /// <param name="after">The note column the last note of this chord went into.</param>
    /// <returns>Which note column to write into, which is always one the track really has.</returns>
    public int RoomForChord(int track, int after)
    {
        if (track < 0 || track >= TrackCount) return 0;

        int wanted = Math.Max(0, after + 1);

        if (wanted >= ColumnsOn(track) && wanted < MaxNoteColumns) SetColumns(track, wanted + 1);

        return Math.Min(wanted, Math.Max(0, ColumnsOn(track) - 1));
    }

    /// <summary>Keeps the per-track list the same length as the track count, and in range.</summary>
    private void EnsureNoteColumns()
    {
        NoteColumns ??= new List<int>();

        while (NoteColumns.Count < TrackCount) NoteColumns.Add(DefaultNoteColumns);
        if (NoteColumns.Count > TrackCount)
            NoteColumns.RemoveRange(TrackCount, NoteColumns.Count - TrackCount);

        for (int track = 0; track < NoteColumns.Count; track++)
            NoteColumns[track] = Math.Clamp(NoteColumns[track], MinNoteColumns, MaxNoteColumns);
    }

    /// <summary>Takes an instrument off every track it is on, which is at most one of them.</summary>
    private void ClearInstrumentFromTracks(int instrument)
    {
        for (int track = 0; track < TrackInstruments.Count; track++)
            if (TrackInstruments[track] == instrument)
                TrackInstruments[track] = TrackerCell.NoInstrument;
    }

    /// <summary>Keeps the per-track list the same length as the track count.</summary>
    private void EnsureTrackInstruments()
    {
        while (TrackInstruments.Count < TrackCount) TrackInstruments.Add(TrackerCell.NoInstrument);
        if (TrackInstruments.Count > TrackCount)
            TrackInstruments.RemoveRange(TrackCount, TrackInstruments.Count - TrackCount);
    }

    /// <summary>
    /// A strip per track. Kept alongside rather than inside the pattern: adding a track should
    /// give it a fader at unity, and removing one should not take another track's settings.
    /// </summary>
    private void EnsureMix()
    {
        Mix ??= new List<TrackMix>();

        while (Mix.Count < TrackCount) Mix.Add(new TrackMix());
        if (Mix.Count > TrackCount) Mix.RemoveRange(TrackCount, Mix.Count - TrackCount);

        foreach (var strip in Mix)
            strip.Clamp();

        Master ??= new TrackMix();
        Master.Clamp();
    }

    /// <summary>Adds a pattern sized to match the song and returns its index.</summary>
    public int AddPattern(int lines = Pattern.DefaultLines)
    {
        var pattern = new Pattern(lines, TrackCount)
        {
            Name = Named(Patterns.Count)
        };

        EnsureNoteColumns();
        pattern.SetColumns(NoteColumns);

        Patterns.Add(pattern);
        return Patterns.Count - 1;
    }

    /// <summary>
    /// Adds a pattern holding the same music as another, and hands back where it landed.
    /// </summary>
    /// <remarks>
    /// A copy and not a second name for the same thing: <see cref="Pattern.Clone"/> takes the
    /// cells and the automation lanes, so editing one afterwards leaves the other alone. That is
    /// the whole point of copying a pattern rather than putting the one you have into the order
    /// twice, which the order already allows and which is a different thing to want.
    ///
    /// Named the way a new one is, by how many there are, so the two ways of getting a pattern
    /// cannot end up with two ways of naming one. The name is what the order list shows and is
    /// not an identity: nothing looks a pattern up by it.
    /// </remarks>
    /// <param name="index">Which pattern to copy.</param>
    /// <returns>Where the copy is in <see cref="Patterns"/>, or -1 for a pattern that is not there.</returns>
    public int ClonePattern(int index)
    {
        if (index < 0 || index >= Patterns.Count) return -1;

        var copy = Patterns[index].Clone();
        copy.Name = Named(Patterns.Count);

        Patterns.Add(copy);
        return Patterns.Count - 1;
    }

    /// <summary>
    /// Moves one slot of the order to another place in it, taking its pattern with it.
    /// </summary>
    /// <remarks>
    /// The slot moves, not the pattern: a pattern that is in the order three times has three
    /// slots and only the one being dragged is touched.
    ///
    /// The destination is read as where the slot should end up once it has been taken out, which
    /// is what dragging a row down a list means to the hand doing it. Out of range at either end
    /// is held to the ends rather than refused, since a drop below the last row is a drop on the
    /// last row as far as anybody dragging is concerned.
    /// </remarks>
    /// <param name="from">The slot being moved.</param>
    /// <param name="to">Where it should sit afterwards.</param>
    /// <returns>False when nothing moved, which is a slot dropped where it already was.</returns>
    public bool MoveOrder(int from, int to)
    {
        if (Order.Count < 2) return false;
        if (from < 0 || from >= Order.Count) return false;

        to = Math.Clamp(to, 0, Order.Count - 1);
        if (to == from) return false;

        int slot = Order[from];

        Order.RemoveAt(from);
        Order.Insert(to, slot);

        return true;
    }

    /// <summary>
    /// Removes an instrument and repairs every cell that referred to one. Cells point at
    /// instruments by index, so deleting one without renumbering would silently repoint every
    /// note above it at the wrong sample.
    /// </summary>
    /// <remarks>
    /// The track defaults point at instruments by index too, so they are renumbered alongside
    /// the cells. A track that held the one being removed is left holding nothing.
    /// </remarks>
    public bool RemoveInstrumentAt(int index)
    {
        if (index < 0 || index >= Instruments.Count) return false;

        Instruments.RemoveAt(index);

        for (int track = 0; track < TrackInstruments.Count; track++)
        {
            if (TrackInstruments[track] == index) TrackInstruments[track] = TrackerCell.NoInstrument;
            else if (TrackInstruments[track] > index) TrackInstruments[track]--;
        }

        foreach (var pattern in Patterns)
            for (int line = 0; line < pattern.Lines; line++)
                for (int track = 0; track < pattern.TrackCount; track++)
                {
                    var cell = pattern[line, track];
                    if (cell.Instrument == TrackerCell.NoInstrument) continue;

                    if (cell.Instrument == index)
                        pattern[line, track] = cell with { Instrument = TrackerCell.NoInstrument };
                    else if (cell.Instrument > index)
                        pattern[line, track] = cell with { Instrument = cell.Instrument - 1 };
                }

        return true;
    }

    /// <summary>
    /// How many notes on a track are addressed to something other than the given instrument.
    /// </summary>
    /// <remarks>
    /// Counted across every pattern, because a track's instrument belongs to the song and not
    /// to one pattern. Cells with a blank instrument column are not counted: they already
    /// follow whatever the track is pointed at.
    /// </remarks>
    public int NotesAddressedElsewhere(int track, int instrument)
    {
        int count = 0;

        foreach (var pattern in Patterns)
        {
            if (track < 0 || track >= pattern.TrackCount) continue;

            for (int line = 0; line < pattern.Lines; line++)
            {
                var cell = pattern[line, track];

                if (cell.Instrument == TrackerCell.NoInstrument) continue;
                if (cell.Instrument == instrument) continue;

                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Points every note already written on a track at one instrument. Returns how many cells
    /// were changed.
    /// </summary>
    /// <remarks>
    /// A cell names its own instrument, and a track separately has one bound to it, so the two
    /// can drift apart: binding an instrument to a track does not touch notes that were
    /// already typed there. When they disagree the notes go where the cells say and the
    /// track's own instrument is never played at all, which sounds like a plugin that has
    /// stopped working rather than like a numbering mistake. This is how the cells are brought
    /// back into line with the track.
    ///
    /// Only the instrument column moves. The notes, the volumes and the effects are what was
    /// played and stay exactly as they were.
    /// </remarks>
    public int PointNotesAtTrackInstrument(int track, int instrument)
    {
        if (instrument < 0 || instrument >= Instruments.Count) return 0;

        int changed = 0;

        foreach (var pattern in Patterns)
        {
            if (track < 0 || track >= pattern.TrackCount) continue;

            for (int line = 0; line < pattern.Lines; line++)
            {
                var cell = pattern[line, track];

                if (cell.Instrument == TrackerCell.NoInstrument) continue;
                if (cell.Instrument == instrument) continue;

                pattern[line, track] = cell with { Instrument = instrument };
                changed++;
            }
        }

        return changed;
    }

    /// <summary>
    /// Moves a whole track to another position: its notes in every pattern, the instrument
    /// bound to it, and its mixer strip.
    /// </summary>
    /// <remarks>
    /// All three move together, because all three are what anybody means by "that track". A
    /// reorder that took the notes and left the instrument behind would put every track's
    /// sound on somebody else's notes, which is the same trap as a cell naming one instrument
    /// while its track is bound to another.
    ///
    /// A side chain names the track that pushes it down, by number, and those numbers have just
    /// changed under it. Remapped rather than cleared: the strip is still keyed off the same
    /// track, and that track is still in the song, only somewhere else.
    /// </remarks>
    public bool MoveTrack(int from, int to)
    {
        if (from == to) return false;
        if (from < 0 || from >= TrackCount || to < 0 || to >= TrackCount) return false;

        EnsureTrackInstruments();
        EnsureMix();
        EnsureNoteColumns();

        foreach (var pattern in Patterns) pattern.MoveTrack(from, to);

        Shift(TrackInstruments, from, to);
        Shift(Mix, from, to);
        Shift(NoteColumns, from, to);

        foreach (var strip in Mix)
        {
            if (strip.DuckFrom == TrackMix.NoKey) continue;

            strip.DuckFrom = WhereTrackWent(strip.DuckFrom, from, to);
        }

        return true;
    }

    /// <summary>Where a track number ends up once one track has been moved to another place.</summary>
    /// <remarks>
    /// Everything the moved track passed over slides one place the other way to fill the gap it
    /// left, and everything outside the stretch between the two positions does not move at all.
    /// Public because a duck's key track and anything else naming a track by number has to be
    /// able to ask the same question and get the same answer.
    /// </remarks>
    public static int WhereTrackWent(int track, int from, int to)
    {
        if (track == from) return to;

        if (from < to) return track > from && track <= to ? track - 1 : track;

        return track >= to && track < from ? track + 1 : track;
    }

    /// <summary>The same move, for the lists that run alongside the patterns.</summary>
    private static void Shift<T>(List<T> list, int from, int to)
    {
        if (from < 0 || from >= list.Count || to < 0 || to >= list.Count) return;

        var moved = list[from];
        list.RemoveAt(from);
        list.Insert(to, moved);
    }

    /// <summary>Applies a new track count to the song and every pattern in it.</summary>
    public void SetTrackCount(int trackCount)
    {
        TrackCount = Math.Clamp(trackCount, MinTrackCount, MaxTrackCount);

        EnsureTrackInstruments();
        EnsureMix();
        EnsureNoteColumns();

        foreach (var pattern in Patterns)
        {
            pattern.SetTrackCount(TrackCount);
            pattern.SetColumns(NoteColumns);
        }
    }

    /// <summary>How long the whole song lasts, every slot of the order counted in turn.</summary>
    public TimeSpan Duration =>
        TimeSpan.FromSeconds(Timing.SecondsPerLine *
            Enumerable.Range(0, Order.Count).Sum(i => PatternAt(i)?.Lines ?? 0));

    /// <summary>
    /// Brings a loaded song back to a state the player can trust: sane tempo, patterns all
    /// the same width, and no order entry pointing at a pattern that is not there.
    /// </summary>
    /// <remarks>
    /// A song file is text anybody can edit, so everything here is a repair rather than a check:
    /// nothing throws, and what cannot be made sense of is replaced with what a new song would
    /// have had.
    ///
    /// Three of the repairs are worth naming. A patch that is missing or out of range would build
    /// a voice that is either a crash or a noise nobody asked for. A track pointed at an
    /// instrument that is not in the list, including a junk negative, becomes "none", so nothing
    /// invalid is ever written back out. And one instrument put on two tracks is a mapping this
    /// song does not have: the first track keeps it.
    /// </remarks>
    public void Normalize()
    {
        Bpm = Math.Clamp(Bpm, TrackerTiming.MinBpm, TrackerTiming.MaxBpm);
        LinesPerBeat = Math.Clamp(LinesPerBeat, TrackerTiming.MinLinesPerBeat, TrackerTiming.MaxLinesPerBeat);
        TrackCount = Math.Clamp(TrackCount, MinTrackCount, MaxTrackCount);

        if (Patterns.Count == 0)
            Patterns.Add(new Pattern(Pattern.DefaultLines, TrackCount) { Name = "01" });

        EnsureNoteColumns();

        foreach (var pattern in Patterns)
        {
            pattern.SetTrackCount(TrackCount);
            pattern.SetColumns(NoteColumns);
        }

        Order.RemoveAll(index => index < 0 || index >= Patterns.Count);
        if (Order.Count == 0)
            Order.Add(0);

        foreach (var instrument in Instruments)
        {
            instrument.Patch ??= new Synth.SynthPatch();
            instrument.Patch.Clamp();
            instrument.EnsureShape();
        }

        EnsureTrackInstruments();
        EnsureMix();

        for (int track = 0; track < TrackInstruments.Count; track++)
        {
            int instrument = TrackInstruments[track];

            if (instrument < 0 || instrument >= Instruments.Count)
            {
                TrackInstruments[track] = TrackerCell.NoInstrument;
                continue;
            }

            for (int later = track + 1; later < TrackInstruments.Count; later++)
                if (TrackInstruments[later] == instrument)
                    TrackInstruments[later] = TrackerCell.NoInstrument;
        }
    }
}
