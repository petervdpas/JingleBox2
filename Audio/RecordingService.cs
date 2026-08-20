using ManagedBass;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace JingleBox2.Audio;

public interface IRecordingService
{
    IReadOnlyList<string> GetInputDevices();
    string? SelectedDevice { get; set; }
    void StartRecording();
    void StopRecording();
    bool IsRecording { get; }
    float GetLevel();
    Task<string> SaveRecordingAsync(string fileName);
}

public sealed class RecordingService : IRecordingService
{
    private int _recordHandle = -1;
    private readonly string _recordingsDir;
    private List<byte> _recordingBuffer = new();
    private bool _isRecording;
    private int _sampleRate = 44100;
    private int _channels = 2;
    private int _currentDeviceId = -1;
    private RecordProcedure? _recordCallback;

    public string? SelectedDevice { get; set; }
    public bool IsRecording => _isRecording;

    public RecordingService()
    {
        _recordingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "JingleBox2", "recordings");

        Directory.CreateDirectory(_recordingsDir);
    }

    public IReadOnlyList<string> GetInputDevices()
    {
        var devices = new List<string>();
        int count = Bass.RecordingDeviceCount;

        for (int i = 0; i < count; i++)
        {
            if (Bass.RecordGetDeviceInfo(i, out var info))
                devices.Add(info.Name);
        }

        return devices;
    }

    public void StartRecording()
    {
        if (_isRecording) return;

        _recordingBuffer.Clear();
        int deviceIndex = GetDeviceIndex(SelectedDevice);

        if (deviceIndex != _currentDeviceId)
        {
            _currentDeviceId = deviceIndex;
            if (!Bass.RecordInit(deviceIndex))
                throw new InvalidOperationException($"Bass.RecordInit failed for device {deviceIndex}: {Bass.LastError}");
        }

        _recordCallback = OnRecordData;
        _recordHandle = Bass.RecordStart(_sampleRate, _channels, BassFlags.Default, _recordCallback);

        if (_recordHandle != 0)
        {
            _isRecording = true;
        }
        else
        {
            throw new InvalidOperationException($"Bass.RecordStart failed: {Bass.LastError}");
        }
    }

    private bool OnRecordData(int handle, IntPtr buffer, int length, IntPtr user)
    {
        if (buffer != IntPtr.Zero && length > 0)
        {
            byte[] data = new byte[length];
            System.Runtime.InteropServices.Marshal.Copy(buffer, data, 0, length);
            _recordingBuffer.AddRange(data);
        }
        return true;
    }

    public void StopRecording()
    {
        if (!_isRecording) return;

        Bass.ChannelStop(_recordHandle);
        _isRecording = false;
        _recordCallback = null;

        Bass.StreamFree(_recordHandle);
        _recordHandle = -1;
    }

    public float GetLevel()
    {
        if (!_isRecording || _recordHandle < 0) return 0;

        int level = Bass.ChannelGetLevel(_recordHandle);
        float left = (level & 0xFFFF) / 32768f;
        float right = ((level >> 16) & 0xFFFF) / 32768f;
        return Math.Max(left, right);
    }

    public async Task<string> SaveRecordingAsync(string fileName)
    {
        if (_recordingBuffer.Count == 0)
            throw new InvalidOperationException("No recording data to save");

        string filePath = Path.Combine(_recordingsDir, $"{fileName}.wav");

        try
        {
            await Task.Run(() => WriteWavFile(filePath, _recordingBuffer.ToArray()));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save recording: {ex.Message}", ex);
        }

        return filePath;
    }

    private void WriteWavFile(string filePath, byte[] pcmData)
    {
        using var fs = new FileStream(filePath, FileMode.Create);
        using var writer = new BinaryWriter(fs);

        int byteRate = _sampleRate * _channels * 2;
        int blockAlign = _channels * 2;

        writer.Write("RIFF".ToCharArray());
        writer.Write(36 + pcmData.Length);
        writer.Write("WAVE".ToCharArray());

        writer.Write("fmt ".ToCharArray());
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write((ushort)_channels);
        writer.Write(_sampleRate);
        writer.Write(byteRate);
        writer.Write((ushort)blockAlign);
        writer.Write((ushort)16);

        writer.Write("data".ToCharArray());
        writer.Write(pcmData.Length);
        writer.Write(pcmData);
    }

    private int GetDeviceIndex(string? deviceName)
    {
        if (string.IsNullOrEmpty(deviceName)) return -1;

        for (int i = 0; i < Bass.RecordingDeviceCount; i++)
        {
            if (Bass.RecordGetDeviceInfo(i, out var info) && info.Name == deviceName)
                return i;
        }

        return -1;
    }
}
