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
/// The same playback the recording machine does, with a map in front of it. Which is the whole
/// design: Zampler will be this list with ranges and transposing, BongaBong is this list with
/// one key apiece and none. Neither needs a second way of getting audio out of a file.
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
    public IEnumerable<string> Files => Pads.Where(p => p.HasSound).Select(p => p.FilePath);

    public DrumKit Clone()
    {
        var kit = new DrumKit();

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
