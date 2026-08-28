using ManagedBass.Wasapi;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// Through WASAPI, which offers a loopback capture per output device. That is a Windows idea, so
/// this needs basswasapi beside the program and reports itself unsupported without it or off
/// Windows, and the recorder then keeps to capture devices. Linux answers the same question from
/// the other side, with a monitor source that appears among the capture devices already.
/// </remarks>
public sealed class WasapiLoopback : ILoopbackCapture
{
    /// <summary>Ask for the device's own mix format rather than imposing one.</summary>
    private const int DeviceFormat = 0;

    /// <summary>Two channels is what everything downstream is written for.</summary>
    private const int StereoChannels = 2;

    /// <summary>What to assume the device runs at when it will not say.</summary>
    private const int FallbackSampleRate = 44100;

    /// <summary>Held while the capture is started or stopped, which can be asked for from either thread.</summary>
    private readonly object _lock = new();

    /// <summary>
    /// The callback WASAPI holds, kept here because it has to outlive the call that handed it
    /// over: WASAPI goes on calling it from its own thread, and a collected delegate is a crash
    /// rather than a silence.
    /// </summary>
    private WasapiProcedure? _procedure;

    /// <summary>Who each block is handed to.</summary>
    private Action<byte[]>? _onAudio;

    /// <summary>The block as WASAPI wrote it, kept so a capture does not allocate per block.</summary>
    private float[] _floats = Array.Empty<float>();

    /// <summary>The same block turned into sixteen bit samples, kept for the same reason.</summary>
    private byte[] _pcm = Array.Empty<byte>();

    /// <summary>Which output is being listened to, or -1 for none.</summary>
    private int _device = -1;

    /// <summary>Whether audio is arriving.</summary>
    private bool _running;

    /// <summary>
    /// Whether this can work at all here. Windows only, since loopback is a WASAPI idea and
    /// Linux answers the same question with a monitor source on the capture side.
    /// </summary>
    public static bool IsSupported => OperatingSystem.IsWindows();

    /// <inheritdoc/>
    public int SampleRate { get; private set; }

    /// <inheritdoc/>
    public int Channels { get; private set; }

    /// <inheritdoc/>
    public bool IsRunning
    {
        get { lock (_lock) return _running; }
    }

    /// <summary>
    /// The outputs that can be listened to. Empty everywhere the add-on is missing, which is
    /// how the rest of the app finds out this is not available without asking.
    /// </summary>
    /// <remarks>
    /// Empty rather than a fault when basswasapi is not beside the program, since a build without
    /// the add-on is a build that does not offer this rather than a build that is broken.
    /// </remarks>
    public static IReadOnlyList<LoopbackDevice> GetDevices()
    {
        if (!IsSupported) return Array.Empty<LoopbackDevice>();

        var devices = new List<LoopbackDevice>();

        try
        {
            for (int index = 0; index < BassWasapi.DeviceCount; index++)
            {
                if (!BassWasapi.GetDeviceInfo(index, out var info)) continue;
                if (!info.IsLoopback || !info.IsEnabled || info.IsUnplugged) continue;

                devices.Add(new LoopbackDevice(index, info.Name ?? $"Output {index}"));
            }
        }
        catch (DllNotFoundException)
        {
            return Array.Empty<LoopbackDevice>();
        }
        catch (Exception)
        {
            return Array.Empty<LoopbackDevice>();
        }

        return devices;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Shared mode at the device's own format. Exclusive mode would take the output away from
    /// whatever is playing, which is the one thing this is here to hear.
    /// </remarks>
    public bool Start(int device, Action<byte[]> onAudio)
    {
        if (!IsSupported) return false;

        lock (_lock)
        {
            Release();

            _onAudio = onAudio;
            _procedure = OnAudio;

            try
            {
                if (!BassWasapi.Init(device, DeviceFormat, DeviceFormat, WasapiInitFlags.Buffer,
                        0f, 0f, _procedure, IntPtr.Zero))
                {
                    _procedure = null;
                    return false;
                }

                BassWasapi.GetInfo(out var info);
                SampleRate = info.Frequency > 0 ? info.Frequency : FallbackSampleRate;
                Channels = StereoChannels;

                if (!BassWasapi.Start())
                {
                    BassWasapi.Free();
                    _procedure = null;
                    return false;
                }

                _device = device;
                _running = true;

                return true;
            }
            catch (Exception)
            {
                _procedure = null;
                return false;
            }
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        lock (_lock) Release();
    }

    /// <summary>Lets the device go and forgets everything about it. Called holding the lock.</summary>
    /// <remarks>
    /// A failure on the way out is swallowed, since this is reached when the capture is going
    /// away regardless and there is nothing left to tell.
    /// </remarks>
    private void Release()
    {
        if (!_running) return;

        try
        {
            BassWasapi.CurrentDevice = _device;
            BassWasapi.Stop(true);
            BassWasapi.Free();
        }
        catch (Exception)
        {
        }

        _running = false;
        _device = -1;
        _procedure = null;
        _onAudio = null;
    }

    /// <summary>
    /// One block from WASAPI, turned into what everything downstream reads.
    /// </summary>
    /// <remarks>
    /// WASAPI hands over floats and everything downstream reads sixteen bit samples, so the
    /// conversion is here and this is the only place that has to know. Called on WASAPI's own
    /// thread.
    /// </remarks>
    /// <param name="buffer">The block, as floats.</param>
    /// <param name="length">How many bytes of it there are.</param>
    /// <param name="user">Unused, since what this needs is on the instance.</param>
    /// <returns>The length, which is what WASAPI expects back.</returns>
    private int OnAudio(IntPtr buffer, int length, IntPtr user)
    {
        var onAudio = _onAudio;
        if (onAudio == null || buffer == IntPtr.Zero || length <= 0) return length;

        int floats = length / sizeof(float);
        if (_floats.Length < floats) _floats = new float[floats];

        Marshal.Copy(buffer, _floats, 0, floats);

        int bytes = floats * sizeof(short);
        if (_pcm.Length < bytes) _pcm = new byte[bytes];

        for (int i = 0; i < floats; i++)
        {
            short sample = ToSample(_floats[i]);

            _pcm[i * 2] = (byte)(sample & 0xFF);
            _pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        var audio = new byte[bytes];
        Array.Copy(_pcm, audio, bytes);
        onAudio(audio);

        return length;
    }

    /// <summary>Full scale is 1.0, and anything past it is held rather than wrapping round.</summary>
    private static short ToSample(float value)
    {
        float scaled = Math.Clamp(value, -1f, 1f) * short.MaxValue;

        return (short)Math.Round(scaled);
    }

    /// <inheritdoc/>
    public void Dispose() => Stop();
}
