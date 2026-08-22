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
    private bool _snapshotStale = true;
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

    public IAudioInsert? InsertOn(int track) =>
        track >= 0 && track < MaxTracks ? _inserts[track] : null;

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

    /// <summary>Sounds a note that releases on its own, for auditioning while editing.</summary>
    public void Preview(SynthPatch patch, Note note, float gain, double holdSeconds)
    {
        if (patch is null || !note.IsPlayable) return;

        var voice = new SynthVoice(patch, note, SynthVoice.NoTrack, gain, 0f, SampleRate, NextSeed());
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

    /// <summary>A recording sounded once, for auditioning while editing.</summary>
    public void Preview(TrackerInstrument instrument, SampleData sample, Note note, float gain, double holdSeconds)
    {
        if (instrument is null || sample is null || sample.IsEmpty || !note.IsPlayable) return;

        var voice = new SampleVoice(
            sample, instrument.Patch, instrument.Shape, note, instrument.BaseNote,
            SynthVoice.NoTrack, gain, 0f, SampleRate);

        voice.HoldFor(holdSeconds);

        lock (_lock) Add(voice);
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
        DuckSetting[] ducking;
        IPluginInstrument?[] instruments;
        IPluginInstrument? preview;
        IPluginInstrument? releasing = null;
        float previewGain;

        lock (_lock)
        {
            if (Diagnostics.Log.IsOn && Environment.TickCount64 - _said > 2000)
            {
                _said = Environment.TickCount64;

                int voices = _voices.Count;
                int played = _instrumentCount;
                int inserts = _insertCount;

                Diagnostics.Log.Write(Diagnostics.LogArea.Audio, () =>
                    "the mixer has " + voices + " voices, " + played + " plugin instruments and " +
                    inserts + " tracks with something inserted");
            }

            if (_voices.Count == 0 && _instrumentCount == 0 && _preview == null && _insertCount == 0)
            {
                Rest();
                return;
            }

            if (_snapshotStale)
            {
                _snapshot = _voices.ToArray();
                _snapshotStale = false;
            }

            playing = _snapshot;
            instruments = (IPluginInstrument?[])_instruments.Clone();

            preview = _preview;
            previewGain = _previewGain;

            // An audition has no key to let go of, so it lets go of itself.
            if (_previewUntil != 0 && Environment.TickCount64 >= _previewUntil)
            {
                _previewUntil = 0;
                releasing = preview;
            }
            ducking = (DuckSetting[])_ducking.Clone();
        }

        // Each track is rendered on its own before anything is summed. Ducking needs one
        // track to be measurable while another is being moved by it, and once everything is
        // added together there is nothing left to measure.
        releasing?.AllNotesOff();

        RenderBusses(playing, instruments, preview, previewGain, frames, samples);

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
    private void RenderBusses(IVoice[] playing, IPluginInstrument?[] instruments, IPluginInstrument? preview, float previewGain, int frames, int samples)
    {
        EnsureBusses(frames);

        Array.Clear(_sounding, 0, MaxTracks);

        foreach (var voice in playing)
        {
            int track = voice.Track;
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
            catch (Exception)
            {
                // A managed fault in a plugin costs that block, not the audio thread.
                Array.Clear(bus, 0, samples);
            }

            Place(bus, samples, _instrumentGain[track], _instrumentPan[track]);
        }

        foreach (var voice in playing)
        {
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

            try
            {
                // Edited in place: the bus is this track and nothing else, which is exactly
                // what an insert is supposed to see.
                insert.Process(bus, frames);
            }
            catch (Exception)
            {
                // A managed fault in an insert costs that block, not the audio thread.
            }
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
        }

        return (Math.Clamp(left, 0f, 1f), Math.Clamp(right, 0f, 1f));
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
