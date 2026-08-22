using JingleBox2.Tracker.Synth;
using System;
using System.Text.Json.Serialization;

namespace JingleBox2.Tracker;

public enum TrackerInstrumentKind
{
    /// <summary>One of your recordings, pitched by resampling.</summary>
    Sample = 0,

    /// <summary>Generated on the fly from a patch, so it needs no file at all.</summary>
    Synth = 1,

    /// <summary>A plugin doing the playing: Serum, Vital, anything that takes notes.</summary>
    Plugin = 2
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

    /// <summary>The plugin this instrument is, when it is one. Found again by id first.</summary>
    public string PluginPath { get; set; } = "";

    public string PluginId { get; set; } = "";

    public Audio.Plugins.PluginFormat PluginFormat { get; set; } = Audio.Plugins.PluginFormat.Clap;

    /// <summary>Kept so a plugin that is no longer installed can be named rather than blank.</summary>
    public string PluginName { get; set; } = "";

    /// <summary>
    /// The plugin's own state, as text so it can live in a JSON file.
    /// </summary>
    /// <remarks>
    /// This is the patch, and it is not the same as the parameters. A Serum sound is its
    /// wavetables, its samples and its modulation as much as it is knob positions, and none of
    /// those is a parameter the host can see. Saving the parameters and not this would reopen
    /// a song with the right knobs on the wrong sound.
    /// </remarks>
    public string PluginState { get; set; } = "";

    [JsonIgnore]
    public bool IsSynth => Kind == TrackerInstrumentKind.Synth;

    [JsonIgnore]
    public bool IsPlugin => Kind == TrackerInstrumentKind.Plugin;

    /// <summary>What the plugin is, in the shape the host loads from.</summary>
    [JsonIgnore]
    public Audio.Plugins.PluginInfo? Plugin =>
        string.IsNullOrWhiteSpace(PluginPath)
            ? null
            : new Audio.Plugins.PluginInfo(PluginId, PluginName, "", "", PluginPath, PluginFormat, IsInstrument: true);

    /// <summary>The state as bytes, or nothing when there is none or it will not read.</summary>
    [JsonIgnore]
    public byte[] StateBytes
    {
        get
        {
            if (string.IsNullOrWhiteSpace(PluginState)) return Array.Empty<byte>();

            try
            {
                return Convert.FromBase64String(PluginState);
            }
            catch (FormatException)
            {
                // A state written by something else, or damaged. The instrument still loads;
                // it opens at the plugin's defaults rather than not at all.
                return Array.Empty<byte>();
            }
        }
        set => PluginState = value == null || value.Length == 0 ? "" : Convert.ToBase64String(value);
    }

    /// <summary>An instrument that is a plugin, at whatever the plugin opens with.</summary>
    public static TrackerInstrument CreatePlugin(string name, Audio.Plugins.PluginInfo plugin)
    {
        return new TrackerInstrument
        {
            Name = name,
            Kind = TrackerInstrumentKind.Plugin,
            PluginPath = plugin.Path,
            PluginId = plugin.Id,
            PluginFormat = plugin.Format,
            PluginName = plugin.Name
        };
    }

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

        // What makes a plugin instrument a plugin instrument. Left out of here, an instrument
        // copied into a song is one that says it is a plugin and cannot say which, so it plays
        // nothing and opens nothing.
        PluginPath = other.PluginPath;
        PluginId = other.PluginId;
        PluginFormat = other.PluginFormat;
        PluginName = other.PluginName;
        PluginState = other.PluginState;
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
        Shape = Shape?.Clone(),
        PluginPath = PluginPath,
        PluginId = PluginId,
        PluginFormat = PluginFormat,
        PluginName = PluginName,
        PluginState = PluginState
    };
}
