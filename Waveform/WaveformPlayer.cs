using Avalonia.Threading;
using JingleBox2.Audio.Interfaces;
using ManagedBass;
using System;

namespace JingleBox2.Waveform;

/// <summary>
/// Plays a region of a recording and reports where it has got to, as a fraction of the file.
/// Owns the BASS channel and the progress timer so callers never have to.
/// </summary>
public sealed class WaveformPlayer : IDisposable
{
    /// <summary>
    /// The bus the take goes onto, or nothing to play it the ordinary way.
    /// </summary>
    /// <remarks>
    /// A take auditioned here is one of the three things this application makes a sound with, and
    /// under an ASIO driver the ordinary way reaches the silent device BASS was opened on: the
    /// take plays, the position runs, and nobody hears it. On the bus it is a decoding channel
    /// like the pads and the tracker.
    ///
    /// Optional and defaulted to nothing, so an editor dialog built on its own still works, and so
    /// this class can be put a question to without an audio engine.
    /// </remarks>
    private readonly IOutputBus? _bus;

    /// <summary>A player over a bus, or over none.</summary>
    /// <param name="bus">Where the audio goes, or nothing to play it the way it always was.</param>
    public WaveformPlayer(IOutputBus? bus = null) => _bus = bus;

    /// <summary>Whether the take is going onto a bus rather than playing itself.</summary>
    private bool OnBus => _bus is { IsOpen: true };
    /// <summary>How wide one sample is, which is sixteen bits everywhere in this app.</summary>
    private const int WavBytesPerSample = 2;

    /// <summary>How often the position is read. Ten a second, which a moving line does not need beating.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>The BASS channel, or 0 when nothing is playing.</summary>
    private int _channel;

    /// <summary>What reads the position, on the drawing thread. Null when nothing is playing.</summary>
    private DispatcherTimer? _timer;

    /// <summary>Where the region ends, in bytes, or 0 when there is no region.</summary>
    private long _endBytes;

    /// <summary>How wide one frame is, read off the file rather than assumed.</summary>
    private long _bytesPerFrame = 4;

    /// <summary>How long the file is in frames, which is what a fraction is a fraction of.</summary>
    private long _totalFrames;

    /// <summary>Whether a region is playing.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>Current position as a fraction of the whole file.</summary>
    public event Action<double>? PositionChanged;

    /// <summary>Raised when playback ends, whether it finished or was stopped.</summary>
    public event Action? Stopped;

    /// <summary>Plays from one fraction of the file to another. Both are clamped to 0..1.</summary>
    /// <remarks>
    /// Whatever was playing is stopped first, so a channel or a timer is never left running behind
    /// this one.
    ///
    /// The position is read on a dispatcher timer rather than a pool one, so whoever is listening
    /// may touch controls directly. A pool thread raising these would throw inside Avalonia and
    /// the timer would swallow it.
    /// </remarks>
    /// <param name="filePath">The recording.</param>
    /// <param name="startFraction">Where to start, 0 to 1.</param>
    /// <param name="endFraction">Where to stop, 0 to 1.</param>
    /// <param name="totalFrames">How long the file is, which nought makes this do nothing.</param>
    public void Play(string filePath, double startFraction, double endFraction, long totalFrames)
    {
        Stop();

        if (totalFrames <= 0) return;

        _channel = Bass.CreateStream(filePath, 0, 0, OnBus ? BassFlags.Decode : BassFlags.Default);
        if (_channel == 0) return;

        var info = Bass.ChannelGetInfo(_channel);
        _bytesPerFrame = Math.Max(1, info.Channels * WavBytesPerSample);
        _totalFrames = totalFrames;

        long startFrame = (long)(Math.Clamp(startFraction, 0, 1) * totalFrames);
        _endBytes = (long)(Math.Clamp(endFraction, 0, 1) * totalFrames) * _bytesPerFrame;

        Bass.ChannelSetPosition(_channel, startFrame * _bytesPerFrame);

        if (OnBus) _bus!.Add(_channel);
        else Bass.ChannelPlay(_channel);

        IsPlaying = true;

        PositionChanged?.Invoke((double)startFrame / _totalFrames);

        _timer = new DispatcherTimer { Interval = PollInterval };
        _timer.Tick += (_, _) => Poll();
        _timer.Start();
    }

    /// <summary>Jumps to a fraction of the file, and does nothing when nothing is playing.</summary>
    /// <param name="fraction">Where to go, 0 to 1.</param>
    public void SeekTo(double fraction)
    {
        if (!IsPlaying || _channel == 0 || _totalFrames <= 0) return;

        long frame = (long)(Math.Clamp(fraction, 0, 1) * _totalFrames);
        Bass.ChannelSetPosition(_channel, frame * _bytesPerFrame);
        PositionChanged?.Invoke((double)frame / _totalFrames);
    }

    /// <summary>Stops, lets the channel go, and says so. Does nothing twice.</summary>
    public void Stop()
    {
        _timer?.Stop();
        _timer = null;

        if (_channel != 0)
        {
            _bus?.Remove(_channel);

            Bass.ChannelStop(_channel);
            Bass.StreamFree(_channel);
            _channel = 0;
        }

        _endBytes = 0;

        if (!IsPlaying) return;

        IsPlaying = false;
        Stopped?.Invoke();
    }

    /// <summary>
    /// Moves where the region ends, while it is playing.
    /// </summary>
    /// <remarks>
    /// The end is told to this when playing starts, and it used to stay where it was told: in
    /// the trim dialog the handles could be dragged in while a take played and the cursor ran
    /// straight past the selection and on to the end of the file. What is playing is meant to be
    /// the selection, so the selection moving has to reach the thing that is playing it.
    ///
    /// A new end already behind the position stops it, which is what dragging the end back past
    /// what you are hearing means.
    ///
    /// Nothing at all while nothing is playing, since the end is an argument to
    /// <see cref="Play"/> and there is no region to move.
    /// </remarks>
    /// <param name="endFraction">Where the region now ends, 0 to 1.</param>
    public void PlayUntil(double endFraction)
    {
        if (!IsPlaying || _channel == 0 || _totalFrames <= 0) return;

        long frame = (long)(Math.Clamp(endFraction, 0, 1) * _totalFrames);

        _endBytes = frame * _bytesPerFrame;

        if (Bass.ChannelGetPosition(_channel) >= _endBytes) Stop();
    }

    /// <summary>Reads where playback has got to, and stops it at the end of the region.</summary>
    /// <remarks>
    /// The state is compared against Stopped rather than tested for Playing, because
    /// PlaybackState is not a flags enum: HasFlag does bitwise arithmetic on it and misreads
    /// Paused and Stalled.
    /// </remarks>
    private void Poll()
    {
        if (_channel == 0)
        {
            Stop();
            return;
        }

        long position = Bass.ChannelGetPosition(_channel);

        bool reachedEnd = _endBytes > 0 && position >= _endBytes;

        bool ended = Bass.ChannelIsActive(_channel) == PlaybackState.Stopped;

        if (reachedEnd || ended)
        {
            Stop();
            return;
        }

        if (_totalFrames > 0)
            PositionChanged?.Invoke((double)(position / _bytesPerFrame) / _totalFrames);
    }

    /// <summary>Stops whatever is playing.</summary>
    public void Dispose() => Stop();
}
