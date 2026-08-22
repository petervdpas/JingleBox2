using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

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

    [JsonIgnore]
    public bool HasSound => FilePath.Length > 0;

    /// <summary>True when this zone answers to that key.</summary>
    public bool Covers(int semitone) => semitone >= Low && semitone <= High;

    /// <summary>How the range reads on a panel: the two notes it runs between.</summary>
    [JsonIgnore]
    public string RangeText => new Note(Low) + " - " + new Note(High);

    [JsonIgnore]
    public string RootText => new Note(Root).ToString();

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

    public void Clamp()
    {
        Low = Math.Clamp(Low, 0, 119);
        High = Math.Clamp(High, 0, 119);

        // A range the wrong way round answers to nothing at all, which reads as a broken zone
        // rather than an empty one. Turned round rather than refused.
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
/// The same playback the recording machine does, with a map in front of it, exactly as
/// BongaBong is. The two machines differ in one line: a pad passes the played note as its own
/// root so nothing is resampled, and a zone passes the root it was recorded at so everything
/// is. Neither needs a second way of getting audio out of a file.
///
/// Zones are asked in order and the first that covers the key wins, so putting a narrow zone
/// above a wide one is how you carve an exception out of it.
/// </remarks>
public sealed class ZoneMap
{
    /// <summary>How many zones a map can hold.</summary>
    public const int MaxZones = 32;

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

        low = Math.Clamp(low, 0, 119);
        high = Math.Clamp(high, low, 119);

        int span = high - low + 1;

        for (int i = 0; i < sounding.Count; i++)
        {
            int from = low + span * i / sounding.Count;
            int to = low + span * (i + 1) / sounding.Count - 1;

            // The last zone takes whatever the division left over, so nothing above it is silent.
            if (i == sounding.Count - 1) to = high;

            sounding[i].Low = from;
            sounding[i].High = Math.Max(from, to);
            sounding[i].Root = (sounding[i].Low + sounding[i].High) / 2;
        }
    }

    public ZoneMap Clone()
    {
        var map = new ZoneMap();

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
