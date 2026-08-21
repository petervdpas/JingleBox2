using ManagedBass;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JingleBox2.Audio;

public interface IRecordingService
{
    IReadOnlyList<string> GetInputDevices();
    string? SelectedDevice { get; set; }
    void StartRecording();
    void StopRecording();
    bool IsRecording { get; }

    /// <summary>
    /// Opens the input to watch its level without keeping any of it, so a gain can be set
    /// before the take rather than during it.
    /// </summary>
    void StartMonitoring();

    void StopMonitoring();

    bool IsMonitoring { get; }

    /// <summary>
    /// Set when StartRecording could not use the selected device and fell back to the
    /// system default. Null when the selected device was used as-is.
    /// </summary>
    string? LastStartWarning { get; }

    /// <summary>Gain applied to incoming audio, in dB. 0 is unity.</summary>
    double GainDb { get; set; }

    /// <summary>How many channels the input is captured with, so a meter knows what to show.</summary>
    int Channels { get; }

    /// <summary>True while clipping was seen in the last moment or so. Decays on its own.</summary>
    bool IsClipping { get; }

    /// <summary>True if anything clipped at any point during the current or last take.</summary>
    bool ClippedDuringTake { get; }

    byte[] GetRecentRecordingData(int maxBytes);
    Task<string> SaveRecordingAsync(string fileName);

    /// <summary>
    /// The output to record the playback of, or null to record from the selected input device.
    /// Setting it while the input is open reopens it on the other path.
    /// </summary>
    int? LoopbackDevice { get; set; }

    /// <summary>The outputs whose playback can be captured. Empty where the system cannot.</summary>
    IReadOnlyList<LoopbackDevice> GetLoopbackDevices();

    /// <summary>
    /// Closes and reopens the input if anything is listening, for a change that only takes
    /// effect on a fresh capture. Does nothing when nothing is open.
    /// </summary>
    void ReopenInput();
}

public sealed class RecordingService : IRecordingService, IDisposable
{
    private const int NoDevice = int.MinValue;
    private const int DefaultDevice = -1;

    private const int DefaultSampleRate = 44100;
    private const int DefaultChannels = 2;

    private int _recordHandle;
    private readonly string _recordingsDir;
    private readonly List<byte> _recordingBuffer = new();
    private bool _isRecording;
    private bool _isMonitoring;

    /// <summary>True while the input is open, whether for a take or only for the meter.</summary>
    private bool _capturing;

    /// <summary>
    /// A fifth of a second of audio: enough for a meter to read, small enough that watching
    /// the input all afternoon costs nothing.
    /// </summary>
    private const int MonitorBufferBytes = 44100 / 5 * 4;

    /// <summary>
    /// How long the recent window keeps answering after the last audio arrived. A source that
    /// stops sending stops the callbacks with it, and without this the meter would sit at
    /// whatever it was reading when the sound stopped.
    /// </summary>
    private const long RecentDataStaleMs = 200;

    /// <summary>When audio last came in, for telling silence apart from nothing at all.</summary>
    private long _lastDataTick = long.MinValue / 2;
    private readonly WasapiLoopback _loopback = new();

    // What the current capture is running at. Loopback comes in at whatever the output mixes
    // at, usually 48k, and the WAV is written at that rate rather than being resampled.
    private int _sampleRate = 44100;
    private int _channels = 2;
    private int? _loopbackDevice;

    public int Channels => _channels;

    public IReadOnlyList<LoopbackDevice> GetLoopbackDevices() => WasapiLoopback.GetDevices();

    public int? LoopbackDevice
    {
        get => _loopbackDevice;
        set
        {
            if (_loopbackDevice == value) return;

            _loopbackDevice = value;

            // Reopen on the other path if something is listening, so the change is heard now
            // rather than at the next take.
            ReopenInput();
        }
    }

    public void ReopenInput()
    {
        if (!_capturing) return;

        CloseInput();
        OpenInput();
    }
    private int _initializedDevice = NoDevice;
    private int _resolvedDevice = NoDevice;
    private RecordProcedure? _recordCallback;

    // Name -> BASS device index for the devices we chose to show.
    private readonly List<(string Name, int Index)> _inputDevices = new();

    public string? SelectedDevice { get; set; }
    public bool IsRecording => _isRecording;
    public bool IsMonitoring => _isMonitoring;
    public string? LastStartWarning { get; private set; }

    private int BytesPerFrame => _channels * 2;

    public const double MinGainDb = -24;
    public const double MaxGainDb = 12;

    /// <summary>How long the clip light stays lit after the last clipped sample.</summary>
    private const long ClipHoldMs = 1500;

    private double _gainDb;
    private volatile float _gainFactor = 1f;
    private long _lastClipTick = long.MinValue / 2;
    private volatile bool _clippedDuringTake;

    public double GainDb
    {
        get => _gainDb;
        set
        {
            _gainDb = Math.Clamp(value, MinGainDb, MaxGainDb);
            _gainFactor = (float)Math.Pow(10, _gainDb / 20.0);
        }
    }

