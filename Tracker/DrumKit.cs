using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace JingleBox2.Tracker;

/// <summary>
/// One pad of a kit: a recording, the key that fires it, and how it sits in the mix.
/// </summary>
/// <remarks>
/// A pad is not an instrument. It has no pitch of its own and is never transposed: a snare
/// played four semitones up is not a snare, it is a mistake. What a key selects here is which
/// recording sounds, and that is the whole difference between this machine and the one it is
/// built on.
/// </remarks>
public sealed class DrumPad
{
    /// <summary>Which key fires it, as an absolute semitone.</summary>
    public int Semitone { get; set; }

    /// <summary>What it is called on the panel. The file's name when nothing is given.</summary>
    public string Name { get; set; } = "";

    /// <summary>The recording it plays. Empty for a pad nothing has been put on yet.</summary>
    public string FilePath { get; set; } = "";

    /// <summary>0-1 gain on top of the cell's volume column.</summary>
    public double Volume { get; set; } = 1.0;

    /// <summary>Where it sits across the stereo field, -1 left to 1 right.</summary>
    public double Pan { get; set; }

    /// <summary>Which part of the recording plays, and how it repeats.</summary>
    public SampleShape? Shape { get; set; }

    /// <summary>
    /// Pads sharing a choke group cut each other off. Nought is no group at all.
    /// </summary>
    /// <remarks>
    /// What a hihat needs and nothing else does: the closed one has to stop the open one dead,
    /// because on a real kit the same piece of metal cannot be doing both. Everything else on
    /// a kit rings over everything else, which is why a kit does not take the tracker's usual
    /// one voice to a track.
    /// </remarks>
    public int Choke { get; set; }

    [JsonIgnore]
    public bool HasSound => FilePath.Length > 0;

    /// <summary>The note this pad answers to, for a panel that shows it.</summary>
    [JsonIgnore]
    public Note Note => new(Semitone);

    public DrumPad Clone() => new()
    {
        Semitone = Semitone,
        Name = Name,
        FilePath = FilePath,
        Volume = Volume,
        Pan = Pan,
        Shape = Shape?.Clone(),
        Choke = Choke
    };

    public void Clamp()
    {
        Semitone = Math.Clamp(Semitone, 0, 119);
        Volume = double.IsNaN(Volume) ? 1 : Math.Clamp(Volume, 0, 1);
        Pan = double.IsNaN(Pan) ? 0 : Math.Clamp(Pan, -1, 1);
        Choke = Math.Clamp(Choke, 0, 8);

        Shape ??= new SampleShape();
        Shape.Clamp();
    }
}

/// <summary>
/// What BongaBong plays: a list of pads, each answering to one key.
/// </summary>
/// <remarks>
/// A key here is an address, not a pitch. It selects which recording sounds and nothing else,
/// which is why a kit is not a map with the transposing turned off: the pads do not cut each
/// other, they ring over each other until a choke group says otherwise, and a track playing a
/// kit is therefore the one place in the tracker holding more than one voice at a time.
///
/// What this shares with a map is reading audio out of a file, and being cuttable into pieces.
/// Nothing above that.
///
/// Sixteen pads laid out four by four, starting at C-4, because that is what the hand expects
/// and because sixteen is as many as a panel can show without becoming a list.
/// </remarks>
public sealed class DrumKit
{
    /// <summary>How many pads a kit has.</summary>
    public const int PadCount = 16;

    /// <summary>The key the first pad answers to: C-4, the note a fresh pattern starts on.</summary>
    public const int FirstSemitone = 48;

    public List<DrumPad> Pads { get; set; } = new();

    /// <summary>A kit with sixteen empty pads, laid out from C-4 upwards.</summary>
    public static DrumKit Empty()
    {
        var kit = new DrumKit();

        for (int i = 0; i < PadCount; i++)
            kit.Pads.Add(new DrumPad { Semitone = FirstSemitone + i, Shape = new SampleShape() });

        return kit;
    }

    /// <summary>Which pad answers to a note, or null when nothing does.</summary>
    public DrumPad? For(Note note) =>
        note.IsPlayable ? Pads.FirstOrDefault(p => p.Semitone == note.Semitone && p.HasSound) : null;

