using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using JingleBox2.Tracker.Records;
using JingleBox2.Music;
using JingleBox2.Music.Interfaces;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.Tracker;

/// <summary>
/// One zone of a map: a recording, the keys it answers to, and the key it was recorded at.
/// </summary>
/// <remarks>
/// The difference between this and a pad is the root. A pad plays its recording and that is
/// all; a zone plays its recording at whatever speed the key asks for, relative to the note it
/// was recorded at. Which is why one is a kit and the other is an instrument: a piano sampled
/// every fourth key is thirteen zones, each covering the keys either side of its own.
/// </remarks>
public sealed class SampleZone
{
    /// <summary>What it is called on the panel. The file's name when nothing is given.</summary>
    public string Name { get; set; } = "";

    /// <summary>The recording it plays. Empty for a zone nothing has been put on yet.</summary>
    public string FilePath { get; set; } = "";

    /// <summary>The lowest key this zone answers to.</summary>
    public int Low { get; set; }

    /// <summary>The highest key this zone answers to.</summary>
    public int High { get; set; } = 119;

    /// <summary>
    /// The key at which the recording plays untouched. Every other key is read faster or slower
    /// than this one.
    /// </summary>
    public int Root { get; set; } = 48;

    /// <summary>0-1 gain on top of the cell's volume column.</summary>
    public double Volume { get; set; } = 1.0;

    /// <summary>Where it sits across the stereo field, -1 left to 1 right.</summary>
    public double Pan { get; set; }

    /// <summary>Hundredths of a semitone, for sitting one zone against the next.</summary>
    public double FineCents { get; set; }

    /// <summary>Which part of the recording plays, and how it repeats.</summary>
    public SampleShape? Shape { get; set; }

    /// <summary>True when something has been put on this zone. An empty zone makes no sound.</summary>
    [JsonIgnore]
    public bool HasSound => FilePath.Length > 0;

    /// <summary>True when this zone answers to that key.</summary>
    public bool Covers(int semitone) => semitone >= Low && semitone <= High;

    /// <summary>How the range reads on a panel: the two notes it runs between.</summary>
    [JsonIgnore]
    public string RangeText => new Note(Low) + " - " + new Note(High);

    /// <summary>How the root reads on a panel: the note the recording plays untouched at.</summary>
    [JsonIgnore]
    public string RootText => new Note(Root).ToString();

    /// <summary>A copy nothing else is holding, for a preset landing on a map already in use.</summary>
    public SampleZone Clone() => new()
    {
        Name = Name,
        FilePath = FilePath,
        Low = Low,
        High = High,
        Root = Root,
        Volume = Volume,
        Pan = Pan,
        FineCents = FineCents,
        Shape = Shape?.Clone()
    };

    /// <summary>
    /// Brings a zone read off disc back inside its ends: keys a note column can say, a level, a
    /// place and a tuning that are numbers.
    /// </summary>
    /// <remarks>
    /// A range the wrong way round answers to nothing at all, which reads as a broken zone
    /// rather than an empty one, so it is turned round rather than refused. Everything else is
    /// clamped for the same reason: a reading nobody can explain would be a zone that goes
    /// quiet with nothing said.
    /// </remarks>
    public void Clamp()
    {
        Low = Math.Clamp(Low, 0, 119);
        High = Math.Clamp(High, 0, 119);

        if (High < Low) (Low, High) = (High, Low);

        Root = Math.Clamp(Root, 0, 119);
        Volume = double.IsNaN(Volume) ? 1 : Math.Clamp(Volume, 0, 1);
        Pan = double.IsNaN(Pan) ? 0 : Math.Clamp(Pan, -1, 1);
        FineCents = double.IsNaN(FineCents) ? 0 : Math.Clamp(FineCents, -100, 100);

        Shape ??= new SampleShape();
        Shape.Clamp();
    }
}

/// <summary>
/// What Zampler plays: recordings laid across the keyboard, each transposed from its own root.
/// </summary>
/// <remarks>
/// A key here is a pitch. It picks a zone and then says how fast to read it, relative to the
/// note that zone was recorded at, so every key but one is resampled. Which is the first of
/// several things that make this a different machine from a kit rather than the same one with
/// ranges: a zone takes the track's one voice as any instrument does, and what comes out of it
/// goes through a four-pole filter and two envelopes before it is heard.
///
/// What this shares with a kit is reading audio out of a file, and being cuttable into pieces.
///
/// Zones are asked in order and the first that covers the key wins, so putting a narrow zone
/// above a wide one is how you carve an exception out of it.
/// </remarks>
public sealed class ZoneMap
{
    /// <summary>Sharing a stretch of keyboard out among a number of pieces.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly IKeyRegions Regions = new KeyRegions();

    /// <summary>What a kit and a map do identically with a chopped recording.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly ISlices Pieces = new Slices();

    /// <summary>How many zones a map can hold.</summary>
    public const int MaxZones = 32;

    /// <summary>
    /// The zones, asked in order, so a narrow one above a wide one carves an exception out of
    /// it. A chop lays its pieces here, which is where <see cref="SlicePoints"/> reads the cuts
    /// back off.
    /// </summary>
    public List<SampleZone> Zones { get; set; } = new();

    /// <summary>A map with one empty zone across the whole keyboard.</summary>
    public static ZoneMap Empty()
    {
        var map = new ZoneMap();

        map.Zones.Add(new SampleZone { Low = 0, High = 119, Root = 48, Shape = new SampleShape() });

        return map;
    }

    /// <summary>
    /// Which zone answers to a note: the first that covers it and has something on it.
    /// </summary>
    public SampleZone? For(Note note) =>
        note.IsPlayable ? Zones.FirstOrDefault(z => z.HasSound && z.Covers(note.Semitone)) : null;

