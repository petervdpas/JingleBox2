using System;
using System.Collections.Generic;
using System.Threading;
using JingleBox2.Audio.Records;
using ManagedBass;
using JingleBox2.Audio.Enums;
using JingleBox2.Config.Enums;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// Through BASS, and everything about it is per pad and held in an array indexed by pad number:
/// the arrays are swapped as a set by <see cref="Resize"/> and by nothing else, which is what
/// lets the audio thread read them without the lock.
/// </remarks>
public sealed class BassAudioEngine : IAudioEngine
{
    /// <summary>The add-ons beside the program, loaded once for the process.</summary>
    private readonly IBassPlugins _plugins = new BassPlugins();

    /// <summary>The ASIO drivers, which are the other half of the output list.</summary>
    /// <remarks>
    /// Held rather than made per call, because it remembers whether the library is there at all
    /// and finding that out costs a thrown exception where it is not.
    /// </remarks>
    private readonly Interfaces.IAsioDevices _asio = new AsioDevices();

    /// <summary>How a device number names one out of two lists. Holds nothing.</summary>
    private readonly Interfaces.IAudioOutputs _outputs = new AudioOutputs();

    /// <summary>
    /// Everything this application plays, summed, which is the only way anything leaves.
    /// </summary>
    /// <remarks>
    /// Three of them because there are three things that make sound and each is one strip: the
    /// pads are one source however many are down, the take being auditioned is another, and the
    /// tracker sums its own tracks and arrives as the third. A sub-bus is what makes that true,
    /// since a mixer stream is itself a decoding channel and can be plugged into another one, and
    /// its level is the strip's fader.
    /// </remarks>
    private readonly Interfaces.IOutputBus _output = new OutputBus();

    /// <inheritdoc cref="_output"/>
    private readonly Interfaces.IOutputBus _padBus = new OutputBus();

    /// <inheritdoc cref="_output"/>
    private readonly Interfaces.IOutputBus _takeBus = new OutputBus();

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

