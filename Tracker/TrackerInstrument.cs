using JingleBox2.Tracker.Synth;
using System;
using System.Text.Json.Serialization;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.SoundDevices.SoundMachines.Records;

namespace JingleBox2.Tracker;

/// <summary>
/// A playable voice: either a recording played back at a pitch, or a synth built from a patch.
/// </summary>
/// <remarks>
/// A song owns its instruments, which are its own copies rather than the rack's, and it holds
/// them in a list that cells point into **by index**: that is why taking one out renumbers every
/// cell in the song. The id below is the instrument's own and travels with it, but it is not what
/// a pattern writes down.
/// </remarks>
public sealed class TrackerInstrument
{
    /// <summary>
    /// Stable across renames, which is what lets a song find its instrument again after you
    /// have called it something else.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>What you called it, which is yours and is not the machine's name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Which machine it is on, and therefore which of the settings below it plays from.</summary>
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
    /// Whether a recording played by hand stops when its key comes up, rather than playing on to
    /// its end.
    /// </summary>
    /// <remarks>
    /// Off is a one-shot, which is what a recording that does not loop has always been: a hit
    /// sounds right through however briefly the key was down, and a click on a drawn key is not a
    /// tick. On is a gate, which is what a long take played from a keyboard wants: the key comes
    /// up and the recording goes into its release. A pattern's OFF cuts a note either way, so
    /// this only decides what a hand letting go does. A looping window already follows the key.
    /// </remarks>
    public bool Gate { get; set; }

    /// <summary>
    /// What happens to the note a track is still sounding when the next one lands on it.
    /// </summary>
    /// <remarks>
    /// A fact about the sound and not about the track, which is why it is here: a piano part
    /// wants the note before it to go on decaying and a bass line wants it gone, and both are
    /// true wherever either is played. Cut is the default and is what a tracker has always
    /// done, so nothing anybody has already made sounds any different for this existing.
    ///
    /// Not read on a kit, whose answer to the same question is its choke groups: a crash has to
    /// ring under the snare that follows it, and only a pad in the same group stops another.
    /// </remarks>
    public VoiceEnding NewNoteAction { get; set; } = VoiceEnding.Cut;

    /// <summary>
    /// How far the pitch wheel bends this instrument, in semitones either way.
    /// </summary>
    /// <remarks>
    /// A fact about the sound and not about the hand, which is why it is here beside
    /// <see cref="NewNoteAction"/> and travels with a preset: a lead wants a whole tone and a
    /// guitar part wants a fourth, and both are true wherever either is played.
    ///
    /// Two is what MIDI has meant by a wheel at full since it was written, so an instrument
    /// saved before this existed reads back as two and bends exactly as any other keyboard
    /// would make it. Nought is a perfectly good answer for a drum kit, whose pitch is the one
    /// thing nobody wants a wheel anywhere near.
    ///
    /// Not asked of a plugin. A plugin is sent the wheel as it arrived and owns its own range,
    /// which is a setting on its own face and not something a host may quietly double.
    /// </remarks>
    public double BendSemitones { get; set; } = DefaultBendSemitones;

    /// <summary>What a pitch wheel bends by where nobody has said otherwise, which is MIDI's own.</summary>
    public const double DefaultBendSemitones = 2;

    /// <summary>
    /// Which of the machine's controls the modulation wheel turns on this instrument, or nothing
    /// for whichever the machine itself names.
    /// </summary>
    /// <remarks>
    /// **The machine's declaration is the default and this is the exception**, which is the same
    /// arrangement <see cref="BendSemitones"/> has with MIDI's own two: a machine says what its
    /// wheel is for, in its own manifest, so it arrives working on somebody else's computer, and
    /// an instrument may want it somewhere else for this part. Empty means the machine's, so
    /// every instrument already on anybody's disc reads back as what it was.
    ///
    /// A fact about the sound, so it travels with a preset and with the song beside
    /// <see cref="NewNoteAction"/>, and it is a key on the machine rather than a number: a
    /// machine edited to have another control in the middle of its face still means the same
    /// thing by the key it was given.
    ///
    /// **A link somebody made on the wheel still beats it**, exactly as one beats the machine's
    /// own declaration. What may not happen is one gesture driving two controls, and which of
    /// the three layers answers is decided in one place.
    ///
    /// A key the machine has not got reaches nothing, in the one place every machine parameter
    /// is looked up, so a preset carried to a machine that has since been edited is silent about
    /// its wheel rather than wrong about it.
    /// </remarks>
    public string WheelKey { get; set; } = "";

