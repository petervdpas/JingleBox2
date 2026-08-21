using System;
using System.Runtime.InteropServices;
using JingleBox2.Tracker.Synth;
using ManagedBass;

namespace JingleBox2.Audio;

/// <summary>
/// The synth's way out to the speakers: one BASS stream that BASS pulls from, filled by the
/// mixer. A single stream for every synth voice rather than a channel per note, because the
/// voices are generated here and mixing them in managed code is cheaper than handing BASS
/// dozens of channels.
/// </summary>
public sealed class SynthOutput : IDisposable
{
    public const int SampleRate = 44100;
    public const int Channels = 2;

    /// <summary>
    /// How far ahead this stream is buffered. Short, because a note typed on a keyboard has to
    /// sound now; BASS is told to update more often to keep a buffer this small fed.
    /// </summary>
    public const float BufferSeconds = 0.06f;

    /// <summary>Milliseconds between BASS buffer updates. The default is far too slow for the above.</summary>
    public const int UpdatePeriodMs = 10;

    private readonly object _lock = new();

    // The delegate has to outlive the call that hands it to BASS: BASS keeps calling it from
    // its own thread, and a collected delegate is a crash rather than a silence.
    private StreamProcedure? _procedure;

    private float[] _scratch = Array.Empty<float>();
    private int _handle;
    private bool _disposed;

    public SynthOutput() => Mixer = new SynthMixer(SampleRate);

    public SynthMixer Mixer { get; }

    public bool IsRunning => _handle != 0;

    /// <summary>Opens the stream on first use. Safe to call before every note.</summary>
    public void EnsureStarted(IAudioEngine audio)
    {
        lock (_lock)
        {
            if (_disposed || _handle != 0) return;

            audio.EnsureInitialized();

            Bass.Configure(Configuration.UpdatePeriod, UpdatePeriodMs);

            _procedure = Fill;
            _handle = Bass.CreateStream(SampleRate, Channels, BassFlags.Float, _procedure, IntPtr.Zero);
            if (_handle == 0)
            {
                _procedure = null;
                return;
            }

            Bass.ChannelSetAttribute(_handle, ChannelAttribute.Buffer, BufferSeconds);
            Bass.ChannelPlay(_handle);
        }
    }

    /// <summary>Silences the voices. The stream stays open, ready for the next note.</summary>
    public void Silence() => Mixer.StopAll();

    private int Fill(int handle, IntPtr buffer, int length, IntPtr user)
    {
        int samples = length / sizeof(float);
        if (samples <= 0) return 0;

        if (_scratch.Length < samples) _scratch = new float[samples];

        Mixer.Render(_scratch, samples / Channels);
        Marshal.Copy(_scratch, 0, buffer, samples);

        // Always a full buffer: returning less would tell BASS the stream has ended.
        return samples * sizeof(float);
    }

    public void Dispose()
    {
        int handle;
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            handle = _handle;
            _handle = 0;
        }

        Mixer.StopAll();

        if (handle != 0) Bass.StreamFree(handle);
        _procedure = null;
    }
}
