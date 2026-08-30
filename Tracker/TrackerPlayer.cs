using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JingleBox2.Diagnostics;
using JingleBox2.Audio.Plugins;
using JingleBox2.Audio.Plugins.Bridge;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Tracker.Enums;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Machines.Interfaces;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
/// <remarks>
/// Three locks, and they are separate because they guard three unrelated things: the song and
/// its sequencer, the plugins loaded on the tracks, and the store of decoded recordings. The
/// plugin one is never held across a load, since starting a plugin is another process coming up
/// and a patch of a quarter of a megabyte going into it, and holding the lock across that put
/// the cost on whichever thread asked first, which is the clock.
///
/// A generation number is bumped whenever playback is torn down. A clock thread on its way out
/// can still be mid-callback, and its events must not overwrite the state of a newer run; the
/// plugins have a generation of their own for the same reason, since a plugin started for the
/// last song can still be coming up when the next one opens.
/// </remarks>
public sealed class TrackerPlayer : ITrackerPlayer
{
    /// <summary>The one place that knows both plugin standards. Holds nothing, so one is enough.</summary>
    private readonly IPluginHost _plugins = new PluginHost();

    /// <summary>A chain of effects, written down and read back. Holds nothing, so one is enough.</summary>
    private readonly IPluginChainState _chains = new PluginChainState();

    /// <summary>What the mix adds up to, mute and solo included.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly IMixLevels Levels = new MixLevels();

    /// <summary>Below this the thread spins instead of sleeping, since sleep is not that precise.</summary>
    private const double SpinThresholdSeconds = 0.002;

    /// <summary>How long an audition holds before it releases, since no key is let go of.</summary>
    public const double PreviewHoldSeconds = 0.4;

    /// <summary>The pads' engine, shared rather than a second one opened for the tracker.</summary>
    private readonly IAudioEngine _audio;

    /// <summary>Every recording the instruments play, decoded once.</summary>
    private readonly ISampleStore _samples = new SampleStore();

    /// <summary>The one stream everything sounds through, and the mixer behind it.</summary>
    private readonly Audio.Interfaces.ITrackerOutput _synth = new Audio.TrackerOutput();

    /// <summary>Guards the song and its sequencer, which are replaced whole when a pass starts.</summary>
    private readonly object _lock = new();

    /// <summary>The clock thread, or null when nothing is running.</summary>
    private Thread? _clock;

    /// <summary>How the clock thread is asked to stop, and what it waits on between steps.</summary>
    private CancellationTokenSource? _cancel;

    /// <summary>
    /// Bumped every time playback is torn down. A clock thread that is on its way out can
    /// still be mid-callback, and its events must not overwrite the state of a newer run.
    /// </summary>
    private int _generation;

    /// <summary>What is being played, or null when nothing has been.</summary>
    private Song? _song;

    /// <summary>Its per-track memory, made fresh for each pass.</summary>
    private ITrackerSequencer? _sequencer;

    /// <summary>
    /// What the note itself asked for, per track, kept so the mixer can be re-applied to a voice
    /// that is already sounding: a fader move has to be heard now, not at the next note.
    /// </summary>
    /// <remarks>
    /// Made when a pass starts and empty before then, which is why <see cref="LevelFor"/> is
    /// deliberately not bounded by it: see the remarks there.
    /// </remarks>
    private float[] _noteGain = Array.Empty<float>();

    /// <summary>Where each column's last note asked to be placed, or null for wherever the strip puts it.</summary>
    private float?[] _notePan = Array.Empty<float?>();

    /// <summary>How many note columns the memory has room for on each track.</summary>
    /// <remarks>
    /// The widest a track can be rather than the widest it is, the same as the sequencer's
    /// memory and for the same reason: a column added while the transport runs must not mean an
    /// array rebuilt under the clock thread.
    /// </remarks>
    private const int Columns = Song.MaxNoteColumns;

    /// <summary>How many tracks the memory is made for, which is the song's count.</summary>
    private int Tracks => _noteGain.Length / Columns;

    /// <summary>Where one note column's memory sits in the two arrays above.</summary>
    private static int At(int track, int column) => track * Columns + column;

    /// <summary>
    /// Takes the engine the pads are already using, rather than opening a second one.
    /// </summary>
    /// <remarks>
    /// The watch is started here and runs for the life of the player. A second is often enough
    /// to see a plugin come up, go busy or fall over, and slow enough that it costs nothing
    /// worth measuring. Its own timer, never the audio thread and never the drawing thread:
    /// writing a line of the log is a file opened and closed, and neither of those threads can
    /// afford to wait on a disc.
    /// </remarks>
    /// <param name="audio">The engine everything is rendered through and mixed into.</param>
    /// <param name="machines">
    /// Which machines this installation has, so an instrument whose machine is missing can be
    /// refused rather than played on the engine underneath it. Left out, one that has nothing in
    /// it, which answers that every machine is missing: a player built without being told what is
    /// installed is a player that has not been wired up, and silence says so.
    /// </param>
    public TrackerPlayer(IAudioEngine audio, IMachineProjects? machines = null)
    {
        _audio = audio;
        _machines = machines ?? new Machines.MachineProjects();

        _watch = new System.Threading.Timer(_ => Muster(), null, WatchMilliseconds, WatchMilliseconds);
    }

    /// <summary>Which machines this installation has, asked before anything is allowed to sound.</summary>
    private readonly IMachineProjects _machines;

    /// <summary>How often the plugins are counted and their state written down.</summary>
    private const int WatchMilliseconds = 1000;

    /// <summary>The watch itself, kept so it can be put down with the player.</summary>
    private readonly System.Threading.Timer _watch;

    /// <summary>What was said about each track last time, so a line is written when it changes.</summary>
    private readonly Dictionary<int, string> _mustered = new();

    /// <inheritdoc/>
    public event EventHandler<TrackerPosition>? PositionChanged;

    /// <inheritdoc/>
    public event EventHandler<TrackerTransportState>? StateChanged;

    /// <inheritdoc/>
    public event EventHandler? Stopped;

    /// <inheritdoc/>
    public event EventHandler<(int Track, Note Note, double Seconds)>? NotePlayed;

    /// <inheritdoc/>
    public TrackerTransportState State { get; private set; } = TrackerTransportState.Stopped;

    /// <inheritdoc/>
    public bool IsPlaying => State == TrackerTransportState.Playing;

    /// <inheritdoc/>
    public bool IsPaused => State == TrackerTransportState.Paused;

    /// <inheritdoc/>
    public TrackerPosition Position { get; private set; } = TrackerPosition.Start;

    /// <inheritdoc/>
    /// <remarks>
    /// Volatile, since the clock thread reads it on every line and the drawing thread writes it
    /// whenever the picker moves. See <c>docs/threads.md</c>.
    /// </remarks>
    public TrackerPlayMode Mode
    {
        get => (TrackerPlayMode)Volatile.Read(ref _mode);
        set => Volatile.Write(ref _mode, (int)value);
    }

    /// <summary>Backs <see cref="Mode"/> as an int, which is what can be read atomically.</summary>
    private int _mode = (int)TrackerPlayMode.Song;

