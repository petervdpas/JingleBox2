using System;
using System.Diagnostics;
using System.Threading;
using JingleBox2.Audio;
using ManagedBass;

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
/// owns the clock, the voices, and the BASS channels.
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

    private readonly IAudioEngine _audio;
    private readonly TrackerSampleBank _bank = new();
    private readonly object _lock = new();

    private Thread? _clock;
    private CancellationTokenSource? _cancel;

    private Song? _song;
    private TrackerSequencer? _sequencer;
    private int[] _voices = Array.Empty<int>();

    public TrackerPlayer(IAudioEngine audio) => _audio = audio;

    /// <summary>Raised from the clock thread. Marshal before touching UI.</summary>
    public event EventHandler<TrackerPosition>? PositionChanged;

    public event EventHandler? Stopped;

    public bool IsPlaying { get; private set; }

    public TrackerPosition Position { get; private set; } = TrackerPosition.Start;

    public TrackerPlayMode Mode { get; private set; } = TrackerPlayMode.Song;

    /// <summary>Start again from the top instead of stopping at the end.</summary>
    public bool Loop { get; set; } = true;

    /// <summary>Instrument files that could not be loaded, for reporting after a take.</summary>
    public System.Collections.Generic.IReadOnlyCollection<string> FailedInstruments => _bank.FailedPaths;

    public void Play(Song song, TrackerPosition from, TrackerPlayMode mode = TrackerPlayMode.Song)
    {
        ArgumentNullException.ThrowIfNull(song);
        Stop();

        _audio.EnsureInitialized();

        lock (_lock)
        {
            _song = song;
            _sequencer = new TrackerSequencer(song.TrackCount);
            _voices = new int[song.TrackCount];
            Mode = mode;
            Position = from;
        }

        _bank.Preload(song.Instruments);

        _cancel = new CancellationTokenSource();
        var token = _cancel.Token;

        IsPlaying = true;
        _clock = new Thread(() => RunClock(token))
        {
            IsBackground = true,
            Name = "JingleBox2 tracker clock",
            // Steps land every few milliseconds, so the clock should not queue behind UI work.
            Priority = ThreadPriority.AboveNormal
        };
        _clock.Start();
    }

    public void Stop()
    {
        var cancel = _cancel;
        var clock = _clock;

        _cancel = null;
        _clock = null;

        cancel?.Cancel();
        if (clock != null && clock != Thread.CurrentThread)
            clock.Join(TimeSpan.FromSeconds(1));

        cancel?.Dispose();

        StopAllVoices();

        if (IsPlaying)
        {
            IsPlaying = false;
            Stopped?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Sounds a single note, for auditioning while editing. Independent of playback.</summary>
    public void Preview(TrackerInstrument instrument, Note note, float gain = 1f)
    {
        if (!note.IsPlayable) return;

        _audio.EnsureInitialized();

        int channel = _bank.GetChannel(instrument, note);
        if (channel == 0) return;

        Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, gain * (float)instrument.Volume);
        Bass.ChannelPlay(channel);
    }

    /// <summary>Forgets a cached sample so an edited or re-recorded file is picked up.</summary>
    public void ReloadInstrument(string filePath) => _bank.Invalidate(filePath);

    private void RunClock(CancellationToken token)
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

        if (!token.IsCancellationRequested)
        {
            // Ran off the end on its own rather than being stopped.
            StopAllVoices();
            IsPlaying = false;
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
            if (e.Track < 0 || e.Track >= _voices.Length) continue;

            switch (e.Kind)
            {
                case TrackerEventKind.Stop:
                    StopVoice(e.Track);
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

        // One voice per track, as a tracker has always worked: a new note cuts the old one.
        StopVoice(e.Track);

        int channel = _bank.GetChannel(instrument, e.Note);
        if (channel == 0) return;

        ApplyVoiceSettings(channel, e, instrument);

        if (!Bass.ChannelPlay(channel)) return;
        _voices[e.Track] = channel;
    }

    private void Adjust(TrackerEvent e, Song song)
    {
        int channel = _voices[e.Track];
        if (channel == 0) return;

        var instrument = song.InstrumentAt(e.Instrument);
        ApplyVoiceSettings(channel, e, instrument);
    }

    private static void ApplyVoiceSettings(int channel, TrackerEvent e, TrackerInstrument? instrument)
    {
        float gain = (e.Gain ?? 1f) * (float)(instrument?.Volume ?? 1.0);

        // The effect column wins over the volume column when both set the same thing.
        if (e.Effect.Command == TrackerEffect.SetVolume)
            gain = Math.Clamp(e.Effect.Parameter / (float)TrackerCell.MaxVolume, 0f, 1f);

        Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, Math.Clamp(gain, 0f, 1f));

        if (e.Effect.Command == TrackerEffect.SetPan)
        {
            // 00 hard left, 40 centre, 80 hard right.
            float pan = Math.Clamp((e.Effect.Parameter - 64) / 64f, -1f, 1f);
            Bass.ChannelSetAttribute(channel, ChannelAttribute.Pan, pan);
        }
    }

    private void StopVoice(int track)
    {
        int channel = _voices[track];
        if (channel == 0) return;

        _voices[track] = 0;
        Bass.ChannelStop(channel);
    }

    private void StopAllVoices()
    {
        for (int track = 0; track < _voices.Length; track++)
            StopVoice(track);
    }

    public void Dispose()
    {
        Stop();
        _bank.Dispose();
    }
}
