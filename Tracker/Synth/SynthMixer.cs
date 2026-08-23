using JingleBox2.Audio.Plugins;
using System;
using System.Collections.Generic;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// Every sounding synth voice, summed into one buffer. One voice per track, the tracker way:
/// a new note cuts the one still ringing there. Auditions sit outside that and simply pile up.
/// </summary>
/// <remarks>
/// Rendering happens on the audio callback thread while notes are started from the clock and
/// from the UI, so the voice list is behind a lock. The critical sections are a few list
/// operations long; the sample loops themselves run on a snapshot.
/// </remarks>
public sealed class SynthMixer
{
    /// <summary>Past this, the oldest voice is taken rather than growing the mix forever.</summary>
    public const int MaxVoices = 48;

    /// <summary>
    /// The level a single voice comes out at. High enough to sit next to a sample played at
    /// its own level; several voices at once are held in by the saturation below rather than
    /// by leaving headroom nobody ever uses.
    /// </summary>
    public const float MasterGain = 0.9f;

    /// <summary>As many tracks as a song can have, so a strip always has a bus of its own.</summary>
    private const int MaxTracks = Song.MaxTrackCount;

    private readonly List<IVoice> _voices = new();
    private readonly object _lock = new();

    /// <summary>One buffer per track, so a track can be measured and moved on its own.</summary>
    private readonly float[]?[] _busses = new float[MaxTracks][];

    /// <summary>Auditions and anything else with no track of its own.</summary>
    private float[] _loose = Array.Empty<float>();

    private readonly bool[] _sounding = new bool[MaxTracks];
    private readonly DuckSetting[] _ducking = new DuckSetting[MaxTracks];
    private readonly Ducker?[] _duckers = new Ducker[MaxTracks];
    private readonly float[] _duckGain = new float[MaxTracks];
    private readonly float[] _trackLevels = new float[MaxTracks];

    /// <summary>What each track's audio passes through before the mix, if anything.</summary>
    private readonly IAudioInsert?[] _inserts = new IAudioInsert[MaxTracks];

    /// <summary>
    /// A plugin playing a track, when that track's instrument is one.
    /// </summary>
    /// <remarks>
    /// Not a voice. A plugin is polyphonic inside itself and holds its own notes, so it fills
    /// a track's bus rather than adding one note to it, and it stays on the track between
    /// notes because it has a release to finish.
    /// </remarks>
    private readonly IPluginInstrument?[] _instruments = new IPluginInstrument[MaxTracks];

    /// <summary>The volume and pan columns, applied to a plugin's bus after it has played.</summary>
    private readonly float[] _instrumentGain = new float[MaxTracks];
    private readonly float[] _instrumentPan = new float[MaxTracks];

    /// <summary>How many tracks have a plugin on them, so the quiet path can stay quick.</summary>
    private int _instrumentCount;

    /// <summary>
    /// A plugin being auditioned, which belongs to no track. Rendered into the loose bus with
    /// the other auditions rather than over one of them.
    /// </summary>
    private IPluginInstrument? _preview;

    private float[] _previewScratch = Array.Empty<float>();
    private float _previewGain = 1f;

    /// <summary>When the audition lets go of its note. Zero while nothing is being auditioned.</summary>
    private long _previewUntil;

    private int _bufferFrames;

    /// <summary>What one strip's side chain is set to.</summary>
    private readonly record struct DuckSetting(double Depth, int Key, double ReleaseMs);

    private IVoice[] _snapshot = Array.Empty<IVoice>();
    private int _voiceCount;
    private bool _snapshotStale = true;

    /// <summary>
    /// What the block being rendered is working from: the voices, the plugins and the ducking
    /// as they stood when the lock was taken.
    /// </summary>
    /// <remarks>
    /// Filled rather than made afresh. Copying the two arrays out was two allocations per
    /// block on the audio thread, forty thousand a second between them, all of it garbage
    /// somebody has to collect while the next block is waiting. These are written only under
    /// the lock and read only by the thread that wrote them.
    /// </remarks>
    private readonly IPluginInstrument?[] _live = new IPluginInstrument[MaxTracks];

    private readonly DuckSetting[] _ducked = new DuckSetting[MaxTracks];
    private int _noiseSeed;

    public SynthMixer(int sampleRate)
    {
        SampleRate = sampleRate;

        for (int track = 0; track < MaxTracks; track++)
        {
            _ducking[track] = new DuckSetting(0, TrackMix.NoKey, TrackMix.DefaultDuckReleaseMs);
            _duckGain[track] = 1f;
            _instrumentGain[track] = 1f;
        }
    }

    /// <summary>
    /// Points one strip's side chain at another track. Depth of zero, or no key, is a strip
    /// that plays at its own level.
    /// </summary>
    /// <summary>
    /// How far through its recording the newest sounding sample voice is, or -1 for none.
    /// </summary>
    /// <remarks>
    /// A track's own voice and a voice auditioned by hand both answer, because a panel showing
    /// a cursor wants the piece that is playing and does not care which of the two started it.
    /// Newest first: playing a second key while the first still rings should move the cursor to
    /// what was just asked for.
    /// </remarks>
    public double SamplePosition(int track)
    {
        lock (_lock)
        {
            for (int i = _voices.Count - 1; i >= 0; i--)
            {
                if (_voices[i] is not SampleVoice voice) continue;
                if (voice.Track != track && voice.Track != SynthVoice.NoTrack) continue;

                double at = voice.Progress;

                if (at >= 0) return at;
            }
        }

        return -1;
    }

