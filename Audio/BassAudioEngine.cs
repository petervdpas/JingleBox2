using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using JingleBox2.Config;
using JingleBox2.Models;
using ManagedBass;
using JingleBox2.Audio.Enums;
using JingleBox2.Config.Enums;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Plugins.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// Through BASS, and everything about it is per pad and held in an array indexed by pad number:
/// the arrays are swapped as a set by <see cref="Resize"/> and by nothing else, which is what
/// lets the audio thread read them without the lock.
/// </remarks>
public sealed class BassAudioEngine : IAudioEngine
{
    /// <summary>Held for anything that touches a pad's state or calls into BASS.</summary>
    private readonly object _lock = new();

    /// <summary>Which output BASS was opened on, or -1 before it has been opened at all.</summary>
    private int _currentDeviceId = -1;

    /// <summary>The BASS channel each pad is playing on, or 0 for one that is not open.</summary>
    private int[] _padStreams;

    /// <summary>What kind of thing each pad plays.</summary>
    private PadSourceKind[] _padKinds;

    /// <summary>The file or the address each pad plays, or null for a pad with nothing on it.</summary>
    private string?[] _padSources;

    /// <summary>How loud each pad is, 0 to 1.</summary>
    private float[] _padVolumes;

    /// <summary>Whether each pad goes round again at the end.</summary>
    private bool[] _padLoops;

    /// <summary>How long each pad takes to come up, in seconds.</summary>
    private double[] _padFadeIn;

    /// <summary>How long each pad takes to go down, in seconds.</summary>
    private double[] _padFadeOut;

    /// <summary>
    /// What BASS calls when a pad reaches its end, kept here because BASS holds the pointer and
    /// a collected delegate is a crash rather than a silence.
    /// </summary>
    private readonly SyncProcedure _endSync;

    /// <summary>Effects on pads, and the BASS handles that run them.</summary>
    private Plugins.Interfaces.IAudioInsert?[] _padInserts;

    /// <summary>The hook each effect is hung on, or 0 where nothing is hung.</summary>
    private int[] _padDsp;

    /// <summary>Kept alive for as long as any pad has an effect: BASS holds the pointer.</summary>
    private readonly DSPProcedure _dspProcedure;

    /// <summary>One scratch buffer per pad, taken on the UI thread, used on the audio one.</summary>
    private float[]?[] _padScratch;

    /// <summary>How many channels each pad's stream carries, read when the effect is hung on.</summary>
    private int[] _padChannels;

    /// <inheritdoc/>
    public int PadCount { get { lock (_lock) return _padStreams.Length; } }

    /// <inheritdoc/>
    public event EventHandler<PadPlaybackChanged>? PadPlaybackChanged;

    /// <summary>An engine with room for a number of pads, playing none of them.</summary>
    /// <remarks>
    /// BASS is not opened here. It is opened when something is first played, so a machine with no
    /// sound card can still start the application and look at it.
    /// </remarks>
    /// <param name="padCount">How many pads there are, which <see cref="Resize"/> can change.</param>
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
        _dspProcedure = OnPadDsp;

