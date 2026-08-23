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
    /// <summary>What the engine runs at when nothing better is known.</summary>
    public const int DefaultSampleRate = 44100;

    /// <summary>Asking for the device's own rate rather than naming one.</summary>
    public const int FollowDevice = 0;

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

    private SynthMixer? _mixer;
    private int _wanted = FollowDevice;

    /// <summary>
    /// What the engine is running at. Fixed for the life of the mixer: voices, filters and
    /// plugins all work their timings out from it, so it cannot move under them.
    /// </summary>
    public int SampleRate { get; private set; } = DefaultSampleRate;

    /// <summary>
    /// The mixer, built the first time anything asks for it. Late on purpose: until the audio
    /// device has been opened there is no way to know what rate to build it for.
    /// </summary>
    public SynthMixer Mixer => _mixer ??= new SynthMixer(SampleRate);

    /// <summary>True once the mixer exists, so a meter can ask without building one.</summary>
    public bool HasMixer => _mixer != null;

    /// <summary>
    /// Asks for a rate, or for the device's own with <see cref="FollowDevice"/>. Only heard
    /// before the mixer is built, which is why it comes from settings at startup.
    /// </summary>
    public void UseSampleRate(int rate)
    {
        if (_mixer != null) return;

        _wanted = rate;
        if (rate > 0) SampleRate = rate;
    }

    public bool IsRunning => _handle != 0;

    /// <summary>
    /// The loudest thing this stream is putting out, 0 to 1. The tracker's half of the main
    /// output meter; the pads are the other half and are their own channels.
    /// </summary>
    public float Level
    {
        get
        {
            int handle = _handle;

            if (handle == 0) return 0;

            int raw = Bass.ChannelGetLevel(handle);

            if (raw == -1) return 0;

            return Math.Clamp(Math.Max((raw >> 16) & 0xFFFF, raw & 0xFFFF) / 32768f, 0f, 1f);
        }
    }

    /// <summary>
    /// Opens the stream on first use, and opens it again if it has gone. Safe to call before
    /// every note.
    /// </summary>
    /// <remarks>
    /// Changing the output device closes BASS and opens it again, which takes this stream with
    /// it without telling anybody. So the handle is not taken as proof: the stream has to still
    /// be running, or it is made again.
    /// </remarks>
    public void EnsureStarted(IAudioEngine audio)
    {
        lock (_lock)
        {
            if (_disposed) return;

            if (_handle != 0)
            {
                if (Bass.ChannelIsActive(_handle) == PlaybackState.Playing) return;

                Diagnostics.Log.Write(Diagnostics.LogArea.Audio, "the synth stream had gone; opening another");

                _handle = 0;
                _procedure = null;
            }

            audio.EnsureInitialized();

            // The device is open now, so this is the first moment its rate can be asked for.
            // Running at the device's own rate means nothing is resampled on the way out, and
            // a plugin is told the rate it is actually being fed at.
            if (_mixer == null && _wanted == FollowDevice)
            {
                int rate = DeviceRate();
                if (rate > 0) SampleRate = rate;
            }

            Bass.Configure(Configuration.UpdatePeriod, UpdatePeriodMs);

            _procedure = Fill;
            _handle = Bass.CreateStream(SampleRate, Channels, BassFlags.Float, _procedure, IntPtr.Zero);

            Diagnostics.Log.Write(Diagnostics.LogArea.Audio, () =>
                _handle == 0
                    ? "the synth stream would not open: " + Bass.LastError
                    : "the synth stream is open at " + SampleRate + " Hz");

            if (_handle == 0)
            {
                _procedure = null;
                return;
            }

            Bass.ChannelSetAttribute(_handle, ChannelAttribute.Buffer, BufferSeconds);
            Bass.ChannelPlay(_handle);
        }
    }

    /// <summary>What the output device is running at, or zero when it will not say.</summary>
    private static int DeviceRate()
    {
        try
        {
            var info = Bass.Info;
            return info.SampleRate;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>Silences the voices. The stream stays open, ready for the next note.</summary>
    public void Silence()
    {
        if (_mixer != null) _mixer.StopAll();
    }

    /// <summary>
    /// The smallest and largest block asked for so far, so the size is written down when it is
    /// something new and not once a block.
    /// </summary>
    /// <remarks>
    /// A pair rather than the last size seen. On Linux the device asks for two sizes turn and
    /// turn about, so "say it when it changes" was every block, which is the audio thread
    /// opening and closing a file eighty times a second and a log too full to read.
    /// </remarks>
    private int _smallest;
    private int _largest;

    private int Fill(int handle, IntPtr buffer, int length, IntPtr user)
    {
        int samples = length / sizeof(float);
        if (samples <= 0) return 0;

        if (Diagnostics.Log.IsOn && (samples < _smallest || samples > _largest || _largest == 0))
        {
            if (_smallest == 0 || samples < _smallest) _smallest = samples;
            if (samples > _largest) _largest = samples;

            int low = _smallest / Channels;
            int high = _largest / Channels;

            Diagnostics.Log.Write(Diagnostics.LogArea.Audio, () =>
                low == high
                    ? "the synth stream is asking for " + low + " frames at a time"
                    : "the synth stream is asking for between " + low + " and " + high + " frames at a time");
        }

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