    public void SetDucking(int track, double depth, int key, double releaseMs)
    {
        if (track < 0 || track >= MaxTracks) return;

        lock (_lock) _ducking[track] = new DuckSetting(Math.Clamp(depth, 0, 1), key, releaseMs);
    }

    /// <summary>
    /// Puts an effect in a track's path, or takes one out with null. The track is rendered on
    /// its own bus, so what the effect sees is that track and nothing else.
    /// </summary>
    public void SetInsert(int track, IAudioInsert? insert)
    {
        if (track < 0 || track >= MaxTracks) return;

        lock (_lock)
        {
            if (_inserts[track] != null) _insertCount--;

            _inserts[track] = insert;

            if (insert != null) _insertCount++;
        }
    }

    /// <summary>
    /// How many tracks have something inserted on them, so the mixer knows it cannot rest.
    /// </summary>
    /// <remarks>
    /// An effect has to be given its audio whether or not anything is going through it. A
    /// delay has a tail to finish after the last note, and a plugin only ever hands the host
    /// what its own window did at the end of a block it was given, so a mixer that rests is a
    /// plugin that has been switched off without being told.
    /// </remarks>
    private int _insertCount;

    /// <summary>When the mixer last said what it was holding, so it says it once, not per block.</summary>
    private long _said;

    /// <summary>
    /// What one track did over the last second, kept so the log can say it once rather than
    /// once a block.
    /// </summary>
    /// <remarks>
    /// The audio callback runs some eighty times a second and a line of the log is a file
    /// opened, written and closed. Writing from inside a block is therefore the audio thread
    /// waiting on a disk, which is a fault of its own and one that hides the fault being looked
    /// for. Nothing here allocates or blocks: it is a handful of comparisons per block, and the
    /// line is built once a second by whichever block happens to be the one that crosses it.
    /// </remarks>
    private struct TrackCensus
    {
        public int Blocks;
        public float PlayedPeak;
        public float BeforeInsert;
        public float AfterInsert;
        public int SilentBlocks;
        public string? Fault;
        public int Faults;
        public string? Instrument;
        public string? Insert;

        public void Played(float peak, IPluginInstrument instrument)
        {
            Blocks++;
            if (peak > PlayedPeak) PlayedPeak = peak;
            if (peak <= Quiet) SilentBlocks++;
            Instrument ??= instrument.GetType().Name;
        }

        public void Inserted(float before, float after, IAudioInsert insert)
        {
            if (before > BeforeInsert) BeforeInsert = before;
            if (after > AfterInsert) AfterInsert = after;
            Insert ??= insert.GetType().Name;
        }

        public void Note(string fault)
        {
            Faults++;
            Fault = fault;
        }

        public bool Worth => Blocks > 0 || Insert != null || Faults > 0;

        public void Clear()
        {
            Blocks = 0;
            PlayedPeak = 0;
            BeforeInsert = 0;
            AfterInsert = 0;
            SilentBlocks = 0;
            Fault = null;
            Faults = 0;
            Instrument = null;
            Insert = null;
        }
    }

    /// <summary>Anything at or below this is silence as far as a meter is concerned.</summary>
    private const float Quiet = 0.0001f;

    private readonly TrackCensus[] _census = new TrackCensus[MaxTracks];

    /// <summary>The loudest sample in a block, which is what every meter here is built on.</summary>
    private static float Peak(float[] buffer, int samples)
    {
        float peak = 0;

        int count = Math.Min(samples, buffer.Length);
        for (int index = 0; index < count; index++)
        {
            float magnitude = Math.Abs(buffer[index]);
            if (magnitude > peak) peak = magnitude;
        }

        return peak;
    }

    public IAudioInsert? InsertOn(int track) =>
        track >= 0 && track < MaxTracks ? _inserts[track] : null;

    /// <summary>Gets the current audio level (0-1) for a track, for UI display.</summary>
    public float GetTrackLevel(int track) =>
        track >= 0 && track < MaxTracks ? _trackLevels[track] : 0f;

    /// <summary>
    /// Puts a plugin on a track, or takes one off with null. Whatever was there is told to
    /// stop first, or it carries on playing into a bus nobody renders.
    /// </summary>
    public void SetInstrument(int track, IPluginInstrument? instrument)
    {
        if (track < 0 || track >= MaxTracks) return;

        IPluginInstrument? leaving;

        lock (_lock)
        {
            leaving = _instruments[track];
            if (ReferenceEquals(leaving, instrument)) return;

            _instruments[track] = instrument;

            int count = 0;
            for (int index = 0; index < MaxTracks; index++)
            {
                if (_instruments[index] != null) count++;
            }

            _instrumentCount = count;
        }

        leaving?.AllNotesOff();
    }