    /// <summary>Every recording this map uses, for preloading and for reporting what is missing.</summary>
    /// <remarks>Worked out from the pieces, so writing it into the file would say it twice.</remarks>
    [JsonIgnore]
    public IEnumerable<string> Files => Zones.Where(z => z.HasSound).Select(z => z.FilePath);

    /// <summary>
    /// Lays the zones out evenly across a stretch of keyboard, each rooted in its own middle.
    /// </summary>
    /// <remarks>
    /// What you want the moment you have dropped eight recordings on a machine and do not care
    /// where each one lands, only that they land somewhere sensible. Rooting a zone in its own
    /// middle keeps the worst transposition down to half its width either way, which is the
    /// best any even split can do.
    /// </remarks>
    public void Spread(int low = 24, int high = 96)
    {
        var sounding = Zones.Where(z => z.HasSound).ToList();
        if (sounding.Count == 0) return;

        var regions = Regions.Split(low, high, sounding.Count);

        for (int i = 0; i < sounding.Count; i++)
        {
            sounding[i].Low = regions[i].Low;
            sounding[i].High = regions[i].High;
            sounding[i].Root = Regions.Middle(regions[i].Low, regions[i].High);
        }
    }

    /// <summary>
    /// True when the zones here are pieces of one recording rather than separate recordings.
    /// </summary>
    /// <remarks>
    /// Stored rather than worked out, because a map of one zone with one file looks exactly
    /// like a map sliced into one piece and the panel has to know which of the two it is
    /// holding. Where the cuts are is not stored: that is the zones' business, and asking them
    /// is the only way to find out.
    /// </remarks>
    public bool Sliced { get; set; }

    /// <summary>
    /// True when this really is a slicing right now: marked as one, and the pieces still agree
    /// on the recording they came from. Putting a different sample on one of them ends it, which
    /// is why this is asked rather than <see cref="Sliced"/> everywhere but the flag's own setter.
    /// </summary>
    [JsonIgnore]
    public bool IsSliced => Sliced && SlicedFile.Length > 0;

    /// <summary>The recording the slices come from, or empty when they do not agree on one.</summary>
    [JsonIgnore]
    public string SlicedFile => Pieces.OneFile(Zones.Select(z => z.FilePath));

    /// <summary>
    /// Where the recording was cut, read back off the zones. One more point than there are
    /// slices: the first is where the sliced region starts, the last where it ends.
    /// </summary>
    public IReadOnlyList<double> SlicePoints() =>
        IsSliced ? Pieces.PointsFrom(Zones.Select(z => z.Shape).ToList()) : Array.Empty<double>();

    /// <summary>
    /// One recording cut at those points and laid across the keyboard, a piece to each stretch
    /// of keys.
    /// </summary>
    public static ZoneMap Slice(
        string filePath,
        IReadOnlyList<double> points,
        int low = KeyRegions.PianoLow,
        int high = KeyRegions.PianoHigh)
    {
        var map = new ZoneMap();

        map.Reslice(filePath, points, low, high);

        return map;
    }

    /// <summary>
    /// Lays the slices out again after a point has moved, arrived or gone.
    /// </summary>
    /// <remarks>
    /// Adding a point moves every key range, not only the two slices either side of it, because
    /// the same stretch of keyboard is now being shared out among one more piece. So the ranges
    /// and the roots are laid again every time. What was set on a zone by hand, its level, its
    /// place in the stereo field, its tuning, is left where it was for as many zones as survive
    /// the change.
    /// </remarks>
    public void Reslice(
        string filePath,
        IReadOnlyList<double> points,
        int low = KeyRegions.PianoLow,
        int high = KeyRegions.PianoHigh)
    {
        int slices = Pieces.CountFor(points, MaxZones);

        if (slices == 0) return;

        while (Zones.Count > slices) Zones.RemoveAt(Zones.Count - 1);
        while (Zones.Count < slices) Zones.Add(new SampleZone());

        var regions = Regions.Split(low, high, slices);

        for (int i = 0; i < slices; i++)
        {
            var zone = Zones[i];

            zone.FilePath = filePath;
            zone.Name = Pieces.NameFor(filePath, i);

            zone.Shape ??= new SampleShape();
            zone.Shape.Start = points[i];
            zone.Shape.End = points[i + 1];

            zone.Low = regions[i].Low;
            zone.High = regions[i].High;
            zone.Root = Regions.Middle(regions[i].Low, regions[i].High);
        }

        Sliced = true;

        Clamp();
    }

    /// <summary>A map nothing else is holding, zones and all, for a preset or a history step.</summary>
    public ZoneMap Clone()
    {
        var map = new ZoneMap { Sliced = Sliced };

        foreach (var zone in Zones) map.Zones.Add(zone.Clone());

        return map;
    }

    /// <summary>
    /// Takes on another map's zones without becoming another object, for a preset landing on
    /// the map the panel is already holding.
    /// </summary>
    public void CopyFrom(ZoneMap other)
    {
        if (other is null || ReferenceEquals(other, this)) return;

        Zones.Clear();

        foreach (var zone in other.Zones) Zones.Add(zone.Clone());

        Sliced = other.Sliced;

        Clamp();
    }

    /// <summary>Brings a map read off disk back into shape. A map with no zones gets one.</summary>
    public void Clamp()
    {
        Zones ??= new List<SampleZone>();

        foreach (var zone in Zones) zone.Clamp();

        if (Zones.Count == 0)
            Zones.Add(new SampleZone { Low = 0, High = 119, Root = 48, Shape = new SampleShape() });

        if (Zones.Count > MaxZones) Zones.RemoveRange(MaxZones, Zones.Count - MaxZones);
    }
}