    /// <inheritdoc/>
    /// <remarks>
    /// Volatile, and read on every line rather than taken at the start of a pass, so switching
    /// it while something is playing is answered at the end of that pattern. The clock thread
    /// reads it and the drawing thread writes it: see <c>docs/threads.md</c>.
    /// </remarks>
    public bool Loop
    {
        get => Volatile.Read(ref _loop);
        set => Volatile.Write(ref _loop, value);
    }

    /// <summary>Backs <see cref="Loop"/>. On, which is what the transport has always done.</summary>
    private bool _loop = true;

    /// <inheritdoc/>
    public System.Collections.Generic.IReadOnlyCollection<string> FailedInstruments => _samples.FailedPaths;

    /// <inheritdoc/>
    public AutomationPlayer? Automation { get; set; }

    /// <inheritdoc/>
    /// <remarks>
    /// The automation is reset before the first line: the parameters have been moved by hand
    /// since the last pass, so what was written last time is no longer what they hold, and a
    /// lane comparing against it would decline to write the first line.
    ///
    /// The one stream is opened for any song that has an instrument at all, recordings included,
    /// since everything sounds through it now.
    /// </remarks>
    public void Play(Song song, TrackerPosition from, TrackerPlayMode mode = TrackerPlayMode.Song)
    {
        ArgumentNullException.ThrowIfNull(song);

        Teardown();
        _audio.EnsureInitialized();

        lock (_lock)
        {
            _song = song;
            _sequencer = new TrackerSequencer(song.TrackCount);
            _noteGain = new float[song.TrackCount * Columns];
            _notePan = new float?[song.TrackCount * Columns];
            Mode = mode;
            Position = from;
        }

        Automation?.Reset();

        _samples.Preload(song.Instruments);

        if (song.Instruments.Count > 0) _synth.EnsureStarted(_audio);

        ApplyMix();

        StartClock();
    }

    /// <inheritdoc/>
    public void Resume()
    {
        if (State != TrackerTransportState.Paused) return;

        Song? song;
        lock (_lock) song = _song;
        if (song == null) return;

        _audio.EnsureInitialized();
        StartClock();
    }

    /// <inheritdoc/>
    public void Pause()
    {
        if (State != TrackerTransportState.Playing) return;

        Teardown();
        SetState(TrackerTransportState.Paused);
    }

