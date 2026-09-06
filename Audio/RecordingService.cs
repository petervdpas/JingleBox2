using ManagedBass;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// Through BASS for anything plugged in, and through <see cref="ILoopbackCapture"/> for an
/// output's own playback.
/// </remarks>
public sealed class RecordingService : IRecordingService, IDisposable
{
    /// <summary>Reading and writing WAV files. Holds nothing, so one serves the whole object.</summary>
    private readonly IWavFile _wav = new WavFile();

    /// <summary>No device at all, which is neither a real one nor the default.</summary>
    private const int NoDevice = int.MinValue;

    /// <summary>What BASS calls the system's own choice of device.</summary>
    private const int DefaultDevice = -1;

    /// <summary>What a capture device is opened at, unless it is a loopback.</summary>
    private const int DefaultSampleRate = 44100;

    /// <summary>How many channels a capture device is opened with.</summary>
    private const int DefaultChannels = 2;

    /// <summary>The channel BASS is recording on, or 0 for none.</summary>
    private int _recordHandle;

    /// <summary>Where takes are written.</summary>
    private readonly string _recordingsDir;

    /// <summary>
    /// What has been heard: the whole take while one is being kept, and the last moment of it
    /// otherwise. Held under its own lock, since it is written from the capture's thread.
    /// </summary>
    private readonly ITakeBuffer _heard = new TakeBuffer();


    /// <summary>Whether the level is being watched.</summary>
    private bool _isMonitoring;

    /// <summary>True while the input is open, whether for a take or only for the meter.</summary>
    private bool _capturing;

    /// <summary>
    /// How long the recent window keeps answering after the last audio arrived. A source that
    /// stops sending stops the callbacks with it, and without this the meter would sit at
    /// whatever it was reading when the sound stopped.
    /// </summary>
    private const long RecentDataStaleMs = 200;

    /// <summary>When audio last came in, for telling silence apart from nothing at all.</summary>
    private long _lastDataTick = long.MinValue / 2;

    /// <summary>The other capture path, for recording what an output is playing.</summary>
    private readonly ILoopbackCapture _loopback = new WasapiLoopback();

    /// <summary>
    /// The third capture path: one program on this machine, where the machine can do it.
    /// </summary>
    /// <remarks>
    /// Asked for once, since what a machine can do does not change while it is running, and
    /// held rather than made per take: the one that says no costs nothing at all and the other
    /// holds a capture that has to be stopped.
    /// </remarks>
    private readonly IProgramCapture _programs = new AudioCapture().Programs();

    /// <summary>
    /// What the current capture is running at. A loopback comes in at whatever the output mixes
    /// at, usually 48k, and the take is written at that rate rather than being resampled.
    /// </summary>
    private int _sampleRate = DefaultSampleRate;

    /// <summary>How many channels the current capture is running with.</summary>
    private int _channels = DefaultChannels;

    /// <summary>Which output is being captured, or null for a device that is plugged in.</summary>
    private int? _loopbackDevice;

    /// <summary>Which program is being captured, or nothing on either of the other two paths.</summary>
    private int? _loopbackProgram;

    /// <inheritdoc/>
    public int Channels => _channels;

    /// <inheritdoc/>
    public IReadOnlyList<LoopbackDevice> GetLoopbackDevices() => WasapiLoopback.GetDevices();