    /// <summary>
    /// Moves everything a track is holding to another position: its plugin, its effects, its
    /// side chain and the columns riding its bus.
    /// </summary>
    /// <remarks>
    /// The song is reordered by the view; this is the live half of the same move. Without it
    /// the notes would arrive at their new track and the plugin would still be answering on
    /// the old one, so every track would play somebody else's sound.
    ///
    /// Voices are cut rather than carried across. A voice remembers the track it was started
    /// on, and there is no sound reason to hear a note go on playing on a track that is no
    /// longer where it was. A cut is a short fade, so this costs a note ending rather than a
    /// click.
    /// </remarks>
    public void MoveTrack(int from, int to)
    {
        if (from == to) return;
        if (from < 0 || from >= MaxTracks || to < 0 || to >= MaxTracks) return;

        lock (_lock)
        {
            Shift(_instruments, from, to);
            Shift(_inserts, from, to);
            Shift(_instrumentGain, from, to);
            Shift(_instrumentPan, from, to);
            Shift(_heldUntil, from, to);
            Shift(_ducking, from, to);
            Shift(_trackLevels, from, to);

            // A side chain names the track that keys it, and those numbers have just moved.
            for (int track = 0; track < MaxTracks; track++)
            {
                var setting = _ducking[track];
                if (setting.Key < 0) continue;

                _ducking[track] = setting with { Key = Song.WhereTrackWent(setting.Key, from, to) };
            }

            // The duckers hold a level worked out from the old arrangement, so they start again.
            for (int track = 0; track < MaxTracks; track++)
            {
                _duckGain[track] = 1f;
                _duckers[track]?.Reset();
            }

            // Cut rather than released: a short fade, so a reorder mid-play does not click.
            foreach (var voice in _voices) voice.Cut();

            _snapshotStale = true;
        }
    }

    /// <summary>One track's worth of per-track state, moved the way the song moves it.</summary>
    private static void Shift<T>(T[] values, int from, int to)
    {
        var moved = values[from];

        int step = from < to ? 1 : -1;
        for (int track = from; track != to; track += step) values[track] = values[track + step];

        values[to] = moved;
    }

    /// <summary>Puts a plugin in the audition slot, or takes one out with null.</summary>
    public void SetPreviewInstrument(IPluginInstrument? instrument)
    {
        IPluginInstrument? leaving;

        lock (_lock)
        {
            leaving = _preview;
            if (ReferenceEquals(leaving, instrument)) return;

            _preview = instrument;
            _previewUntil = 0;
        }

        leaving?.AllNotesOff();
    }

    public IPluginInstrument? PreviewInstrument
    {
        get { lock (_lock) return _preview; }
    }

    /// <summary>
    /// Plays a note on the audition plugin, letting go of it after a while. There is no key to
    /// release when a note is played by clicking on it, so it releases itself.
    /// </summary>
    public void PreviewPlugin(Note note, float gain, double holdSeconds)
    {
        if (!note.IsPlayable) return;

        IPluginInstrument? instrument;

        lock (_lock)
        {
            instrument = _preview;
            _previewGain = gain;
            _previewUntil = Environment.TickCount64 + (long)(Math.Max(0.05, holdSeconds) * 1000);
        }

        if (instrument == null) return;

        instrument.AllNotesOff();
        instrument.NoteOn(note.Semitone, 1f);
    }

    /// <summary>When a track's plugin lets go of a note played by hand. Zero when there is none.</summary>
    private readonly long[] _heldUntil = new long[MaxTracks];

    /// <summary>Reused, because this runs on the audio thread and must not make work for the collector.</summary>
    private readonly List<IPluginInstrument> _letting = new(MaxTracks);

    /// <summary>
    /// Plays a note by hand on the plugin a track is already playing, letting go of it after a
    /// while.
    /// </summary>
    /// <remarks>
    /// The track's own copy rather than the audition one, deliberately. It is the copy whose
    /// window is open and whose knobs have just been turned; a second copy would be a second
    /// sound, playing whatever the song was last saved with.
    /// </remarks>
    public void PreviewOnTrack(int track, Note note, float gain, double holdSeconds)
    {
        if (track < 0 || track >= MaxTracks || !note.IsPlayable) return;

        IPluginInstrument? instrument;

        lock (_lock)
        {
            instrument = _instruments[track];

            _instrumentGain[track] = gain;
            _heldUntil[track] = Environment.TickCount64 + (long)(Math.Max(0.05, holdSeconds) * 1000);
        }

        if (instrument == null) return;

        instrument.AllNotesOff();
        instrument.NoteOn(note.Semitone, 1f);
    }

    public IPluginInstrument? InstrumentOn(int track) =>
        track >= 0 && track < MaxTracks ? _instruments[track] : null;

    /// <summary>Starts a note on a track's plugin. The volume column rides its bus after.</summary>
    public void PluginNoteOn(int track, Note note, float gain, float pan)
    {
        if (track < 0 || track >= MaxTracks || !note.IsPlayable) return;

        IPluginInstrument? instrument;
        lock (_lock)
        {
            instrument = _instruments[track];
            _instrumentGain[track] = gain;
            _instrumentPan[track] = Math.Clamp(pan, -1f, 1f);
        }

        if (instrument == null) return;

        // One note a track, as a tracker has always worked. The note that was there is let go
        // rather than cut off, so a plugin plays its own release instead of clicking.
        instrument.AllNotesOff();
        instrument.NoteOn(note.Semitone, 1f);
    }

    /// <summary>Lets go of whatever a track's plugin is holding.</summary>
    public void PluginNoteOff(int track)
    {
        if (track < 0 || track >= MaxTracks) return;

        IPluginInstrument? instrument;
        lock (_lock) instrument = _instruments[track];

        instrument?.AllNotesOff();
    }

