using System;
using System.Collections.Generic;
using System.Threading;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Synth.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Synth;

/// <inheritdoc/>
/// <remarks>
/// Everything here is indexed by track and sized for as many tracks as a song can have, so a
/// strip always has a bus, a ducker and an instrument slot of its own whether or not the song
/// currently reaches that far. Indexed things go wrong quietly, which is why
/// <c>Tests/MixerIsolationTests.cs</c> plays a note on one track and asks what every other
/// track is sounding.
///
/// The voice list is behind a lock and the render runs off a snapshot of it. The critical
/// sections are a few list operations long, and the arrays the render works from are filled
/// rather than made afresh: copying them out was two allocations per block on the audio thread,
/// forty thousand a second between them, all of it garbage somebody has to collect while the
/// next block is waiting.
/// </remarks>
public sealed class TrackMixer : ITrackMixer
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
    /// <remarks>
    /// Sized for the largest song rather than for the one that is open, so nothing has to be
    /// rebuilt when a track is added and no index can walk off an array that shrank underneath
    /// it.
    /// </remarks>
    private const int MaxTracks = Song.MaxTrackCount;

    /// <summary>
    /// The whole mix, after every track has been added to it.
    /// </summary>
    /// <remarks>
    /// Not a track and deliberately not one: it has no bus, no voices, no instrument and nothing
    /// keying it, because everything has already been summed by the time it is reached. What it
    /// has is a level, a place in the stereo field, and one effect the whole song goes through,
    /// which is the thing there was nowhere to put before.
    ///
    /// Applied where the fixed <see cref="MasterGain"/> always was, so the order is the same as
    /// it ever was with one thing added to it: sum, effect, level, the saturation that keeps it
    /// inside.
    /// </remarks>
    private float _masterGain = 1f;

    /// <summary>Where the whole mix sits, -1 hard left to 1 hard right.</summary>
    private float _masterPan;

    /// <summary>The one effect the whole song goes through, if there is one.</summary>
    private IAudioInsert? _masterInsert;

    /// <summary>What left on each side in the last block, before it was stamped and aged.</summary>
    private float _masterLeft;

    private float _masterRight;

    /// <summary>Every voice sounding, on a track or not. Only ever touched under the lock.</summary>
    private readonly List<IVoice> _voices = new();

    /// <summary>
    /// What guards everything the audio thread reads and the other threads write.
    /// </summary>
    /// <remarks>
    /// Held for a few list or array operations and never while somebody else's code runs: a
    /// plugin being rendered or told to stop is outside it, always.
    /// </remarks>
    private readonly object _lock = new();

    /// <summary>One buffer per track, so a track can be measured and moved on its own.</summary>
    private readonly float[]?[] _busses = new float[MaxTracks][];

    /// <summary>Auditions and anything else with no track of its own.</summary>
    private float[] _loose = Array.Empty<float>();

    /// <summary>
    /// Which tracks are worth rendering this block.
    /// </summary>
    /// <remarks>
    /// A track sounds if it has a voice, a plugin, or something inserted on it. The last is the
    /// one that looks wrong and is not: see the remarks on <see cref="ITrackMixer"/>.
    /// </remarks>
    private readonly bool[] _sounding = new bool[MaxTracks];

    /// <summary>What each strip's side chain is set to.</summary>
    private readonly DuckSetting[] _ducking = new DuckSetting[MaxTracks];

    /// <summary>The follower per strip, made the first time a strip is actually ducked.</summary>
    private readonly Ducker?[] _duckers = new Ducker[MaxTracks];

    /// <summary>Where each strip's duck ended the last block, for the knob's own read-out.</summary>
    private readonly float[] _duckGain = new float[MaxTracks];

    /// <summary>The peak off each track's bus in the last block, measured after its insert.</summary>
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

    /// <summary>
    /// Where the audition plugin renders before being added to the loose bus.
    /// </summary>
    /// <remarks>
    /// A plugin fills a buffer rather than adding to one, and the loose bus may already have
    /// another audition in it, so it cannot render straight into it. Kept and regrown rather
    /// than made per block, since this is the audio thread.
    /// </remarks>
    private float[] _previewScratch = Array.Empty<float>();

    /// <summary>How loud the audition plays, which is applied as its scratch is added in.</summary>
    private float _previewGain = 1f;

    /// <summary>How long the last block was, so the busses are only rebuilt when it changes.</summary>

    /// <summary>What one strip's side chain is set to.</summary>
    /// <param name="Depth">How far down the strip goes when the key is at full scale, 0 to 1.</param>
    /// <param name="Key">The track doing the pushing, or <see cref="TrackMix.NoKey"/>.</param>
    /// <param name="ReleaseMs">How long it takes to come back up.</param>
    private readonly record struct DuckSetting(double Depth, int Key, double ReleaseMs);

    /// <summary>
    /// The voices as they stood when the lock was taken, which is what the block renders.
    /// </summary>
    /// <remarks>
    /// Grown when it has to be and reused when it does not, so a run of notes does not leave an
    /// array behind for every one of them. Nothing is cleared past
    /// <see cref="_voiceCount"/>: the tail is whatever the last, longer block held, and the
    /// count is what says where to stop.
    /// </remarks>
    private IVoice[] _snapshot = Array.Empty<IVoice>();

    /// <summary>How much of <see cref="_snapshot"/> is this block's, the rest being stale.</summary>
    private int _voiceCount;

    /// <summary>Something has been added or reaped, so the snapshot is worth taking again.</summary>
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

    /// <summary>The side chains as they stood when the lock was taken. Filled, never made.</summary>
    private readonly DuckSetting[] _ducked = new DuckSetting[MaxTracks];

    /// <summary>Counts up, one per voice, so two noise hits are never the same noise.</summary>
    private int _noiseSeed;

    /// <summary>
    /// Sets up a mix for a card running at that rate, with every strip open and unducked.
    /// </summary>
    /// <remarks>
    /// The busses are not made here. A track gets one the first time it sounds, so a song using
    /// four tracks does not carry twenty-eight empty buffers about with it.
    /// </remarks>
    public TrackMixer(int sampleRate)
    {
        SampleRate = sampleRate;

        for (int track = 0; track < MaxTracks; track++)
        {
            _ducking[track] = new DuckSetting(0, TrackMix.NoKey, TrackMix.DefaultDuckReleaseMs);
            _duckGain[track] = 1f;
            _instrumentGain[track] = 1f;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Walked backwards, which is what makes it the newest: the list is in the order the notes
    /// started. Only sample voices have a position at all, so everything else is stepped over.
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

    /// <inheritdoc/>
    public void SetDucking(int track, double depth, int key, double releaseMs)
    {
        if (track < 0 || track >= MaxTracks) return;

        lock (_lock) _ducking[track] = new DuckSetting(Math.Clamp(depth, 0, 1), key, releaseMs);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The running total is kept here rather than counted per block, because the render asks
    /// whether anything is inserted anywhere before it decides it may rest.
    /// </remarks>
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
        /// <summary>How many blocks the track's plugin was asked for over the second.</summary>
        public int Blocks;

        /// <summary>The loudest the plugin came out over the second, before the columns.</summary>
        public float PlayedPeak;

        /// <summary>The loudest that went into the insert.</summary>
        public float BeforeInsert;

        /// <summary>And the loudest that came back out, which is how an effect eating a track shows.</summary>
        public float AfterInsert;

        /// <summary>How many of the blocks came out silent, which separates quiet from not running.</summary>
        public int SilentBlocks;

        /// <summary>What the last fault said, since one line cannot carry all of them.</summary>
        public string? Fault;

        /// <summary>How many there were, which is what says whether one was a one-off.</summary>
        public int Faults;

        /// <summary>What is playing the track, by type name. Written once and then left alone.</summary>
        public string? Instrument;

        /// <summary>And what is inserted on it.</summary>
        public string? Insert;

        /// <summary>Takes note of one block a plugin played. On the audio thread: comparisons only.</summary>
        public void Played(float peak, IPluginInstrument instrument)
        {
            Blocks++;
            if (peak > PlayedPeak) PlayedPeak = peak;
            if (peak <= Quiet) SilentBlocks++;
            Instrument ??= instrument.GetType().Name;
        }

        /// <summary>Takes note of what one insert was given and what it gave back.</summary>
        public void Inserted(float before, float after, IAudioInsert insert)
        {
            if (before > BeforeInsert) BeforeInsert = before;
            if (after > AfterInsert) AfterInsert = after;
            Insert ??= insert.GetType().Name;
        }

        /// <summary>Takes note of a plugin or an insert that threw, keeping the last message.</summary>
        public void Note(string fault)
        {
            Faults++;
            Fault = fault;
        }

        /// <summary>Whether this track did anything worth a line, so silent tracks say nothing.</summary>
        public bool Worth => Blocks > 0 || Insert != null || Faults > 0;

        /// <summary>Starts the next second from nothing, once the line has been written.</summary>
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

    /// <inheritdoc/>
    public IAudioInsert? InsertOn(int track) =>
        track >= 0 && track < MaxTracks ? _inserts[track] : null;

    /// <inheritdoc/>
    public float GetTrackLevel(int track) =>
        track >= 0 && track < MaxTracks ? _trackLevels[track] : 0f;

    /// <inheritdoc/>
    /// <remarks>
    /// The one leaving is told to stop outside the lock, since that is somebody else's code
    /// and it has no business running inside it.
    /// </remarks>
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

    /// <inheritdoc/>
    /// <remarks>
    /// The busses themselves are not moved. They hold the last block and nothing else, and the
    /// next block fills them again from whatever is now on each track.
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
            ShiftColumns(_pluginHeld, from, to);
            Shift(_ducking, from, to);
            Shift(_trackLevels, from, to);

            for (int track = 0; track < MaxTracks; track++)
            {
                var setting = _ducking[track];
                if (setting.Key < 0) continue;

                _ducking[track] = setting with { Key = Song.WhereTrackWent(setting.Key, from, to) };
            }

            for (int track = 0; track < MaxTracks; track++)
            {
                _duckGain[track] = 1f;
                _duckers[track]?.Reset();
            }

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

    /// <summary>The same, for state held as a block of note columns per track.</summary>
    private static void ShiftColumns<T>(T[] values, int from, int to)
    {
        var moved = new T[Columns];
        Array.Copy(values, from * Columns, moved, 0, Columns);

        int step = from < to ? 1 : -1;
        for (int track = from; track != to; track += step)
            Array.Copy(values, (track + step) * Columns, values, track * Columns, Columns);

        Array.Copy(moved, 0, values, to * Columns, Columns);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// What the one leaving was holding is forgotten along with it, and the notes are thrown
    /// away rather than sent: it is being told to let go of everything in one message, which is
    /// the right way to end a plugin that is on its way out of the slot.
    /// </remarks>
    public void SetPreviewInstrument(IPluginInstrument? instrument)
    {
        IPluginInstrument? leaving;
        Span<int> letting = stackalloc int[HeldNotes.Most];

        lock (_lock)
        {
            leaving = _preview;
            if (ReferenceEquals(leaving, instrument)) return;

            _preview = instrument;
            _previewHeld.LetAll(letting);
        }

        leaving?.AllNotesOff();
    }

    /// <inheritdoc/>
    public IPluginInstrument? PreviewInstrument
    {
        get { lock (_lock) return _preview; }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Each note is let go of at its own moment rather than the panel holding one moment for
    /// whatever it last played: a chord is several keys and they are not pressed at one
    /// instant, so one moment for all of them means the first key of a chord outliving its own
    /// hold by however long the hand took to finish the chord.
    /// </remarks>
    public void PreviewPlugin(Note note, float gain, double holdSeconds,
                              VoiceEnding ending = VoiceEnding.Sustain)
    {
        if (!note.IsPlayable) return;

        IPluginInstrument? instrument;
        Span<int> letting = stackalloc int[HeldNotes.Most];
        int count;
        bool everything;

        lock (_lock)
        {
            instrument = _preview;
            _previewGain = gain;

            everything = ending != VoiceEnding.Sustain && _previewHeld.Count == 0;

            count = instrument == null
                ? 0
                : MakeWay(_previewHeld, note.Semitone, Until(holdSeconds), ending, letting);
        }

        if (instrument == null) return;

        Play(instrument, note.Semitone, letting, count, everything);
    }

    /// <summary>
    /// What each note column has told its track's plugin to play and has not told it to end.
    /// </summary>
    /// <remarks>
    /// One per column and not one per track, because a column is a voice: an OFF in the second
    /// column of a chord has to end that column's note and leave the other two sounding, and
    /// the only way to name one note of a plugin's chord is to have written down which column
    /// started it.
    ///
    /// One apiece and never null, so a note can be written down for a track whose plugin has
    /// not loaded yet without anything having to be made on the way past. They move with the
    /// track when the song moves it, since what a plugin is holding is the plugin's, not the
    /// position's.
    /// </remarks>
    private readonly IHeldNotes[] _pluginHeld = PerColumn();

    /// <summary>How many note columns each track has room for, which is as many as it can have.</summary>
    private const int Columns = Song.MaxNoteColumns;

    /// <summary>Where one note column's record sits.</summary>
    private static int At(int track, int column) => track * Columns + column;

    /// <summary>The same, for the instrument being auditioned off a track.</summary>
    private readonly IHeldNotes _previewHeld = new HeldNotes();

    /// <summary>Reused, because this runs on the audio thread and must not make work for the collector.</summary>
    private readonly List<(IPluginInstrument Instrument, int Semitone)> _letting = new(MaxTracks);

    /// <summary>A record per note column of every track, made once.</summary>
    private static IHeldNotes[] PerColumn()
    {
        var held = new IHeldNotes[MaxTracks * Columns];

        for (int at = 0; at < held.Length; at++) held[at] = new HeldNotes();

        return held;
    }

    /// <summary>The moment a note played by hand should be let go of.</summary>
    /// <remarks>
    /// A wall clock instant rather than a count of samples, since the render checks it once a
    /// block. A twentieth of a second is the shortest it will honour, because anything less
    /// would be let go of in the same block it started in.
    /// </remarks>
    private static long Until(double holdSeconds) =>
        Environment.TickCount64 + (long)(Math.Max(0.05, holdSeconds) * 1000);

    /// <summary>
    /// Decides what a plugin has to be told to let go of before it is told to start a note, and
    /// writes those notes into <paramref name="letting"/>.
    /// </summary>
    /// <remarks>
    /// Held under the lock, because it changes the record; the plugin is told outside it. A
    /// plugin has one ending of its own, its release, so cut and release are the same thing
    /// here and only sustain reads differently: under it nothing is let go of but the note
    /// arriving again, which is a retrigger.
    /// </remarks>
    private static int MakeWay(IHeldNotes held, int semitone, long until, VoiceEnding ending, Span<int> letting)
    {
        int count = 0;

        if (ending == VoiceEnding.Sustain)
        {
            if (held.Let(semitone)) letting[count++] = semitone;
        }
        else
        {
            count = held.LetAll(letting);
        }

        int stolen = held.Press(semitone, until);

        if (stolen >= 0 && count < letting.Length) letting[count++] = stolen;

        return count;
    }

    /// <summary>
    /// Tells a plugin to let go of what it was holding and then to start the new note.
    /// </summary>
    /// <remarks>
    /// Where nothing was remembered anywhere on the track the whole plugin is asked to let go
    /// instead, which is what this always did and is worth keeping: the record is what this
    /// side said, and a plugin sent a note by anything else is exactly the case a per-note off
    /// cannot reach. It costs one message on the first note after a stop.
    ///
    /// Anywhere on the track, and not in this column: the other columns are the rest of a
    /// chord, and a sweep that fired whenever one column happened to be empty would take the
    /// chord down every time a note landed in a column that had not played yet, which is every
    /// first note of every chord.
    /// </remarks>
    private static void Play(IPluginInstrument instrument, int semitone, ReadOnlySpan<int> letting,
                             int count, bool everything)
    {
        if (everything) instrument.AllNotesOff();

        for (int i = 0; i < count; i++) instrument.NoteOff(letting[i]);

        instrument.NoteOn(semitone, 1f);
    }

    /// <summary>
    /// Moves whatever a plugin should have let go of by now onto the list the render empties
    /// once it is out of the lock.
    /// </summary>
    /// <remarks>
    /// Runs on the audio thread, so the notes go into a span on the stack and from there into
    /// a list that is kept rather than made. A note from a pattern has no moment and is never
    /// reached by this: it is held until an OFF, the next note or the transport.
    /// </remarks>
    private static void Expired(IHeldNotes held, IPluginInstrument plugin, long now, Span<int> into,
                                List<(IPluginInstrument Instrument, int Semitone)> letting)
    {
        if (held.Count == 0) return;

        int count = held.LetExpired(now, into);

        for (int i = 0; i < count; i++) letting.Add((plugin, into[i]));
    }

    /// <inheritdoc/>
    public void PreviewOnTrack(int track, Note note, float gain, double holdSeconds,
                               VoiceEnding ending = VoiceEnding.Sustain, float pan = 0f)
    {
        if (track < 0 || track >= MaxTracks || !note.IsPlayable) return;

        IPluginInstrument? instrument;
        Span<int> letting = stackalloc int[HeldNotes.Most];
        int count;
        bool everything;

        lock (_lock)
        {
            instrument = _instruments[track];

            _instrumentGain[track] = gain;
            _instrumentPan[track] = Math.Clamp(pan, -1f, 1f);

            everything = ending != VoiceEnding.Sustain && Silent(track);

            count = instrument == null
                ? 0
                : MakeWay(_pluginHeld[At(track, 0)], note.Semitone, Until(holdSeconds), ending, letting);
        }

        if (instrument == null) return;

        Play(instrument, note.Semitone, letting, count, everything);
    }

    /// <inheritdoc/>
    public void LetPluginNote(int track, int semitone)
    {
        if (track < 0 || track >= MaxTracks) return;

        IPluginInstrument? instrument;
        bool held;

        lock (_lock)
        {
            instrument = _instruments[track];
            held = _pluginHeld[At(track, 0)].Let(semitone);
        }

        if (held) instrument?.NoteOff(semitone);
    }

    /// <inheritdoc/>
    public void LetPreviewNote(int semitone)
    {
        IPluginInstrument? instrument;
        bool held;

        lock (_lock)
        {
            instrument = _preview;
            held = _previewHeld.Let(semitone);
        }

        if (held) instrument?.NoteOff(semitone);
    }

    /// <inheritdoc/>
    public IPluginInstrument? InstrumentOn(int track) =>
        track >= 0 && track < MaxTracks ? _instruments[track] : null;

    /// <inheritdoc/>
    /// <remarks>
    /// The plugin is played at full and the volume column is applied to its bus afterwards,
    /// because a plugin's own velocity is part of its patch and turning a note down with it
    /// would change the sound rather than the level.
    /// </remarks>
    public void PluginNoteOn(int track, int column, Note note, float gain, float pan,
                             VoiceEnding ending = VoiceEnding.Cut)
    {
        if (track < 0 || track >= MaxTracks || column < 0 || column >= Columns) return;
        if (!note.IsPlayable) return;

        IPluginInstrument? instrument;
        Span<int> letting = stackalloc int[HeldNotes.Most];
        int count;
        bool everything;

        lock (_lock)
        {
            instrument = _instruments[track];
            _instrumentGain[track] = gain;
            _instrumentPan[track] = Math.Clamp(pan, -1f, 1f);

            everything = ending != VoiceEnding.Sustain && Silent(track);

            count = instrument == null
                ? 0
                : MakeWay(_pluginHeld[At(track, column)], note.Semitone, 0, ending, letting);
        }

        if (instrument == null) return;

        Play(instrument, note.Semitone, letting, count, everything);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// One message per note, because an OFF belongs to one note column and the other columns of
    /// the same track are the rest of a chord. All notes off would take the chord down to end
    /// one note of it, which is the whole reason this side keeps a record at all.
    ///
    /// Where nothing is remembered anywhere on the track the plugin is asked to let go of
    /// everything instead. That is what this always did, and it is the one case a per-note off
    /// cannot reach: a plugin that has been sent a note by something other than this.
    /// </remarks>
    public void PluginNoteOff(int track, int column = 0)
    {
        if (track < 0 || track >= MaxTracks || column < 0 || column >= Columns) return;

        IPluginInstrument? instrument;
        Span<int> letting = stackalloc int[HeldNotes.Most];
        int count;
        bool nothing;

        lock (_lock)
        {
            instrument = _instruments[track];
            count = _pluginHeld[At(track, column)].LetAll(letting);
            nothing = count == 0 && Silent(track);
        }

        if (instrument == null) return;

        if (nothing)
        {
            instrument.AllNotesOff();
            return;
        }

        for (int i = 0; i < count; i++) instrument.NoteOff(letting[i]);
    }

    /// <summary>True when no note column of a track remembers holding anything.</summary>
    /// <remarks>Held under the lock by its callers, since it reads the records.</remarks>
    private bool Silent(int track)
    {
        for (int column = 0; column < Columns; column++)
            if (_pluginHeld[At(track, column)].Count > 0) return false;

        return true;
    }

    /// <inheritdoc/>
    public void SetPluginLevels(int track, float gain, float? pan)
    {
        if (track < 0 || track >= MaxTracks) return;

        lock (_lock)
        {
            _instrumentGain[track] = gain;
            if (pan.HasValue) _instrumentPan[track] = Math.Clamp(pan.Value, -1f, 1f);
        }
    }

    /// <inheritdoc/>
    public float DuckGainFor(int track) =>
        track >= 0 && track < MaxTracks ? _duckGain[track] : 1f;

    /// <inheritdoc/>
    public int SampleRate { get; }

    /// <inheritdoc/>
    public int VoiceCount
    {
        get { lock (_lock) return _voices.Count; }
    }

    /// <inheritdoc/>
    /// <remarks>The voice is built outside the lock, since making one is the expensive half.</remarks>
    public void NoteOn(int track, int column, SynthPatch patch, Note note, float gain, float pan,
                       VoiceEnding ending = VoiceEnding.Cut)
    {
        if (patch is null || !note.IsPlayable) return;

        var voice = new SynthVoice(patch, note, track, gain, pan, SampleRate, NextSeed())
        {
            Column = column
        };

        lock (_lock)
        {
            MakeWay(track, column, note, ending);
            Add(voice);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The one note-on built inside the lock rather than outside it, because what it slides
    /// from is read off the voice list and has to be read before that voice is cut.
    /// </remarks>
    public void NoteOn(int track, int column, MonoSynthPatch patch, Note note, float gain, float pan,
                       VoiceEnding ending = VoiceEnding.Cut)
    {
        if (patch is null || !note.IsPlayable) return;

        lock (_lock)
        {
            double? from = null;

            if (track >= 0)
            {
                foreach (var playing in _voices)
                {
                    if (playing.Track == track && playing.Column == column
                        && !playing.IsFinished && playing is MonoSynthVoice last)
                        from = last.Hz;
                }
            }

            MakeWay(track, column, note, ending);

            Add(new MonoSynthVoice(patch, note, track, gain, pan, SampleRate, NextSeed(), from)
            {
                Column = column
            });
        }
    }

    /// <summary>
    /// Makes room on a track for the note that is about to start there.
    /// </summary>
    /// <remarks>
    /// Held under the lock by its callers, since what it decides about has to still be true
    /// when the new voice is added.
    ///
    /// Room is made in one note column and not across the track. A column is a voice, so the
    /// other columns of the same track are other notes of the same chord and are none of this
    /// note's business.
    ///
    /// The same note arriving where it is already sounding is cut whichever ending was asked
    /// for. Two copies of one note are a retrigger, and letting them pile up is how a part left
    /// sustaining walks into <see cref="MaxVoices"/> and starts stealing notes somebody meant
    /// to hear.
    /// </remarks>
    private void MakeWay(int track, int column, Note note, VoiceEnding ending)
    {
        if (track < 0) return;

        foreach (var playing in _voices)
        {
            if (playing.Track != track || playing.Column != column) continue;

            if (ending == VoiceEnding.Cut || playing.Note.Semitone == note.Semitone) playing.Cut();
            else if (ending == VoiceEnding.Release) playing.NoteOff();
        }
    }

    /// <inheritdoc/>
    public void Preview(SynthPatch patch, Note note, float gain, double holdSeconds, string audition,
                        int track = SynthVoice.NoTrack, float pan = 0f)
    {
        if (patch is null || !note.IsPlayable) return;

        var voice = new SynthVoice(patch, note, track, gain, pan, SampleRate, NextSeed())
        {
            Audition = audition
        };

        voice.HoldFor(holdSeconds);

        lock (_lock) Add(voice);
    }

    /// <inheritdoc/>
    /// <remarks>Nothing is handed in for the note before, which is what switches the glide off.</remarks>
    public void Preview(MonoSynthPatch patch, Note note, float gain, double holdSeconds, string audition,
                        int track = MonoSynthVoice.NoTrack, float pan = 0f)
    {
        if (patch is null || !note.IsPlayable) return;

        var voice = new MonoSynthVoice(
            patch, note, track, gain, pan, SampleRate, NextSeed(), null)
        {
            Audition = audition
        };

        voice.HoldFor(holdSeconds);

        lock (_lock) Add(voice);
    }

    /// <inheritdoc/>
    public void NoteOn(int track, int column, TrackerInstrument instrument, SampleData sample,
                       Note note, float gain, float pan)
    {
        if (instrument is null || sample is null || sample.IsEmpty || !note.IsPlayable) return;

        var voice = new SampleVoice(
            sample, instrument.Patch, instrument.Shape, note, instrument.BaseNote,
            track, gain, pan, SampleRate)
        {
            Column = column
        };

        lock (_lock)
        {
            MakeWay(track, column, note, instrument.NewNoteAction);
            Add(voice);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A choke group of nought is no group at all, so nothing is walked and nothing is cut.
    /// </remarks>
    public void NoteOn(int track, int column, DrumPad pad, SynthPatch patch, SampleData sample,
                       Note note, float gain, float pan)
    {
        if (pad is null || patch is null || sample is null || sample.IsEmpty || !note.IsPlayable) return;

        var voice = new SampleVoice(
            sample, patch, pad.Shape, note, note,
            track, gain, pan, SampleRate)
        {
            Choke = pad.Choke,
            Column = column
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

    /// <inheritdoc/>
    /// <remarks>
    /// A fresh empty <see cref="SynthPatch"/> goes in beside Zampler's own, because the shared
    /// voice takes both: the plain path's shaping is left doing nothing and the four pole
    /// filters do the work instead.
    /// </remarks>
    public void NoteOn(int track, int column, SampleZone zone, SamplerPatch patch, SampleData sample,
                       Note note, float gain, float pan, VoiceEnding ending = VoiceEnding.Cut)
    {
        if (zone is null || patch is null || sample is null || sample.IsEmpty || !note.IsPlayable) return;

        var voice = new SampleVoice(
            sample, new SynthPatch(), zone.Shape, note, new Note(zone.Root),
            track, gain, pan, SampleRate, patch)
        {
            Column = column
        };

        lock (_lock)
        {
            MakeWay(track, column, note, ending);
            Add(voice);
        }
    }

    /// <inheritdoc/>
    public double Preview(
        SampleZone zone, SamplerPatch patch, SampleData sample, Note note, float gain,
        double holdSeconds, string audition,
        int track = SynthVoice.NoTrack, float pan = 0f)
    {
        if (zone is null || patch is null || sample is null || sample.IsEmpty || !note.IsPlayable) return 0;

        var voice = new SampleVoice(
            sample, new SynthPatch(), zone.Shape, note, new Note(zone.Root),
            track, gain, pan, SampleRate, patch)
        {
            Audition = audition
        };

        double held = Held(voice, holdSeconds);

        voice.HoldFor(held);

        lock (_lock) Add(voice);

        return held;
    }

    /// <inheritdoc/>
    /// <remarks>An audition carries its choke group, so two pads that cannot both ring still cannot.</remarks>
    public double Preview(
        DrumPad pad, SynthPatch patch, SampleData sample, Note note, float gain,
        double holdSeconds, string audition,
        int track = SynthVoice.NoTrack, float pan = 0f)
    {
        if (pad is null || patch is null || sample is null || sample.IsEmpty || !note.IsPlayable) return 0;

        var voice = new SampleVoice(
            sample, patch, pad.Shape, note, note,
            track, gain, pan, SampleRate)
        {
            Choke = pad.Choke,
            Audition = audition
        };

        double held = Held(voice, holdSeconds);

        voice.HoldFor(held);

        lock (_lock) Add(voice);

        return held;
    }

    /// <inheritdoc/>
    public double Preview(
        TrackerInstrument instrument, SampleData sample, Note note, float gain,
        double holdSeconds, string audition,
        int track = SynthVoice.NoTrack, float pan = 0f)
    {
        if (instrument is null || sample is null || sample.IsEmpty || !note.IsPlayable) return 0;

        var voice = new SampleVoice(
            sample, instrument.Patch, instrument.Shape, note, instrument.BaseNote,
            track, gain, pan, SampleRate)
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

    /// <inheritdoc/>
    /// <remarks>
    /// Matched on the audition alone. A note played by hand carries the track it was played on
    /// as well, so that it goes through that track's inserts and moves its meter, and asking
    /// for a track of none as well as an audition meant that every note played on the tracker's
    /// own keyboard was unreachable: the id said which panel was holding it and the test threw
    /// the answer away.
    /// </remarks>
    public void CutAuditions(string audition)
    {
        if (string.IsNullOrEmpty(audition)) return;

        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                if (voice.Audition == audition) voice.Cut();
            }
        }
    }

    /// <inheritdoc/>
    public void LetAudition(string audition, int semitone)
    {
        if (string.IsNullOrEmpty(audition)) return;

        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                if (voice.OneShot) continue;

                if (voice.Audition == audition && voice.Note.Semitone == semitone) voice.NoteOff();
            }
        }
    }

    /// <inheritdoc/>
    public void NoteOff(int track, int column = 0)
    {
        if (track < 0) return;

        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                if (voice.Track == track && voice.Column == column) voice.NoteOff();
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Every voice in that note column, not the newest, because a kit can have several sounding
    /// at once and a volume column is about the column rather than about one drum.
    /// </remarks>
    public void SetLevels(int track, int column, float gain, float? pan)
    {
        if (track < 0) return;

        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                if (voice.Track != track || voice.Column != column) continue;

                voice.Gain = gain;
                if (pan.HasValue) voice.Pan = pan.Value;
            }
        }
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    /// <remarks>
    /// Once a second, and only while the audio log is on, it says what it is holding and each
    /// track says what it did: see <see cref="Census"/>. Off, that costs a comparison and does
    /// not build the line, which matters because a line of the log is a file opened, written
    /// and closed, and doing that from inside a block is the audio thread waiting on a disk.
    ///
    /// A plugin is never told anything with the lock held. What has to be let go of is
    /// collected under it into <see cref="_letting"/>, which is kept rather than made, and
    /// emptied outside.
    ///
    /// **One thread renders at a time, and a second one asking is given silence rather than a
    /// place in a queue.** Everything the mixing uses is sized from the frame count it was
    /// called with, so two threads rendering at once with different counts is not a race over a
    /// value, it is one of them shortening the arrays the other is halfway through: the bus, the
    /// loose bus and the scratch are all reallocated by <see cref="EnsureBusses"/> whenever the
    /// block size changes. It crashed inside the loop that adds the preview onto the loose bus,
    /// which is simply the first place a shortened array is indexed.
    ///
    /// It was never meant to have two, and for almost all of a run it does not: either the sound
    /// card's own thread renders in step, or a thread of its own renders ahead into a queue, and
    /// which of those is happening is decided by one number. The window is the changeover.
    /// <c>TrackerOutput.StopMixingAhead</c> waits two tenths of a second for the ahead thread and
    /// then carries on regardless, which is right, since a plugin holding it up must not hang the
    /// application, but it leaves that thread still inside here while the sound card's own thread
    /// starts. Changing the output device, or the render-ahead setting, is exactly that moment.
    ///
    /// So the guard is here rather than there. Here it is true whoever calls, which is what a
    /// rule about this class ought to be, and it can be put a question to without a sound card.
    /// Refused rather than waited on, deliberately, and it is the rule this file already keeps
    /// for a queue that has run dry: one quiet block is a click, and a blocked callback is every
    /// stream on the device stuttering.
    /// </remarks>
    public void Render(float[] buffer, int frames)
    {
        if (!Monitor.TryEnter(_rendering))
        {
            Array.Clear(buffer, 0, Math.Min(Math.Max(0, frames) * 2, buffer.Length));
            return;
        }

        try
        {
            Mix(buffer, frames);
        }
        finally
        {
            Monitor.Exit(_rendering);
        }
    }

    /// <summary>Held for the whole of a render, so only one thread is ever inside one.</summary>
    /// <remarks>
    /// Its own lock and not <see cref="_lock"/>, which is about the mixer's state and is taken
    /// and let go of several times during a render, including by callers who are not rendering
    /// at all. Sharing them would have a note being played wait behind a block of audio.
    /// </remarks>
    private readonly object _rendering = new();

    /// <summary>
    /// One block, with the caller already established as the only one in here.
    /// </summary>
    /// <remarks>
    /// How long the block really is, is decided once and here, and everything after it works to
    /// that number. It used to be <c>frames * 2</c> taken on trust, with only the first clear
    /// asking whether the buffer was that long: the bus mixing, the loose bus and the master all
    /// wrote past the end of a buffer smaller than the caller claimed, which is the same crash
    /// as two threads rendering at once and arrives from the other direction. Half of a fault
    /// guarded is worse than none, because the guard that is there reads as the question having
    /// been asked.
    ///
    /// Rounded down to whole frames, so nothing downstream can be handed half of one, and
    /// nothing at all is done for a block with no room in it. Clamped rather than refused, since
    /// the caller has a sound card waiting either way and a short block of real audio beats an
    /// exception on the audio thread.
    /// </remarks>
    private void Mix(float[] buffer, int frames)
    {
        if (frames <= 0 || buffer.Length < 2) return;

        int samples = (int)Math.Min((long)frames * 2, buffer.Length & ~1);
        if (samples < 2) return;

        frames = samples / 2;

        Array.Clear(buffer, 0, samples);

        IVoice[] playing;
        int sounding;
        DuckSetting[] ducking;
        IPluginInstrument?[] instruments;
        IPluginInstrument? preview;
        float previewGain;

        Span<int> expired = stackalloc int[HeldNotes.Most];

        var letting = _letting;
        letting.Clear();

        lock (_lock)
        {
            if (Diagnostics.Log.On(Diagnostics.Enums.LogArea.Audio) && Environment.TickCount64 - _said > 1000)
            {
                _said = Environment.TickCount64;

                int voices = _voices.Count;
                int played = _instrumentCount;
                int inserts = _insertCount;

                Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Audio, () =>
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
                if (_snapshot.Length < _voices.Count) _snapshot = new IVoice[Math.Max(_voices.Count, 16)];

                _voices.CopyTo(_snapshot);

                _voiceCount = _voices.Count;
                _snapshotStale = false;
            }

            playing = _snapshot;
            sounding = _voiceCount;

            Array.Copy(_instruments, _live, MaxTracks);
            instruments = _live;

            preview = _preview;
            previewGain = _previewGain;

            long now = Environment.TickCount64;

            if (preview != null) Expired(_previewHeld, preview, now, expired, letting);

            for (int track = 0; track < MaxTracks; track++)
            {
                if (_instruments[track] is not IPluginInstrument plugin) continue;

                for (int column = 0; column < Columns; column++)
                    Expired(_pluginHeld[At(track, column)], plugin, now, expired, letting);
            }

            Array.Copy(_ducking, _ducked, MaxTracks);
            ducking = _ducked;
        }

        foreach (var (plugin, semitone) in letting) plugin.NoteOff(semitone);

        letting.Clear();

        RenderBusses(playing, sounding, instruments, preview, previewGain, frames, samples);

        ApplyInserts(frames);

        for (int track = 0; track < MaxTracks; track++)
            MixTrack(buffer, track, ducking[track], frames, samples);

        for (int i = 0; i < samples; i++)
            buffer[i] += _loose[i];

        Master(buffer, samples);

        Reap();
    }

    /// <summary>
    /// What the whole mix goes through on its way out: an effect, a level, a place, a limit.
    /// </summary>
    /// <remarks>
    /// The effect first, because a limiter on the master is put there to catch what the mix
    /// does and not what the fader does; then the level and the pan, which is the fader doing
    /// its one job; then the saturation, which is the last thing before the card and has to be,
    /// or the fader could put the mix outside it again.
    ///
    /// Applied where the fixed <see cref="MasterGain"/> always was, so a song written before
    /// there was a master strip opens at unity with nothing across it and sounds exactly as it
    /// did.
    ///
    /// Measured after all of that rather than before, so the meter beside the fader reads what
    /// is actually leaving. A plugin across the master that throws costs the block it threw in
    /// and nothing else: the mix carries on without it, which is the same bargain a track's
    /// chain makes.
    /// </remarks>
    private void Master(float[] buffer, int samples)
    {
        float gain;
        float pan;
        IAudioInsert? insert;

        lock (_lock)
        {
            gain = _masterGain;
            pan = _masterPan;
            insert = _masterInsert;
        }

        if (insert != null)
        {
            try
            {
                insert.Process(buffer, samples / 2);
            }
            catch
            {
            }
        }

        float left = gain * MasterGain * (pan <= 0 ? 1f : 1f - pan);
        float right = gain * MasterGain * (pan >= 0 ? 1f : 1f + pan);

        float loudestLeft = 0;
        float loudestRight = 0;

        for (int i = 0; i < samples; i += 2)
        {
            float one = SoftClip(buffer[i] * left);
            float two = SoftClip(buffer[i + 1] * right);

            buffer[i] = one;
            buffer[i + 1] = two;

            one = Math.Abs(one);
            two = Math.Abs(two);

            if (one > loudestLeft) loudestLeft = one;
            if (two > loudestRight) loudestRight = two;
        }

        _masterLeft = loudestLeft;
        _masterRight = loudestRight;
        _masterAt = Environment.TickCount64;
    }

    /// <inheritdoc/>
    public void SetMaster(float gain, float? pan)
    {
        lock (_lock)
        {
            _masterGain = Math.Max(0, gain);

            if (pan.HasValue) _masterPan = Math.Clamp(pan.Value, -1f, 1f);
        }
    }

    /// <inheritdoc/>
    public void SetMasterInsert(IAudioInsert? insert)
    {
        lock (_lock) _masterInsert = insert;
    }

    /// <inheritdoc/>
    public IAudioInsert? MasterInsert
    {
        get { lock (_lock) return _masterInsert; }
    }

    /// <summary>
    /// How long a master reading is worth anything, in milliseconds.
    /// </summary>
    /// <remarks>
    /// Longer than a block, which is a few milliseconds, and shorter than anyone would call a
    /// pause. Nothing turns on the exact number: it only has to be long enough that a reading
    /// never flickers while the mix is running, and short enough that it is gone before a hand
    /// leaves the fader.
    /// </remarks>
    public const double MeterHoldMs = 250;

    /// <summary>Whether a reading taken that long ago still says anything.</summary>
    /// <remarks>
    /// The rule on its own so it can be put a question to without an audio device. See
    /// <see cref="MasterLevel"/> for why there is a rule at all.
    /// </remarks>
    public static bool Fresh(double ageMs) => ageMs <= MeterHoldMs;

    /// <summary>Whether the meters are still worth reading, for whatever is polling them.</summary>
    /// <remarks>
    /// About what is sounding, and only then about the transport. Reading them while a pass runs
    /// and not otherwise is the mistake that has now been made twice: it leaves the master lit
    /// at whatever the last thing to play was, since the reading that stays on screen is a true
    /// one that is never taken again, and it shows nothing at all for a note played by hand with
    /// the transport stopped. The transport is in it only because a pass between two notes is
    /// silent and is not over.
    /// </remarks>
    public static bool Sounding(bool playing, float loudest) => playing || loudest > 0;

    /// <inheritdoc/>
    public (float Left, float Right) MasterLevel =>
        Fresh(Environment.TickCount64 - _masterAt) ? (_masterLeft, _masterRight) : (0f, 0f);

    /// <summary>When the master's reading was taken, so an old one can be seen to be old.</summary>
    private long _masterAt;

    /// <inheritdoc/>
    /// <remarks>
    /// The array is copied under the lock and the plugins are told outside it, which is the one
    /// place here a copy is worth its allocation: this is a stop, not a block.
    /// </remarks>
    public void AllPluginNotesOff()
    {
        IPluginInstrument?[] instruments;
        IPluginInstrument? preview;
        Span<int> letting = stackalloc int[HeldNotes.Most];

        lock (_lock)
        {
            instruments = (IPluginInstrument?[])_instruments.Clone();
            preview = _preview;

            _previewHeld.LetAll(letting);

            foreach (var held in _pluginHeld) held.LetAll(letting);
        }

        foreach (var instrument in instruments) instrument?.AllNotesOff();

        preview?.AllNotesOff();
    }

    /// <summary>
    /// Nothing to render: the duckers let go and every meter falls to nothing.
    /// </summary>
    /// <remarks>
    /// The levels have to be cleared here and not only in the render, because this is the path
    /// that skips the render. A track's meter falls on its own, since it is worked out from the
    /// voices that are sounding and there are none; the master's is a peak measured off the last
    /// buffer, so left alone it would hold whatever the last thing to play was, for ever. The
    /// mixer looked as though the song were still going after it had stopped.
    /// </remarks>
    private void Rest()
    {
        for (int track = 0; track < MaxTracks; track++)
        {
            _duckGain[track] = 1f;
            _duckers[track]?.Reset();
            _trackLevels[track] = 0f;
        }

        _masterLeft = 0f;
        _masterRight = 0f;
    }

    /// <summary>
    /// Puts every voice on its own track's bus, auditions aside.
    /// </summary>
    /// <remarks>
    /// Three things make a track sound. A voice, which is the ordinary case. A plugin, always,
    /// because it holds its own notes and its own release and there is no voice here to say
    /// whether it is still ringing. And something inserted on it, playing or not: an effect has
    /// to be given its audio whether or not anything is going through it, since a delay has a
    /// tail to finish after the last note and a plugin only ever hands the host what its own
    /// window did at the end of a block it was given. A track that goes quiet and stops being
    /// processed is a plugin switched off without being told, and a knob turned in its window
    /// then reaches nothing and nobody.
    ///
    /// Plugins render before voices, because a plugin fills its track's bus rather than adding
    /// to it and anything else on that track has to land on top of what it played. The audition
    /// plugin is the same problem on the loose bus, which may already hold another audition, so
    /// it goes through a scratch buffer and is added in.
    ///
    /// A plugin that throws costs that block and no more: the bus it was filling is cleared and
    /// the fault is counted for the log rather than allowed off the audio thread.
    /// </remarks>
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

        for (int track = 0; track < MaxTracks; track++)
        {
            if (instruments[track] != null) _sounding[track] = true;
        }

        lock (_lock)
        {
            for (int track = 0; track < MaxTracks; track++)
            {
                if (_inserts[track] != null) _sounding[track] = true;
            }
        }

        Array.Clear(_loose, 0, samples);

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

            _busses[track] ??= new float[_room];
            Array.Clear(_busses[track]!, 0, samples);
        }

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
                _census[track].Note(error.Message);
                Array.Clear(bus, 0, samples);
            }

            if (Diagnostics.Log.On(Diagnostics.Enums.LogArea.Audio)) _census[track].Played(Peak(bus, samples), instrument);

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

    /// <summary>
    /// Runs each sounding track's audio through whatever is inserted on it.
    /// </summary>
    /// <remarks>
    /// Before the side chains, so what keys a duck is the track as it sounds, effects included,
    /// which is what anyone listening would call the track. What went in and what came out are
    /// measured only while the audio log is on: two passes over every sample of every track is
    /// not something the audio thread should pay for when nobody is reading.
    ///
    /// An insert that throws costs that block and no more; the bus is left holding whatever the
    /// plugin managed before it gave up.
    /// </remarks>
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

            bool watching = Diagnostics.Log.On(Diagnostics.Enums.LogArea.Audio);
            int samples = frames * 2;
            float before = watching ? Peak(bus, samples) : 0f;

            try
            {
                insert.Process(bus, frames);
            }
            catch (Exception error)
            {
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
    /// <remarks>
    /// The follower runs even when the track itself is silent. Left standing it would keep
    /// whatever gain the last note ducked it to, and the first note after a rest would come in
    /// at that instead of at its own level.
    /// </remarks>
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

            if (source == null) continue;

            buffer[i] += source[i] * gain;
            buffer[i + 1] += source[i + 1] * gain;
        }

        _duckGain[track] = gain;
    }

    /// <summary>
    /// Makes sure every bus is as long as the block being asked for.
    /// </summary>
    /// <remarks>
    /// Only tracks that have sounded have a bus at all; the rest are made where they are first
    /// needed. The block length rarely changes, so this is a comparison per block and an
    /// allocation only when the card asks for something a different size.
    /// </remarks>
    private void EnsureBusses(int frames)
    {
        int samples = frames * 2;

        if (samples <= _room) return;

        _room = samples;
        _loose = new float[samples];

        for (int track = 0; track < MaxTracks; track++)
        {
            if (_busses[track] != null) _busses[track] = new float[samples];
        }
    }

    /// <summary>
    /// The longest block these buffers have been asked for, which is the size they are all kept
    /// at.
    /// </summary>
    /// <remarks>
    /// **Grow only.** It held the last block's frame count and rebuilt every buffer here whenever
    /// the next block was a different length. That is not a rare event, and this machine's own log
    /// says so in as many words: <c>the tracker stream is asking for between 8 and 529 frames at a
    /// time</c>. So the loose bus and one bus per sounding track were thrown away and allocated
    /// again on very nearly every callback, tens of kilobytes at a time, thousands of times a
    /// second, on the one thread with a deadline. The collector then runs during the mix, and a
    /// collection stops every managed thread in the process including that one.
    ///
    /// Measured rather than argued: twelve blocks of shifting size allocate nought bytes grown,
    /// and three hundred thousand sized to the last block. See
    /// <c>Tests/MixerRenderTests.A_different_block_size_does_not_rebuild_the_buffers</c>.
    ///
    /// Nothing else has to change, because nothing in here is bounded by a buffer's own length:
    /// every loop works to the <c>samples</c> or <c>frames</c> it was handed, and a buffer longer
    /// than the block simply has room left at the end. <see cref="_previewScratch"/> has always
    /// been grown this way, which is the rule this file already kept everywhere but here.
    /// </remarks>
    private int _room;

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

    /// <inheritdoc/>
    /// <remarks>
    /// **A meter may not take the mixer's lock.** This walked the whole voice list under
    /// <c>_lock</c>, and what asks it is a timer twenty times a second, once per strip: a drawing
    /// thread taking the lock the mixing thread needs, dozens of times a second, for a number
    /// nobody can hear. That is the shape <c>docs/threads.md</c> warns about, written into the one
    /// class the whole mix goes through.
    ///
    /// It reads the peak the render already measured off the track's own bus, which is a plain
    /// float written by one thread and read by another. It is also the truer answer: what the
    /// track sent, rather than the sum of what its voices thought they were worth. The two sides
    /// read the same, since the measurement is of the bus rather than of a voice's pan, and a
    /// meter is a loudness rather than a picture of the stereo field.
    ///
    /// Bounded by how many tracks a song can have, deliberately. It once asked whether the
    /// track number was inside the volume column's own memory, which is made when a pass
    /// starts, so before one there were no tracks to report on at all and every track meter
    /// read nought until somebody pressed play.
    /// </remarks>
    public (float Left, float Right) LevelFor(int track)
    {
        if (track < 0 || track >= MaxTracks) return (0, 0);

        float level = Math.Clamp(_trackLevels[track] * MasterGain, 0f, 1f);

        return (level, level);
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

            Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Tracker, () =>
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

    /// <summary>
    /// Puts a voice on the list, taking the oldest away if there is no room.
    /// </summary>
    /// <remarks>
    /// Oldest first, so voice stealing takes the one that has been ringing longest rather than
    /// the one somebody just asked for. Held under the lock by its callers.
    /// </remarks>
    private void Add(IVoice voice)
    {
        while (_voices.Count >= MaxVoices)
            _voices.RemoveAt(0);

        _voices.Add(voice);
        _snapshotStale = true;
    }

    /// <summary>A different seed per voice, so two noise hits are not the same noise.</summary>
    private int NextSeed() => System.Threading.Interlocked.Increment(ref _noiseSeed);
}