    /// <inheritdoc/>
    public int? LoopbackDevice
    {
        get => _loopbackDevice;
        set
        {
            if (_loopbackDevice == value) return;

            _loopbackDevice = value;

            ReopenInput();
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<AudioProgram> GetPrograms() => _programs.Programs();

    /// <inheritdoc/>
    public int? LoopbackProgram
    {
        get => _loopbackProgram;
        set
        {
            if (_loopbackProgram == value) return;

            _loopbackProgram = value;

            ReopenInput();
        }
    }

    /// <inheritdoc/>
    public void ReopenInput()
    {
        if (!_capturing) return;

        CloseInput();
        OpenInput();
    }

    /// <summary>Which device BASS was told to open, or <see cref="NoDevice"/>.</summary>
    private int _initializedDevice = NoDevice;

    /// <summary>
    /// Which device that turned out to be, since the default resolves to a real number and the
    /// current recording device has to be pointed back at it.
    /// </summary>
    private int _resolvedDevice = NoDevice;

    /// <summary>
    /// The callback BASS holds, kept here so it outlives the call that handed it over.
    /// </summary>
    private RecordProcedure? _recordCallback;

    /// <summary>What each device shown is called, and which number BASS knows it by.</summary>
    private readonly List<(string Name, int Index)> _inputDevices = new();

    /// <inheritdoc/>
    public string? SelectedDevice { get; set; }

    /// <inheritdoc/>
    public bool IsRecording => _heard.Recording;

    /// <inheritdoc/>
    public bool IsMonitoring => _isMonitoring;

    /// <inheritdoc/>
    public string? LastStartWarning { get; private set; }

    /// <summary>How wide one frame is, which is how a read is kept to whole frames.</summary>
    private int BytesPerFrame => _channels * 2;

    /// <summary>The quietest the input can be turned down to.</summary>
    public const double MinGainDb = -24;

    /// <summary>The loudest it can be turned up to.</summary>
    public const double MaxGainDb = 12;

    /// <summary>How long the clip light stays lit after the last clipped sample.</summary>
    private const long ClipHoldMs = 1500;

    /// <summary>The gain as it is set and shown, in decibels.</summary>
    private double _gainDb;

    /// <summary>
    /// The same gain as what a sample is multiplied by. Volatile, since it is set on one thread
    /// and read on the capture's.
    /// </summary>
    private volatile float _gainFactor = 1f;

    /// <summary>When a sample last clipped, for holding the light lit a moment.</summary>
    private long _lastClipTick = long.MinValue / 2;

    /// <summary>Whether anything clipped at all this take.</summary>
    private volatile bool _clippedDuringTake;

    /// <inheritdoc/>
    public double GainDb
    {
        get => _gainDb;
        set
        {
            _gainDb = Math.Clamp(value, MinGainDb, MaxGainDb);
            _gainFactor = (float)Math.Pow(10, _gainDb / 20.0);
        }
    }

    /// <inheritdoc/>
    public bool IsClipping => Environment.TickCount64 - _lastClipTick < ClipHoldMs;

    /// <inheritdoc/>
    public bool ClippedDuringTake => _clippedDuringTake;

    /// <summary>Makes the recordings folder if it is not there, and records into it.</summary>
    public RecordingService()
    {
        _recordingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "JingleBox2", "recordings");

        Directory.CreateDirectory(_recordingsDir);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Devices BASS reports but nothing could ever be recorded from are left out, which on Linux
    /// is most of them: see <see cref="IsUsableInput"/>.
    /// </remarks>
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

    /// <summary>What an ALSA name for real hardware looks like.</summary>
    private static readonly Regex HardwareDriverPattern =
        new(@"^(plug)?hw:\d+(,\d+)*$", RegexOptions.Compiled);

    /// <inheritdoc/>
    public void StartRecording()
    {
        if (_heard.Recording) return;

        _clippedDuringTake = false;
        _lastClipTick = long.MinValue / 2;

        _heard.Reset();

        if (!_capturing) OpenInput();

        _heard.Start();
    }

    /// <inheritdoc/>
    public void StartMonitoring()
    {
        if (_isMonitoring) return;

        if (!_capturing) OpenInput();

        _isMonitoring = true;
    }

    /// <inheritdoc/>
    public void StopMonitoring()
    {
        if (!_isMonitoring) return;

        _isMonitoring = false;

        if (!_heard.Recording) CloseInput();
    }

    /// <summary>Opens the selected input, falling back to the default when it will not open.</summary>
    /// <remarks>
    /// A hardware device is often held exclusively by PipeWire or PulseAudio, so opening it
    /// directly fails and the default is tried instead rather than the take dead-ending. What
    /// happened is left in <see cref="LastStartWarning"/> for the page to say.
    /// </remarks>
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
            StartOnDevice(DefaultDevice);
            LastStartWarning = $"'{SelectedDevice}' could not be opened ({ex.Message}); recording from the default input instead.";
        }
    }

