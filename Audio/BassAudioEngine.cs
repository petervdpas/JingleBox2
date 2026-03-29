// ===============================
// Audio/BassAudioEngine.cs
// ===============================
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using JingleBox2.Config;
using JingleBox2.Models;
using ManagedBass;

namespace JingleBox2.Audio;

public sealed class BassAudioEngine : IAudioEngine
{
    private readonly object _lock = new();

    private int _currentDeviceId = -1;

    private int[] _padStreams;
    private PadSourceKind[] _padKinds;
    private string?[] _padSources;
    private float[] _padVolumes;
    private bool[] _padLoops;
    private double[] _padFadeIn;
    private double[] _padFadeOut;

    // ManagedBass sync must be kept alive
    private readonly SyncProcedure _endSync;

    public int PadCount { get { lock (_lock) return _padStreams.Length; } }

    public event EventHandler<PadPlaybackChanged>? PadPlaybackChanged;

    public BassAudioEngine(int padCount = 8)
    {
        _padStreams = new int[padCount];
        _padKinds = new PadSourceKind[padCount];
        _padSources = new string?[padCount];
        _padVolumes = new float[padCount];
        _padLoops = new bool[padCount];
        _padFadeIn = new double[padCount];
        _padFadeOut = new double[padCount];

        _endSync = OnChannelEnd;

        for (int i = 0; i < padCount; i++)
        {
            _padKinds[i] = PadSourceKind.None;
            _padSources[i] = null;
            _padVolumes[i] = 1.0f;
            _padStreams[i] = 0;
            _padLoops[i] = false;
            _padFadeIn[i] = 0;
            _padFadeOut[i] = 0;
        }
    }

    public bool IsPadPlaying(int padIndex)
    {
        lock (_lock)
        {
            if (!InRange(padIndex)) return false;

            var handle = _padStreams[padIndex];
            if (handle == 0) return false;

            var state = Bass.ChannelIsActive(handle);
            return state == PlaybackState.Playing || state == PlaybackState.Stalled;
        }
    }

    public double GetPadProgress(int padIndex)
    {
        lock (_lock)
        {
            if (!InRange(padIndex)) return 0;
            if (_padKinds[padIndex] == PadSourceKind.StreamUrl) return 0;
            var handle = _padStreams[padIndex];
            if (handle == 0) return 0;
            var len = Bass.ChannelGetLength(handle);
            if (len <= 0) return 0;
            var pos = Bass.ChannelGetPosition(handle);
            return Math.Clamp((double)pos / len, 0, 1);
        }
    }

    public float GetPadLevel(int padIndex)
    {
        lock (_lock)
        {
            if (!InRange(padIndex)) return 0;
            var handle = _padStreams[padIndex];
            if (handle == 0) return 0;

            var state = Bass.ChannelIsActive(handle);
            if (state != PlaybackState.Playing && state != PlaybackState.Stalled) return 0;

            int raw = Bass.ChannelGetLevel(handle);
            if (raw == -1) return 0;

            int left = (raw >> 16) & 0xFFFF;
            int right = raw & 0xFFFF;
            float peak = Math.Max(left, right) / 32768f;
            return Math.Clamp(peak, 0f, 1f);
        }
    }

    public float GetPadChannelVolume(int padIndex)
    {
        lock (_lock)
        {
            if (!InRange(padIndex)) return _padVolumes[padIndex];
            var handle = _padStreams[padIndex];
            if (handle == 0) return _padVolumes[padIndex];
            if (Bass.ChannelGetAttribute(handle, ChannelAttribute.Volume, out float vol))
                return vol;
            return _padVolumes[padIndex];
        }
    }

    public IReadOnlyList<OutputDevice> GetOutputDevices()
    {
        var list = new List<OutputDevice>();

        for (int i = 0; Bass.GetDeviceInfo(i, out var info); i++)
        {
            if (!info.IsEnabled) continue;
            list.Add(new OutputDevice(i, info.Name));
        }

        return list;
    }

    IEnumerable<OutputDevice> IAudioEngine.GetOutputDevices() => GetOutputDevices();