        _padInserts = new Plugins.Interfaces.IAudioInsert?[padCount];
        _padDsp = new int[padCount];
        _padScratch = new float[padCount][];
        _padChannels = new int[padCount];

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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public double GetPadProgress(int padIndex)
    {
        lock (_lock)
        {
            if (!InRange(padIndex)) return 0;
            if (_padKinds[padIndex] == PadSourceKind.Stream) return 0;
            var handle = _padStreams[padIndex];
            if (handle == 0) return 0;
            var len = Bass.ChannelGetLength(handle);
            if (len <= 0) return 0;
            var pos = Bass.ChannelGetPosition(handle);
            return Math.Clamp((double)pos / len, 0, 1);
        }
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public float GetOutputLevel()
    {
        lock (_lock)
        {
            float loudest = 0;

            for (int pad = 0; pad < _padStreams.Length; pad++)
            {
                int handle = _padStreams[pad];

                if (handle == 0) continue;

                var state = Bass.ChannelIsActive(handle);

                if (state != PlaybackState.Playing && state != PlaybackState.Stalled) continue;

                int raw = Bass.ChannelGetLevel(handle);

                if (raw == -1) continue;

                float peak = Math.Max((raw >> 16) & 0xFFFF, raw & 0xFFFF) / 32768f;

                if (peak > loudest) loudest = peak;
            }

            return Math.Clamp(loudest, 0f, 1f);
        }
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    /// <remarks>
    /// Whether a stream loops is decided when it is made, so the pad's stream is let go and the
    /// next play builds a new one with the right flag.
    /// </remarks>
    public void SetPadLoop(int padIndex, bool loop)
    {
        lock (_lock)
        {
            if (!InRange(padIndex)) return;

            _padLoops[padIndex] = loop;

            FreeStreamLocked(padIndex);
        }
    }

    /// <inheritdoc/>
    public void SetPadFadeIn(int padIndex, double seconds)
    {
        lock (_lock)
        {
            if (!InRange(padIndex)) return;
            _padFadeIn[padIndex] = Math.Max(0, seconds);
        }
    }

    /// <inheritdoc/>
    public void SetPadFadeOut(int padIndex, double seconds)
    {
        lock (_lock)
        {
            if (!InRange(padIndex)) return;
            _padFadeOut[padIndex] = Math.Max(0, seconds);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The stream is made as float, so a plugin on this pad meets the samples as they are rather
    /// than through a conversion each way, and it keeps whatever effect the pad has across
    /// whatever it plays next. Only a stream that does not loop is watched for its end, since one
    /// that loops never reaches one.
    /// </remarks>
    public void PlaySample(int padIndex, string filePath, float volume)
    {
        lock (_lock)
        {
            if (!InRange(padIndex)) return;
            if (string.IsNullOrWhiteSpace(filePath)) return;

            EnsureInitLocked();

            _padKinds[padIndex] = PadSourceKind.Recording;

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
                var flags = BassFlags.Prescan | BassFlags.Float
                    | (_padLoops[padIndex] ? BassFlags.Loop : BassFlags.Default);

                handle = Bass.CreateStream(filePath, Flags: flags);
                if (handle == 0)
                    throw new InvalidOperationException($"CreateStream(file) failed: {Bass.LastError}");

                _padStreams[padIndex] = handle;

                if (_padInserts[padIndex] != null) AttachDspLocked(padIndex, handle);

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

    /// <inheritdoc/>
    /// <remarks>
    /// A Mixcloud page is refused by name. It is a page rather than audio, so BASS opens it and
    /// then plays nothing, which reads as a broken pad; the address of a file on their own CDN is
    /// still allowed. Browser headers go with the request because some CDNs answer nothing
    /// without them. A stream reaching its end means the connection dropped or the station went
    /// off, which is why one is watched for its end whether it loops or not.
    /// </remarks>
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

            if (uri.Host.Contains("mixcloud.com", StringComparison.OrdinalIgnoreCase) &&
                !uri.Host.Contains("audiocdn.mixcloud.com", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Mixcloud page links are not direct audio streams.");

            _padKinds[padIndex] = PadSourceKind.Stream;

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
                var urlWithHeaders =
                    url + "\r\n" +
                    "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36\r\n" +
                    "Referer: https://www.mixcloud.com/\r\n";

                var flags = BassFlags.AutoFree | BassFlags.StreamDownloadBlocks | BassFlags.Float;

                handle = Bass.CreateStream(urlWithHeaders, 0, flags, null);
                if (handle == 0)
                    throw new InvalidOperationException($"CreateStream(url) failed: {Bass.LastError}");

                _padStreams[padIndex] = handle;

                if (_padInserts[padIndex] != null) AttachDspLocked(padIndex, handle);

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

    /// <inheritdoc/>
    /// <remarks>
    /// A pad with a fade out is left playing and taken down over that time, and tidied up
    /// afterwards by a timer, which checks the handle is still the one it started on: a pad
    /// played again during its own fade has a new stream by then, and stopping that would silence
    /// the press somebody has just made. A stream is let go rather than stopped, so the next play
    /// makes a fresh connection instead of carrying on from a stale one.
    /// </remarks>
    public void StopSample(int padIndex)
    {
        lock (_lock)
        {
            if (!InRange(padIndex)) return;

            var handle = _padStreams[padIndex];
            if (handle == 0) return;

            var isStream = _padKinds[padIndex] == PadSourceKind.Stream;

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

    /// <summary>A pad has reached its end, on BASS's own thread.</summary>
    /// <remarks>
    /// A stream is made to free itself, and BASS lets the handle go once this returns, so the
    /// reference to it is dropped here rather than being kept and reused after it is dead.
    /// </remarks>
    /// <param name="handle">The sync this came from.</param>
    /// <param name="channel">The channel that ended.</param>
    /// <param name="data">Unused.</param>
    /// <param name="user">Which pad it was, as it was handed over when the sync was set.</param>
    private void OnChannelEnd(int handle, int channel, int data, IntPtr user)
    {
        var padIndex = user.ToInt32();

        lock (_lock)
        {
            if (InRange(padIndex) && _padStreams[padIndex] == handle)
                _padStreams[padIndex] = 0;
        }

        Raise(padIndex, PadPlaybackState.Stopped);
    }

    /// <summary>Tells whoever is listening that a pad started, stopped or went wrong.</summary>
    private void Raise(int padIndex, PadPlaybackState state, string? message = null)
    {
        PadPlaybackChanged?.Invoke(this, new PadPlaybackChanged(padIndex, state, message));
    }

    /// <summary>Whether there is a pad by that number.</summary>
    private bool InRange(int padIndex) =>
        padIndex >= 0 && padIndex < _padStreams.Length;

    /// <summary>
    /// Loads whatever BASS add-ons are beside the program, so a pad can play what they read.
    /// </summary>
    /// <remarks>
    /// Named nothing in particular, and the same call the importer makes. Dropping a library in
    /// beside the program is then the whole of adding a format: the pads play it and the shelf
    /// takes it, without either of them being told it exists.
    /// </remarks>
    private void LoadPlugins() => BassPlugins.Load();

    /// <inheritdoc/>
    public void EnsureInitialized()
    {
        lock (_lock) EnsureInitLocked();
    }

    /// <summary>Opens BASS on the default output if nothing has. Called holding the lock.</summary>
    private void EnsureInitLocked()
    {
        if (_currentDeviceId >= 0) return;

        if (!Bass.Init(0, 44100))
            throw new InvalidOperationException($"Bass.Init default device failed: {Bass.LastError}");
        _currentDeviceId = 0;

        LoadPlugins();
    }

    /// <summary>Stops a pad and lets its stream go. Called holding the lock.</summary>
    /// <remarks>
    /// The effect stays with the pad, but the hook it is hung on belongs to the stream that is
    /// going. BASS may have freed the handle already, since a stream frees itself at its end, so
    /// it is asked what state the channel is in rather than being told to stop regardless.
    /// </remarks>
    private void FreeStreamLocked(int padIndex)
    {
        var handle = _padStreams[padIndex];
        if (handle == 0) return;

        _padStreams[padIndex] = 0;

        _padDsp[padIndex] = 0;

        var state = Bass.ChannelIsActive(handle);
        if (state != PlaybackState.Stopped)
            Bass.ChannelStop(handle);
        Bass.StreamFree(handle);

        Raise(padIndex, PadPlaybackState.Stopped);
    }

    /// <summary>Stops every pad and lets every stream go. Called holding the lock.</summary>
    private void StopAllAndFreeStreamsLocked()
    {
        for (int i = 0; i < _padStreams.Length; i++)
            FreeStreamLocked(i);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Hung on whatever that pad is playing now. A pad with nothing loaded gets it when its next
    /// stream is made.
    /// </remarks>
    public void SetPadInsert(int padIndex, Plugins.Interfaces.IAudioInsert? insert)
    {
        lock (_lock)
        {
            if (!InRange(padIndex)) return;

            _padInserts[padIndex] = insert;

            int handle = _padStreams[padIndex];
            if (handle == 0) return;

            if (insert == null) RemoveDspLocked(padIndex, handle);
            else AttachDspLocked(padIndex, handle);
        }
    }

    /// <inheritdoc/>
    public Plugins.Interfaces.IAudioInsert? GetPadInsert(int padIndex)
    {
        lock (_lock) return InRange(padIndex) ? _padInserts[padIndex] : null;
    }

    /// <inheritdoc/>
    public int PadSampleRate(int padIndex)
    {
        lock (_lock)
        {
            if (!InRange(padIndex)) return 0;

            int handle = _padStreams[padIndex];
            if (handle == 0) return 0;

            var info = Bass.ChannelGetInfo(handle);
            return info.Frequency;
        }
    }

    /// <summary>
    /// Hangs the effect on a pad's stream, and takes everything the audio thread will need
    /// while it is here: the channel count and a buffer to work in.
    /// </summary>
    /// <remarks>
    /// Prepared here rather than in the callback on purpose. The callback runs on the audio
    /// thread and must not allocate, and it must not ask this class anything either: this
    /// method runs under the engine's lock, and a callback waiting for that same lock while
    /// the lock holder waits for BASS to finish a call is a deadlock, which is an application
    /// that stops responding rather than one that crashes.
    /// </remarks>
    private void AttachDspLocked(int padIndex, int handle)
    {
        var info = Bass.ChannelGetInfo(handle);

        _padChannels[padIndex] = Math.Max(1, info.Channels);
        _padScratch[padIndex] = new float[MaxDspFrames * 2];

        if (_padDsp[padIndex] != 0) return;

        _padDsp[padIndex] = Bass.ChannelSetDSP(handle, _dspProcedure, new IntPtr(padIndex));
    }

    /// <summary>
    /// The longest block the pad effects are prepared for. BASS hands out far less than this;
    /// anything longer is left alone rather than allocated for on the audio thread.
    /// </summary>
    private const int MaxDspFrames = 8192;

    /// <summary>Takes the effect's hook off a pad's stream. Called holding the lock.</summary>
    private void RemoveDspLocked(int padIndex, int handle)
    {
        if (_padDsp[padIndex] == 0) return;

        Bass.ChannelRemoveDSP(handle, _padDsp[padIndex]);
        _padDsp[padIndex] = 0;
    }

    /// <summary>
    /// The pad's audio, on its way out, handed to whatever effect is on that pad.
    /// </summary>
    /// <remarks>
    /// Runs on the audio thread with the channel's own samples in front of it. The streams are
    /// created as float for exactly this reason: a 16 bit stream would mean converting a
    /// buffer twice per block for no reason. A mono pad is widened into a stereo scratch and
    /// folded back afterwards, because an effect is a stereo thing.
    ///
    /// Nothing here takes the lock. This runs on the audio thread while another thread may be
    /// holding it inside a BASS call, and BASS waits for this callback to return: waiting for
    /// that lock here is a deadlock. The arrays are only ever swapped by a resize, which stops
    /// everything first, so a local copy of each reference is enough.
    ///
    /// The audio is worked through in pieces and never skipped. The first block BASS asks for is
    /// the whole playback buffer, half a second of it, which is far more than the working buffer
    /// holds, and a block passed over would be the start of every pad playing dry.
    /// </remarks>
    /// <param name="handle">The hook this came from.</param>
    /// <param name="channel">The channel being played.</param>
    /// <param name="buffer">The samples, changed where they lie.</param>
    /// <param name="length">How many bytes of them there are.</param>
    /// <param name="user">Which pad it is, as it was handed over when the hook was hung.</param>
    private void OnPadDsp(int handle, int channel, IntPtr buffer, int length, IntPtr user)
    {
        int padIndex = user.ToInt32();

        var inserts = _padInserts;
        var scratchpads = _padScratch;
        var counts = _padChannels;

        if (padIndex < 0 || padIndex >= inserts.Length) return;

        var insert = inserts[padIndex];
        if (insert == null) return;

        int channels = Math.Max(1, counts[padIndex]);

        int samples = length / sizeof(float);
        if (samples <= 0) return;

        int frames = samples / channels;
        if (frames <= 0) return;

        var scratch = scratchpads[padIndex];
        if (scratch == null) return;

        int most = scratch.Length / 2;

        for (int start = 0; start < frames; start += most)
        {
            int take = Math.Min(most, frames - start);
            if (!ProcessPadBlock(insert, scratch, buffer, start, take, channels)) return;
        }
    }

    /// <summary>
    /// One piece of a block: out of the channel's buffer, through the effect, and back in.
    /// Returns false when the effect fell over, which costs the rest of that block only.
    /// </summary>
    /// <remarks>
    /// More than two channels on a pad is unusual and the ones past the second are left alone.
    /// </remarks>
    /// <param name="insert">The effect.</param>
    /// <param name="scratch">The stereo buffer to work in, which is the pad's own.</param>
    /// <param name="buffer">The channel's samples.</param>
    /// <param name="start">Which frame of them this piece begins at.</param>
    /// <param name="frames">How many frames this piece holds.</param>
    /// <param name="channels">How many channels the pad's stream carries.</param>
    private static unsafe bool ProcessPadBlock(
        Plugins.Interfaces.IAudioInsert insert,
        float[] scratch,
        IntPtr buffer,
        int start,
        int frames,
        int channels)
    {
        float* audio = (float*)buffer + start * channels;

        if (channels == 1)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                scratch[frame * 2] = audio[frame];
                scratch[frame * 2 + 1] = audio[frame];
            }
        }
        else
        {
            for (int frame = 0; frame < frames; frame++)
            {
                scratch[frame * 2] = audio[frame * channels];
                scratch[frame * 2 + 1] = audio[frame * channels + 1];
            }
        }

        try
        {
            insert.Process(scratch, frames);
        }
        catch (Exception)
        {
            return false;
        }

        if (channels == 1)
        {
            for (int frame = 0; frame < frames; frame++)
                audio[frame] = (scratch[frame * 2] + scratch[frame * 2 + 1]) * 0.5f;
        }
        else
        {
            for (int frame = 0; frame < frames; frame++)
            {
                audio[frame * channels] = scratch[frame * 2];
                audio[frame * channels + 1] = scratch[frame * 2 + 1];
            }
        }

        return true;
    }

    /// <inheritdoc/>
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
            _padInserts = new Plugins.Interfaces.IAudioInsert?[newPadCount];
            _padDsp = new int[newPadCount];
            _padScratch = new float[newPadCount][];
            _padChannels = new int[newPadCount];

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

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_lock)
        {
            StopAllAndFreeStreamsLocked();
            Bass.Free();
        }
    }
}