    /// <summary>
    /// The same, for a pad on the bus, which needs the add-on's own sync and runs on the mixing
    /// thread. Kept for the same reason: the library holds the pointer.
    /// </summary>
    private readonly SyncProcedure _mixEndSync;

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
    /// <param name="deviceRate">What to open the card at, or nought for the default.</param>
    /// <param name="rate">The rule that decides, handed in so it can be asked without a card.</param>
    public BassAudioEngine(
        int padCount = 8,
        int deviceRate = 0,
        Interfaces.IOutputRate? rate = null)
    {
        _deviceRate = (rate ?? new OutputRate()).Chosen(deviceRate);

        _padStreams = new int[padCount];
        _padKinds = new PadSourceKind[padCount];
        _padSources = new string?[padCount];
        _padVolumes = new float[padCount];
        _padLoops = new bool[padCount];
        _padFadeIn = new double[padCount];
        _padFadeOut = new double[padCount];

        _endSync = OnChannelEnd;
        _mixEndSync = OnMixChannelEnd;
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

            return SoundingLocked(handle);
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

            return LevelLocked(handle);
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

                float peak = LevelLocked(handle);

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
    /// <remarks>
    /// The system's endpoints first and the ASIO drivers after, which is the order somebody wants
    /// them in: the system's is what everything already uses and ASIO is the deliberate choice.
    /// A machine with no ASIO library adds nothing and says nothing, since that is every Linux
    /// machine and it is not news.
    /// </remarks>
    public IReadOnlyList<AudioOutput> GetOutputDevices()
    {
        var list = new List<AudioOutput>();

        for (int i = 0; Bass.GetDeviceInfo(i, out var info); i++)
        {
            if (!info.IsEnabled) continue;
            list.Add(new AudioOutput(i, info.Name));
        }

        list.AddRange(_asio.Devices);

        return list;
    }

    IEnumerable<AudioOutput> IAudioEngine.GetOutputDevices() => GetOutputDevices();

    /// <inheritdoc/>
    /// <remarks>
    /// An ASIO driver is not a device BASS can be opened on. The driver owns the card, so BASS is
    /// opened on its own silent device instead and everything that would have been played is
    /// decoded and pulled through the driver, which is what <see cref="OutputKind"/> is for: the
    /// tracker asks before it makes its stream, since a stream that plays itself and is also
    /// pulled would be the same audio leaving by two routes.
    /// </remarks>
    public void SetOutputDevice(int deviceId)
    {
        lock (_lock)
        {
            if (_currentDeviceId == deviceId) return;

            OpenLocked(deviceId);
        }
    }

    /// <summary>Lets the current device go and opens one, with the lock held.</summary>
    /// <param name="deviceId">Which output, numbered across both lists.</param>
    private void OpenLocked(int deviceId)
    {
        StopAllAndFreeStreamsLocked();

        CloseBussesLocked();

        _asio.Close();

        if (_currentDeviceId >= 0)
            Bass.Free();

        _currentDeviceId = deviceId;

        var (kind, index) = _outputs.Which(deviceId);

        if (!Bass.Init(kind == Enums.AudioOutputKind.Asio ? SilentDevice : index, _deviceRate))
            throw new InvalidOperationException($"Bass.Init failed: {Bass.LastError}");

        LoadPlugins();

        OpenBussesLocked(kind == Enums.AudioOutputKind.Asio, index);
    }

    /// <summary>
    /// Opens the bus and its two sub-busses, with the lock held and BASS already up.
    /// </summary>
    /// <remarks>
    /// Nothing at all while the switch is off, which is what keeps the old path exactly as it
    /// was.
    ///
    /// The order matters and is the order of the audio: the sub-busses are made first and plugged
    /// into the output, so that a pad played before the tracker has ever started still has
    /// somewhere to go. What is played, or handed to the driver, is the output and never a source
    /// on it: a stream that plays itself and is also pulled is the same audio leaving by two
    /// routes.
    ///
    /// A driver that will not take the bus leaves everything open and silent rather than half
    /// wired, and says so.
    ///
    /// **A bus that will not open throws rather than being worked around.** There was a second
    /// path once, where a pad played at the card on its own, and it was reached by a setting
    /// somebody could turn off; the setting is gone and so is the path. What is left that can
    /// fail is BASSmix not being beside the program, and on that machine nothing can be summed
    /// at all: saying so where the output is opened reaches the pad that was pressed, which
    /// puts it on that pad. Playing the pads a different way and losing solo, pan, mute and
    /// ASIO in silence is the alternative, and it is worse.
    /// </remarks>
    /// <param name="pulled">Whether an ASIO driver drives the output rather than BASS playing it.</param>
    /// <param name="device">Which ASIO driver, where one is being used.</param>
    private void OpenBussesLocked(bool pulled, int device)
    {
        _output.BufferMs = StartingBufferMs();

        if (!_output.Open(_deviceRate, BusChannels, pulled))
            throw new InvalidOperationException(
                "The mixer stream could not be opened, so nothing can be played. " +
                "This needs BASSmix beside the program.");

        if (!_padBus.Open(_deviceRate, BusChannels, true) || !_takeBus.Open(_deviceRate, BusChannels, true))
            throw new InvalidOperationException(
                "The pad and take busses could not be opened, so nothing can be played.");

        _output.Add(_padBus.Handle);
        _output.Add(_takeBus.Handle);

        if (pulled)
        {
            if (!_asio.Open(device, _output.Handle, _deviceRate))
                Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Audio,
                    "bus: the driver would not take the bus, so nothing will be heard");

            return;
        }

        if (!Bass.ChannelPlay(_output.Handle))
            Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Audio,
                () => "bus: the bus would not play: " + Bass.LastError);
    }

    /// <summary>Lets the bus and its sub-busses go, with the lock held.</summary>
    /// <remarks>
    /// Before <c>Bass.Free</c>, since freeing the library takes every stream with it and a bus
    /// asked to unplug a source that has already gone is a refusal for no reason.
    /// </remarks>
    private void CloseBussesLocked()
    {
        _takeBus.Close();
        _padBus.Close();
        _output.Close();
    }

    /// <summary>Stereo, which is what everything either side of the bus is written for.</summary>
    private const int BusChannels = 2;

    /// <summary>
    /// What the bus holds ahead of the card until the tracker says what the settings ask for.
    /// </summary>
    /// <remarks>
    /// The platform's own default rather than nothing, because nothing means the library's 500 ms
    /// and a pad can be pressed before the tracker has ever started. Handing the bus the figure
    /// this application has actually been listened to on is the honest opening position, and the
    /// tracker overwrites it with whatever is stored the moment it joins.
    /// </remarks>
    private int StartingBufferMs()
    {
        var sizes = new AudioDefaults().Here;

        return Math.Max(1, (int)Math.Round(sizes.BufferFrames * 1000.0 / Math.Max(1, _deviceRate)));
    }

    /// <summary>
    /// BASS's own device that plays nothing, which is what is opened behind an ASIO driver.
    /// </summary>
    /// <remarks>
    /// Nought is not the first sound card, it is the one that decodes and outputs nothing at all.
    /// Everything above BASS goes on working, and what is decoded is pulled out by whatever is
    /// really driving the card.
    /// </remarks>
    private const int SilentDevice = 0;

    /// <inheritdoc/>
    /// <remarks>
    /// Two different silences, and they want different answers. No library at all is a file that
    /// was not shipped or a system ASIO was never made for; a library with nothing behind it is a
    /// machine where no ASIO driver has been installed, which is most Windows machines until a
    /// card or a driver like ASIO4ALL puts one there. Both look identical in the picker, which is
    /// an empty list and no reason.
    /// </remarks>
    public string OutputsMissing =>
        !_asio.Present ? _asio.Missing
        : _asio.Devices.Count == 0
            ? "No ASIO driver is installed on this machine, so there is none to pick. "
              + "One arrives with a card's own driver, or with something like ASIO4ALL."
            : "";

    /// <inheritdoc/>
    public Enums.AudioOutputKind OutputKind
    {
        get
        {
            lock (_lock) return _outputs.Which(_currentDeviceId).Kind;
        }
    }

    /// <inheritdoc/>
    public bool Feed(int stream, int rate)
    {
        int device;

        lock (_lock)
        {
            var (kind, index) = _outputs.Which(_currentDeviceId);

            if (kind != Enums.AudioOutputKind.Asio) return false;

            device = index;
        }

        return _asio.Open(device, stream, rate);
    }

    /// <inheritdoc/>
    public int OutputFrames => _asio.Frames;

    /// <inheritdoc/>
    public Interfaces.IOutputBus Output => _output;

    /// <inheritdoc/>
    public Interfaces.IOutputBus PadBus => _padBus;

    /// <inheritdoc/>
    public Interfaces.IOutputBus TakeBus => _takeBus;

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
                var flags = PadFlagsLocked(BassFlags.Prescan | BassFlags.Float
                    | (_padLoops[padIndex] ? BassFlags.Loop : BassFlags.Default));

                handle = Bass.CreateStream(filePath, Flags: flags);
                if (handle == 0)
                    throw new InvalidOperationException($"CreateStream(file) failed: {Bass.LastError}");

                _padStreams[padIndex] = handle;

                if (_padInserts[padIndex] != null) AttachDspLocked(padIndex, handle);

                if (!_padLoops[padIndex])
                    WatchEndLocked(handle, padIndex);
            }

            var fadeIn = _padFadeIn[padIndex];
            if (fadeIn > 0)
            {
                Bass.ChannelSetAttribute(handle, ChannelAttribute.Volume, 0f);
                Bass.ChannelSetPosition(handle, 0);
                if (!SoundLocked(handle))
                    throw new InvalidOperationException($"the pad would not start: {Bass.LastError}");
                Bass.ChannelSlideAttribute(handle, ChannelAttribute.Volume, _padVolumes[padIndex], (int)(fadeIn * 1000));
            }
            else
            {
                Bass.ChannelSetAttribute(handle, ChannelAttribute.Volume, _padVolumes[padIndex]);
                Bass.ChannelSetPosition(handle, 0);
                if (!SoundLocked(handle))
                    throw new InvalidOperationException($"the pad would not start: {Bass.LastError}");
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

                var flags = PadFlagsLocked(BassFlags.StreamDownloadBlocks | BassFlags.Float);

                handle = Bass.CreateStream(urlWithHeaders, 0, flags, null);
                if (handle == 0)
                    throw new InvalidOperationException($"CreateStream(url) failed: {Bass.LastError}");

                _padStreams[padIndex] = handle;

                if (_padInserts[padIndex] != null) AttachDspLocked(padIndex, handle);

                WatchEndLocked(handle, padIndex);
            }

            var fadeIn = _padFadeIn[padIndex];
            if (fadeIn > 0)
            {
                Bass.ChannelSetAttribute(handle, ChannelAttribute.Volume, 0f);
                if (!SoundLocked(handle))
                    throw new InvalidOperationException($"the pad would not start: {Bass.LastError}");
                Bass.ChannelSlideAttribute(handle, ChannelAttribute.Volume, _padVolumes[padIndex], (int)(fadeIn * 1000));
            }
            else
            {
                Bass.ChannelSetAttribute(handle, ChannelAttribute.Volume, _padVolumes[padIndex]);
                if (!SoundLocked(handle))
                    throw new InvalidOperationException($"the pad would not start: {Bass.LastError}");
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
            if (fadeOut > 0 && SoundingLocked(handle))
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
                                    SilenceLocked(capturedHandle);
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

            SilenceLocked(handle);
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
    private void LoadPlugins() => _plugins.Load();

    /// <inheritdoc/>
    public void EnsureInitialized()
    {
        lock (_lock) EnsureInitLocked();
    }

    /// <summary>
    /// What the card is opened at, decided once on the way up.
    /// </summary>
    /// <remarks>
    /// It was a literal 44100 in both places that open a device, while the tracker's mixer read
    /// the rate from the settings. They agree at 44100 and nowhere else, and where they disagree
    /// nothing says so: the sound is quietly resampled down to the card and back up by the system
    /// mixer. See <see cref="Interfaces.IOutputRate"/>.
    /// </remarks>
    private readonly int _deviceRate;

    /// <summary>Opens BASS on the default output if nothing has. Called holding the lock.</summary>
    private void EnsureInitLocked()
    {
        if (_currentDeviceId >= 0) return;

        if (!Bass.Init(0, _deviceRate))
            throw new InvalidOperationException($"Bass.Init default device failed: {Bass.LastError}");
        _currentDeviceId = 0;

        LoadPlugins();

        OpenBussesLocked(false, 0);
    }


    /// <summary>
    /// Starts a pad sounding, which on the bus is plugging it in rather than playing it.
    /// </summary>
    /// <remarks>
    /// A pad is a decoding channel, so nothing plays it: it is a source on the bus and sounding
    /// it is being added. Every place that starts, stops or asks about a pad goes through these
    /// four, because written out at each call site they would be a dozen chances for one of them
    /// to be forgotten, and a pad that quietly never joins the bus is exactly the fault the bus
    /// exists to end.
    ///
    /// **There is no second path any more.** Each of these used to fork on whether the bus was
    /// open, because the bus was a setting somebody could turn off; it is the only path now, and
    /// a machine where it cannot be opened says so at the moment the output is opened rather
    /// than playing pads a different way and losing solo, pan, mute and ASIO in silence.
    /// </remarks>
    /// <param name="handle">The pad's stream.</param>
    /// <returns>False where it would not start.</returns>
    private bool SoundLocked(int handle) => _padBus.Add(handle);

    /// <inheritdoc cref="SoundLocked"/>
    /// <param name="handle">The pad's stream.</param>
    private void SilenceLocked(int handle) => _padBus.Remove(handle);

    /// <inheritdoc cref="SoundLocked"/>
    /// <remarks>
    /// This cannot be asked of the channel. A decoding channel answers
    /// <see cref="PlaybackState.Playing"/> for as long as it has data in it, whether or not
    /// anything is pulling it, so a pad that has been stopped would go on reporting itself as
    /// playing until its stream was let go. Being plugged in is the question, and the bus is what
    /// knows the answer.
    /// </remarks>
    /// <param name="handle">The pad's stream.</param>
    /// <returns>Whether it is sounding now.</returns>
    private bool SoundingLocked(int handle) => _padBus.Holds(handle);

    /// <summary>
    /// How loud a pad is, 0 to 1, and nought for one that is not sounding.
    /// </summary>
    /// <remarks>
    /// **It has to be the add-on's own call, and the plain one costs the audio itself.**
    /// <c>Bass.ChannelGetLevel</c> measures by decoding data out of the channel, which is
    /// harmless where the channel is being played by the library and is theft where it is a
    /// source on a bus: every block it measured is a block the bus never got, so a meter would eat
    /// the sound it was reporting on. The add-on's own reads the channel's buffer instead, which
    /// is what the <see cref="BassFlags.MixerChanBuffer"/> given to every source is for.
    /// </remarks>
    /// <param name="handle">The pad's stream.</param>
    private float LevelLocked(int handle)
    {
        if (!SoundingLocked(handle)) return 0;

        int raw = ManagedBass.Mix.BassMix.ChannelGetLevel(handle);

        if (raw == -1) return 0;

        float peak = Math.Max((raw >> 16) & 0xFFFF, raw & 0xFFFF) / 32768f;

        return Math.Clamp(peak, 0f, 1f);
    }

    /// <summary>
    /// What a pad's stream is made with, which is what it asked for plus
    /// <see cref="BassFlags.Decode"/>.
    /// </summary>
    /// <remarks>
    /// A source has to be a decoding channel or the bus refuses it, and the refusal would be a pad
    /// that presses and makes no sound.
    /// </remarks>
    /// <param name="flags">What the pad wanted anyway.</param>
    private BassFlags PadFlagsLocked(BassFlags flags) => flags | BassFlags.Decode;

    /// <summary>Watches a pad for its end, whichever path its audio takes.</summary>
    /// <remarks>
    /// **A source behind a mixer needs the add-on's own sync, and it fires on the mixing thread.**
    /// A plain end sync on a decoding channel is never raised, since nothing is playing it, so
    /// without this a pad would reach its end and go on being reported as playing for ever.
    ///
    /// Which is why the callback does as little as it can. It runs where the audio is rendered,
    /// under a driver that is the ASIO thread with a deadline on it, and this class's lock is held
    /// by everything a hand does on the PADS page. Taking that lock there would be the audio
    /// thread waiting on the drawing thread, which is the one thing this codebase's own rule
    /// forbids: on the audio path the loser refuses rather than waits. So the work is handed to
    /// the pool and the mixing thread goes straight back to mixing.
    /// </remarks>
    /// <param name="handle">The pad's stream.</param>
    /// <param name="padIndex">Which pad it is.</param>
    private void WatchEndLocked(int handle, int padIndex) =>
        ManagedBass.Mix.BassMix.ChannelSetSync(handle, SyncFlags.End, 0, _mixEndSync, new IntPtr(padIndex));

    /// <summary>
    /// A pad reached its end while on the bus, on the thread that renders the audio.
    /// </summary>
    /// <remarks>
    /// Nothing here touches the lock or raises anything. See <see cref="WatchEndLocked"/>.
    /// </remarks>
    /// <param name="handle">The sync this came from.</param>
    /// <param name="channel">The channel that ended.</param>
    /// <param name="data">Unused.</param>
    /// <param name="user">Which pad it was.</param>
    private void OnMixChannelEnd(int handle, int channel, int data, IntPtr user)
    {
        int padIndex = user.ToInt32();

        ThreadPool.UnsafeQueueUserWorkItem(_ => OnChannelEnd(handle, channel, data, new IntPtr(padIndex)), null);
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

        _padBus.Remove(handle);

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
    /// The curve everything leaving this engine goes through, the same one the master uses.
    /// </summary>
    /// <remarks>
    /// A pad's audio never touches the tracker's mixer, so the guard on the master reached none
    /// of it: an effect on a pad's chain handing back a NaN wrote it straight back into the
    /// sound library's own buffer and out of the card. One rule and both ways out.
    /// </remarks>
    private static readonly Interfaces.IOutputCurve Leaving = new OutputCurve();

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

        Leaving.Bend(scratch, frames * 2);

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