    public void SetOutputDevice(int deviceId)
    {
        lock (_lock)
        {
            if (_currentDeviceId == deviceId) return;

            StopAllAndFreeStreamsLocked();

            if (_currentDeviceId >= 0)
                Bass.Free();

            _currentDeviceId = deviceId;

            if (!Bass.Init(deviceId, 44100))
                throw new InvalidOperationException($"Bass.Init failed: {Bass.LastError}");

            LoadPlugins();
        }
    }

    public void SetPadSource(int padIndex, PadSourceKind kind, string? source)
    {
        lock (_lock)
        {
            if (!InRange(padIndex)) return;

            _padKinds[padIndex] = kind;
            _padSources[padIndex] = string.IsNullOrWhiteSpace(source) ? null : source;

            FreeStreamLocked(padIndex);
            Raise(padIndex, PadPlaybackState.Stopped);
        }
    }

    public void SetPadVolume(int padIndex, float volume)
    {
        lock (_lock)
        {
            if (!InRange(padIndex)) return;

            volume = Math.Clamp(volume, 0f, 1f);
            _padVolumes[padIndex] = volume;

            var handle = _padStreams[padIndex];
            if (handle != 0)
                Bass.ChannelSetAttribute(handle, ChannelAttribute.Volume, volume);
        }
    }

    public void SetPadLoop(int padIndex, bool loop)
    {
        lock (_lock)
        {
            if (!InRange(padIndex)) return;

            _padLoops[padIndex] = loop;

            // Free the existing stream so it's recreated with the correct loop flag on next play
            FreeStreamLocked(padIndex);
        }
    }

    public void SetPadFadeIn(int padIndex, double seconds)
    {
        lock (_lock)
        {
            if (!InRange(padIndex)) return;
            _padFadeIn[padIndex] = Math.Max(0, seconds);
        }
    }

    public void SetPadFadeOut(int padIndex, double seconds)
    {
        lock (_lock)
        {
            if (!InRange(padIndex)) return;
            _padFadeOut[padIndex] = Math.Max(0, seconds);
        }
    }

    public void PlaySample(int padIndex, string filePath, float volume)
    {
        lock (_lock)
        {
            if (!InRange(padIndex)) return;
            if (string.IsNullOrWhiteSpace(filePath)) return;

            EnsureInitLocked();

            _padKinds[padIndex] = PadSourceKind.File;

            if (!string.Equals(_padSources[padIndex], filePath, StringComparison.OrdinalIgnoreCase))
            {
                _padSources[padIndex] = filePath;
                FreeStreamLocked(padIndex);
            }

            _padVolumes[padIndex] = Math.Clamp(volume, 0f, 1f);

            var handle = _padStreams[padIndex];
            if (handle != 0)
                Bass.ChannelSetAttribute(handle, ChannelAttribute.Volume, _padVolumes[padIndex]);

            if (handle == 0)
            {
                var flags = BassFlags.Prescan | (_padLoops[padIndex] ? BassFlags.Loop : BassFlags.Default);
                handle = Bass.CreateStream(filePath, Flags: flags);
                if (handle == 0)
                    throw new InvalidOperationException($"CreateStream(file) failed: {Bass.LastError}");

                _padStreams[padIndex] = handle;

                // Only register end-sync for non-looping streams; looping streams never end
                if (!_padLoops[padIndex])
                    Bass.ChannelSetSync(handle, SyncFlags.End, 0, _endSync, new IntPtr(padIndex));
            }

            var fadeIn = _padFadeIn[padIndex];
            if (fadeIn > 0)
            {
                Bass.ChannelSetAttribute(handle, ChannelAttribute.Volume, 0f);
                Bass.ChannelSetPosition(handle, 0);
                if (!Bass.ChannelPlay(handle))
                    throw new InvalidOperationException($"ChannelPlay(file) failed: {Bass.LastError}");
                Bass.ChannelSlideAttribute(handle, ChannelAttribute.Volume, _padVolumes[padIndex], (int)(fadeIn * 1000));
            }
            else
            {
                Bass.ChannelSetAttribute(handle, ChannelAttribute.Volume, _padVolumes[padIndex]);
                Bass.ChannelSetPosition(handle, 0);
                if (!Bass.ChannelPlay(handle))
                    throw new InvalidOperationException($"ChannelPlay(file) failed: {Bass.LastError}");
            }

            Raise(padIndex, PadPlaybackState.Playing);
        }
    }