    public bool IsClipping => Environment.TickCount64 - _lastClipTick < ClipHoldMs;

    public bool ClippedDuringTake => _clippedDuringTake;

    public RecordingService()
    {
        _recordingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "JingleBox2", "recordings");

        Directory.CreateDirectory(_recordingsDir);
    }

    public IReadOnlyList<string> GetInputDevices()
    {
        _inputDevices.Clear();
        var names = new List<string>();

        for (int i = 0; i < Bass.RecordingDeviceCount; i++)
        {
            if (!Bass.RecordGetDeviceInfo(i, out var info)) continue;
            if (!info.IsEnabled) continue;
            if (!IsUsableInput(info.Driver)) continue;

            _inputDevices.Add((info.Name, i));
            names.Add(info.Name);
        }

        return names;
    }

    /// <summary>
    /// On Linux, BASS enumerates every ALSA PCM definition as a "device", which drags in
    /// rate converters, up/downmixers and other plugins that can be opened but never
    /// deliver audio (lavrate, samplerate, speexrate, speex, upmix, vdownmix, ...).
    /// Keep only the real capture routes: the ALSA default, the sound servers, and hardware.
    /// Other platforms report actual devices, so nothing is filtered there.
    /// </summary>
    private static bool IsUsableInput(string? driver)
    {
        if (!OperatingSystem.IsLinux()) return true;
        if (string.IsNullOrEmpty(driver)) return true;

        if (driver is "default" or "pulse" or "pipewire" or "jack") return true;

        return HardwareDriverPattern.IsMatch(driver);
    }

    private static readonly Regex HardwareDriverPattern =
        new(@"^(plug)?hw:\d+(,\d+)*$", RegexOptions.Compiled);

    public void StartRecording()
    {
        if (_isRecording) return;

        _clippedDuringTake = false;
        _lastClipTick = long.MinValue / 2;
        lock (_recordingBuffer)
        {
            _recordingBuffer.Clear();
        }

        // Already open for the meter: keep the same capture and start keeping what it hears,
        // rather than closing the device and opening it again under the user.
        if (!_capturing) OpenInput();

        _isRecording = true;
    }

    public void StartMonitoring()
    {
        if (_isMonitoring) return;

        if (!_capturing) OpenInput();

        _isMonitoring = true;
    }

    public void StopMonitoring()
    {
        if (!_isMonitoring) return;

        _isMonitoring = false;

        // A take keeps the input open, whatever the meter is doing.
        if (!_isRecording) CloseInput();
    }

    /// <summary>Opens the selected input, falling back to the default when it will not open.</summary>
    private void OpenInput()
    {
        LastStartWarning = null;

        int deviceIndex = GetDeviceIndex(SelectedDevice);

        try
        {
            StartOnDevice(deviceIndex);
        }
        catch (InvalidOperationException ex) when (deviceIndex != DefaultDevice)
        {
            // A hardware device is often held exclusively by PipeWire/PulseAudio, so opening
            // it directly fails. Retry on the system default instead of dead-ending.
            StartOnDevice(DefaultDevice);
            LastStartWarning = $"'{SelectedDevice}' could not be opened ({ex.Message}); recording from the default input instead.";
        }
    }

    private void StartOnDevice(int deviceIndex)
    {
        // Recording what an output is playing is a different capture altogether, so it takes
        // the other path and none of the device handling below applies to it.
        if (_loopbackDevice is int loopback)
        {
            if (!_loopback.Start(loopback, OnLoopbackData))
                throw new InvalidOperationException("The output could not be captured.");

            _sampleRate = _loopback.SampleRate;
            _channels = _loopback.Channels;
            _capturing = true;

            return;
        }

        _sampleRate = DefaultSampleRate;
        _channels = DefaultChannels;

        if (_initializedDevice != deviceIndex)
        {
            FreeDevice();

            if (!Bass.RecordInit(deviceIndex))
                throw new InvalidOperationException($"Bass.RecordInit failed: {Bass.LastError}");

            _initializedDevice = deviceIndex;
            _resolvedDevice = Bass.CurrentRecordingDevice; // -1 resolves to a concrete index
        }
        else
        {
            // The current recording device is a per-thread setting in BASS, so re-assert it
            // in case start and stop run on different threads.
            Bass.CurrentRecordingDevice = _resolvedDevice;
        }

        _recordCallback = OnRecordData;
        _recordHandle = Bass.RecordStart(_sampleRate, _channels, BassFlags.Default, _recordCallback);

        if (_recordHandle == 0)
        {
            var error = Bass.LastError;
            _recordCallback = null;
            FreeDevice();
            throw new InvalidOperationException($"Bass.RecordStart failed: {error}");
        }

        _capturing = true;
    }

    public void StopRecording()
    {
        if (!_isRecording) return;

        _isRecording = false;

        // Still watching the level: leave the input open so the meter keeps reading.
        if (!_isMonitoring) CloseInput();
    }

    private void CloseInput()
    {
        if (!_capturing) return;

        if (_loopback.IsRunning)
        {
            _loopback.Stop();
            _capturing = false;
            return;
        }

        Bass.ChannelStop(_recordHandle);
        Bass.StreamFree(_recordHandle);
        _recordHandle = 0;
        _capturing = false;
        _recordCallback = null;

        FreeDevice();
    }

    private void FreeDevice()
    {
        if (_initializedDevice == NoDevice) return;

        // RecordFree() frees the calling thread's current device, so point it at ours first.
        if (_resolvedDevice != NoDevice)
            Bass.CurrentRecordingDevice = _resolvedDevice;

        Bass.RecordFree();
        _initializedDevice = NoDevice;
        _resolvedDevice = NoDevice;
    }

    public byte[] GetRecentRecordingData(int maxBytes)
    {
        // Nothing has arrived for a while: the source is not sending, which is silence and not
        // the last thing it sent.
        if (Environment.TickCount64 - _lastDataTick > RecentDataStaleMs) return Array.Empty<byte>();

        lock (_recordingBuffer)
        {
            if (_recordingBuffer.Count == 0) return Array.Empty<byte>();

            int count = Math.Min(maxBytes, _recordingBuffer.Count);
            count -= count % BytesPerFrame; // keep whole frames so samples stay aligned
            if (count <= 0) return Array.Empty<byte>();

            int start = _recordingBuffer.Count - count;
            return _recordingBuffer.GetRange(start, count).ToArray();
        }
    }

    public Task<string> SaveRecordingAsync(string fileName)
    {
        byte[] pcmData;
        lock (_recordingBuffer)
        {
            if (_recordingBuffer.Count == 0)
                throw new InvalidOperationException("No recording data to save");

            pcmData = _recordingBuffer.ToArray();
        }

        string filePath = Path.Combine(_recordingsDir, $"{fileName}.wav");

        return Task.Run(() =>
        {
            try
            {
                WavFile.Write(filePath, pcmData, _sampleRate, _channels);
                return filePath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save recording: {ex.Message}", ex);
            }
        });
    }

    /// <summary>
    /// Audio from the loopback capture. It has already been turned into 16 bit samples, so from
    /// here it goes the same way as anything from a microphone.
    /// </summary>
    private void OnLoopbackData(byte[] data)
    {
        if (data.Length == 0) return;

        _lastDataTick = Environment.TickCount64;

        if (ApplyGainAndDetectClipping(data))
        {
            _lastClipTick = Environment.TickCount64;
            _clippedDuringTake = true;
        }

        lock (_recordingBuffer)
        {
            _recordingBuffer.AddRange(data);

            if (!_isRecording && _recordingBuffer.Count > MonitorBufferBytes)
                _recordingBuffer.RemoveRange(0, _recordingBuffer.Count - MonitorBufferBytes);
        }
    }

    private bool OnRecordData(int handle, IntPtr buffer, int length, IntPtr user)
    {
        if (buffer != IntPtr.Zero && length > 0)
        {
            _lastDataTick = Environment.TickCount64;

            byte[] data = new byte[length];
            System.Runtime.InteropServices.Marshal.Copy(buffer, data, 0, length);

            if (ApplyGainAndDetectClipping(data))
            {
                _lastClipTick = Environment.TickCount64;
                _clippedDuringTake = true;
            }

            lock (_recordingBuffer)
            {
                _recordingBuffer.AddRange(data);

                // Only watching the level: keep the last moment and let the rest go, or an
                // afternoon of monitoring would fill memory with audio nobody asked for.
                if (!_isRecording && _recordingBuffer.Count > MonitorBufferBytes)
                    _recordingBuffer.RemoveRange(0, _recordingBuffer.Count - MonitorBufferBytes);
            }
        }
        return true;
    }

    /// <summary>
    /// Scales the buffer in place and reports whether anything clipped, either because the
    /// input arrived at full scale or because the gain pushed it there.
    /// </summary>
    private bool ApplyGainAndDetectClipping(byte[] data)
    {
        float gain = _gainFactor;
        bool unity = gain == 1f;
        bool clipped = false;

        for (int i = 0; i + 1 < data.Length; i += 2)
        {
            short sample = (short)(data[i] | (data[i + 1] << 8));

            // Full-scale input means the signal was already squared off before we saw it.
            if (sample >= short.MaxValue || sample <= short.MinValue)
                clipped = true;

            if (unity) continue;

            float scaled = sample * gain;
            if (scaled > short.MaxValue) { scaled = short.MaxValue; clipped = true; }
            else if (scaled < short.MinValue) { scaled = short.MinValue; clipped = true; }

            sample = (short)scaled;
            data[i] = (byte)(sample & 0xFF);
            data[i + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return clipped;
    }

    private int GetDeviceIndex(string? deviceName)
    {
        if (string.IsNullOrEmpty(deviceName)) return DefaultDevice;

        if (_inputDevices.Count == 0)
            GetInputDevices();

        foreach (var (name, index) in _inputDevices)
        {
            if (name == deviceName) return index;
        }

        return DefaultDevice;
    }

    public void Dispose()
    {
        StopRecording();
        FreeDevice();
    }
}
