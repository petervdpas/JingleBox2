using JingleBox2.Tracker.Synth;
using System;
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
/// A playable voice: either a recording played back at a pitch, or a synth built from a patch.
/// Instruments live in a library of their own and are used by any number of songs, so the
/// identity below is what a song holds on to, not the position in a list.
/// </summary>
public sealed class TrackerInstrument
{
    /// <summary>
    /// Stable across renames, which is what lets a song find its instrument again after you
    /// have called it something else.
    /// </summary>
    public string Id { get; set; } = "";

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

    /// <summary>
    /// Kept for files written before the shape existed, and kept in step with it since: an
    /// older build reads this and still loops.
    /// </summary>
    public bool Loop { get; set; }

    /// <summary>
    /// Which part of the recording plays, and how it repeats. Null on an instrument written
    /// before samples had a shape at all, which is the one reliable sign that its envelope
    /// was never heard: see <see cref="EnsureShape"/>.
    /// </summary>
    public SampleShape? Shape { get; set; }

    [JsonIgnore]
    public bool IsSynth => Kind == TrackerInstrumentKind.Synth;

    [JsonIgnore]
    public Note BaseNote
    {
        get => new(BaseNoteSemitone);
        set => BaseNoteSemitone = value.Semitone;
    }

    /// <summary>An instrument that plays a recording, with the envelope out of the way.</summary>
    public static TrackerInstrument CreateSample(string name, string filePath, Note baseNote)
    {
        var instrument = new TrackerInstrument
        {
            Name = name,
            Kind = TrackerInstrumentKind.Sample,
            FilePath = filePath,
            BaseNote = baseNote
        };

        instrument.EnsureId();
        instrument.EnsureShape();

        return instrument;
    }

    /// <summary>A synth instrument with the starting patch, ready to be edited.</summary>
    public static TrackerInstrument CreateSynth(string name) => CreateSynth(name, new SynthPatch());

    /// <summary>A synth instrument built from a patch, which is how a preset starts a new one.</summary>
    public static TrackerInstrument CreateSynth(string name, SynthPatch patch)
    {
        var instrument = new TrackerInstrument
        {
            Name = name,
            Kind = TrackerInstrumentKind.Synth,
            Patch = patch.Clone()
        };

        instrument.EnsureId();
        return instrument;
    }

    /// <summary>Gives an instrument an identity if it has none, for anything read off disk.</summary>
    public void EnsureId()
    {
        if (string.IsNullOrWhiteSpace(Id)) Id = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Straightens the sample settings, for anything read off disk. An instrument saved before
    /// there was a shape carries its loop as a flag, so that is what the shape starts from;
    /// after that the two are kept saying the same thing.
    /// </summary>
    public void EnsureShape()
    {
        if (Shape == null)
        {
            Shape = new SampleShape();

            // A sample used to be handed to the audio library whole, so whatever its patch
            // says was never heard. Playing it through the voice now would put a decay on
            // every recording that has one, so the envelope opens flat instead.
            if (!IsSynth) FlattenEnvelope();
        }

        Shape.Clamp();

        if (Loop && Shape.LoopMode == SampleLoopMode.None) Shape.LoopMode = SampleLoopMode.Forward;

        Loop = Shape.IsLooping;
    }

    /// <summary>
    /// The envelope a recording starts with: none. It plays as it was recorded until the note
    /// ends, and the short release is only there so a note off does not click.
    /// </summary>
    public void FlattenEnvelope()
    {
        Patch ??= new SynthPatch();

        Patch.AttackMs = 0;
        Patch.DecayMs = 0;
        Patch.Sustain = 1;
        Patch.ReleaseMs = 20;
    }

    /// <summary>
    /// Takes on another instrument's sound and name, keeping this object. A song's copy is
    /// refreshed this way, so everything already pointing at it stays pointing at it.
    /// </summary>
    public void CopyFrom(TrackerInstrument other)
    {
        if (other is null || ReferenceEquals(other, this)) return;

        Id = other.Id;
        Name = other.Name;
        Kind = other.Kind;
        Patch = other.Patch.Clone();
        FilePath = other.FilePath;
        BaseNoteSemitone = other.BaseNoteSemitone;
        Volume = other.Volume;
        Loop = other.Loop;
        Shape = other.Shape?.Clone();
    }

    public TrackerInstrument Clone() => new()
    {
        Id = Id,
        Name = Name,
        Kind = Kind,
        Patch = Patch.Clone(),
        FilePath = FilePath,
        BaseNoteSemitone = BaseNoteSemitone,
        Volume = Volume,
        Loop = Loop,
        Shape = Shape?.Clone()
    };
}
