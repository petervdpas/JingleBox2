using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace JingleBox2.Tracker;

/// <summary>
/// Patterns, the order they play in, and the instruments they reference.
/// The order list holds pattern indexes, so the same pattern can appear more than once.
/// </summary>
public sealed class Song
{
    public const int MinTrackCount = 1;

    /// <summary>Two digits is as wide as the track badges and headers are built for.</summary>
    public const int MaxTrackCount = 32;

    public const int DefaultTrackCount = 4;

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

    public double Bpm { get; set; } = TrackerTiming.DefaultBpm;
    public int LinesPerBeat { get; set; } = TrackerTiming.DefaultLinesPerBeat;

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
    public int TrackCount { get; set; } = DefaultTrackCount;

    public List<Pattern> Patterns { get; set; } = new();

    /// <summary>Indexes into <see cref="Patterns"/>, in playing order.</summary>
    public List<int> Order { get; set; } = new();

    public List<TrackerInstrument> Instruments { get; set; } = new();

    /// <summary>
    /// Which instrument each track holds, by index, or -1 for none. The mapping is one to one
    /// in both directions: an instrument sits on a single track, and a track holds a single
    /// instrument. To play one sample on two tracks, add the recording twice.
    /// </summary>
    public List<int> TrackInstruments { get; set; } = new();

    /// <summary>One strip per track: level, placement, mute and solo.</summary>
    public List<TrackMix> Mix { get; set; } = new();

    [JsonIgnore]
    public TrackerTiming Timing => new(Bpm, LinesPerBeat);

    public static Song CreateDefault()
    {
        var song = new Song();
        song.Patterns.Add(new Pattern(Pattern.DefaultLines, song.TrackCount) { Name = "01" });
        song.Order.Add(0);
        return song;
    }

    public Pattern? PatternAt(int orderIndex)
    {
        if (orderIndex < 0 || orderIndex >= Order.Count) return null;

        int patternIndex = Order[orderIndex];
        return patternIndex >= 0 && patternIndex < Patterns.Count ? Patterns[patternIndex] : null;
    }

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
    }

    /// <summary>Adds a pattern sized to match the song and returns its index.</summary>
    public int AddPattern(int lines = Pattern.DefaultLines)
    {
        var pattern = new Pattern(lines, TrackCount)
        {
            Name = (Patterns.Count + 1).ToString("00")
        };
        Patterns.Add(pattern);
        return Patterns.Count - 1;
    }

    /// <summary>
    /// Removes an instrument and repairs every cell that referred to one. Cells point at
    /// instruments by index, so deleting one without renumbering would silently repoint every
    /// note above it at the wrong sample.
    /// </summary>
    public bool RemoveInstrumentAt(int index)
    {
        if (index < 0 || index >= Instruments.Count) return false;

        Instruments.RemoveAt(index);

        // Track defaults point at instruments by index too, so they renumber alongside cells.
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
    /// </remarks>
    public bool MoveTrack(int from, int to)
    {
        if (from == to) return false;
        if (from < 0 || from >= TrackCount || to < 0 || to >= TrackCount) return false;

        EnsureTrackInstruments();
        EnsureMix();

        foreach (var pattern in Patterns) pattern.MoveTrack(from, to);

        Shift(TrackInstruments, from, to);
        Shift(Mix, from, to);

        // A side chain names the track that pushes it down, by number, and those numbers have
        // just changed under it. Remapped rather than cleared: the strip is still keyed off
        // the same track, and that track is still in the song, only somewhere else.
        foreach (var strip in Mix)
        {
            if (strip.DuckFrom == TrackMix.NoKey) continue;

            strip.DuckFrom = WhereTrackWent(strip.DuckFrom, from, to);
        }

        return true;
    }

    /// <summary>Where a track number ends up once one track has been moved to another place.</summary>
    public static int WhereTrackWent(int track, int from, int to)
    {
        if (track == from) return to;

        // Everything the moved track passed over slides one place the other way to fill in.
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
        foreach (var pattern in Patterns)
            pattern.SetTrackCount(TrackCount);

        EnsureTrackInstruments();
        EnsureMix();
    }

    public TimeSpan Duration =>
        TimeSpan.FromSeconds(Timing.SecondsPerLine *
            Enumerable.Range(0, Order.Count).Sum(i => PatternAt(i)?.Lines ?? 0));

    /// <summary>
    /// Brings a loaded song back to a state the player can trust: sane tempo, patterns all
    /// the same width, and no order entry pointing at a pattern that is not there.
    /// </summary>
    public void Normalize()
    {
        Bpm = Math.Clamp(Bpm, TrackerTiming.MinBpm, TrackerTiming.MaxBpm);
        LinesPerBeat = Math.Clamp(LinesPerBeat, TrackerTiming.MinLinesPerBeat, TrackerTiming.MaxLinesPerBeat);
        TrackCount = Math.Clamp(TrackCount, MinTrackCount, MaxTrackCount);

        if (Patterns.Count == 0)
            Patterns.Add(new Pattern(Pattern.DefaultLines, TrackCount) { Name = "01" });

        foreach (var pattern in Patterns)
            pattern.SetTrackCount(TrackCount);

        Order.RemoveAll(index => index < 0 || index >= Patterns.Count);
        if (Order.Count == 0)
            Order.Add(0);

        // A hand-edited file can leave a patch missing or out of range, and a voice built from
        // one of those is either a crash or a noise nobody asked for.
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

            // Anything outside the instrument list becomes "none", including junk negatives
            // from a hand-edited file, so nothing invalid is ever written back out.
            if (instrument < 0 || instrument >= Instruments.Count)
            {
                TrackInstruments[track] = TrackerCell.NoInstrument;
                continue;
            }

            // A hand-edited file can put one instrument on two tracks. The first keeps it.
            for (int later = track + 1; later < TrackInstruments.Count; later++)
                if (TrackInstruments[later] == instrument)
                    TrackInstruments[later] = TrackerCell.NoInstrument;
        }
    }
}