    /// <inheritdoc/>
    public void Stop()
    {
        Teardown();

        bool wasRunning = State != TrackerTransportState.Stopped;
        Position = TrackerPosition.Start;

        SetState(TrackerTransportState.Stopped);
        if (wasRunning) Stopped?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Takes a fresh generation and puts the clock thread on it.
    /// </summary>
    /// <remarks>
    /// Above normal priority, since steps land every few milliseconds and the clock should not
    /// queue behind drawing work. A background thread, so it cannot hold the application open.
    /// </remarks>
    private void StartClock()
    {
        _cancel = new CancellationTokenSource();
        var token = _cancel.Token;
        int generation = Interlocked.Increment(ref _generation);

        SetState(TrackerTransportState.Playing);

        _clock = new Thread(() => RunClock(token, generation))
        {
            IsBackground = true,
            Name = "JingleBox2 tracker clock",
            Priority = ThreadPriority.AboveNormal
        };
        _clock.Start();
    }

    /// <summary>Stops the clock and silences the voices without deciding what state follows.</summary>
    /// <remarks>
    /// A plugin holds its own notes and nothing else will let go of them, so stopping the clock
    /// has to stop the sound too, or a chord hangs on until the application closes.
    /// </remarks>
    private void Teardown()
    {
        Interlocked.Increment(ref _generation);

        var cancel = _cancel;
        var clock = _clock;

        _cancel = null;
        _clock = null;

        cancel?.Cancel();
        if (clock != null && clock != Thread.CurrentThread)
            clock.Join(TimeSpan.FromSeconds(1));

        if (_synth.HasMixer) _synth.Mixer.AllPluginNotesOff();

        cancel?.Dispose();
        StopAllVoices();
    }

    /// <summary>Moves the transport, and says so only when it really moved.</summary>
    private void SetState(TrackerTransportState state)
    {
        if (State == state) return;

        State = state;
        StateChanged?.Invoke(this, state);
    }

    /// <inheritdoc/>
    public void Use(Song song)
    {
        if (song is null) return;

        lock (_lock) _song = song;
    }

    /// <inheritdoc/>
    public void CutPreview(TrackerInstrument? instrument)
    {
        if (instrument == null) return;

        _synth.Mixer.CutAuditions(instrument.Id);
    }

    /// <inheritdoc/>
    public void LetPreview(TrackerInstrument? instrument, Note note)
    {
        if (instrument == null || !note.IsPlayable) return;

        if (instrument.IsPlugin)
        {
            int playing = TrackPlaying(instrument.Id);

            if (playing >= 0) _synth.Mixer.LetPluginNote(playing, note.Semitone);
            else _synth.Mixer.LetPreviewNote(note.Semitone);

            return;
        }

        _synth.Mixer.LetAudition(instrument.Id, note.Semitone);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A plugin is auditioned through the copy already on a track wherever there is one. That is
    /// the copy whose window is open and whose knobs have just been turned; a second copy would
    /// play the sound the song was last saved with and leave you wondering what you changed.
    /// Where the track named has no plugin loaded yet, it is loaded there rather than beside it:
    /// the caller worked this instrument out from that very track, and there is no song to check
    /// against anyway while the transport is stopped.
    ///
    /// An instrument whose machine is not registered here makes no sound at all, and answers
    /// with no length, so nothing lights and nothing waits for it to finish. It is on that
    /// machine, and without it there is nothing here to play.
    ///
    /// A plugin's notes pile up here as every other machine's audition already did, since a
    /// hand plays chords. <see cref="TrackerInstrument.OneVoice"/> is what asks for the other
    /// behaviour, and it is asked here rather than in the mixer because it is a fact about the
    /// instrument and the mixer is handed a track.
    /// </remarks>
    public double Preview(TrackerInstrument instrument, Note note, float gain = 1f, int track = -1,
                          double holdSeconds = PreviewHoldSeconds)
    {
        if (!note.IsPlayable) return 0;

        if (!_machines.Has(instrument.Kind)) return 0;

        _audio.EnsureInitialized();
        _synth.EnsureStarted(_audio);

        Song? song;
        lock (_lock) song = _song;

        var (level, placed) = WithMix(song, track, gain * (float)instrument.Volume, null);
        float pan = placed ?? 0f;

        if (instrument.OneVoice) _synth.Mixer.CutAuditions(instrument.Id);

        if (instrument.IsPlugin)
        {
            int playing = TrackPlaying(instrument.Id);

            if (playing < 0 && track >= 0)
            {
                EnsurePlayerOn(track, instrument);
                playing = TrackPlaying(instrument.Id);
            }

            var ending = instrument.OneVoice ? VoiceEnding.Cut : VoiceEnding.Sustain;

            if (playing >= 0)
            {
                _synth.Mixer.PreviewOnTrack(playing, note, level, holdSeconds, ending, pan);
                return holdSeconds;
            }

            var player = PreviewPlayerFor(instrument);
            if (player == null) return 0;

            _synth.Mixer.PreviewPlugin(note, level, holdSeconds, ending);
            return holdSeconds;
        }

        if (instrument.IsSampler)
        {
            var zone = instrument.Zones?.For(note);
            var zoneSample = zone == null ? null : _samples.Load(zone.FilePath);

            if (zone == null || zoneSample == null) return 0;

            return _synth.Mixer.Preview(
                zone, instrument.Sampler ?? new Synth.SamplerPatch(), zoneSample, note,
                (float)(level * zone.Volume), holdSeconds, instrument.Id, track, pan);
        }

        if (instrument.IsKit)
        {
            var pad = instrument.Kit?.For(note);
            var padSample = pad == null ? null : _samples.Load(pad.FilePath);

            if (pad == null || padSample == null) return 0;

            return _synth.Mixer.Preview(
                pad, instrument.Patch, padSample, note,
                (float)(level * pad.Volume), holdSeconds, instrument.Id, track, pan);
        }

        if (instrument.IsMonoSynth)
        {
            _synth.Mixer.Preview(instrument.MonoSynth ?? new Synth.MonoSynthPatch(),
                note, level, holdSeconds, instrument.Id, track, pan);
            return holdSeconds;
        }

        if (instrument.IsSynth)
        {
            _synth.Mixer.Preview(instrument.Patch, note, level, holdSeconds, instrument.Id, track, pan);
            return holdSeconds;
        }

        var sample = _samples.Load(instrument.FilePath);
        if (sample == null) return 0;

        return _synth.Mixer.Preview(instrument, sample, note, level, holdSeconds, instrument.Id, track, pan);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Bounded by <see cref="Song.MaxTrackCount"/> and deliberately not by <c>_noteGain</c>,
    /// which is the volume column's memory and is only made when a pass starts. Asking that
    /// array how many tracks there are answered nought until somebody pressed play, so every
    /// track's meter read nought while the transport was stopped and a note played by hand moved
    /// the master's meter, which goes through the branch above this one, and no track's. It
    /// looked like the tracks were not isolated when what was really happening is that nobody
    /// was asking them.
    ///
    /// No mixer is made here. This is asked several times a second by the meters, and building
    /// one would fix the rate before the device is even open.
    /// </remarks>
    public (float Left, float Right) LevelFor(int track)
    {
        if (!_synth.HasMixer) return (0, 0);

        if (track == MasterStrip) return _synth.Mixer.MasterLevel;

        if (track < 0 || track >= Song.MaxTrackCount) return (0, 0);

        var (left, right) = _synth.Mixer.LevelFor(track);

        return (Math.Clamp(left, 0f, 1f), Math.Clamp(right, 0f, 1f));
    }

    /// <summary>
    /// The largest block a plugin is asked to handle in one go. The audio callback's blocks
    /// are whatever the device asks for; anything longer than this is fed through in pieces.
    /// </summary>
    public const int MaxPluginFrames = 2048;

    /// <inheritdoc/>
    public int SampleRate => _synth.SampleRate;

    /// <inheritdoc/>
    public void UseSampleRate(int rate) => _synth.UseSampleRate(rate);

    /// <inheritdoc/>
    public void UseRenderAhead(int milliseconds) => _synth.UseRenderAhead(milliseconds);

    /// <inheritdoc/>
    public void UseSizes(Audio.Records.AudioSizes sizes) => _synth.UseSizes(sizes);

    /// <inheritdoc/>
    public void RestartOutput() => _synth.Restart(_audio);

    /// <summary>
    /// The strip that is not a track: the whole mix, after all of them.
    /// </summary>
    /// <remarks>
    /// Minus one rather than a number past the last track, so it cannot be reached by counting
    /// and cannot collide with a song that grows another track. Everything that walks the strips
    /// walks the tracks and then this.
    /// </remarks>
    public const int MasterStrip = -1;

    /// <inheritdoc/>
    public PluginChain ChainFor(int track)
    {
        if (InsertOn(track) is PluginChain existing) return existing;

        EnsureEngine();

        var chain = new PluginChain();

        if (track < 0) _synth.Mixer.SetMasterInsert(chain);
        else _synth.Mixer.SetInsert(track, chain);

        return chain;
    }

    /// <summary>What is on a strip, whether that strip is a track or the master.</summary>
    private IAudioInsert? InsertOn(int track) =>
        track < 0
            ? (_synth.HasMixer ? _synth.Mixer.MasterInsert : null)
            : (_synth.HasMixer ? _synth.Mixer.InsertOn(track) : null);

    /// <summary>The settings of one strip, the master included.</summary>
    private static TrackMix? StripOf(Song song, int track) =>
        track < 0 ? song.Master : track < song.Mix.Count ? song.Mix[track] : null;

    /// <summary>Every strip a song has, its tracks and then the master.</summary>
    private static IEnumerable<int> StripsOf(Song song)
    {
        for (int track = 0; track < song.Mix.Count; track++) yield return track;

        yield return MasterStrip;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A strip with nothing on it is written as null rather than as an empty list, so a song
    /// with no effects is not full of empty chains.
    /// </remarks>
    public void CaptureChains(Song song, bool patches = true)
    {
        if (song == null) return;

        foreach (int track in StripsOf(song))
        {
            if (StripOf(song, track) is not { } strip) continue;

            var chain = InsertOn(track) as PluginChain;
            var captured = _chains.Capture(chain, patches);

            strip.Plugins = captured.IsEmpty ? null : captured;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Nothing loaded and nothing wanted costs nothing, which matters most for the master:
    /// almost no song has an effect on it and none should pay for a chain.
    /// </remarks>
    public IReadOnlyList<string> RestoreChains(Song song)
    {
        var missing = new List<string>();
        if (song == null) return missing;

        foreach (int track in StripsOf(song))
        {
            if (StripOf(song, track) is not { } strip) continue;

            if (strip.Plugins is null or { IsEmpty: true } && InsertOn(track) is null) continue;

            var chain = ChainFor(track);
            missing.AddRange(_chains.Restore(
                chain, strip.Plugins, _synth.SampleRate, MaxPluginFrames));
        }

        return missing;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Compared as the two would be written down, so nothing can be forgotten: an ordering, a
    /// parameter, a plugin swapped for another of the same name. Without the patches, since
    /// nothing here is being saved and a chain is what its description says rather than what its
    /// plugins happen to be holding.
    ///
    /// Nothing loaded and nothing wanted is the ordinary case and costs nothing: no chain is
    /// made for a strip that has never had one.
    /// </remarks>
    public IReadOnlyList<int> MatchChains(Song song)
    {
        var changed = new List<int>();
        if (song == null) return changed;

        foreach (int track in StripsOf(song))
        {
            var wanted = StripOf(song, track)?.Plugins;

            var chain = InsertOn(track) as PluginChain;

            if (chain is null && (wanted is null || wanted.IsEmpty)) continue;

            var loaded = _chains.Capture(chain);

            if (Same(loaded, wanted)) continue;

            _chains.Restore(ChainFor(track), wanted, _synth.SampleRate, MaxPluginFrames);

            changed.Add(track);
        }

        return changed;
    }

    /// <summary>True when two chains describe the same devices with the same settings.</summary>
    /// <remarks>
    /// The plugins' own patches are left out of it. One side is read off what is loaded and the
    /// other comes out of a history step, and a plugin asked for its lump twice is under no
    /// obligation to answer the same bytes, so comparing them would report every chain as
    /// changed and rebuild all of them on every undo.
    ///
    /// Unreadable either way is a reason to rebuild, not a reason to stop.
    /// </remarks>
    private static bool Same(PluginChainConfig? left, PluginChainConfig? right)
    {
        bool nothingLeft = left is null || left.IsEmpty;
        bool nothingRight = right is null || right.IsEmpty;

        if (nothingLeft || nothingRight) return nothingLeft && nothingRight;

        try
        {
            return System.Text.Json.JsonSerializer.Serialize(left!.Described())
                   == System.Text.Json.JsonSerializer.Serialize(right!.Described());
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// The plugin playing each track, and which instrument it is. A track holds on to its
    /// plugin between notes because a plugin has a release to finish.
    /// </summary>
    private readonly Dictionary<int, (string Instrument, IPluginInstrument Plugin)> _players = new();

    /// <summary>Guards the players, the ones coming up, and the auditioned one. Never held across a load.</summary>
    private readonly object _playerLock = new();

    /// <summary>
    /// The plugins being started right now, by track and by which instrument they are.
    /// </summary>
    /// <remarks>
    /// Loading happens outside the lock, so two things can ask a track for its plugin while it
    /// is still coming up: the song opening and starting them all, and the first note arriving
    /// before that has finished. Without this they each start a copy, which for a plugin means
    /// a second process, a second set of wavetables, and one of them thrown away. Whoever asks
    /// second waits on the one already running instead.
    /// </remarks>
    private readonly Dictionary<int, (string Instrument, Task<IPluginInstrument?> Loading)> _loading = new();

    /// <summary>
    /// Which set of players is the current one. Bumped whenever they are all put down.
    /// </summary>
    /// <remarks>
    /// Opening a song puts the last song's plugins down, and a plugin started for the last
    /// song can still be coming up when that happens. Installed after the fact it would be an
    /// instrument nobody asked for, playing under the new song's notes on a track that means
    /// something else now. So a plugin that comes up into a different generation goes straight
    /// back down.
    /// </remarks>
    private int _playerGeneration;

    /// <inheritdoc/>
    /// <remarks>
    /// The table of players is rebuilt rather than edited in place: every key from the moved one
    /// onwards changes, so editing while walking it would trip over its own renumbering.
    /// </remarks>
    public void MoveTrack(int from, int to)
    {
        if (from == to) return;
        if (from < 0 || from >= Tracks || to < 0 || to >= Tracks) return;

        lock (_playerLock)
        {
            var moved = new Dictionary<int, (string Instrument, IPluginInstrument Plugin)>();

            foreach (var (track, loaded) in _players)
                moved[Song.WhereTrackWent(track, from, to)] = loaded;

            _players.Clear();
            foreach (var (track, loaded) in moved) _players[track] = loaded;
        }

        ShiftColumns(_noteGain, from, to);
        ShiftColumns(_notePan, from, to);

        _synth.Mixer.MoveTrack(from, to);

        Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Tracker, () =>
            "track " + from + " moved to " + to + ", with its plugin, its effects and its levels");
    }

    /// <summary>One track's worth of per-track state, moved the way the song moves it.</summary>
    private static void Shift<T>(T[] values, int from, int to)
    {
        var moved = values[from];

        int step = from < to ? 1 : -1;
        for (int track = from; track != to; track += step) values[track] = values[track + step];

        values[to] = moved;
    }

    /// <summary>The same, for the memory that holds a block of note columns per track.</summary>
    /// <remarks>
    /// A track's columns travel with it, so the block moves whole. Written out rather than
    /// reusing the walk above, because that one moves single entries and a block move that
    /// pretended to be one would silently interleave two tracks' columns.
    /// </remarks>
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
    public void EnsureEngine()
    {
        _audio.EnsureInitialized();
        _synth.EnsureStarted(_audio);
    }

    /// <inheritdoc/>
    public IPluginInstrument? EnsurePlayerOn(int track, TrackerInstrument instrument)
    {
        EnsureEngine();

        return PlayerFor(track, instrument);
    }

    /// <summary>
    /// The plugin on a track, started if it is not already there.
    /// </summary>
    /// <remarks>
    /// Starting one is not quick. It is another process, a plugin binary of its own, and a
    /// patch of a quarter of a megabyte to swallow before it will make the right sound. Held
    /// under the lock, that cost fell on whichever thread asked first, which is the clock, and
    /// every other track's first note queued behind it. So the lock is taken twice around the
    /// slow part and not across it: once to see whether there is anything to do, and once to
    /// put down what came back.
    ///
    /// Which means two callers can arrive at the same track at once, and <see cref="_loading"/>
    /// is what stops that from starting the plugin twice. It is also what lets a song start
    /// all of its plugins side by side, in <see cref="PreloadPlugins"/>.
    ///
    /// Started on a thread of its own rather than a pool one. Starting a plugin is mostly
    /// waiting on another process, and a song's worth of them waiting at once on pool threads is
    /// a pool with nothing left to run the note that is waiting for one of them.
    ///
    /// The caller wanted a plugin and is entitled to wait for one. What it is not entitled to is
    /// holding the lock while it waits, which is the whole of the arrangement.
    /// </remarks>
    private IPluginInstrument? PlayerFor(int track, TrackerInstrument instrument)
    {
        if (track < 0 || instrument == null || !instrument.IsPlugin) return null;
        if (instrument.Plugin == null) return null;

        Task<IPluginInstrument?> loading;

        lock (_playerLock)
        {
            if (_players.TryGetValue(track, out var existing)
                && string.Equals(existing.Instrument, instrument.Id, StringComparison.Ordinal))
                return existing.Plugin;

            if (_loading.TryGetValue(track, out var running)
                && string.Equals(running.Instrument, instrument.Id, StringComparison.Ordinal))
            {
                loading = running.Loading;
            }
            else
            {
                int generation = _playerGeneration;

                loading = Task.Factory.StartNew(
                    () => Start(track, instrument, generation),
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

                _loading[track] = (instrument.Id, loading);
            }
        }

        return loading.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Brings one plugin up and puts it on its track. Runs off the lock, and off whatever
    /// thread asked.
    /// </summary>
    /// <remarks>
    /// The patch goes in before the first note, or the first note is the wrong sound. A plugin
    /// that will not start is a silent track, not a silent application.
    ///
    /// Three things can be true by the time it is up, and all three are ordinary. The song it
    /// was being started for may not be the song any more, in which case it goes straight back
    /// down. Somebody may have put this very plugin on the track while it was coming up, and
    /// theirs is the one the mixer is already playing, so this one goes down instead. Or a
    /// different instrument may be on the track, and the old one comes off the mix before it is
    /// put down, or it plays into a bus that is about to be somebody else's.
    /// </remarks>
    private IPluginInstrument? Start(int track, TrackerInstrument instrument, int generation)
    {
        var description = instrument.Plugin;
        if (description == null) return null;

        IPluginInstrument? player = null;

        try
        {
            player = _plugins.LoadInstrument(description, _synth.SampleRate, MaxPluginFrames);
            if (player == null) return null;

            player.LoadState(instrument.PluginState);
        }
        catch (Exception)
        {
            try { player?.Dispose(); } catch (Exception) { }
            player = null;
        }

        lock (_playerLock)
        {
            if (_loading.TryGetValue(track, out var mine)
                && string.Equals(mine.Instrument, instrument.Id, StringComparison.Ordinal))
                _loading.Remove(track);

            if (player == null) return null;

            if (generation != _playerGeneration)
            {
                var late = player;
                Task.Run(() => { try { late.Dispose(); } catch (Exception) { } });
                return null;
            }

            if (_players.TryGetValue(track, out var existing))
            {
                if (string.Equals(existing.Instrument, instrument.Id, StringComparison.Ordinal))
                {
                    var spare = player;
                    Task.Run(() => { try { spare.Dispose(); } catch (Exception) { } });
                    return existing.Plugin;
                }

                _synth.Mixer.SetInstrument(track, null);
                existing.Plugin.Dispose();
                _players.Remove(track);
            }

            _players[track] = (instrument.Id, player);
            _synth.Mixer.SetInstrument(track, player);

            return player;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The engine is opened first, and only where there is something to start. A plugin is
    /// opened at a sample rate and keeps it, so the rate has to be the real one before any of
    /// them start; a song of recordings and synths does not turn the device on early for the
    /// sake of it.
    /// </remarks>
    public void PreloadPlugins(Song song)
    {
        if (song == null) return;

        var wanted = new List<(int Track, TrackerInstrument Instrument)>();

        for (int track = 0; track < song.TrackCount; track++)
        {
            var instrument = song.InstrumentAt(song.GetTrackInstrument(track));
            if (instrument == null || !instrument.IsPlugin || instrument.Plugin == null) continue;

            wanted.Add((track, instrument));
        }

        if (wanted.Count == 0) return;

        EnsureEngine();

        foreach (var (track, instrument) in wanted)
        {
            int which = track;
            var playing = instrument;

            Task.Factory.StartNew(
                () =>
                {
                    try { PlayerFor(which, playing); }
                    catch (Exception) { }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }
    }

    /// <summary>The track already playing this instrument, or -1 when none is.</summary>
    private int TrackPlaying(string instrumentId)
    {
        if (string.IsNullOrEmpty(instrumentId)) return -1;

        lock (_playerLock)
        {
            foreach (var (track, loaded) in _players)
            {
                if (string.Equals(loaded.Instrument, instrumentId, StringComparison.Ordinal)) return track;
            }
        }

        return -1;
    }

    /// <inheritdoc/>
    public IPluginInstrument? PlayerOn(int track)
    {
        lock (_playerLock) return _players.TryGetValue(track, out var found) ? found.Plugin : null;
    }

    /// <summary>
    /// The plugin used for auditioning, which belongs to no track. One at a time: opening a
    /// second instrument in the editor puts the first one down.
    /// </summary>
    private (string Instrument, IPluginInstrument Plugin)? _auditioned;

    /// <inheritdoc/>
    public IPluginInstrument? PreviewPlayerFor(TrackerInstrument instrument)
    {
        if (instrument == null || !instrument.IsPlugin) return null;

        int onTrack = TrackPlaying(instrument.Id);

        if (onTrack >= 0)
        {
            var playing = PlayerOn(onTrack);
            if (playing != null) return playing;
        }

        lock (_playerLock)
        {
            if (_auditioned is { } current)
            {
                if (string.Equals(current.Instrument, instrument.Id, StringComparison.Ordinal)) return current.Plugin;

                _synth.Mixer.SetPreviewInstrument(null);
                current.Plugin.Dispose();
                _auditioned = null;
            }

            var description = instrument.Plugin;
            if (description == null) return null;

            var player = _plugins.LoadInstrument(description, _synth.SampleRate, MaxPluginFrames);
            if (player == null) return null;

            player.LoadState(instrument.PluginState);

            _auditioned = (instrument.Id, player);
            _synth.Mixer.SetPreviewInstrument(player);

            return player;
        }
    }

    /// <inheritdoc/>
    public void ClearPreviewPlayer()
    {
        IPluginInstrument? leaving;

        lock (_playerLock)
        {
            leaving = _auditioned?.Plugin;
            _auditioned = null;
        }

        if (leaving == null) return;

        _synth.Mixer.SetPreviewInstrument(null);
        leaving.Dispose();
    }

    /// <inheritdoc/>
    public void ClearPlayers()
    {
        (string Instrument, IPluginInstrument Plugin)[] leaving;

        lock (_playerLock)
        {
            leaving = _players.Values.ToArray();

            foreach (var track in _players.Keys) _synth.Mixer.SetInstrument(track, null);

            _players.Clear();
            _loading.Clear();
            _playerGeneration++;
        }

        foreach (var (_, plugin) in leaving) plugin.Dispose();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The chain is emptied by hand rather than by <c>Clear</c>, which drops the devices without
    /// putting them down: that would leave the processes running with nothing holding them,
    /// which is the leak this is supposed to be the opposite of.
    /// </remarks>
    public void LetGoOfPlugins(Song song)
    {
        if (song == null) return;

        CaptureChains(song);

        ClearPlayers();
        ClearPreviewPlayer();

        for (int track = 0; track < song.Mix.Count; track++)
        {
            if (_synth.Mixer.InsertOn(track) is not PluginChain chain) continue;

            var leaving = chain.Devices.ToArray();

            chain.Clear();

            foreach (var device in leaving)
            {
                try { (device.Insert as IDisposable)?.Dispose(); }
                catch (Exception) { }
            }
        }

        Log.Write(LogArea.Tracker, () => "let go of the plugins '" + song.Name + "' was holding");
    }

    /// <inheritdoc/>
    public void TakeUpPlugins(Song song)
    {
        if (song == null) return;

        RestoreChains(song);
        PreloadPlugins(song);
    }

    /// <inheritdoc/>
    public void ReloadInstrument(string filePath) => _samples.Invalidate(filePath);

    /// <inheritdoc/>
    public double SamplePosition(int track) => _synth.Mixer.SamplePosition(track);

    /// <inheritdoc/>
    public double OutputLevel => _synth.Level;

    /// <summary>
    /// The clock: one step a line, until it is stopped or the song runs out.
    /// </summary>
    /// <remarks>
    /// The automation is written before the notes, and that is the whole of the ordering
    /// question: a note landing on a line where the filter also moves should be played through
    /// the filter as the line leaves it, not as the line before it left it.
    ///
    /// The tempo is read per step rather than once. A tempo moved while playing has to be heard
    /// on the next line, not at the next take; the times are still absolute from the start, so
    /// the clock does not drift and a change simply lengthens or shortens the steps from there
    /// on. The song's tempo is written by the drawing thread while this one reads it, which is
    /// why every use goes through the clamped timing: even a value caught mid-write can only be
    /// a tempo, never a stall or a division by nought.
    ///
    /// Running off the end is not the same as being stopped, and only the first is this
    /// thread's business to report. A newer run may already have started, in which case this
    /// thread has none.
    /// </remarks>
    /// <remarks>
    /// The mode is read on every line rather than taken once at the top, so switching between
    /// looping a pattern and playing the song is answered on the next line. Taken once, the
    /// picker changed nothing until the transport was stopped and started again, which reads as
    /// a song that will not move past its first pattern.
    /// </remarks>
    private void RunClock(CancellationToken token, int generation)
    {
        Song song;
        ITrackerSequencer sequencer;
        lock (_lock)
        {
            if (_song == null || _sequencer == null) return;
            song = _song;
            sequencer = _sequencer;
        }

        var clock = Stopwatch.StartNew();

        var position = Position;
        double nextLine = 0;

        while (!token.IsCancellationRequested)
        {
            if (generation != Volatile.Read(ref _generation)) return;

            Automation?.Play(song, position);

            ApplyEvents(sequencer.EventsFor(song, position), song);
            Position = position;
            PositionChanged?.Invoke(this, position);

            var next = Mode == TrackerPlayMode.Pattern
                ? TrackerSequencer.AdvanceWithinPattern(song, position, Loop)
                : TrackerSequencer.Advance(song, position, Loop);

            if (next == null) break;
            position = next.Value;

            nextLine += song.Timing.SecondsPerLine;
            if (!WaitUntil(clock, nextLine, token)) return;
        }

        if (!token.IsCancellationRequested && generation == Volatile.Read(ref _generation))
        {
            StopAllVoices();
            Position = TrackerPosition.Start;
            SetState(TrackerTransportState.Stopped);
            Stopped?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Sleeps until the step is due, then spins out the last couple of milliseconds.</summary>
    /// <remarks>
    /// Sleep is not precise enough to land a step on, and spinning the whole wait would burn a
    /// core. The wait handle is the cancellation token's, so a stop is answered at once rather
    /// than at the end of the sleep.
    /// </remarks>
    /// <returns>False when it was cancelled rather than reaching the time.</returns>
    private static bool WaitUntil(Stopwatch clock, double targetSeconds, CancellationToken token)
    {
        while (true)
        {
            if (token.IsCancellationRequested) return false;

            double remaining = targetSeconds - clock.Elapsed.TotalSeconds;
            if (remaining <= 0) return true;

            if (remaining > SpinThresholdSeconds)
            {
                if (token.WaitHandle.WaitOne(TimeSpan.FromSeconds(remaining - SpinThresholdSeconds)))
                    return false;
            }
            else
            {
                Thread.SpinWait(50);
            }
        }
    }

    /// <summary>
    /// Plays one step's events. Anything for a track this pass does not have is dropped rather
    /// than reaching past the end of the per-track arrays.
    /// </summary>
    private void ApplyEvents(System.Collections.Generic.IReadOnlyList<TrackerEvent> events, Song song)
    {
        foreach (var e in events)
        {
            if (e.Track < 0 || e.Track >= Tracks || e.Column < 0 || e.Column >= Columns) continue;

            switch (e.Kind)
            {
                case TrackerEventKind.Stop:
                    _synth.Mixer.NoteOff(e.Track, e.Column);
                    _synth.Mixer.PluginNoteOff(e.Track, e.Column);

                    NotePlayed?.Invoke(this, (e.Track, Note.Off, 0d));
                    break;

                case TrackerEventKind.Trigger:
                    Trigger(e, song);
                    break;

                case TrackerEventKind.Adjust:
                    Adjust(e, song);
                    break;
            }
        }
    }

    /// <summary>
    /// Starts a note on a track, on whichever machine that track's instrument is.
    /// </summary>
    /// <remarks>
    /// Room is made on the track first, and what that means is the instrument's to say through
    /// <see cref="TrackerInstrument.NewNoteAction"/>: cut, which is what a tracker has always
    /// done, release, or nothing at all. A plugin holds its own notes, so the track's voices
    /// are let go rather than left ringing underneath it.
    ///
    /// The note is announced once, before the kinds part company: a note played on a plugin is
    /// as much a note this track played as one played on Ouroboros. With no length, since a note
    /// in a pattern lasts until whatever the track plays next and that has not happened yet.
    ///
    /// An instrument whose machine is not registered here is one of those failures. It is on
    /// that machine, and goes on naming it until the track is pointed at another instrument, so
    /// nothing sounds and the line says why.
    ///
    /// Every way of failing writes a line saying which, because from outside they are all the
    /// same thing: a track that did not sound.
    /// </remarks>
    private void Trigger(TrackerEvent e, Song song)
    {
        var instrument = song.InstrumentAt(e.Instrument);

        if (instrument == null)
        {
            Where(e.Track, e.Instrument, null, song, "there is no such instrument in the song");
            return;
        }

        if (!_machines.Has(instrument.Kind))
        {
            Where(e.Track, e.Instrument, instrument, song, "its machine is not installed here");
            return;
        }

        var (gain, pan) = LevelsFor(e, instrument);

        _noteGain[At(e.Track, e.Column)] = gain;
        _notePan[At(e.Track, e.Column)] = pan;

        var (mixed, placed) = WithMix(song, e.Track, gain, pan);

        NotePlayed?.Invoke(this, (e.Track, e.Note, 0d));

        if (instrument.IsPlugin)
        {
            LetGo(instrument, e.Track, e.Column);

            if (PlayerFor(e.Track, instrument) != null)
            {
                Where(e.Track, e.Instrument, instrument, song, "sent to its plugin");
                _synth.Mixer.PluginNoteOn(e.Track, e.Column, e.Note, mixed, placed ?? 0f,
                    instrument.NewNoteAction);
            }
            else
            {
                Where(e.Track, e.Instrument, instrument, song, "its plugin would not load, so nothing was played");
            }

            return;
        }

        if (instrument.IsSampler)
        {
            var zone = instrument.Zones?.For(e.Note);

            if (zone == null)
            {
                Where(e.Track, e.Instrument, instrument, song, "no zone on its map answers to that note");
                LetGo(instrument, e.Track, e.Column);
                return;
            }

            var zoneSample = _samples.Load(zone.FilePath);

            if (zoneSample == null)
            {
                Where(e.Track, e.Instrument, instrument, song, "its zone's recording would not load");
                LetGo(instrument, e.Track, e.Column);
                return;
            }

            Where(e.Track, e.Instrument, instrument, song, "played on " + instrument.Machine.Name);

            _synth.Mixer.NoteOn(
                e.Track, e.Column, zone, instrument.Sampler ?? new Synth.SamplerPatch(), zoneSample,
                e.Note, (float)(mixed * zone.Volume), Placed(placed, zone.Pan),
                instrument.NewNoteAction);

            return;
        }

        if (instrument.IsKit)
        {
            var pad = instrument.Kit?.For(e.Note);

            if (pad == null)
            {
                Where(e.Track, e.Instrument, instrument, song, "no pad on its kit answers to that note");
                return;
            }

            var padSample = _samples.Load(pad.FilePath);

            if (padSample == null)
            {
                Where(e.Track, e.Instrument, instrument, song, "its pad's recording would not load");
                return;
            }

            Where(e.Track, e.Instrument, instrument, song, "played on " + instrument.Machine.Name);

            _synth.Mixer.NoteOn(
                e.Track, e.Column, pad, instrument.Patch, padSample, e.Note,
                (float)(mixed * pad.Volume), Placed(placed, pad.Pan));

            return;
        }

        if (instrument.IsMonoSynth)
        {
            Where(e.Track, e.Instrument, instrument, song, "played on " + instrument.Machine.Name);
            _synth.Mixer.NoteOn(e.Track, e.Column, instrument.MonoSynth ?? new Synth.MonoSynthPatch(),
                e.Note, mixed, placed ?? 0f, instrument.NewNoteAction);
            return;
        }

        if (instrument.IsSynth)
        {
            Where(e.Track, e.Instrument, instrument, song, "played as a synth voice");
            _synth.Mixer.NoteOn(e.Track, e.Column, instrument.Patch, e.Note, mixed, placed ?? 0f,
                instrument.NewNoteAction);
            return;
        }

        var sample = _samples.Load(instrument.FilePath);
        if (sample == null)
        {
            Where(e.Track, e.Instrument, instrument, song, "its recording would not load, so nothing was played");
            LetGo(instrument, e.Track, e.Column);
            return;
        }

        Where(e.Track, e.Instrument, instrument, song, "played as a recording");
        _synth.Mixer.NoteOn(e.Track, e.Column, instrument, sample, e.Note, mixed, placed ?? 0f);
    }

    /// <summary>
    /// Lets go of what a track was sounding where the note meant to follow it is not going to
    /// be added to that track's voices.
    /// </summary>
    /// <remarks>
    /// Two of those: a note going to the track's plugin instead, which holds its own, and a
    /// note that could not be played at all. Both used to end the track's voices outright, and
    /// still do, because a track sounding the tail of an instrument it is no longer pointed at
    /// is not something anybody asked for.
    ///
    /// Nothing is let go of under <see cref="VoiceEnding.Sustain"/>. That is the whole of what
    /// sustain asks for, and a note that could not be played is not a reason to end the ones
    /// that could.
    /// </remarks>
    private void LetGo(TrackerInstrument instrument, int track, int column)
    {
        if (instrument.NewNoteAction == VoiceEnding.Sustain) return;

        _synth.Mixer.NoteOff(track, column);
    }

    /// <summary>What the last note on each track was addressed to, so it is said once a second.</summary>
    private readonly int[] _lastAddressed = new int[Song.MaxTrackCount];

    /// <summary>And where that note actually went, in the words the line will use.</summary>
    private readonly string[] _lastWent = new string[Song.MaxTrackCount];

    /// <summary>How many notes each track has played since the last line was written.</summary>
    private readonly int[] _triggers = new int[Song.MaxTrackCount];

    /// <summary>
    /// When the last line was written. A note per line per track is a log nobody reads, so the
    /// whole picture is written once a second instead.
    /// </summary>
    private long _toldWhere;

    /// <summary>
    /// Where a track's notes are actually going, and what that track's own instrument is.
    /// </summary>
    /// <remarks>
    /// A cell names the instrument it wants, and a track separately has one bound to it. The
    /// two can drift apart: putting an instrument on a track takes it off whatever track it
    /// was on, and nothing rewrites the cells that were already typed. When they disagree the
    /// notes go to the instrument the cells name and the track's own instrument is never
    /// played, which sounds exactly like a plugin that has stopped working. So the line says
    /// both, and says so plainly when they are not the same.
    /// </remarks>
    private void Where(int track, int addressed, TrackerInstrument? instrument, Song song, string went)
    {
        if (!Diagnostics.Log.On(Diagnostics.Enums.LogArea.Tracker) || track < 0 || track >= _lastAddressed.Length) return;

        _lastAddressed[track] = addressed;
        _lastWent[track] = went;
        _triggers[track]++;

        long now = Environment.TickCount64;
        if (now - _toldWhere < 1000) return;

        _toldWhere = now;

        for (int line = 0; line < _lastAddressed.Length; line++)
        {
            if (_triggers[line] == 0) continue;

            int number = line;
            int wanted = _lastAddressed[line];
            string ending = _lastWent[line];
            int count = _triggers[line];

            _triggers[line] = 0;

            int bound = song.TrackInstruments.Count > number ? song.TrackInstruments[number] : -1;
            var boundTo = bound >= 0 ? song.InstrumentAt(bound) : null;
            var wantedTo = number == track ? instrument : song.InstrumentAt(wanted);

            Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Tracker, () =>
                "track " + number + ": " + count + " notes in the last second, the last one asking for " +
                "instrument " + wanted.ToString("00") + " (" + (wantedTo?.Name ?? "none") + "), " + ending +
                "; this track's own instrument is " +
                (bound < 0 ? "none" : bound.ToString("00") + " (" + (boundTo?.Name ?? "none") + ")") +
                (bound >= 0 && bound != wanted
                    ? "  <-- THE CELLS AND THE TRACK DISAGREE, so " + (boundTo?.Name ?? "it") + " is never played"
                    : ""));
        }
    }

    /// <summary>
    /// Moves the level or the placement of what a track is already sounding, for a cell with a
    /// volume or effect column and no note in it.
    /// </summary>
    private void Adjust(TrackerEvent e, Song song)
    {
        var instrument = song.InstrumentAt(e.Instrument);
        var (gain, pan) = LevelsFor(e, instrument);

        _noteGain[At(e.Track, e.Column)] = gain;
        _notePan[At(e.Track, e.Column)] = pan;

        var (mixed, placed) = WithMix(song, e.Track, gain, pan);

        _synth.Mixer.SetLevels(e.Track, e.Column, mixed, placed);
        _synth.Mixer.SetPluginLevels(e.Track, mixed, placed);
    }

    /// <summary>
    /// Where a pad sits, once the track's own placement has had its say.
    /// </summary>
    /// <remarks>
    /// A pad's pan is where it stands on the kit, and the track's is where the kit stands in
    /// the mix, so the two add rather than one replacing the other. Held inside the field, so
    /// a kit panned hard right cannot push a pad past the wall.
    /// </remarks>
    private static float Placed(float? track, double pad) =>
        (float)Math.Clamp((track ?? 0f) + pad, -1.0, 1.0);

    /// <summary>
    /// Puts a note's own level through the track's strip. The cell's pan effect wins when it
    /// set one: an effect written into the pattern is a decision about that note.
    /// </summary>
    /// <remarks>
    /// A note belonging to no track, which is what a machine's own keyboard plays, goes through
    /// nobody's strip: the instrument it is sounding may not be in any song. So does a note
    /// played before the player has been told which song is open, which is a state nothing
    /// should be in and is answered rather than thrown at.
    ///
    /// Auditions go through this too, and did not until a chord made it obvious: the fader and
    /// the mute were applied to a pattern note and not to the same note played by hand, so a
    /// muted track still sounded under your hands and a fader anywhere but unity auditioned at
    /// a level the part would never play at. The whole point of auditioning is that it tells
    /// you what the part will sound like.
    /// </remarks>
    private static (float Gain, float? Pan) WithMix(Song? song, int track, float gain, float? pan)
    {
        if (song is null || track < 0) return (Math.Clamp(gain, 0f, MaxGain), pan);

        float mixed = Math.Clamp(gain * Levels.GainFor(song.Mix, track), 0f, MaxGain);

        return (mixed, pan ?? Levels.PanFor(song.Mix, track));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The side chain is part of the strip, so it is pushed with the rest of it rather than
    /// waiting for the next note.
    ///
    /// Then the strip everything has already been through. Muted is nothing rather than a level,
    /// the same as a track: a fader pulled to the bottom and a mute are two different gestures
    /// and only one of them is remembered when it is undone.
    /// </remarks>
    public void ApplyMix()
    {
        Song? song;
        lock (_lock) song = _song;
        if (song == null) return;

        for (int track = 0; track < Tracks; track++)
        {
            for (int column = 0; column < song.ColumnsOn(track); column++)
            {
                var (mixed, placed) = WithMix(
                    song, track, _noteGain[At(track, column)], _notePan[At(track, column)]);

                _synth.Mixer.SetLevels(track, column, mixed, placed);
            }

            _synth.Mixer.SetDucking(
                track,
                Levels.DuckFor(song.Mix, track, song.TrackCount),
                Levels.KeyFor(song.Mix, track, song.TrackCount),
                Levels.DuckReleaseFor(song.Mix, track));
        }

        var master = song.Master;

        _synth.Mixer.SetMaster(
            master.Mute ? 0f : (float)master.Volume,
            (float)master.Pan);
    }

    /// <summary>An instrument can be pushed past unity, so the ceiling is not one.</summary>
    private const float MaxGain = 2f;

    /// <summary>
    /// The level and placement a voice should have, from the cell and the instrument. Shared by
    /// every machine, so the volume column means the same thing whichever one is playing.
    /// </summary>
    /// <remarks>
    /// The effect column wins over the volume column when both set the same thing, since an
    /// effect written into the pattern is a decision about that note.
    ///
    /// The pan effect reads the way a tracker's always has: 00 hard left, 40 centre, 80 hard
    /// right, which is why the middle is 64 rather than half of the parameter's range.
    /// </remarks>
    private static (float Gain, float? Pan) LevelsFor(TrackerEvent e, TrackerInstrument? instrument)
    {
        float gain = (e.Gain ?? 1f) * (float)(instrument?.Volume ?? 1.0);

        if (e.Effect.Command == TrackerEffect.SetVolume)
            gain = Math.Clamp(e.Effect.Parameter / (float)TrackerCell.MaxVolume, 0f, 1f);

        float? pan = null;
        if (e.Effect.Command == TrackerEffect.SetPan)
        {
            pan = Math.Clamp((e.Effect.Parameter - 64) / 64f, -1f, 1f);
        }

        return (Math.Clamp(gain, 0f, MaxGain), pan);
    }

    /// <summary>Cuts everything sounding, without deciding anything about the transport.</summary>
    private void StopAllVoices() => _synth.Silence();

    /// <inheritdoc/>
    /// <remarks>
    /// The watch goes first, so nothing is walking the plugins while they are being put down.
    /// </remarks>
    public void Dispose()
    {
        _watch.Dispose();

        Stop();
        _samples.Clear();
        _synth.Dispose();
    }

    /// <summary>
    /// Every track's plugins: which process each one really is, and whether it is still up.
    /// </summary>
    /// <remarks>
    /// The point of a plugin having a process of its own is that nothing it does can reach
    /// another track. That is a claim, and this is what checks it. Two tracks reporting the
    /// same process are not isolated, whatever the design says, and a plugin that has stopped
    /// is named here rather than being noticed later as a track that went quiet.
    ///
    /// Written only when something changes. A line a second per track is a log nobody reads;
    /// a line when a plugin appears, stops or changes process is a log that says what happened
    /// and when.
    ///
    /// Every track a song can have is walked, not the length of the levels array: that one is
    /// empty until a song is loaded, and a plugin can be put on a track before then. Two tracks
    /// reporting one process would mean the isolation is not there at all, which is the line
    /// worth shouting.
    ///
    /// A watch that throws is not worth taking anything down for.
    /// </remarks>
    private void Muster()
    {
        if (!Diagnostics.Log.On(Diagnostics.Enums.LogArea.Tracker)) return;

        try
        {
            var processes = new Dictionary<int, string>();
            var seen = new List<int>();

            for (int track = 0; track < Song.MaxTrackCount; track++)
            {
                string account = Account(track, processes);
                seen.Add(track);

                if (_mustered.TryGetValue(track, out string? said) && said == account) continue;

                _mustered[track] = account;

                if (account.Length == 0) continue;

                int number = track;
                Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Plugins, () => "track " + number + " holds " + account);
            }

            foreach (var pair in processes)
            {
                if (!pair.Value.Contains(','.ToString())) continue;

                var shared = pair;
                Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Plugins, () =>
                    "process " + shared.Key + " is serving " + shared.Value +
                    "  <-- THESE ARE NOT ISOLATED FROM EACH OTHER");
            }
        }
        catch (Exception)
        {
        }
    }

    /// <summary>What one track is holding, in a line, or nothing when it holds no plugin.</summary>
    private string Account(int track, Dictionary<int, string> processes)
    {
        IPluginInstrument? player;
        lock (_playerLock) player = _players.TryGetValue(track, out var found) ? found.Plugin : null;

        var chain = _synth.HasMixer ? _synth.Mixer.InsertOn(track) as PluginChain : null;

        if (player == null && (chain == null || chain.Count == 0)) return "";

        var line = new System.Text.StringBuilder();

        if (player != null) line.Append(Describe(player, track, processes));

        if (chain != null && chain.Count > 0)
        {
            if (line.Length > 0) line.Append("; ");

            line.Append("chain of ").Append(chain.Count).Append(": ");

            bool first = true;
            foreach (var device in chain.Devices)
            {
                if (!first) line.Append(", ");
                first = false;

                line.Append(Describe(device.Insert, track, processes));
                if (device.Bypassed) line.Append(" (bypassed)");
            }
        }

        return line.ToString();
    }

    /// <summary>One plugin: what it is, which process it really is, and whether it is up.</summary>
    private static string Describe(object plugin, int track, Dictionary<int, string> processes)
    {
        if (plugin is not BridgedPlugin bridged)
            return plugin.GetType().Name + " in this process";

        int id = bridged.ProcessId;

        if (id > 0)
        {
            processes[id] = processes.TryGetValue(id, out string? already)
                ? already + ", track " + track
                : "track " + track;
        }

        return bridged.Info.Name + " in process " + id +
            (bridged.IsActive ? "" : ", STOPPED" +
                (bridged.StoppedNote.Length == 0 ? "" : ": " + bridged.StoppedNote));
    }
}