    /// <summary>Every recording this kit uses, for preloading and for reporting what is missing.</summary>
    /// <remarks>Worked out from the pieces, so writing it into the file would say it twice.</remarks>
    [JsonIgnore]
    public IEnumerable<string> Files => Pads.Where(p => p.HasSound).Select(p => p.FilePath);

    /// <summary>
    /// True when the pads here are pieces of one recording rather than separate recordings.
    /// </summary>
    /// <remarks>
    /// Which is what chopping a loop over the pads amounts to: sixteen keys onto one break,
    /// each firing a different moment of it. The same operation Zampler does, landing on fixed
    /// keys instead of stretches of keyboard.
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
    public string SlicedFile => Slices.OneFile(Sounding().Select(p => p.FilePath));

    /// <summary>
    /// Where the recording was cut, read back off the pads. One more point than there are
    /// slices: the first is where the sliced region starts, the last where it ends.
    /// </summary>
    public IReadOnlyList<double> SlicePoints() =>
        IsSliced ? Slices.PointsFrom(Sounding().Select(p => p.Shape).ToList()) : Array.Empty<double>();

    /// <summary>
    /// One recording cut at those points and laid over the pads, a piece to each key.
    /// </summary>
    public static DrumKit Slice(string filePath, IReadOnlyList<double> points)
    {
        var kit = Empty();

        kit.Reslice(filePath, points);

        return kit;
    }

    /// <summary>
    /// Lays the slices over the pads again after a point has moved, arrived or gone.
    /// </summary>
    /// <remarks>
    /// Pads past the last slice are emptied rather than left holding a piece of the previous
    /// chop, which would sound and could not be explained. What was set on a pad by hand, its
    /// level, its place in the stereo field, its choke group, stays where it was.
    /// </remarks>
    public void Reslice(string filePath, IReadOnlyList<double> points)
    {
        int slices = Slices.CountFor(points, PadCount);

        if (slices == 0) return;

        Clamp();

        for (int i = 0; i < PadCount; i++)
        {
            var pad = Pads[i];

            pad.Shape ??= new SampleShape();

            if (i < slices)
            {
                pad.FilePath = filePath;
                pad.Name = Slices.NameFor(filePath, i);
                pad.Shape.Start = points[i];
                pad.Shape.End = points[i + 1];
            }
            else
            {
                pad.FilePath = "";
                pad.Name = "";
                pad.Shape.Start = 0;
                pad.Shape.End = 1;
            }
        }

        Sliced = true;

        Clamp();
    }

    /// <summary>
    /// The pads holding pieces, from the first up to the first empty one. A chop fills the
    /// pads from the start, so the run stops where the chop did.
    /// </summary>
    private IEnumerable<DrumPad> Sounding() => Pads.TakeWhile(p => p.HasSound);

    public DrumKit Clone()
    {
        var kit = new DrumKit { Sliced = Sliced };

        foreach (var pad in Pads) kit.Pads.Add(pad.Clone());

        return kit;
    }

    /// <summary>
    /// Takes on another kit's pads without becoming another object, for a preset landing on
    /// the kit the panel is already holding.
    /// </summary>
    public void CopyFrom(DrumKit other)
    {
        if (other is null || ReferenceEquals(other, this)) return;

        Pads.Clear();

        foreach (var pad in other.Pads) Pads.Add(pad.Clone());

        Sliced = other.Sliced;

        Clamp();
    }

    /// <summary>
    /// Brings a kit read off disk back into shape: sixteen pads, one to a key, in order.
    /// </summary>
    public void Clamp()
    {
        Pads ??= new List<DrumPad>();

        foreach (var pad in Pads) pad.Clamp();

        // A file may hold fewer pads than the machine has, or none at all. The missing ones
        // are added silently rather than the kit being refused.
        for (int i = Pads.Count; i < PadCount; i++)
            Pads.Add(new DrumPad { Semitone = FirstSemitone + i, Shape = new SampleShape() });

        if (Pads.Count > PadCount) Pads.RemoveRange(PadCount, Pads.Count - PadCount);

        // One key to a pad. Two pads on the same key means one of them can never sound.
        for (int i = 0; i < Pads.Count; i++)
            if (Pads.Take(i).Any(p => p.Semitone == Pads[i].Semitone))
                Pads[i].Semitone = FirstSemitone + i;
    }
}
