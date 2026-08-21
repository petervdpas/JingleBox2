using ManagedBass.Wasapi;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace JingleBox2.Audio;

/// <summary>One of the outputs whose playback can be captured.</summary>
public readonly record struct LoopbackDevice(int Index, string Name);

/// <summary>
/// Recording what an output is playing, on Windows. WASAPI offers a loopback capture per
/// output device, which is the same idea as a monitor source on Linux and needs no extra
/// hardware, no Stereo Mix, and no virtual cable.
/// </summary>
/// <remarks>
/// This is a separate capture path from BASS's own recording: loopback does not come through
/// Bass.RecordStart. Everything above it still sees 16 bit interleaved audio, so the gain,
/// the clip detection, the meter and the WAV writer are unchanged; only where the audio comes
/// from is different.
///
/// It needs basswasapi.dll beside the app. Without it, or off Windows, this reports itself
/// unsupported and the recorder keeps to capture devices.
/// </remarks>
public sealed class WasapiLoopback : IDisposable
{
    /// <summary>Ask for the device's own mix format rather than imposing one.</summary>
    private const int DeviceFormat = 0;

    /// <summary>Two channels is what everything downstream is written for.</summary>
    private const int StereoChannels = 2;

    private readonly object _lock = new();

    // The callback has to outlive the call that hands it over: WASAPI keeps calling it from
    // its own thread, and a collected delegate is a crash rather than a silence.
    private WasapiProcedure? _procedure;

    private Action<byte[]>? _onAudio;
    private float[] _floats = Array.Empty<float>();
    private byte[] _pcm = Array.Empty<byte>();
    private int _device = -1;
    private bool _running;

    public static bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>What the capture is actually running at, once it has started.</summary>
    public int SampleRate { get; private set; }

    public int Channels { get; private set; }

    public bool IsRunning
    {
        get { lock (_lock) return _running; }
    }

    /// <summary>
    /// The outputs that can be listened to. Empty everywhere the add-on is missing, which is
    /// how the rest of the app finds out this is not available without asking.
    /// </summary>
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
            // basswasapi.dll is not beside the app: nothing to offer, and nothing to report.
            return Array.Empty<LoopbackDevice>();
        }
        catch (Exception)
        {
            return Array.Empty<LoopbackDevice>();
        }

        return devices;
    }

    /// <summary>
    /// Starts listening to an output. The audio arrives as 16 bit interleaved stereo, whatever
    /// the device mixes at, so the caller does not have to know it came from WASAPI.
    /// </summary>
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
                // Shared mode at the device's own format: exclusive mode would take the output
                // away from whatever is playing, which is the one thing we want to hear.
                if (!BassWasapi.Init(device, DeviceFormat, DeviceFormat, WasapiInitFlags.Buffer,
                        0f, 0f, _procedure, IntPtr.Zero))
                {
                    _procedure = null;
                    return false;
                }

                BassWasapi.GetInfo(out var info);
                SampleRate = info.Frequency > 0 ? info.Frequency : 44100;
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
                // Missing add-on, or a device that will not open: either way, not available.
                _procedure = null;
                return false;
            }
        }
    }

    public void Stop()
    {
        lock (_lock) Release();
    }

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
            // Going away regardless.
        }

        _running = false;
        _device = -1;
        _procedure = null;
        _onAudio = null;
    }

    /// <summary>
    /// WASAPI hands over floats; everything downstream reads 16 bit samples. The conversion is
    /// here so that is the only place that has to know.
    /// </summary>
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

    public void Dispose() => Stop();
}
