using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using JingleBox2.Audio;
using JingleBox2.Audio.Plugins;
using JingleBox2.Tracker.Synth;

namespace JingleBox2.Tracker;

public enum TrackerPlayMode
{
    /// <summary>Walk the order list to the end.</summary>
    Song,

    /// <summary>Stay on one pattern.</summary>
    Pattern
}

/// <summary>
/// Plays a song. Sequencing decisions come from <see cref="TrackerSequencer"/>; this class
/// owns the clock and the voices.
/// </summary>
/// <remarks>
/// The clock runs on its own thread against a stopwatch, with each step's time computed from
/// the start rather than added to the last one. A timer that sleeps "one step" at a time
/// accumulates its own lateness, and over a 64 line pattern that drift is audible.
/// </remarks>
public sealed class TrackerPlayer : IDisposable
{
    /// <summary>Below this the thread spins instead of sleeping, since sleep is not that precise.</summary>
    private const double SpinThresholdSeconds = 0.002;

    /// <summary>How long an audition holds before it releases, since no key is let go of.</summary>
    public const double PreviewHoldSeconds = 0.4;

    private readonly IAudioEngine _audio;
    private readonly SampleStore _samples = new();
    private readonly SynthOutput _synth = new();
    private readonly object _lock = new();

    private Thread? _clock;
    private CancellationTokenSource? _cancel;

    /// <summary>
    /// Bumped every time playback is torn down. A clock thread that is on its way out can
    /// still be mid-callback, and its events must not overwrite the state of a newer run.
    /// </summary>
    private int _generation;

    private Song? _song;
    private TrackerSequencer? _sequencer;

    // What the note itself asked for, kept so the mixer can be re-applied to a voice that is
    // already sounding: a fader move has to be heard now, not at the next note.
    private float[] _noteGain = Array.Empty<float>();
    private float?[] _notePan = Array.Empty<float?>();

    public TrackerPlayer(IAudioEngine audio) => _audio = audio;

    /// <summary>Raised from the clock thread. Marshal before touching UI.</summary>
    public event EventHandler<TrackerPosition>? PositionChanged;

    /// <summary>Raised on every transport change, from whichever thread caused it.</summary>
    public event EventHandler<TrackerTransportState>? StateChanged;

    public event EventHandler? Stopped;

    public TrackerTransportState State { get; private set; } = TrackerTransportState.Stopped;

    public bool IsPlaying => State == TrackerTransportState.Playing;

    public bool IsPaused => State == TrackerTransportState.Paused;

    public TrackerPosition Position { get; private set; } = TrackerPosition.Start;

    public TrackerPlayMode Mode { get; private set; } = TrackerPlayMode.Song;

    /// <summary>Start again from the top instead of stopping at the end.</summary>
    public bool Loop { get; set; } = true;

    /// <summary>Instrument files that could not be loaded, for reporting after a take.</summary>
    public System.Collections.Generic.IReadOnlyCollection<string> FailedInstruments => _samples.FailedPaths;

    public void Play(Song song, TrackerPosition from, TrackerPlayMode mode = TrackerPlayMode.Song)
    {
        ArgumentNullException.ThrowIfNull(song);

        Teardown();
        _audio.EnsureInitialized();

        lock (_lock)
        {
            _song = song;
            _sequencer = new TrackerSequencer(song.TrackCount);
            _noteGain = new float[song.TrackCount];
            _notePan = new float?[song.TrackCount];
            Mode = mode;
            Position = from;
        }

        _samples.Preload(song.Instruments);

        // Everything sounds through the one stream now, recordings included, so it is opened
        // for any song that has an instrument at all.
        if (song.Instruments.Count > 0) _synth.EnsureStarted(_audio);

        // The strips are pushed once up front: a side chain set while stopped has nowhere to
        // go until there is a song to take it.
        ApplyMix();

        StartClock();
    }

    /// <summary>Continues from where a pause left off. Does nothing when not paused.</summary>
    public void Resume()
    {
        if (State != TrackerTransportState.Paused) return;

        Song? song;
        lock (_lock) song = _song;
        if (song == null) return;

        _audio.EnsureInitialized();
        StartClock();
    }

    /// <summary>Freezes at the current step. The voices are cut, the position is kept.</summary>
    public void Pause()
    {
        if (State != TrackerTransportState.Playing) return;

        Teardown();
        SetState(TrackerTransportState.Paused);
    }

