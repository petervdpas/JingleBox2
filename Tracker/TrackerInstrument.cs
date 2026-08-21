using JingleBox2.Tracker.Synth;
using System.Text.Json.Serialization;

namespace JingleBox2.Tracker;

public enum TrackerInstrumentKind
{
    /// <summary>One of your recordings, pitched by resampling.</summary>
    Sample = 0,

    /// <summary>Generated on the fly from a patch, so it needs no file at all.</summary>
    Synth = 1
}

/// <summary>
/// What a pattern cell's instrument number points at. Either a recording played back at a
/// pitch, or a synth voice built from a patch. Both carry a name and a level, and the rest
/// only applies to one of the two.
/// </summary>
public sealed class TrackerInstrument
{
    public string Name { get; set; } = "";

    public TrackerInstrumentKind Kind { get; set; } = TrackerInstrumentKind.Sample;

    /// <summary>The synth settings. Carried by every instrument, used when Kind is Synth.</summary>
    public SynthPatch Patch { get; set; } = new();

    /// <summary>Absolute path to the WAV file.</summary>
    public string FilePath { get; set; } = "";

    /// <summary>
    /// The pitch the sample actually sounds at. Playing this note reproduces the file
    /// untouched; every other note is a resample relative to it.
    /// </summary>
    public int BaseNoteSemitone { get; set; } = Note.C4.Semitone;

    /// <summary>0-1 gain applied on top of the cell's volume column.</summary>
    public double Volume { get; set; } = 1.0;

    public bool Loop { get; set; }

    [JsonIgnore]
    public bool IsSynth => Kind == TrackerInstrumentKind.Synth;

    [JsonIgnore]
    public Note BaseNote
    {
        get => new(BaseNoteSemitone);
        set => BaseNoteSemitone = value.Semitone;
    }

    /// <summary>A synth instrument with the starting patch, ready to be edited.</summary>
    public static TrackerInstrument CreateSynth(string name) => new()
    {
        Name = name,
        Kind = TrackerInstrumentKind.Synth,
        Patch = new SynthPatch()
    };

    public TrackerInstrument Clone() => new()
    {
        Name = Name,
        Kind = Kind,
        Patch = Patch.Clone(),
        FilePath = FilePath,
        BaseNoteSemitone = BaseNoteSemitone,
        Volume = Volume,
        Loop = Loop
    };
}
