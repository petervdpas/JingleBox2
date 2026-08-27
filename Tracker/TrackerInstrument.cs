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
    Plugin = 2,

    /// <summary>Ouroboros: one oscillator, a filter that sweeps, and glide between notes.</summary>
    MonoSynth = 3,

    /// <summary>BongaBong: a kit, one recording to a key, none of them transposed.</summary>
    Kit = 4,

    /// <summary>Zampler: recordings laid across the keyboard, each transposed from its root.</summary>
    Sampler = 5
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
    /// Whether a note played by hand cuts the one before it, the way a track does.
    /// </summary>
    /// <remarks>
    /// Only notes played by hand: in a pattern a track has one voice already, so this changes
    /// nothing there. It is for a long recording, where holding the panel's keys down builds a
    /// pile of takes all sounding over each other and there is no way to stop them.
    /// </remarks>
    public bool OneVoice { get; set; }

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
    /// The plugin's own state, as the plugin handed it over.
    /// </summary>
    /// <remarks>
    /// This is the patch, and it is not the same as the parameters. A Serum sound is its
    /// wavetables, its samples and its modulation as much as it is knob positions, and none of
    /// those is a parameter the host can see. Saving the parameters and not this would reopen
    /// a song with the right knobs on the wrong sound.
    ///
    /// Bytes rather than base64 text, because that is what both ends of it are. The plugin
    /// gives bytes and takes bytes back, and a quarter of a megabyte of wavetables was being
    /// encoded to text every time a knob stopped moving and decoded again to play a note. A
    /// rack file still writes it as base64, since a rack file is JSON and has nowhere else to
    /// put it; a song writes it as it stands, beside the song rather than inside it.
    ///
    /// Passed by reference and never written into. Anything changing a patch replaces the
    /// array, so two instruments sharing one costs nothing and surprises nobody.
    /// </remarks>
    [JsonConverter(typeof(PluginStateJson))]
    public byte[] PluginState { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// What the mono synth plays from. Null on every other machine, and left out of the file there.
    /// </summary>
    /// <remarks>
    /// Its own field rather than a wider version of the older patch. The two machines have
    /// different panels and different parameters, and a single patch with everything on it
    /// would be a shape neither of them fits, growing another set of fields for every machine
    /// added after.
    ///
    /// Written under the name the machine had when the field was added, because every song
    /// already saved says that. What it is called in a file is a fact about the file; what it
    /// is called here is a fact about the thing, and the two need not agree.
    /// </remarks>
    [JsonPropertyName("Ouroboros")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Synth.MonoSynthPatch? MonoSynth { get; set; }

    /// <summary>
    /// What BongaBong plays: sixteen pads, each with a recording and a key of its own.
    /// </summary>
    /// <remarks>
    /// Its own field for the same reason Ouroboros has one: two machines with different panels
    /// and different parameters do not share a shape, and a single patch with everything on it
    /// would fit neither. Left out of the file on every other machine.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DrumKit? Kit { get; set; }

    /// <summary>
    /// What Zampler plays: recordings laid across the keyboard, each with a range and a root.
    /// </summary>
    /// <remarks>
    /// Its own field, like the kit and the two patches. Left out of the file on every other
    /// machine.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ZoneMap? Zones { get; set; }

    /// <summary>
    /// What the sampler does to a recording once it has been read: its filter and its envelopes.
    /// </summary>
    /// <remarks>
    /// Written under the machine's name for the reason the mono synth's patch is: that is what
    /// the songs already saved call it.
    /// </remarks>
    [JsonPropertyName("Zampler")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Synth.SamplerPatch? Sampler { get; set; }

    [JsonIgnore]
    public bool IsSampler => Kind == TrackerInstrumentKind.Sampler;

    [JsonIgnore]
    public bool IsKit => Kind == TrackerInstrumentKind.Kit;

    [JsonIgnore]
    public bool IsMonoSynth => Kind == TrackerInstrumentKind.MonoSynth;

    /// <summary>Which machine this instrument is on, by name and description.</summary>
    [JsonIgnore]
    public Machine Machine => Machine.For(Kind);

    /// <summary>
    /// One line saying what this instrument is: which machine, and a word about how it is set.
    /// </summary>
    /// <remarks>
    /// Here rather than on either of the two things that print it, because it is a fact about
    /// an instrument and not about a list. The song's instrument list and the block at the head
    /// of a track's chain both say it, and two copies of this sentence would drift.
    ///
    /// The machine comes first, the same as on the rack, because the machine is the organising
    /// idea and because a name you chose says nothing about which panel you get when you open
    /// it. It used to say "square synth", from before there were machines at all.
    /// </remarks>
    [JsonIgnore]
    public string Detail
    {
        get
        {
            if (IsPlugin) return PluginName is { Length: > 0 } plugin ? plugin : "Plugin";

            string machine = Machine.Name;

            return Kind switch
            {
                TrackerInstrumentKind.Synth => machine + ", " + Patch.Wave.ToString().ToLowerInvariant(),
                TrackerInstrumentKind.MonoSynth => machine + ", " + (MonoSynth?.Wave.ToString().ToLowerInvariant() ?? "saw"),
                _ => machine + ", " + BaseNote
            };
        }
    }

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

    /// <summary>A new instrument on Zampler: one empty zone across the whole keyboard.</summary>
    public static TrackerInstrument CreateSampler(string name)
    {
        var instrument = new TrackerInstrument
        {
            Name = name,
            Kind = TrackerInstrumentKind.Sampler,
            Zones = ZoneMap.Empty(),
            Sampler = new Synth.SamplerPatch()
        };

        instrument.EnsureId();
        return instrument;
    }

    /// <summary>A new kit on BongaBong: sixteen pads with nothing on them yet.</summary>
    public static TrackerInstrument CreateKit(string name)
    {
        var instrument = new TrackerInstrument
        {
            Name = name,
            Kind = TrackerInstrumentKind.Kit,
            Kit = DrumKit.Empty()
        };

        instrument.EnsureId();
        return instrument;
    }

    /// <summary>A new instrument on Ouroboros, with the machine's own patch ready to shape.</summary>
    public static TrackerInstrument CreateMonoSynth(string name)
    {
        var instrument = new TrackerInstrument
        {
            Name = name,
            Kind = TrackerInstrumentKind.MonoSynth,
            MonoSynth = new Synth.MonoSynthPatch()
        };

        instrument.EnsureId();
        return instrument;
    }

    /// <summary>A new instrument on whichever machine was asked for.</summary>
    public static TrackerInstrument CreateOn(Machine machine, string name) => machine?.Kind switch
    {
        TrackerInstrumentKind.MonoSynth => CreateMonoSynth(name),
        TrackerInstrumentKind.Kit => CreateKit(name),
        TrackerInstrumentKind.Sampler => CreateSampler(name),

        // A recording with no recording on it yet, which is what the Recording machine is until
        // you put a take on it. Without this it fell through and came back an OddSkilla wearing
        // the wrong name.
        TrackerInstrumentKind.Sample => CreateSample(name, "", new Note(48)),

        _ => CreateSynth(name)
    };

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
        MonoSynth = other.MonoSynth?.Clone();
        Kit = other.Kit?.Clone();
        Zones = other.Zones?.Clone();
        Sampler = other.Sampler?.Clone();
        FilePath = other.FilePath;
        BaseNoteSemitone = other.BaseNoteSemitone;
        Volume = other.Volume;
        Loop = other.Loop;
        OneVoice = other.OneVoice;
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

    /// <summary>
    /// Takes on another instrument's sound, keeping its own name, its own id and its own level.
    /// </summary>
    /// <remarks>
    /// What a preset is. The machines the rack offers as presets are the instruments already
    /// on it, so loading one means copying what it plays from and nothing else: this is still
    /// the track's instrument, called what it is called, at the level it was set to.
    ///
    /// Only what the machine in question actually plays from is copied, so a preset picked for
    /// one machine cannot quietly write over another's settings.
    /// </remarks>
    public void TakeSoundFrom(TrackerInstrument other)
    {
        if (other is null || ReferenceEquals(other, this) || other.Kind != Kind) return;

        switch (Kind)
        {
            case TrackerInstrumentKind.Synth:
                Patch.CopyFrom(other.Patch);
                break;

            case TrackerInstrumentKind.MonoSynth:
                MonoSynth ??= new Synth.MonoSynthPatch();
                MonoSynth.CopyFrom(other.MonoSynth ?? new Synth.MonoSynthPatch());
                break;

            case TrackerInstrumentKind.Sampler:
                Zones ??= ZoneMap.Empty();
                Zones.CopyFrom(other.Zones ?? ZoneMap.Empty());

                Sampler ??= new Synth.SamplerPatch();
                Sampler.CopyFrom(other.Sampler ?? new Synth.SamplerPatch());
                break;

            case TrackerInstrumentKind.Kit:
                Kit ??= DrumKit.Empty();
                Kit.CopyFrom(other.Kit ?? DrumKit.Empty());
                Patch.CopyFrom(other.Patch);
                break;

            case TrackerInstrumentKind.Sample:
                FilePath = other.FilePath;
                BaseNoteSemitone = other.BaseNoteSemitone;
                Loop = other.Loop;
                OneVoice = other.OneVoice;
                Shape = other.Shape?.Clone();
                Patch.CopyFrom(other.Patch);
                break;

            case TrackerInstrumentKind.Plugin:
                // Only between two instruments on the same plugin: another plugin's state is
                // not a preset for this one, it is a file it cannot read.
                if (other.PluginId == PluginId && other.PluginPath == PluginPath)
                    PluginState = other.PluginState;

                break;
        }
    }

    public TrackerInstrument Clone() => new()
    {
        Id = Id,
        Name = Name,
        Kind = Kind,
        Patch = Patch.Clone(),
        MonoSynth = MonoSynth?.Clone(),
        Kit = Kit?.Clone(),
        Zones = Zones?.Clone(),
        Sampler = Sampler?.Clone(),
        FilePath = FilePath,
        BaseNoteSemitone = BaseNoteSemitone,
        Volume = Volume,
        Loop = Loop,
        OneVoice = OneVoice,
        Shape = Shape?.Clone(),
        PluginPath = PluginPath,
        PluginId = PluginId,
        PluginFormat = PluginFormat,
        PluginName = PluginName,
        PluginState = PluginState
    };
}