    /// <summary>Follows the volume and pan columns while a plugin note holds.</summary>
    public void SetPluginLevels(int track, float gain, float? pan)
    {
        if (track < 0 || track >= MaxTracks) return;

        lock (_lock)
        {
            _instrumentGain[track] = gain;
            if (pan.HasValue) _instrumentPan[track] = Math.Clamp(pan.Value, -1f, 1f);
        }
    }

    /// <summary>How far a track is being pushed down right now, 1 being not at all.</summary>
    public float DuckGainFor(int track) =>
        track >= 0 && track < MaxTracks ? _duckGain[track] : 1f;

    public int SampleRate { get; }

    public int VoiceCount
    {
        get { lock (_lock) return _voices.Count; }
    }

    public void NoteOn(int track, SynthPatch patch, Note note, float gain, float pan)
    {
        if (patch is null || !note.IsPlayable) return;

        var voice = new SynthVoice(patch, note, track, gain, pan, SampleRate, NextSeed());

        lock (_lock)
        {
            Cut(track);
            Add(voice);
        }
    }

    /// <summary>
    /// Starts a note on Ouroboros, sliding from whatever the track was sounding.
    /// </summary>
    /// <remarks>
    /// The note before is what glide glides from, and this is the only place that knows what
    /// it was. Read before the old voice is cut, because cutting it is what makes it stop
    /// being the note before.
    /// </remarks>
    public void NoteOn(int track, OuroborosPatch patch, Note note, float gain, float pan)
    {
        if (patch is null || !note.IsPlayable) return;

        lock (_lock)
        {
            double? from = null;

            if (track >= 0)
            {
                foreach (var playing in _voices)
                {
                    if (playing.Track == track && !playing.IsFinished && playing is OuroborosVoice last)
                        from = last.Hz;
                }
            }

            Cut(track);

            Add(new OuroborosVoice(patch, note, track, gain, pan, SampleRate, NextSeed(), from));
        }
    }

    /// <summary>Lets go of whatever a track was sounding. Held under the lock by its callers.</summary>
    private void Cut(int track)
    {
        if (track < 0) return;

        foreach (var playing in _voices)
        {
            if (playing.Track == track) playing.Cut();
        }
    }

    /// <summary>Sounds a note that releases on its own, for auditioning while editing.</summary>
    public void Preview(SynthPatch patch, Note note, float gain, double holdSeconds, string audition)
    {
        if (patch is null || !note.IsPlayable) return;

        var voice = new SynthVoice(patch, note, SynthVoice.NoTrack, gain, 0f, SampleRate, NextSeed())
        {
            Audition = audition
        };

        voice.HoldFor(holdSeconds);

        lock (_lock) Add(voice);
    }

    /// <summary>The same, on Ouroboros, for a note played while building the sound.</summary>
    /// <remarks>
    /// No glide: an audition has no note before it to slide from. It belongs to no track
    /// either, so it piles up with the other auditions rather than cutting one.
    /// </remarks>
    public void Preview(OuroborosPatch patch, Note note, float gain, double holdSeconds, string audition)
    {
        if (patch is null || !note.IsPlayable) return;

        var voice = new OuroborosVoice(
            patch, note, OuroborosVoice.NoTrack, gain, 0f, SampleRate, NextSeed(), null)
        {
            Audition = audition
        };

        voice.HoldFor(holdSeconds);

        lock (_lock) Add(voice);
    }

    /// <summary>
    /// Sounds a recording on a track, under the same rules: the track's last note is cut, and
    /// the voice takes its place. The caller brings the audio, so the mixer never reads a file.
    /// </summary>
    public void NoteOn(int track, TrackerInstrument instrument, SampleData sample, Note note, float gain, float pan)
    {
        if (instrument is null || sample is null || sample.IsEmpty || !note.IsPlayable) return;

        var voice = new SampleVoice(
            sample, instrument.Patch, instrument.Shape, note, instrument.BaseNote,
            track, gain, pan, SampleRate);

        lock (_lock)
        {
            if (track >= 0)
            {
                foreach (var playing in _voices)
                {
                    if (playing.Track == track) playing.Cut();
                }
            }

            Add(voice);
        }
    }

    /// <summary>
    /// Fires one pad of a kit: its own recording, at its own pitch, over whatever else is
    /// already sounding on the track.
    /// </summary>
    /// <remarks>
    /// The one place in this engine where a track's last note is not cut. Everywhere else one
    /// voice to a track is the rule and glide, legato and the tracker's own habits are built on
    /// it; a kit is the exception, because a crash has to go on ringing under the snare that
    /// follows it. The only thing that stops a pad is another pad in its choke group.
    ///
    /// The pad's own note is passed as the base note as well, so the ratio comes out at one and
    /// nothing is resampled. That is the machine: a key chooses which recording sounds, not how
    /// fast to read one.
    /// </remarks>
    public void NoteOn(int track, DrumPad pad, SynthPatch patch, SampleData sample, Note note, float gain, float pan)
    {
        if (pad is null || patch is null || sample is null || sample.IsEmpty || !note.IsPlayable) return;

        var voice = new SampleVoice(
            sample, patch, pad.Shape, note, note,
            track, gain, pan, SampleRate)
        {
            Choke = pad.Choke
        };

        lock (_lock)
        {
            if (track >= 0 && pad.Choke > 0)
            {
                foreach (var playing in _voices)
                {
                    if (playing.Track == track && playing is SampleVoice other && other.Choke == pad.Choke)
                        playing.Cut();
                }
            }

            Add(voice);
        }
    }