    /// <summary>
    /// Which part of the recording plays, and how it repeats. Null on an instrument written
    /// before samples had a shape at all, which is the one reliable sign that its envelope
    /// was never heard: see <see cref="EnsureShape"/>.
    /// </summary>
    public SampleShape? Shape { get; set; }

    /// <summary>The plugin this instrument is, when it is one. Found again by id first.</summary>
    public string PluginPath { get; set; } = "";

    /// <summary>
    /// The plugin's own identifier, which is what it is found by first: a plugin moved to
    /// another folder is the same plugin, and the path is only the fallback.
    /// </summary>
    public string PluginId { get; set; } = "";

    /// <summary>VST3 or CLAP, since the host loads the two differently.</summary>
    public Audio.Plugins.Enums.PluginFormat PluginFormat { get; set; } = Audio.Plugins.Enums.PluginFormat.Clap;

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
    [JsonConverter(typeof(Audio.Plugins.PluginStateJson))]
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
    /// What Operetta plays from: four operators and how they are wired. Null on every other
    /// machine, and left out of the file there.
    /// </summary>
    /// <remarks>
    /// Its own field for the reason Ouroboros has one, and written under the machine's name for
    /// the same reason: that is what a file calls it.
    /// </remarks>
    [JsonPropertyName("Operetta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Synth.FmPatch? Fm { get; set; }

    /// <summary>
    /// What Lighttower plays from: the two drawn waves and how a note goes through the ones
    /// between them. Null on every other machine, and left out of the file there.
    /// </summary>
    /// <remarks>
    /// Its own field for the reason Operetta has one, and written under the machine's name for the
    /// same reason: that is what a file calls it.
    /// </remarks>
    [JsonPropertyName("Lighttower")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Synth.SegmentPatch? Segments { get; set; }

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

    /// <summary>On Zampler: recordings across the keyboard, each transposed from its own root.</summary>
    [JsonIgnore]
    public bool IsSampler => Kind == TrackerInstrumentKind.Sampler;

    /// <summary>On BongaBong: one recording to a key, none of them transposed.</summary>
    [JsonIgnore]
    public bool IsKit => Kind == TrackerInstrumentKind.Kit;

    /// <summary>On Ouroboros: one oscillator, a filter that sweeps, and glide between notes.</summary>
    [JsonIgnore]
    public bool IsMonoSynth => Kind == TrackerInstrumentKind.MonoSynth;

    /// <summary>On Operetta: four sine operators bending each other's frequency.</summary>
    [JsonIgnore]
    public bool IsFm => Kind == TrackerInstrumentKind.Fm;

    /// <summary>On Lighttower: two drawn waves and the ones between them, played in turn.</summary>
    [JsonIgnore]
    public bool IsSegments => Kind == TrackerInstrumentKind.Segments;

    /// <summary>
    /// The id of the machine this instrument came off, or nothing where it was never said.
    /// </summary>
    /// <remarks>
    /// The engine says what plays the instrument and this says whose face it wears, and the two
    /// are only the same answer while one machine is on each engine. Two kits are two machines
    /// on one engine, so an instrument that knew only its engine would open whichever of the two
    /// happened to register first.
    ///
    /// Nothing is what every instrument saved before this says, and it reads back as the machine
    /// its engine always meant; see <see cref="Machine"/>. Left out of the file while empty.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MachineId { get; set; }

    /// <summary>
    /// The preset last picked for this instrument's sound, by the name its picker shows, or
    /// nothing.
    /// </summary>
    /// <remarks>
    /// Written into the song with the rest of the instrument and handed back to the picker when
    /// the song is opened, so what was picked is what shows. By the name rather than the file,
    /// since a song carried to another computer finds its presets somewhere else. Left out of the
    /// file while there is none.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Preset { get; set; }

    /// <summary>Which machine this instrument is on, by name and description.</summary>
    /// <remarks>
    /// The machine it says it came off, where that is registered on its engine; then a machine
    /// whose own slot this is, which is what an instrument on the rack is; then the machine its
    /// engine has always meant.
    /// </remarks>
    [JsonIgnore]
    public SoundMachine Machine => SoundMachine.For(Kind, MachineId ?? Id);

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
            if (IsPlugin) return PluginName is { Length: > 0 } named ? named : "Plugin";

            string machine = Machine.Name;

            return Kind switch
            {
                TrackerInstrumentKind.Synth => machine + ", " + Patch.Wave.ToString().ToLowerInvariant(),
                TrackerInstrumentKind.MonoSynth => machine + ", " + (MonoSynth?.Wave.ToString().ToLowerInvariant() ?? "saw"),
                TrackerInstrumentKind.Fm => machine + ", algorithm " + (Fm?.Algorithm ?? 1),
                TrackerInstrumentKind.Segments => machine + ", " + (Segments?.Motion ?? Synth.Enums.SegmentMotion.Once).ToString().ToLowerInvariant(),
                _ => machine + ", " + BaseNote
            };
        }
    }

    /// <summary>On OddSkilla: generated from a patch, so it needs no recording at all.</summary>
    [JsonIgnore]
    public bool IsSynth => Kind == TrackerInstrumentKind.Synth;

    /// <summary>Somebody else's instrument, playing in a process of its own.</summary>
    [JsonIgnore]
    public bool IsPlugin => Kind == TrackerInstrumentKind.Plugin;

    /// <summary>What the plugin is, in the shape the host loads from.</summary>
    [JsonIgnore]
    public Audio.Plugins.Records.PluginInfo? Plugin =>
        string.IsNullOrWhiteSpace(PluginPath)
            ? null
            : new Audio.Plugins.Records.PluginInfo(PluginId, PluginName, "", "", PluginPath, PluginFormat, IsInstrument: true);

    /// <summary>An instrument that is a plugin, at whatever the plugin opens with.</summary>
    /// <param name="name">What to call it.</param>
    /// <param name="plugin">Which plugin.</param>
    public static TrackerInstrument CreatePlugin(string name, Audio.Plugins.Records.PluginInfo plugin)
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

    /// <summary>
    /// The same pitch as <see cref="BaseNoteSemitone"/>, as a note rather than a number, for
    /// anything printing it or comparing it with a cell.
    /// </summary>
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

    /// <summary>A new instrument on Operetta, with a plain two operator sound ready to shape.</summary>
    public static TrackerInstrument CreateFm(string name)
    {
        var instrument = new TrackerInstrument
        {
            Name = name,
            Kind = TrackerInstrumentKind.Fm,
            Fm = new Synth.FmPatch()
        };

        instrument.EnsureId();
        return instrument;
    }

    /// <summary>A new instrument on Lighttower, going from a sine to a saw.</summary>
    public static TrackerInstrument CreateSegments(string name)
    {
        var instrument = new TrackerInstrument
        {
            Name = name,
            Kind = TrackerInstrumentKind.Segments,
            Segments = new Synth.SegmentPatch()
        };

        instrument.EnsureId();
        return instrument;
    }

    /// <summary>A new instrument on whichever machine was asked for.</summary>
    /// <remarks>
    /// The Recording machine has to be named here even though what it makes is an instrument
    /// with no recording on it yet, which is exactly what that machine is until you put a take
    /// on it. Left out, it fell through to the last arm and came back an OddSkilla wearing the
    /// name you had just typed.
    /// </remarks>
    public static TrackerInstrument CreateOn(SoundMachine machine, string name)
    {
        var made = machine?.Kind switch
        {
            TrackerInstrumentKind.MonoSynth => CreateMonoSynth(name),
            TrackerInstrumentKind.Fm => CreateFm(name),
            TrackerInstrumentKind.Segments => CreateSegments(name),
            TrackerInstrumentKind.Kit => CreateKit(name),
            TrackerInstrumentKind.Sampler => CreateSampler(name),
            TrackerInstrumentKind.Sample => CreateSample(name, "", new Note(48)),

            _ => CreateSynth(name)
        };

        if (machine is { IsOurs: true, Id.Length: > 0 }) made.MachineId = machine.Id;

        return made;
    }

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
    /// <remarks>
    /// A missing shape is the one reliable sign that an instrument predates the change, and it
    /// is why the envelope is flattened here. A sample used to be handed to the audio library
    /// whole, so whatever its patch said was never heard; playing it through the voice now
    /// would put a decay on every recording that has one, and a sound that quietly changed
    /// under somebody is worse than one that never had the setting.
    /// </remarks>
    public void EnsureShape()
    {
        if (Shape == null)
        {
            Shape = new SampleShape();

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
    /// <remarks>
    /// Everything a plugin instrument is comes across too, and that is not incidental: left
    /// out, an instrument copied into a song is one that says it is a plugin and cannot say
    /// which, so it plays nothing and opens nothing.
    /// </remarks>
    public void CopyFrom(TrackerInstrument other)
    {
        if (other is null || ReferenceEquals(other, this)) return;

        Id = other.Id;
        Name = other.Name;
        Kind = other.Kind;
        MachineId = other.MachineId;
        Preset = other.Preset;
        Patch = other.Patch.Clone();
        MonoSynth = other.MonoSynth?.Clone();
        Fm = other.Fm?.Clone();
        Segments = other.Segments?.Clone();
        Kit = other.Kit?.Clone();
        Zones = other.Zones?.Clone();
        Sampler = other.Sampler?.Clone();
        FilePath = other.FilePath;
        BaseNoteSemitone = other.BaseNoteSemitone;
        Volume = other.Volume;
        Loop = other.Loop;
        OneVoice = other.OneVoice;
        Gate = other.Gate;
        NewNoteAction = other.NewNoteAction;
        BendSemitones = other.BendSemitones;
        WheelKey = other.WheelKey;
        Shape = other.Shape?.Clone();

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
    ///
    /// A plugin's patch moves only between two instruments on the same plugin. Another
    /// plugin's state is not a preset for this one, it is a file it cannot read.
    ///
    /// <see cref="NewNoteAction"/>, <see cref="BendSemitones"/> and <see cref="WheelKey"/>
    /// travel with the sound rather than with the machine, since all three are part of what the
    /// sound does: a preset for a pad that overlaps is not that preset with the overlap taken
    /// off it, a lead that bends a fourth is not that lead bending a tone, and a patch whose
    /// wheel opens the filter is not that patch with the wheel back on the vibrato.
    /// </remarks>
    public void TakeSoundFrom(TrackerInstrument other)
    {
        if (other is null || ReferenceEquals(other, this) || other.Kind != Kind) return;

        NewNoteAction = other.NewNoteAction;
        BendSemitones = other.BendSemitones;
        WheelKey = other.WheelKey;

        switch (Kind)
        {
            case TrackerInstrumentKind.Synth:
                Patch.CopyFrom(other.Patch);
                break;

            case TrackerInstrumentKind.MonoSynth:
                MonoSynth ??= new Synth.MonoSynthPatch();
                MonoSynth.CopyFrom(other.MonoSynth ?? new Synth.MonoSynthPatch());
                break;

            case TrackerInstrumentKind.Fm:
                Fm ??= new Synth.FmPatch();
                Fm.CopyFrom(other.Fm ?? new Synth.FmPatch());
                break;

            case TrackerInstrumentKind.Segments:
                Segments ??= new Synth.SegmentPatch();
                Segments.CopyFrom(other.Segments ?? new Synth.SegmentPatch());
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
                Gate = other.Gate;
                Shape = other.Shape?.Clone();
                Patch.CopyFrom(other.Patch);
                break;

            case TrackerInstrumentKind.Plugin:
                if (other.PluginId == PluginId && other.PluginPath == PluginPath)
                    PluginState = other.PluginState;

                break;
        }
    }

    /// <summary>
    /// An instrument nothing else is holding, for a history step or for taking one into a song.
    /// </summary>
    /// <remarks>
    /// The plugin's patch is passed by reference rather than copied, which is safe because
    /// nothing ever writes into one: anything changing a patch replaces the array. A third of a
    /// megabyte of wavetables copied per clone would be paid on every undo step.
    /// </remarks>
    public TrackerInstrument Clone() => new()
    {
        Id = Id,
        Name = Name,
        Kind = Kind,
        MachineId = MachineId,
        Preset = Preset,
        Patch = Patch.Clone(),
        MonoSynth = MonoSynth?.Clone(),
        Fm = Fm?.Clone(),
        Segments = Segments?.Clone(),
        Kit = Kit?.Clone(),
        Zones = Zones?.Clone(),
        Sampler = Sampler?.Clone(),
        FilePath = FilePath,
        BaseNoteSemitone = BaseNoteSemitone,
        Volume = Volume,
        Loop = Loop,
        OneVoice = OneVoice,
        Gate = Gate,
        NewNoteAction = NewNoteAction,
        BendSemitones = BendSemitones,
        WheelKey = WheelKey,
        Shape = Shape?.Clone(),
        PluginPath = PluginPath,
        PluginId = PluginId,
        PluginFormat = PluginFormat,
        PluginName = PluginName,
        PluginState = PluginState
    };
}