    /// <summary>Opens one device and starts the audio arriving.</summary>
    /// <remarks>
    /// Recording what an output is playing is a different capture altogether, so it takes the
    /// other path and none of the device handling applies to it. On the BASS path the current
    /// recording device is a per-thread setting, so it is pointed back at the open one every time
    /// in case a start and a stop ran on different threads.
    /// </remarks>
    /// <param name="deviceIndex">The device, or <see cref="DefaultDevice"/>.</param>
    /// <exception cref="InvalidOperationException">It would not open.</exception>
    private void StartOnDevice(int deviceIndex)
    {
        if (_loopbackProgram is int program)
        {
            if (!_programs.Start(program, OnLoopbackData))
                throw new InvalidOperationException("That program could not be captured.");

            _sampleRate = _programs.SampleRate;
            _channels = _programs.Channels;
            _capturing = true;

            return;
        }

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
            _resolvedDevice = Bass.CurrentRecordingDevice;
        }
        else
        {
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

    /// <inheritdoc/>
    /// <remarks>
    /// **The take is taken out of the buffer here, under the lock, and that is not tidiness.**
    /// While nothing is being recorded the buffer is a monitor: every block that arrives trims
    /// it back to the last fifth of a second, so a callback landing between this method and the
    /// save is a take reduced to its last 200 milliseconds of near silence. The trim reads
    /// its own recording flag inside the same lock, so flipping it and lifting the audio out
    /// together is what closes the door: nothing can arrive in between.
    ///
    /// It was a race before this and is now not one. A window of a few instructions is still a
    /// window, and what falls through it is somebody's only copy of a performance.
    /// </remarks>
    /// <inheritdoc/>
    public void StopRecording()
    {
        if (!_heard.Recording) return;

        _heard.Stop();

        if (!_isMonitoring) CloseInput();
    }

    /// <summary>Stops the audio arriving and lets the device go, whichever path it came in on.</summary>
    private void CloseInput()
    {
        if (!_capturing) return;

        if (_programs.IsRunning)
        {
            _programs.Stop();
            _capturing = false;
            return;
        }

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

    /// <summary>Hands the recording device back to BASS.</summary>
    /// <remarks>
    /// RecordFree frees the calling thread's current device, so the current one is pointed at the
    /// one that is open first, or another thread's device would be freed instead.
    /// </remarks>
    private void FreeDevice()
    {
        if (_initializedDevice == NoDevice) return;

        if (_resolvedDevice != NoDevice)
            Bass.CurrentRecordingDevice = _resolvedDevice;

        Bass.RecordFree();
        _initializedDevice = NoDevice;
        _resolvedDevice = NoDevice;
    }

    /// <inheritdoc/>
    public byte[] GetRecentRecordingData(int maxBytes)
    {
        if (Environment.TickCount64 - _lastDataTick > RecentDataStaleMs) return Array.Empty<byte>();

        return _heard.Recent(maxBytes, BytesPerFrame);
    }

    /// <inheritdoc/>
    public Plugins.Interfaces.IAudioInsert? Effect { get; set; }

    /// <inheritdoc/>
    public int SampleRate => _sampleRate;

    /// <summary>What runs a take through the chain, which is arithmetic and nothing else.</summary>
    private readonly ITakeEffects _effects = new TakeEffects();

    /// <summary>The longest block the chain here is built for, and is therefore given.</summary>
    private const int ChainFrames = 2048;

    /// <summary>How long a stretch of silence the chain is given before a take goes through it.</summary>
    /// <remarks>
    /// Two seconds, which is past the tail of anything but a delay somebody has set to repeat
    /// for ever. See <see cref="ITakeEffects.Settle"/> for what it is for.
    /// </remarks>
    private const double SettleSeconds = 2;

    /// <inheritdoc/>
    /// <remarks>
    /// The buffer is read once, under the lock, and everything after that happens on the pool:
    /// running a take through a chain is a plugin's own arithmetic and a crossing per block, and
    /// none of it belongs on the thread that was asked to stop recording.
    ///
    /// A chain that leaves nothing behind is not a chain: an empty one writes one file and says
    /// there is no twin, so a take made with nothing on the page is exactly the take it always
    /// was.
    /// </remarks>
    public Task<SavedTake> WriteTakeAsync(string folder, string fileName, string cleanName)
    {
        byte[] pcmData = _heard.Take;

        if (pcmData.Length == 0)
            throw new InvalidOperationException("No recording data to save");

        Directory.CreateDirectory(folder);

        string filePath = Path.Combine(folder, $"{fileName}.wav");
        string cleanPath = Path.Combine(folder, $"{cleanName}.wav");

        var effect = Effect;
        int rate = _sampleRate;
        int channels = _channels;

        return Task.Run(() =>
        {
            try
            {
                if (effect is not { } chain || Empty(chain))
                {
                    _wav.Write(filePath, pcmData, rate, channels);
                    return new SavedTake(filePath, null);
                }

                _effects.Settle(chain, (int)(rate * SettleSeconds), ChainFrames);

                byte[] worked = _effects.Through(pcmData, channels, chain, ChainFrames);

                _wav.Write(cleanPath, pcmData, rate, channels);
                _wav.Write(filePath, worked, rate, _effects.Channels);

                return new SavedTake(filePath, cleanPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save recording: {ex.Message}", ex);
            }
        });
    }

    /// <summary>Whether a chain would leave a take exactly as it found it.</summary>
    /// <remarks>
    /// Asked of a <see cref="Plugins.PluginChain"/> and answered no for anything else, since an
    /// insert that is not a chain is something somebody put there on purpose. A chain holding
    /// nothing but bypassed slots counts as empty: what it would write is the take again under
    /// a second name.
    /// </remarks>
    /// <param name="effect">The chain to ask about.</param>
    private static bool Empty(Plugins.Interfaces.IAudioInsert effect)
    {
        if (effect is not Plugins.PluginChain chain) return false;

        foreach (var slot in chain.Slots)
        {
            if (!slot.Bypassed) return false;
        }

        return true;
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

        _heard.Add(data);
    }

    /// <summary>Audio from a capture device, on BASS's own thread.</summary>
    /// <remarks>
    /// While only the level is being watched the last moment is kept and the rest let go, or an
    /// afternoon of monitoring would fill memory with audio nobody asked for.
    /// </remarks>
    /// <param name="handle">The channel it came from, which is the only one there is.</param>
    /// <param name="buffer">The block.</param>
    /// <param name="length">How many bytes of it there are.</param>
    /// <param name="user">Unused, since what this needs is on the instance.</param>
    /// <returns>True, which is BASS's word for carry on.</returns>
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

            _heard.Add(data);
        }
        return true;
    }

    /// <summary>
    /// Scales the buffer in place and reports whether anything clipped, either because the
    /// input arrived at full scale or because the gain pushed it there.
    /// </summary>
    /// <remarks>
    /// A sample already at full scale counts as clipped whatever the gain is, since the signal was
    /// squared off before it ever arrived here and turning it down would only hide that.
    /// </remarks>
    /// <param name="data">The block, changed where it lies.</param>
    /// <returns>Whether anything in it clipped.</returns>
    private bool ApplyGainAndDetectClipping(byte[] data)
    {
        float gain = _gainFactor;
        bool unity = gain == 1f;
        bool clipped = false;

        for (int i = 0; i + 1 < data.Length; i += 2)
        {
            short sample = (short)(data[i] | (data[i + 1] << 8));

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

    /// <summary>Which number BASS knows a named device by, or the default when it is gone.</summary>
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

    /// <summary>Stops any take and lets the device go.</summary>
    public void Dispose()
    {
        StopRecording();
        FreeDevice();
    }
}