    /// <summary>
    /// Plays one zone of a map: its recording, read at whatever speed the key asks for.
    /// </summary>
    /// <remarks>
    /// The kit's method with one word changed. There the played note goes in as the root, so
    /// the ratio comes out at one; here the zone's own root goes in, so the note decides how
    /// fast to read. That one word is the whole difference between BongaBong and Zampler.
    ///
    /// And unlike a kit, the track's last note is cut: this is an instrument rather than a rack
    /// of them, and one voice to a track is how the tracker has always played one.
    /// </remarks>
    public void NoteOn(int track, SampleZone zone, ZamplerPatch patch, SampleData sample, Note note, float gain, float pan)
    {
        if (zone is null || patch is null || sample is null || sample.IsEmpty || !note.IsPlayable) return;

        var voice = new SampleVoice(
            sample, new SynthPatch(), zone.Shape, note, new Note(zone.Root),
            track, gain, pan, SampleRate, patch);

        lock (_lock)
        {
            Cut(track);
            Add(voice);
        }
    }

    /// <summary>The same, for a zone played on the panel rather than by a pattern.</summary>
    /// <returns>How long the note will sound, or zero if it did not start.</returns>
    public double Preview(
        SampleZone zone, ZamplerPatch patch, SampleData sample, Note note, float gain,
        double holdSeconds, string audition)
    {
        if (zone is null || patch is null || sample is null || sample.IsEmpty || !note.IsPlayable) return 0;

        var voice = new SampleVoice(
            sample, new SynthPatch(), zone.Shape, note, new Note(zone.Root),
            SynthVoice.NoTrack, gain, 0f, SampleRate, patch)
        {
            Audition = audition
        };

        double held = Held(voice, holdSeconds);

        voice.HoldFor(held);

        lock (_lock) Add(voice);

        return held;
    }

    /// <summary>The same, for a pad tapped on the panel rather than played by a pattern.</summary>
    /// <returns>How long the note will sound, or zero if it did not start.</returns>
    public double Preview(
        DrumPad pad, SynthPatch patch, SampleData sample, Note note, float gain,
        double holdSeconds, string audition)
    {
        if (pad is null || patch is null || sample is null || sample.IsEmpty || !note.IsPlayable) return 0;

        var voice = new SampleVoice(
            sample, patch, pad.Shape, note, note,
            SynthVoice.NoTrack, gain, 0f, SampleRate)
        {
            Choke = pad.Choke,
            Audition = audition
        };

        double held = Held(voice, holdSeconds);

        voice.HoldFor(held);

        lock (_lock) Add(voice);

        return held;
    }

    /// <summary>A recording sounded once, for auditioning while editing.</summary>
    /// <returns>How long the note will sound, or zero if it did not start.</returns>
    public double Preview(
        TrackerInstrument instrument, SampleData sample, Note note, float gain,
        double holdSeconds, string audition)
    {
        if (instrument is null || sample is null || sample.IsEmpty || !note.IsPlayable) return 0;

        var voice = new SampleVoice(
            sample, instrument.Patch, instrument.Shape, note, instrument.BaseNote,
            SynthVoice.NoTrack, gain, 0f, SampleRate)
        {
            Audition = audition
        };

        double held = Held(voice, holdSeconds);

        voice.HoldFor(held);

        lock (_lock) Add(voice);

        return held;
    }

    /// <summary>
    /// How long an auditioned recording holds: long enough to be heard right through.
    /// </summary>
    /// <remarks>
    /// The fixed hold is what a generated sound needs, since it would otherwise never stop. A
    /// recording has an end of its own, and stopping short of it plays a different sound from
    /// the one the instrument makes. A looping window has no end, so it keeps the fixed hold.
    /// </remarks>
    private static double Held(SampleVoice voice, double asked) =>
        voice.WindowSeconds > 0 ? Math.Max(asked, voice.WindowSeconds) : asked;

