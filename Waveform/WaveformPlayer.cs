using Avalonia.Threading;
using ManagedBass;
using System;

namespace JingleBox2.Waveform;

/// <summary>
/// Plays a region of a recording and reports where it has got to, as a fraction of the file.
/// Owns the BASS channel and the progress timer so callers never have to.
/// </summary>
public sealed class WaveformPlayer : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private int _channel;
    private DispatcherTimer? _timer;
    private long _endBytes;
    private long _bytesPerFrame = 4;
    private long _totalFrames;

    public bool IsPlaying { get; private set; }

    /// <summary>Current position as a fraction of the whole file.</summary>
    public event Action<double>? PositionChanged;

    /// <summary>Raised when playback ends, whether it finished or was stopped.</summary>
    public event Action? Stopped;

    /// <summary>Plays from one fraction of the file to another. Both are clamped to 0..1.</summary>
    public void Play(string filePath, double startFraction, double endFraction, long totalFrames)
    {
        Stop(); // never leave a previous channel or timer running

        if (totalFrames <= 0) return;

        _channel = Bass.CreateStream(filePath, 0, 0, BassFlags.Default);
        if (_channel == 0) return;

        var info = Bass.ChannelGetInfo(_channel);
        _bytesPerFrame = Math.Max(1, info.Channels * 2); // the app records 16-bit
        _totalFrames = totalFrames;

        long startFrame = (long)(Math.Clamp(startFraction, 0, 1) * totalFrames);
        _endBytes = (long)(Math.Clamp(endFraction, 0, 1) * totalFrames) * _bytesPerFrame;

        Bass.ChannelSetPosition(_channel, startFrame * _bytesPerFrame);
        Bass.ChannelPlay(_channel);
        IsPlaying = true;

        PositionChanged?.Invoke((double)startFrame / _totalFrames);

        // DispatcherTimer ticks on the UI thread so subscribers may touch controls directly.
        // A System.Timers.Timer would raise these events on a pool thread, where an Avalonia
        // update throws and the timer silently swallows it.
        _timer = new DispatcherTimer { Interval = PollInterval };
        _timer.Tick += (_, _) => Poll();
        _timer.Start();
    }

    public void SeekTo(double fraction)
    {
        if (!IsPlaying || _channel == 0 || _totalFrames <= 0) return;

        long frame = (long)(Math.Clamp(fraction, 0, 1) * _totalFrames);
        Bass.ChannelSetPosition(_channel, frame * _bytesPerFrame);
        PositionChanged?.Invoke((double)frame / _totalFrames);
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;

        if (_channel != 0)
        {
            Bass.ChannelStop(_channel);
            Bass.StreamFree(_channel);
            _channel = 0;
        }

        _endBytes = 0;

        if (!IsPlaying) return;

        IsPlaying = false;
        Stopped?.Invoke();
    }

    private void Poll()
    {
        if (_channel == 0)
        {
            Stop();
            return;
        }

        long position = Bass.ChannelGetPosition(_channel);

        bool reachedEnd = _endBytes > 0 && position >= _endBytes;

        // Compare against Stopped rather than testing for Playing: PlaybackState is not a
        // [Flags] enum, so HasFlag does bitwise maths and misreads Paused and Stalled.
        bool ended = Bass.ChannelIsActive(_channel) == PlaybackState.Stopped;

        if (reachedEnd || ended)
        {
            Stop();
            return;
        }

        if (_totalFrames > 0)
            PositionChanged?.Invoke((double)(position / _bytesPerFrame) / _totalFrames);
    }

    public void Dispose() => Stop();
}