    public void PlayStream(int padIndex, string url, float volume)
    {
        lock (_lock)
        {
            if (!InRange(padIndex)) return;
            if (string.IsNullOrWhiteSpace(url)) return;

            EnsureInitLocked();

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException("Stream URL must start with http:// or https://");

            // If you want to block Mixcloud *pages* but allow audiocdn.mixcloud.com mp3:
            if (uri.Host.Contains("mixcloud.com", StringComparison.OrdinalIgnoreCase) &&
                !uri.Host.Contains("audiocdn.mixcloud.com", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Mixcloud page links are not direct audio streams.");

            _padKinds[padIndex] = PadSourceKind.StreamUrl;

            if (!string.Equals(_padSources[padIndex], url, StringComparison.OrdinalIgnoreCase))
            {
                _padSources[padIndex] = url;
                FreeStreamLocked(padIndex);
            }

            _padVolumes[padIndex] = Math.Clamp(volume, 0f, 1f);

            var handle = _padStreams[padIndex];
            if (handle != 0)
                Bass.ChannelSetAttribute(handle, ChannelAttribute.Volume, _padVolumes[padIndex]);

            if (handle == 0)
            {
                // Browser-ish headers. Some CDNs require these.
                var urlWithHeaders =
                    url + "\r\n" +
                    "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36\r\n" +
                    "Referer: https://www.mixcloud.com/\r\n";

                var flags = BassFlags.AutoFree | BassFlags.StreamDownloadBlocks;

                handle = Bass.CreateStream(urlWithHeaders, 0, flags, null);
                if (handle == 0)
                    throw new InvalidOperationException($"CreateStream(url) failed: {Bass.LastError}");

                _padStreams[padIndex] = handle;

                // For streams, "end" can occur if connection drops or stream closes.
                Bass.ChannelSetSync(handle, SyncFlags.End, 0, _endSync, new IntPtr(padIndex));
            }

            var fadeIn = _padFadeIn[padIndex];
            if (fadeIn > 0)
            {
                Bass.ChannelSetAttribute(handle, ChannelAttribute.Volume, 0f);
                if (!Bass.ChannelPlay(handle))
                    throw new InvalidOperationException($"ChannelPlay(url) failed: {Bass.LastError}");
                Bass.ChannelSlideAttribute(handle, ChannelAttribute.Volume, _padVolumes[padIndex], (int)(fadeIn * 1000));
            }
            else
            {
                Bass.ChannelSetAttribute(handle, ChannelAttribute.Volume, _padVolumes[padIndex]);
                if (!Bass.ChannelPlay(handle))
                    throw new InvalidOperationException($"ChannelPlay(url) failed: {Bass.LastError}");
            }

            Raise(padIndex, PadPlaybackState.Playing);
        }
    }

    public void StopSample(int padIndex)
    {
        lock (_lock)
        {
            if (!InRange(padIndex)) return;

            var handle = _padStreams[padIndex];
            if (handle == 0) return;

            var isStream = _padKinds[padIndex] == PadSourceKind.StreamUrl;

            var fadeOut = _padFadeOut[padIndex];
            if (fadeOut > 0 && Bass.ChannelIsActive(handle) == PlaybackState.Playing)
            {
                Bass.ChannelSlideAttribute(handle, ChannelAttribute.Volume, 0f, (int)(fadeOut * 1000));

                var capturedIndex = padIndex;
                var capturedIsStream = isStream;
                var capturedHandle = handle;
                Timer? timer = null;
                timer = new Timer(_ =>
                {
                    lock (_lock)
                    {
                        try
                        {
                            // Only clean up if the handle hasn't been replaced by a new PlaySample call
                            if (_padStreams.Length > capturedIndex && _padStreams[capturedIndex] == capturedHandle)
                            {
                                if (capturedIsStream)
                                    FreeStreamLocked(capturedIndex);
                                else
                                {
                                    Bass.ChannelStop(capturedHandle);
                                    Bass.ChannelSetPosition(capturedHandle, 0);
                                    Raise(capturedIndex, PadPlaybackState.Stopped);
                                }
                            }
                        }
                        finally
                        {
                            timer?.Dispose();
                        }
                    }
                }, null, (int)(fadeOut * 1000), Timeout.Infinite);
                return;
            }

            // For streams: free completely so a fresh connection is made on next play
            if (isStream)
            {
                FreeStreamLocked(padIndex);
                return;
            }

            Bass.ChannelStop(handle);
            Bass.ChannelSetPosition(handle, 0);
            Raise(padIndex, PadPlaybackState.Stopped);
        }
    }

    private void OnChannelEnd(int handle, int channel, int data, IntPtr user)
    {
        var padIndex = user.ToInt32();

        lock (_lock)
        {
            // For AutoFree streams, BASS frees the handle after this callback.
            // Clear our reference so we don't try to reuse a dead handle.
            if (InRange(padIndex) && _padStreams[padIndex] == handle)
                _padStreams[padIndex] = 0;
        }

        Raise(padIndex, PadPlaybackState.Stopped);
    }

    private void Raise(int padIndex, PadPlaybackState state, string? message = null)
    {
        PadPlaybackChanged?.Invoke(this, new PadPlaybackChanged(padIndex, state, message));
    }

    private bool InRange(int padIndex) =>
        padIndex >= 0 && padIndex < _padStreams.Length;

    private void LoadPlugins()
    {
        // Load AAC plugin if available
        var dir = AppContext.BaseDirectory;
        var aacLib = OperatingSystem.IsWindows()
            ? Path.Combine(dir, "bass_aac.dll")
            : Path.Combine(dir, "libbass_aac.so");

        if (File.Exists(aacLib))
            Bass.PluginLoad(aacLib);
    }

    private void EnsureInitLocked()
    {
        if (_currentDeviceId >= 0) return;

        if (!Bass.Init(0, 44100))
            throw new InvalidOperationException($"Bass.Init default device failed: {Bass.LastError}");
        _currentDeviceId = 0;

        LoadPlugins();
    }

    private void FreeStreamLocked(int padIndex)
    {
        var handle = _padStreams[padIndex];
        if (handle == 0) return;

        _padStreams[padIndex] = 0;

        // Check if BASS still knows about this handle (may be auto-freed already)
        var state = Bass.ChannelIsActive(handle);
        if (state != PlaybackState.Stopped)
            Bass.ChannelStop(handle);
        Bass.StreamFree(handle);

        Raise(padIndex, PadPlaybackState.Stopped);
    }

    private void StopAllAndFreeStreamsLocked()
    {
        for (int i = 0; i < _padStreams.Length; i++)
            FreeStreamLocked(i);
    }

    public void Resize(int newPadCount)
    {
        lock (_lock)
        {
            if (newPadCount == _padStreams.Length) return;

            StopAllAndFreeStreamsLocked();

            _padStreams = new int[newPadCount];
            _padKinds = new PadSourceKind[newPadCount];
            _padSources = new string?[newPadCount];
            _padVolumes = new float[newPadCount];
            _padLoops = new bool[newPadCount];
            _padFadeIn = new double[newPadCount];
            _padFadeOut = new double[newPadCount];

            for (int i = 0; i < newPadCount; i++)
            {
                _padKinds[i] = PadSourceKind.None;
                _padSources[i] = null;
                _padVolumes[i] = 1.0f;
                _padStreams[i] = 0;
                _padLoops[i] = false;
                _padFadeIn[i] = 0;
                _padFadeOut[i] = 0;
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            StopAllAndFreeStreamsLocked();
            Bass.Free();
        }
    }
}