    /// <summary>
    /// Stops what this instrument was sounding by hand, for one that plays one note at a time.
    /// </summary>
    /// <remarks>
    /// A short fade rather than a release, the same as a track retriggering itself: the next
    /// note starts now, and a full release would still be running underneath it.
    /// </remarks>
    public void CutAuditions(string audition)
    {
        if (string.IsNullOrEmpty(audition)) return;

        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                if (voice.Track == SynthVoice.NoTrack && voice.Audition == audition) voice.Cut();
            }
        }
    }

    public void NoteOff(int track)
    {
        if (track < 0) return;

        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                if (voice.Track == track) voice.NoteOff();
            }
        }
    }

    /// <summary>Follows the volume and pan columns while a note holds.</summary>
    public void SetLevels(int track, float gain, float? pan)
    {
        if (track < 0) return;

        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                if (voice.Track != track) continue;

                voice.Gain = gain;
                if (pan.HasValue) voice.Pan = pan.Value;
            }
        }
    }

    /// <summary>Silence, now. Used by the transport rather than by a note off.</summary>
    public void StopAll()
    {
        lock (_lock)
        {
            foreach (var voice in _voices) voice.Kill();

            _voices.Clear();
            _snapshotStale = true;

            Rest();
        }
    }

    /// <summary>
    /// Fills an interleaved stereo buffer with everything playing. Always writes the whole
    /// buffer: the audio callback has no way to say "nothing this time".
    /// </summary>
    public void Render(float[] buffer, int frames)
    {
        int samples = frames * 2;
        Array.Clear(buffer, 0, Math.Min(samples, buffer.Length));

        IVoice[] playing;
        int sounding;
        DuckSetting[] ducking;
        IPluginInstrument?[] instruments;
        IPluginInstrument? preview;
        IPluginInstrument? releasing = null;
        float previewGain;

        // Collected under the lock and let go of outside it: a plugin being told to stop is
        // somebody else's code, and it has no business running with our lock held.
        var letting = _letting;
        letting.Clear();

        lock (_lock)
        {
            if (Diagnostics.Log.IsOn && Environment.TickCount64 - _said > 1000)
            {
                _said = Environment.TickCount64;

                int voices = _voices.Count;
                int played = _instrumentCount;
                int inserts = _insertCount;

                Diagnostics.Log.Write(Diagnostics.LogArea.Audio, () =>
                    "the mixer has " + voices + " voices, " + played + " plugin instruments and " +
                    inserts + " tracks with something inserted");

                Census();
            }

            if (_voices.Count == 0 && _instrumentCount == 0 && _preview == null && _insertCount == 0)
            {
                Rest();
                return;
            }

            if (_snapshotStale)
            {
                // Grown when it has to be and reused when it does not, so a run of notes
                // does not leave an array behind for every one of them.
                if (_snapshot.Length < _voices.Count) _snapshot = new IVoice[Math.Max(_voices.Count, 16)];

                _voices.CopyTo(_snapshot);

                // Nothing is cleared past the count: the tail is whatever the last, longer
                // block held, and the count is what says where to stop.
                _voiceCount = _voices.Count;
                _snapshotStale = false;
            }

            playing = _snapshot;
            sounding = _voiceCount;

            Array.Copy(_instruments, _live, MaxTracks);
            instruments = _live;

            preview = _preview;
            previewGain = _previewGain;

            // An audition has no key to let go of, so it lets go of itself.
            if (_previewUntil != 0 && Environment.TickCount64 >= _previewUntil)
            {
                _previewUntil = 0;
                releasing = preview;
            }

            // And the same for a note played by hand on a track's own plugin.
            for (int track = 0; track < MaxTracks; track++)
            {
                if (_heldUntil[track] == 0 || Environment.TickCount64 < _heldUntil[track]) continue;

                _heldUntil[track] = 0;

                if (_instruments[track] != null) letting.Add(_instruments[track]!);
            }
            Array.Copy(_ducking, _ducked, MaxTracks);
            ducking = _ducked;
        }

        // Each track is rendered on its own before anything is summed. Ducking needs one
        // track to be measurable while another is being moved by it, and once everything is
        // added together there is nothing left to measure.
        releasing?.AllNotesOff();

        foreach (var held in letting) held.AllNotesOff();

        letting.Clear();

        RenderBusses(playing, sounding, instruments, preview, previewGain, frames, samples);

        // Inserts run on the bus, before the side chains: what keys a duck is the track as it
        // sounds, effects included, which is what anyone listening would call the track.
        ApplyInserts(frames);

        for (int track = 0; track < MaxTracks; track++)
            MixTrack(buffer, track, ducking[track], frames, samples);

        for (int i = 0; i < samples; i++)
            buffer[i] += _loose[i];

        for (int i = 0; i < samples; i++)
            buffer[i] = SoftClip(buffer[i] * MasterGain);

        Reap();
    }

    /// <summary>Lets go of every note on every plugin, for a stop.</summary>
    public void AllPluginNotesOff()
    {
        IPluginInstrument?[] instruments;
        IPluginInstrument? preview;

        lock (_lock)
        {
            instruments = (IPluginInstrument?[])_instruments.Clone();
            preview = _preview;
            _previewUntil = 0;
        }

        foreach (var instrument in instruments) instrument?.AllNotesOff();

        preview?.AllNotesOff();
    }

    /// <summary>Nothing is playing: the side chains fall back open rather than staying shut.</summary>
    private void Rest()
    {
        for (int track = 0; track < MaxTracks; track++)
        {
            _duckGain[track] = 1f;
            _duckers[track]?.Reset();
        }
    }

    /// <summary>Puts every voice on its own track's bus, auditions aside.</summary>
    private void RenderBusses(
        IVoice[] playing, int sounding, IPluginInstrument?[] instruments,
        IPluginInstrument? preview, float previewGain, int frames, int samples)
    {
        EnsureBusses(frames);

        Array.Clear(_sounding, 0, MaxTracks);

        for (int index = 0; index < sounding; index++)
        {
            int track = playing[index].Track;
            if (track >= 0 && track < MaxTracks) _sounding[track] = true;
        }

        // A track with a plugin on it always sounds. The plugin holds its own notes and its
        // own release, and there is no voice here to say whether it is still ringing.
        for (int track = 0; track < MaxTracks; track++)
        {
            if (instruments[track] != null) _sounding[track] = true;
        }

        // And so does a track with something inserted on it, playing or not. An effect has to
        // be given its audio whether or not anything is going through it: a delay has a tail
        // to finish after the last note, and a plugin only ever hands the host what its own
        // window did at the end of a block it was given. A track that goes quiet and stops
        // being processed is a plugin switched off without being told, and a knob turned in
        // its window then reaches nothing and nobody.
        lock (_lock)
        {
            for (int track = 0; track < MaxTracks; track++)
            {
                if (_inserts[track] != null) _sounding[track] = true;
            }
        }

        Array.Clear(_loose, 0, samples);

        // An audition is added to the loose bus rather than written over it: a plugin fills a
        // buffer, and the loose bus may already have another audition in it.
        if (preview != null)
        {
            if (_previewScratch.Length < samples) _previewScratch = new float[samples];
            Array.Clear(_previewScratch, 0, samples);

            try
            {
                preview.Render(_previewScratch, frames);
            }
            catch (Exception)
            {
                Array.Clear(_previewScratch, 0, samples);
            }

            for (int index = 0; index < samples; index++) _loose[index] += _previewScratch[index] * previewGain;
        }

        for (int track = 0; track < MaxTracks; track++)
        {
            if (!_sounding[track]) continue;

            _busses[track] ??= new float[samples];
            Array.Clear(_busses[track]!, 0, samples);
        }

        // Plugins first: one fills its track's bus, and anything else on that track adds on
        // top of what it played.
        for (int track = 0; track < MaxTracks; track++)
        {
            var instrument = instruments[track];
            if (instrument == null) continue;

            var bus = _busses[track];
            if (bus == null) continue;

            try
            {
                instrument.Render(bus, frames);
            }
            catch (Exception error)
            {
                // A managed fault in a plugin costs that block, not the audio thread.
                _census[track].Note(error.Message);
                Array.Clear(bus, 0, samples);
            }

            // Only when somebody is reading. Scanning the block to see how loud it came out is
            // a pass over every sample on the audio thread, and off it must cost nothing.
            if (Diagnostics.Log.IsOn) _census[track].Played(Peak(bus, samples), instrument);

            Place(bus, samples, _instrumentGain[track], _instrumentPan[track]);
        }

        for (int index = 0; index < sounding; index++)
        {
            var voice = playing[index];
            int track = voice.Track;

            var target = track >= 0 && track < MaxTracks ? _busses[track] : _loose;
            if (target != null) voice.Render(target, frames);
        }
    }

    /// <summary>
    /// The volume and pan columns applied to a plugin's bus. A plugin plays at its own level
    /// and knows nothing about the tracker's columns, so they are applied to what came out.
    /// </summary>
    private static void Place(float[] bus, int samples, float gain, float pan)
    {
        float left = gain * Math.Min(1f, 1f - pan);
        float right = gain * Math.Min(1f, 1f + pan);

        if (Math.Abs(left - 1f) < 0.0001f && Math.Abs(right - 1f) < 0.0001f) return;

        for (int index = 0; index + 1 < samples; index += 2)
        {
            bus[index] *= left;
            bus[index + 1] *= right;
        }
    }

    /// <summary>Runs each sounding track's audio through whatever is inserted on it.</summary>
    private void ApplyInserts(int frames)
    {
        for (int track = 0; track < MaxTracks; track++)
        {
            if (!_sounding[track]) continue;

            IAudioInsert? insert;
            lock (_lock) insert = _inserts[track];

            if (insert == null) continue;

            var bus = _busses[track];
            if (bus == null) continue;

            bool watching = Diagnostics.Log.IsOn;
            int samples = frames * 2;
            float before = watching ? Peak(bus, samples) : 0f;

            try
            {
                insert.Process(bus, frames);
            }
            catch (Exception error)
            {
                // A managed fault in an insert costs that block, not the audio thread.
                _census[track].Note(error.Message);
            }

            if (watching) _census[track].Inserted(before, Peak(bus, samples), insert);
        }
    }

    /// <summary>
    /// Adds one track into the mix, through its side chain if it has one. The key is read
    /// before it is itself ducked, so two tracks keying each other cannot chase each other
    /// down into silence.
    /// </summary>
    private void MixTrack(float[] buffer, int track, DuckSetting setting, int frames, int samples)
    {
        var source = _sounding[track] ? _busses[track] : null;

        float peak = 0f;
        if (source != null)
        {
            for (int i = 0; i < samples; i++)
            {
                float abs = Math.Abs(source[i]);
                if (abs > peak) peak = abs;
            }
        }
        _trackLevels[track] = peak;

        bool ducked = setting.Depth > 0
            && setting.Key >= 0
            && setting.Key < MaxTracks
            && setting.Key != track;

        if (!ducked)
        {
            _duckGain[track] = 1f;
            _duckers[track]?.Reset();

            if (source == null) return;

            for (int i = 0; i < samples; i++) buffer[i] += source[i];
            return;
        }

        var ducker = _duckers[track] ??= new Ducker(setting.ReleaseMs, SampleRate);
        ducker.ReleaseMs = setting.ReleaseMs;

        var key = _sounding[setting.Key] ? _busses[setting.Key] : null;
        float gain = 1f;

        for (int frame = 0; frame < frames; frame++)
        {
            int i = frame * 2;

            double magnitude = key == null ? 0 : Math.Max(Math.Abs(key[i]), Math.Abs(key[i + 1]));
            gain = Ducker.GainFor(ducker.Next(magnitude), setting.Depth);

            // The follower still has to run for a silent track, or the first note after a
            // rest would come in at whatever gain the duck was left at.
            if (source == null) continue;

            buffer[i] += source[i] * gain;
            buffer[i + 1] += source[i + 1] * gain;
        }

        _duckGain[track] = gain;
    }

    private void EnsureBusses(int frames)
    {
        int samples = frames * 2;
        if (_bufferFrames == frames && _loose.Length >= samples) return;

        _bufferFrames = frames;
        _loose = new float[samples];

        for (int track = 0; track < MaxTracks; track++)
        {
            // Only tracks that have sounded have a bus; the rest are made when they are needed.
            if (_busses[track] != null) _busses[track] = new float[samples];
        }
    }

    /// <summary>Below this the bus is a wire; above it, it bends. Roughly -3 dB.</summary>
    public const float Knee = 0.7f;

    /// <summary>
    /// Saturates rather than clipping. A chord of voices can sum past full scale, and a hard
    /// clip on that sounds like a fault; this bends instead.
    /// </summary>
    /// <remarks>
    /// Bending starts at the knee rather than at zero. Recordings come through here too now,
    /// and a curve applied from the bottom up would quietly reshape every sample in the song
    /// on its way out, which is not the bus's business. Only what is loud enough to be a
    /// problem is touched.
    /// </remarks>
    public static float SoftClip(float value)
    {
        float magnitude = MathF.Abs(value);
        if (magnitude <= Knee) return value;

        float over = (magnitude - Knee) / (1 - Knee);
        float shaped = Knee + (1 - Knee) * MathF.Tanh(over);

        return value < 0 ? -shaped : shaped;
    }

    /// <summary>
    /// How loud a track is sounding, for a meter. Taken from the voices rather than from the
    /// mixed buffer: the voices are already summed together by the time that exists.
    /// </summary>
    public (float Left, float Right) LevelFor(int track)
    {
        if (track < 0) return (0, 0);

        float left = 0;
        float right = 0;

        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                if (voice.Track != track || voice.IsFinished) continue;

                float level = voice.Level * MasterGain * DuckGainFor(track);
                float pan = voice.Pan;

                left = Math.Max(left, level * (pan <= 0 ? 1f : 1f - pan));
                right = Math.Max(right, level * (pan >= 0 ? 1f : 1f + pan));
            }

            if (_instruments[track] != null && _trackLevels[track] > 0)
            {
                float level = _trackLevels[track] * MasterGain * DuckGainFor(track);
                left = Math.Max(left, level);
                right = Math.Max(right, level);
            }
        }

        return (Math.Clamp(left, 0f, 1f), Math.Clamp(right, 0f, 1f));
    }

    /// <summary>
    /// One line a second per track that is doing anything, saying what came out of the plugin,
    /// what went into the insert, what came out of it, and what the meter is being told.
    /// </summary>
    /// <remarks>
    /// Everything a silent plugin could be is separable from this one line: a plugin that is
    /// not being rendered has no blocks, one that is rendering silence has blocks and no peak,
    /// one being turned down by the volume column has a peak and a level well below it, and one
    /// whose insert is eating it has a peak going in and none coming out.
    /// </remarks>
    private void Census()
    {
        for (int track = 0; track < MaxTracks; track++)
        {
            if (!_census[track].Worth) continue;

            var seen = _census[track];
            _census[track].Clear();

            int number = track;
            float gain = _instrumentGain[number];
            float pan = _instrumentPan[number];
            float meter = _trackLevels[number];

            Diagnostics.Log.Write(Diagnostics.LogArea.Tracker, () =>
                "track " + number + ": " +
                (seen.Instrument == null ? "no plugin playing it" :
                    seen.Instrument + " played " + seen.Blocks + " blocks, peak " + seen.PlayedPeak.ToString("F4") +
                    ", silent in " + seen.SilentBlocks + " of them") +
                "; volume column " + gain.ToString("F2") + ", pan " + pan.ToString("F2") +
                (seen.Insert == null ? "; nothing inserted" :
                    "; " + seen.Insert + " was given " + seen.BeforeInsert.ToString("F4") +
                    " and gave back " + seen.AfterInsert.ToString("F4")) +
                "; the meter is being told " + meter.ToString("F4") +
                (seen.Faults == 0 ? "" : "; " + seen.Faults + " faults, last was " + seen.Fault));
        }
    }

    /// <summary>Drops finished voices. Called after rendering, off the note-on path.</summary>
    private void Reap()
    {
        lock (_lock)
        {
            int removed = _voices.RemoveAll(v => v.IsFinished);
            if (removed > 0) _snapshotStale = true;
        }
    }

    private void Add(IVoice voice)
    {
        // Oldest first, so voice stealing takes the one that has been ringing longest.
        while (_voices.Count >= MaxVoices)
            _voices.RemoveAt(0);

        _voices.Add(voice);
        _snapshotStale = true;
    }

    /// <summary>A different seed per voice, so two noise hits are not the same noise.</summary>
    private int NextSeed() => System.Threading.Interlocked.Increment(ref _noiseSeed);
}
