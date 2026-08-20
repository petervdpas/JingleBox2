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
    /// Set when StartRecording could not use the selected device and fell back to the
    /// system default. Null when the selected device was used as-is.
    /// </summary>
    string? LastStartWarning { get; }

    byte[] GetRecentRecordingData(int maxBytes);
    Task<string> SaveRecordingAsync(string fileName);
}

public sealed class RecordingService : IRecordingService, IDisposable
{
    private const int NoDevice = int.MinValue;
    private const int DefaultDevice = -1;

    private int _recordHandle;
    private readonly string _recordingsDir;
    private readonly List<byte> _recordingBuffer = new();
    private bool _isRecording;
    private readonly int _sampleRate = 44100;
    private readonly int _channels = 2;
    private int _initializedDevice = NoDevice;
    private int _resolvedDevice = NoDevice;
    private RecordProcedure? _recordCallback;

    // Name -> BASS device index for the devices we chose to show.
    private readonly List<(string Name, int Index)> _inputDevices = new();

    public string? SelectedDevice { get; set; }
    public bool IsRecording => _isRecording;
    public string? LastStartWarning { get; private set; }

    private int BytesPerFrame => _channels * 2;

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

        LastStartWarning = null;
        lock (_recordingBuffer)
        {
            _recordingBuffer.Clear();
        }

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

        _isRecording = true;
    }

    public void StopRecording()
    {
        if (!_isRecording) return;

        Bass.ChannelStop(_recordHandle);
        Bass.StreamFree(_recordHandle);
        _recordHandle = 0;
        _isRecording = false;
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

    private bool OnRecordData(int handle, IntPtr buffer, int length, IntPtr user)
    {
        if (buffer != IntPtr.Zero && length > 0)
        {
            byte[] data = new byte[length];
            System.Runtime.InteropServices.Marshal.Copy(buffer, data, 0, length);
            lock (_recordingBuffer)
            {
                _recordingBuffer.AddRange(data);
            }
        }
        return true;
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
