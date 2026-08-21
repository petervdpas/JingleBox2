using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using JingleBox2.Audio;
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
    public void Preview(TrackerInstrument instrument, Note note, float gain = 1f)
    {
        if (!note.IsPlayable) return;

        _audio.EnsureInitialized();
        _synth.EnsureStarted(_audio);

        float level = gain * (float)instrument.Volume;

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
        if (track < 0 || track >= _noteGain.Length) return (0, 0);

        var (left, right) = _synth.Mixer.LevelFor(track);

        return (Math.Clamp(left, 0f, 1f), Math.Clamp(right, 0f, 1f));
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

        double secondsPerLine = song.Timing.SecondsPerLine;
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

            nextLine += secondsPerLine;
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