    public void Stop()
    {
        Teardown();

        bool wasRunning = State != TrackerTransportState.Stopped;
        Position = TrackerPosition.Start;

        SetState(TrackerTransportState.Stopped);
        if (wasRunning) Stopped?.Invoke(this, EventArgs.Empty);
    }

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
            // Steps land every few milliseconds, so the clock should not queue behind UI work.
            Priority = ThreadPriority.AboveNormal
        };
        _clock.Start();
    }

    /// <summary>Stops the clock and silences the voices without deciding what state follows.</summary>
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

        // A plugin holds its own notes, and nothing else will let go of them: stopping the
        // clock has to stop the sound too, or a chord hangs on until the app closes.
        if (_synth.HasMixer) _synth.Mixer.AllPluginNotesOff();

        cancel?.Dispose();
        StopAllVoices();
    }

    private void SetState(TrackerTransportState state)
    {
        if (State == state) return;

        State = state;
        StateChanged?.Invoke(this, state);
    }

    /// <summary>Sounds a single note, for auditioning while editing. Independent of playback.</summary>
    public void Preview(TrackerInstrument instrument, Note note, float gain = 1f, int track = -1)
    {
        if (!note.IsPlayable) return;

        _audio.EnsureInitialized();
        _synth.EnsureStarted(_audio);

        float level = gain * (float)instrument.Volume;

        if (instrument.IsPlugin)
        {
            // The copy already on a track wins. It is the one whose window is open and whose
            // knobs have just been turned; auditioning through a second copy would play the
            // sound the song was last saved with and leave you wondering what you changed.
            int playing = TrackPlaying(instrument.Id);

            // Not loaded yet, and this is the instrument that track plays: load it there. A
            // note played on a track should sound through that track's plugin whether or not
            // anybody has opened its window, and through one copy of it rather than two.
            if (playing < 0 && track >= 0 && OnTrack(track, instrument))
            {
                EnsurePlayerOn(track, instrument);
                playing = TrackPlaying(instrument.Id);
            }

            if (playing >= 0)
            {
                _synth.Mixer.PreviewOnTrack(playing, note, level, PreviewHoldSeconds);
                return;
            }

            var player = PreviewPlayerFor(instrument);
            if (player == null) return;

            _synth.Mixer.PreviewPlugin(note, level, PreviewHoldSeconds);
            return;
        }

        if (instrument.IsSynth)
        {
            _synth.Mixer.Preview(instrument.Patch, note, level, PreviewHoldSeconds);
            return;
        }

        var sample = _samples.Load(instrument.FilePath);
        if (sample == null) return;

        _synth.Mixer.Preview(instrument, sample, note, level, PreviewHoldSeconds);
    }

    /// <summary>
    /// How loud a track is right now, both sides, for the mixer's meters. Zero for a track that
    /// is not sounding, and for every track when nothing is playing.
    /// </summary>
    public (float Left, float Right) LevelFor(int track)
    {
        // Asked several times a second by the meters, and before anything has played there is
        // no mixer yet. Building one here would fix the rate before the device is even open.
        if (!_synth.HasMixer) return (0, 0);
        if (track < 0 || track >= _noteGain.Length) return (0, 0);

        var (left, right) = _synth.Mixer.LevelFor(track);

        return (Math.Clamp(left, 0f, 1f), Math.Clamp(right, 0f, 1f));
    }

    /// <summary>
    /// The largest block a plugin is asked to handle in one go. The audio callback's blocks
    /// are whatever the device asks for; anything longer than this is fed through in pieces.
    /// </summary>
    public const int MaxPluginFrames = 2048;

    /// <summary>What the engine is running at, which is what a plugin here has to be built for.</summary>
    public int SampleRate => _synth.SampleRate;

    /// <summary>
    /// Asks the engine to run at a rate, or at the device's own. Only heard before the first
    /// note, so it comes from settings when the tracker is built.
    /// </summary>
    public void UseSampleRate(int rate) => _synth.UseSampleRate(rate);

    /// <summary>
    /// The chain of effects on a track, made and put into the mix the first time it is asked
    /// for. A track with nothing on it costs an empty chain, which does nothing per block.
    /// </summary>
    public PluginChain ChainFor(int track)
    {
        if (_synth.Mixer.InsertOn(track) is PluginChain existing) return existing;

        // The engine has to be running for an effect to be given anything at all. Until now it
        // was opened by the first note, so a track with an effect on it and nothing playing was
        // an effect that never saw a single block: it could not work on the audio, could not
        // finish a delay's tail, and could not tell the host what its own window had done.
        EnsureEngine();

        var chain = new PluginChain();
        _synth.Mixer.SetInsert(track, chain);

        return chain;
    }

    /// <summary>
    /// Writes every track's chain into the song, ready to be saved with it.
    /// </summary>
    public void CaptureChains(Song song)
    {
        if (song == null) return;

        for (int track = 0; track < song.Mix.Count; track++)
        {
            var chain = _synth.Mixer.InsertOn(track) as PluginChain;
            var captured = PluginChainState.Capture(chain);

            // Null rather than an empty list: a song with no effects should not be full of
            // empty chains.
            song.Mix[track].Plugins = captured.IsEmpty ? null : captured;
        }
    }

    /// <summary>
    /// Builds every track's chain from what the song holds. Returns the plugins it could not
    /// find, so the song can say so rather than quietly sounding different.
    /// </summary>
    public IReadOnlyList<string> RestoreChains(Song song)
    {
        var missing = new List<string>();
        if (song == null) return missing;

        for (int track = 0; track < song.Mix.Count; track++)
        {
            var chain = ChainFor(track);
            missing.AddRange(PluginChainState.Restore(
                chain, song.Mix[track].Plugins, _synth.SampleRate, MaxPluginFrames));
        }

        return missing;
    }

    /// <summary>
    /// The plugin playing each track, and which instrument it is. A track holds on to its
    /// plugin between notes because a plugin has a release to finish.
    /// </summary>
    private readonly Dictionary<int, (string Instrument, IPluginInstrument Plugin)> _players = new();

    private readonly object _playerLock = new();

    /// <summary>
    /// Opens the audio engine if it is not already open. A plugin has to be built for the rate
    /// the engine settled on, and until the device is open there is no rate to build for.
    /// </summary>
    public void EnsureEngine()
    {
        _audio.EnsureInitialized();
        _synth.EnsureStarted(_audio);
    }

    /// <summary>
    /// The plugin for a track, loading it if that track is not already playing this
    /// instrument. Null when the plugin is missing or this host cannot play its kind.
    /// </summary>
    /// <remarks>
    /// Loading happens here, on the thread that triggered the note, which is the clock. That
    /// is a stall on the very first note of a plugin and nothing after: an instrument that has
    /// been opened in the editor is already in memory, and a plugin put down is parked rather
    /// than taken apart, so picking it up again costs almost nothing.
    /// </remarks>
    /// <summary>
    /// The plugin a track plays, loaded if it is not already. The same one the notes go to,
    /// deliberately: a second copy would be a second sound, and turning a knob on it would
    /// change something nobody can hear.
    /// </summary>
    public IPluginInstrument? EnsurePlayerOn(int track, TrackerInstrument instrument)
    {
        EnsureEngine();

        return PlayerFor(track, instrument);
    }

    private IPluginInstrument? PlayerFor(int track, TrackerInstrument instrument)
    {
        if (track < 0 || instrument == null || !instrument.IsPlugin) return null;

        lock (_playerLock)
        {
            if (_players.TryGetValue(track, out var existing))
            {
                if (string.Equals(existing.Instrument, instrument.Id, StringComparison.Ordinal)) return existing.Plugin;

                // A different instrument on this track. The old one comes off the mix before
                // it is put down, or it plays into a bus that is about to be somebody else's.
                _synth.Mixer.SetInstrument(track, null);
                existing.Plugin.Dispose();
                _players.Remove(track);
            }

            var description = instrument.Plugin;
            if (description == null) return null;

            var player = PluginHost.LoadInstrument(description, _synth.SampleRate, MaxPluginFrames);
            if (player == null) return null;

            // The patch goes in before the first note, or the first note is the wrong sound.
            player.LoadState(instrument.StateBytes);

            _players[track] = (instrument.Id, player);
            _synth.Mixer.SetInstrument(track, player);

            return player;
        }
    }

    /// <summary>True when this instrument is the one the song has on this track.</summary>
    private bool OnTrack(int track, TrackerInstrument instrument)
    {
        Song? song;
        lock (_lock) song = _song;

        if (song == null || instrument == null) return false;

        return ReferenceEquals(song.InstrumentAt(song.GetTrackInstrument(track)), instrument);
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

    /// <summary>
    /// The plugin a track is playing, without loading one. What the editor asks when it wants
    /// to save a patch back.
    /// </summary>
    public IPluginInstrument? PlayerOn(int track)
    {
        lock (_playerLock) return _players.TryGetValue(track, out var found) ? found.Plugin : null;
    }

    /// <summary>
    /// The plugin used for auditioning, which belongs to no track. One at a time: opening a
    /// second instrument in the editor puts the first one down.
    /// </summary>
    private (string Instrument, IPluginInstrument Plugin)? _auditioned;

    /// <summary>
    /// The plugin behind an audition, loaded if it is not already the one being auditioned.
    /// Also what the editor calls to get a live plugin to work on.
    /// </summary>
    public IPluginInstrument? PreviewPlayerFor(TrackerInstrument instrument)
    {
        if (instrument == null || !instrument.IsPlugin) return null;

        // One copy of a plugin, not two. If a track is already playing this instrument, that is
        // the copy to work on: a second one is a second process holding a second set of
        // wavetables, and a knob turned on it would change something nobody can hear.
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

            var player = PluginHost.LoadInstrument(description, _synth.SampleRate, MaxPluginFrames);
            if (player == null) return null;

            player.LoadState(instrument.StateBytes);

            _auditioned = (instrument.Id, player);
            _synth.Mixer.SetPreviewInstrument(player);

            return player;
        }
    }

    /// <summary>Puts the auditioned plugin down, for a page that is being left.</summary>
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

    /// <summary>Takes every plugin off the tracks and puts it down. For closing a song.</summary>
    public void ClearPlayers()
    {
        (string Instrument, IPluginInstrument Plugin)[] leaving;

        lock (_playerLock)
        {
            leaving = _players.Values.ToArray();

            foreach (var track in _players.Keys) _synth.Mixer.SetInstrument(track, null);

            _players.Clear();
        }

        foreach (var (_, plugin) in leaving) plugin.Dispose();
    }

    /// <summary>Forgets a cached sample so an edited or re-recorded file is picked up.</summary>
    public void ReloadInstrument(string filePath) => _samples.Invalidate(filePath);

    private void RunClock(CancellationToken token, int generation)
    {
        Song song;
        TrackerSequencer sequencer;
        lock (_lock)
        {
            if (_song == null || _sequencer == null) return;
            song = _song;
            sequencer = _sequencer;
        }

        var clock = Stopwatch.StartNew();

        var position = Position;
        double nextLine = 0;
        bool loopPattern = Mode == TrackerPlayMode.Pattern;

        while (!token.IsCancellationRequested)
        {
            if (generation != Volatile.Read(ref _generation)) return;

            ApplyEvents(sequencer.EventsFor(song, position), song);
            Position = position;
            PositionChanged?.Invoke(this, position);

            var next = loopPattern
                ? TrackerSequencer.AdvanceWithinPattern(song, position, Loop)
                : TrackerSequencer.Advance(song, position, Loop);

            if (next == null) break;
            position = next.Value;

            // Read per step rather than once: a tempo moved while playing has to be heard on
            // the next line, not at the next take. The times are still absolute from the
            // start, so the clock does not drift; a change simply lengthens or shortens the
            // steps from here on. The song's tempo is written by the UI thread while this one
            // reads it, which is why every use goes through the clamped timing: even a value
            // caught mid-write can only be a tempo, never a stall or a division by zero.
            nextLine += song.Timing.SecondsPerLine;
            if (!WaitUntil(clock, nextLine, token)) return;
        }

        // Ran off the end on its own rather than being stopped. A newer run may already have
        // started, in which case this thread has no business touching the transport.
        if (!token.IsCancellationRequested && generation == Volatile.Read(ref _generation))
        {
            StopAllVoices();
            Position = TrackerPosition.Start;
            SetState(TrackerTransportState.Stopped);
            Stopped?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Sleeps until the step is due, then spins out the last couple of milliseconds.</summary>
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

    private void ApplyEvents(System.Collections.Generic.IReadOnlyList<TrackerEvent> events, Song song)
    {
        foreach (var e in events)
        {
            if (e.Track < 0 || e.Track >= _noteGain.Length) continue;

            switch (e.Kind)
            {
                case TrackerEventKind.Stop:
                    _synth.Mixer.NoteOff(e.Track);
                    _synth.Mixer.PluginNoteOff(e.Track);
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

    private void Trigger(TrackerEvent e, Song song)
    {
        var instrument = song.InstrumentAt(e.Instrument);
        if (instrument == null) return;

        var (gain, pan) = LevelsFor(e, instrument);

        _noteGain[e.Track] = gain;
        _notePan[e.Track] = pan;

        var (mixed, placed) = WithMix(song, e.Track, gain, pan);

        // One voice per track, as a tracker has always worked: the mixer cuts whatever that
        // track was sounding, whichever kind of instrument it was.
        if (instrument.IsPlugin)
        {
            // The plugin holds its own notes, so the track's voices are let go rather than
            // left ringing underneath it.
            _synth.Mixer.NoteOff(e.Track);

            if (PlayerFor(e.Track, instrument) != null)
            {
                _synth.Mixer.PluginNoteOn(e.Track, e.Note, mixed, placed ?? 0f);
            }

            return;
        }

        if (instrument.IsSynth)
        {
            _synth.Mixer.NoteOn(e.Track, instrument.Patch, e.Note, mixed, placed ?? 0f);
            return;
        }

        var sample = _samples.Load(instrument.FilePath);
        if (sample == null)
        {
            _synth.Mixer.NoteOff(e.Track);
            return;
        }

        _synth.Mixer.NoteOn(e.Track, instrument, sample, e.Note, mixed, placed ?? 0f);
    }

    private void Adjust(TrackerEvent e, Song song)
    {
        var instrument = song.InstrumentAt(e.Instrument);
        var (gain, pan) = LevelsFor(e, instrument);

        _noteGain[e.Track] = gain;
        _notePan[e.Track] = pan;

        var (mixed, placed) = WithMix(song, e.Track, gain, pan);

        _synth.Mixer.SetLevels(e.Track, mixed, placed);
        _synth.Mixer.SetPluginLevels(e.Track, mixed, placed);
    }

    /// <summary>
    /// Puts a note's own level through the track's strip. The cell's pan effect wins when it
    /// set one: an effect written into the pattern is a decision about that note.
    /// </summary>
    private static (float Gain, float? Pan) WithMix(Song song, int track, float gain, float? pan)
    {
        float mixed = Math.Clamp(gain * MixLevels.GainFor(song.Mix, track), 0f, MaxGain);

        return (mixed, pan ?? MixLevels.PanFor(song.Mix, track));
    }

    /// <summary>
    /// Re-applies the mix to whatever is sounding, for a fader or a mute moved mid-take. The
    /// note's own level is kept, so the two are combined rather than one replacing the other.
    /// </summary>
    public void ApplyMix()
    {
        Song? song;
        lock (_lock) song = _song;
        if (song == null) return;

        for (int track = 0; track < _noteGain.Length; track++)
        {
            var (mixed, placed) = WithMix(song, track, _noteGain[track], _notePan[track]);

            _synth.Mixer.SetLevels(track, mixed, placed);

            // The side chain is part of the strip, so it is pushed with the rest of it.
            _synth.Mixer.SetDucking(
                track,
                MixLevels.DuckFor(song.Mix, track, song.TrackCount),
                MixLevels.KeyFor(song.Mix, track, song.TrackCount),
                MixLevels.DuckReleaseFor(song.Mix, track));
        }
    }

    /// <summary>
    /// The level and placement a voice should have, from the cell and the instrument. Shared
    /// by both kinds of instrument so the volume column means the same thing either way.
    /// </summary>
    /// <summary>An instrument can be pushed past unity, so the ceiling is not one.</summary>
    private const float MaxGain = 2f;

    private static (float Gain, float? Pan) LevelsFor(TrackerEvent e, TrackerInstrument? instrument)
    {
        float gain = (e.Gain ?? 1f) * (float)(instrument?.Volume ?? 1.0);

        // The effect column wins over the volume column when both set the same thing.
        if (e.Effect.Command == TrackerEffect.SetVolume)
            gain = Math.Clamp(e.Effect.Parameter / (float)TrackerCell.MaxVolume, 0f, 1f);

        float? pan = null;
        if (e.Effect.Command == TrackerEffect.SetPan)
        {
            // 00 hard left, 40 centre, 80 hard right.
            pan = Math.Clamp((e.Effect.Parameter - 64) / 64f, -1f, 1f);
        }

        return (Math.Clamp(gain, 0f, MaxGain), pan);
    }

    private void StopAllVoices() => _synth.Silence();

    public void Dispose()
    {
        Stop();
        _samples.Clear();
        _synth.Dispose();
    }
}
