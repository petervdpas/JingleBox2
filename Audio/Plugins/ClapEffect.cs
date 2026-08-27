using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// A loaded plugin with audio running through it: the host side of one insert slot.
/// </summary>
/// <remarks>
/// Two threads meet here. Creating, activating and destroying happen on the UI thread; Process
/// runs on the audio callback. Everything the audio thread touches is allocated once at
/// activation, because allocating inside a callback is how a mixer starts crackling.
///
/// Parameter moves are not written straight into the plugin. CLAP says they arrive as events
/// at the start of a block, so a knob leaves its value in a queue here and the audio thread
/// hands it over on its next pass.
/// </remarks>
public sealed unsafe class ClapEffect : IPluginEffect, IPluginWindowSource
{
    /// <summary>Stereo in, stereo out. Wider plugins are fed and read on their first two.</summary>
    public const int Channels = 2;

    // From the CLAP parameter flags, declared rather than worked out from a shift each time.
    private const uint SteppedFlag = 1 << 0;
    private const uint HiddenFlag = 1 << 2;
    private const uint ReadOnlyFlag = 1 << 3;
    private const uint BypassFlag = 1 << 4;

    private readonly ClapBundle _bundle;
    private readonly ClapPlugin* _plugin;
    private readonly ClapHost* _host;
    private readonly ClapPluginParams* _params;
    private readonly ClapPluginAudioPorts* _ports;
    private readonly ClapPluginState* _state;

    private readonly object _lock = new();
    private readonly Dictionary<uint, double> _pending = new();

    private float** _inputChannels;
    private float** _outputChannels;
    private float* _inputData;
    private float* _outputData;
    private ClapAudioBuffer* _inputBuffer;
    private ClapAudioBuffer* _outputBuffer;
    private ClapProcess* _process;
    private ClapInputEvents* _inEvents;
    private ClapOutputEvents* _outEvents;
    private ClapEventParamValue* _events;

    private int _maxFrames;
    private int _inputChannelCount;
    private int _eventCount;
    private long _steadyTime;
    private bool _active;
    private bool _disposed;

    private ClapEffect(ClapBundle bundle, ClapPlugin* plugin, ClapHost* host, PluginInfo info)
    {
        _bundle = bundle;
        _plugin = plugin;
        _host = host;
        Info = info;

        using var parameters = new NativeText(ClapAbi.ParamsExtension);
        _params = (ClapPluginParams*)plugin->GetExtension(plugin, parameters.Pointer);

        using var ports = new NativeText(ClapAbi.AudioPortsExtension);
        _ports = (ClapPluginAudioPorts*)plugin->GetExtension(plugin, ports.Pointer);

        using var state = new NativeText(ClapAbi.StateExtension);
        _state = (ClapPluginState*)plugin->GetExtension(plugin, state.Pointer);

        _inputPortChannels = PortsOf(input: true);
        _outputPortChannels = PortsOf(input: false);

        InputChannels = _inputPortChannels.Length > 0 ? _inputPortChannels[0] : 0;
        OutputChannels = _outputPortChannels.Length > 0 ? _outputPortChannels[0] : 0;
    }

    /// <summary>
    /// What the plugin's main audio port carries. A mono compressor handed two channels is not
    /// a mono compressor with a spare, it is a plugin that stops dead.
    /// </summary>
    public int InputChannels { get; }

    public int OutputChannels { get; }

    /// <summary>How many ports the plugin has each way. A side chain is a port of its own.</summary>
    public int InputPorts => _inputPortChannels.Length;

    public int OutputPorts => _outputPortChannels.Length;

    private readonly int[] _inputPortChannels;
    private readonly int[] _outputPortChannels;

    /// <summary>
    /// Every audio port the plugin declares, with the channels each one carries.
    /// </summary>
    /// <remarks>
    /// All of them, not just the first. A compressor with a side chain declares two input
    /// ports, and handing it one is a plugin that refuses the block: what it was given has to
    /// match what it asked for, whether or not the host has anything to put in the second one.
    /// </remarks>
    private int[] PortsOf(bool input)
    {
        if (_ports == null || _ports->Count == null || _ports->Get == null) return new[] { Channels };

        byte isInput = input ? (byte)1 : (byte)0;
        uint count = Math.Min(_ports->Count(_plugin, isInput), MaxPorts);

        var channels = new int[count];
        var info = new ClapAudioPortInfo();

        for (uint port = 0; port < count; port++)
        {
            channels[port] = _ports->Get(_plugin, port, isInput, &info) == 0
                ? Channels
                : (int)Math.Clamp(info.ChannelCount, 0, MaxChannelsPerPort);
        }

        return channels;
    }

    /// <summary>More ports than any plugin this host is meant for, as a backstop.</summary>
    private const uint MaxPorts = 8;

    private const int MaxChannelsPerPort = 8;

    public PluginInfo Info { get; }

    public bool IsActive => _active;

    /// <summary>The plugin itself, for the parts of the ABI that live in another file.</summary>
    internal ClapPlugin* Handle => _plugin;

    /// <summary>
    /// Opens the plugin's own interface, when it has one.
    /// </summary>
    /// <remarks>
    /// Every plugin that draws itself should be allowed to. Nobody sets a compressor by reading
    /// its parameter names off an alphabetical list when the plugin has a picture of a meter it
    /// would rather show. See <see cref="ClapEditor"/>.
    /// </remarks>
    public IPluginEditor? OpenEditor() => _disposed ? null : ClapEditor.Open(this);

    /// <summary>One of the plugin's timers has come round. Called on the thread that draws.</summary>
    internal void RingTimer(uint id)
    {
        if (_disposed || _plugin == null || _plugin->GetExtension == null) return;

        using var name = new NativeText(ClapAbi.TimerExtension);

        var timers = (ClapPluginTimerSupport*)_plugin->GetExtension(_plugin, name.Pointer);

        if (timers == null || timers->OnTimer == null) return;

        timers->OnTimer(_plugin, id);
    }

    /// <summary>One of the plugin's files has something on it. Its X11 connection, in practice.</summary>
    internal void RingFile(int file, uint flags)
    {
        if (_disposed || _plugin == null || _plugin->GetExtension == null) return;

        using var name = new NativeText(ClapAbi.PosixFdExtension);

        var files = (ClapPluginPosixFd*)_plugin->GetExtension(_plugin, name.Pointer);

        if (files == null || files->OnFd == null) return;

        files->OnFd(_plugin, file, flags);
    }

    /// <summary>
    /// Loads a plugin and gets it ready to run. Returns null when the bundle cannot be opened,
    /// does not hold that plugin, or the plugin refuses to start.
    /// </summary>
    public static ClapEffect? Load(string bundlePath, string pluginId, int sampleRate, int maxFrames)
    {
        string key = Key(bundlePath, pluginId, sampleRate, maxFrames);

        var parked = TakeParked(key);
        if (parked != null) return parked;

        var bundle = ClapBundle.Acquire(bundlePath);
        if (bundle == null) return null;

        var info = FindPlugin(bundle, pluginId);
        if (info == null)
        {
            bundle.Dispose();
            return null;
        }

        var host = ClapHostDescription.Create();
        var plugin = bundle.Create(info.Id, host);

        if (plugin == null || plugin->Init == null || plugin->Init(plugin) == 0)
        {
            if (plugin != null && plugin->Destroy != null) plugin->Destroy(plugin);

            NativeMemory.Free(host);
            bundle.Dispose();
            return null;
        }

        var effect = new ClapEffect(bundle, plugin, host, info) { _key = key };

        ClapHostExtensions.Bind(host, effect);

        if (effect.Activate(sampleRate, maxFrames)) return effect;

        effect.Retire();
        return null;
    }

    /// <summary>
    /// Instances that have been finished with, kept rather than destroyed, ready to be picked
    /// up again by the next slot that wants the same plugin.
    /// </summary>
    private static readonly Dictionary<string, Stack<ClapEffect>> Parked = new(StringComparer.Ordinal);

    private static readonly object ParkLock = new();

    private string _key = "";

    private static string Key(string path, string id, int sampleRate, int maxFrames) =>
        path + "|" + id + "|" + sampleRate + "|" + maxFrames;

    private static ClapEffect? TakeParked(string key)
    {
        ClapEffect? effect = null;

        lock (ParkLock)
        {
            if (Parked.TryGetValue(key, out var waiting) && waiting.Count > 0) effect = waiting.Pop();
        }

        if (effect == null) return null;

        // A parked plugin still holds the tail of whatever it was last doing.
        if (effect._plugin->Reset != null) effect._plugin->Reset(effect._plugin);
        if (effect._plugin->StartProcessing != null && effect._plugin->StartProcessing(effect._plugin) == 0) return null;

        effect._disposed = false;
        effect._steadyTime = 0;

        lock (effect._lock) effect._pending.Clear();

        return effect;
    }

    private static PluginInfo? FindPlugin(ClapBundle bundle, string pluginId)
    {
        var plugins = bundle.Plugins();
        if (plugins.Count == 0) return null;

        // No id given means the first one, which is what a bundle holding a single plugin is.
        if (string.IsNullOrWhiteSpace(pluginId)) return plugins[0];

        foreach (var plugin in plugins)
        {
            if (string.Equals(plugin.Id, pluginId, StringComparison.Ordinal)) return plugin;
        }

        return null;
    }

    private bool Activate(int sampleRate, int maxFrames)
    {
        if (_plugin->Activate == null) return false;

        _maxFrames = Math.Max(1, maxFrames);

        Allocate(_maxFrames);

        if (_plugin->Activate(_plugin, sampleRate <= 0 ? 44100 : sampleRate, 1, (uint)_maxFrames) == 0) return false;

        if (_plugin->StartProcessing != null && _plugin->StartProcessing(_plugin) == 0)
        {
            _plugin->Deactivate(_plugin);
            return false;
        }

        _active = true;
        return true;
    }

    /// <summary>Everything the audio thread needs, taken once so it never allocates.</summary>
    private void Allocate(int frames)
    {
        _inputChannelCount = 0;
        foreach (int port in _inputPortChannels) _inputChannelCount += Math.Max(0, port);

        _inputBuffer = AllocPorts(_inputPortChannels, frames, out _inputData, out _inputChannels);
        _outputBuffer = AllocPorts(_outputPortChannels, frames, out _outputData, out _outputChannels);

        _events = Alloc<ClapEventParamValue>(MaxEventsPerBlock);

        _inEvents = Alloc<ClapInputEvents>(1);
        _inEvents->Size = &EventCount;
        _inEvents->Get = &EventAt;

        _outEvents = Alloc<ClapOutputEvents>(1);
        _outEvents->TryPush = &TakeEvent;

        _process = Alloc<ClapProcess>(1);
        _process->AudioInputs = _inputBuffer;
        _process->AudioOutputs = _outputBuffer;
        _process->AudioInputsCount = (uint)_inputPortChannels.Length;
        _process->AudioOutputsCount = (uint)_outputPortChannels.Length;
        _process->InEvents = _inEvents;
        _process->OutEvents = _outEvents;
    }

    /// <summary>
    /// Lays out one side of the plugin's audio: a buffer per port, a pointer per channel, and
    /// one block of memory holding all of it.
    /// </summary>
    private ClapAudioBuffer* AllocPorts(int[] ports, int frames, out float* data, out float** pointers)
    {
        int channels = 0;
        foreach (int port in ports) channels += Math.Max(0, port);

        // A port declaring no channels still needs somewhere to point.
        channels = Math.Max(1, channels);

        data = Alloc<float>(frames * channels);
        pointers = (float**)Alloc<nint>(channels);

        for (int channel = 0; channel < channels; channel++)
            pointers[channel] = data + channel * frames;

        var buffers = Alloc<ClapAudioBuffer>(Math.Max(1, ports.Length));
        int taken = 0;

        for (int port = 0; port < ports.Length; port++)
        {
            buffers[port] = new ClapAudioBuffer
            {
                Data32 = pointers + taken,
                ChannelCount = (uint)Math.Max(0, ports[port])
            };

            taken += Math.Max(0, ports[port]);
        }

        return buffers;
    }

    /// <summary>More parameter moves in one block than any hand can produce.</summary>
    private const int MaxEventsPerBlock = 64;

    private static T* Alloc<T>(int count) where T : unmanaged
    {
        var memory = (T*)NativeMemory.AllocZeroed((nuint)count, (nuint)sizeof(T));
        return memory;
    }

    /// <summary>
    /// Runs one block through the plugin, in place. The buffer is interleaved stereo, which is
    /// what the mixer works in; CLAP wants a pointer per channel, so it is split going in and
    /// woven back together coming out.
    /// </summary>
    public void Process(float[] buffer, int frames)
    {
        if (_disposed || !_active || buffer == null) return;
        if (frames <= 0 || _plugin->Process == null) return;
        if (frames * 2 > buffer.Length) frames = buffer.Length / 2;

        // A plugin is activated for a maximum block and may not be handed more than that.
        // The audio engine's blocks are whatever the device felt like, so a long one is fed
        // through in pieces rather than refused.
        int offset = 0;

        while (offset < frames)
        {
            int chunk = Math.Min(_maxFrames, frames - offset);
            ProcessBlock(buffer, offset, chunk);
            offset += chunk;
        }
    }

    private void ProcessBlock(float[] buffer, int offset, int frames)
    {
        _lastProcess = Environment.TickCount64;


        // The track goes into the main port. Everything else the plugin declared, a side
        // chain included, is given silence rather than whatever was left in it last block.
        int fed = 0;

        if (InputChannels == 1)
        {
            // Mono in takes the two sides summed rather than the left one alone, so a signal
            // panned right does not vanish into it.
            for (int frame = 0; frame < frames; frame++)
                _inputChannels[0][frame] = (buffer[(offset + frame) * 2] + buffer[(offset + frame) * 2 + 1]) * 0.5f;

            fed = 1;
        }
        else if (InputChannels >= 2)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                _inputChannels[0][frame] = buffer[(offset + frame) * 2];
                _inputChannels[1][frame] = buffer[(offset + frame) * 2 + 1];
            }

            fed = 2;
        }

        for (int channel = fed; channel < _inputChannelCount; channel++)
            NativeMemory.Clear(_inputChannels[channel], (nuint)(frames * sizeof(float)));

        TakePending();

        _process->FramesCount = (uint)frames;
        _process->SteadyTime = _steadyTime;

        // The event list is read through static callbacks, which have no instance to work
        // from: the block being processed is handed over here for the length of the call.
        // Held so a parameter handed over from the UI cannot land in the middle of a block.
        // The other side of this uses TryEnter and gives up, so the audio thread waits for
        // nothing longer than one short flush.
        lock (_flush)
        {
            _current = this;

            try
            {
                _plugin->Process(_plugin, _process);
            }
            finally
            {
                _current = null;
                _eventCount = 0;
            }
        }

        if (OutputChannels == 1)
        {
            // Mono out goes to both sides, or half the mixer would go quiet.
            for (int frame = 0; frame < frames; frame++)
            {
                float value = _outputChannels[0][frame];
                buffer[(offset + frame) * 2] = value;
                buffer[(offset + frame) * 2 + 1] = value;
            }
        }
        else if (OutputChannels >= 2)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                buffer[(offset + frame) * 2] = _outputChannels[0][frame];
                buffer[(offset + frame) * 2 + 1] = _outputChannels[1][frame];
            }
        }

        _steadyTime += frames;
    }

    /// <summary>Turns the knob moves waiting since the last block into events for this one.</summary>
    private void TakePending()
    {
        lock (_lock)
        {
            if (_pending.Count == 0) return;

            foreach (var (id, value) in _pending)
            {
                if (_eventCount >= MaxEventsPerBlock) break;

                _events[_eventCount++] = new ClapEventParamValue
                {
                    Header = new ClapEventHeader
                    {
                        Size = (uint)sizeof(ClapEventParamValue),
                        Time = 0,
                        SpaceId = ClapAbi.CoreEventSpace,
                        Type = ClapAbi.ParamValueEvent,
                        Flags = 0
                    },
                    ParamId = id,
                    PortIndex = -1,
                    Channel = -1,
                    Key = -1,
                    NoteId = -1,
                    Value = value
                };
            }

            _pending.Clear();
        }
    }

    [ThreadStatic]
    private static ClapEffect? _current;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static uint EventCount(ClapInputEvents* list) => (uint)(_current?._eventCount ?? 0);

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static ClapEventHeader* EventAt(ClapInputEvents* list, uint index)
    {
        var effect = _current;
        if (effect == null || index >= effect._eventCount) return null;

        return (ClapEventHeader*)(effect._events + index);
    }

    /// <summary>
    /// Plugins report their own parameter changes back this way: a knob turned in the plugin's
    /// own window arrives here, on the audio thread, at the end of the block it happened in.
    /// Anything else is accepted and dropped rather than refused, which some plugins treat as
    /// an error.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static byte TakeEvent(ClapOutputEvents* list, ClapEventHeader* header)
    {
        if (header == null) return 1;

        var effect = _current;
        if (effect == null) return 1;

        if (header->SpaceId != ClapAbi.CoreEventSpace || header->Type != ClapAbi.ParamValueEvent) return 1;

        var moved = (ClapEventParamValue*)header;

        try
        {
            effect.Moved(moved->ParamId, moved->Value);
        }
        catch (Exception)
        {
            // A knob move is not worth throwing back into somebody else's audio thread.
        }

        return 1;
    }

    /// <summary>
    /// Everything inside the plugin, as a lump to keep. This is where a patch really lives:
    /// the parameters are knob positions, and a wavetable is not a knob position.
    /// </summary>
    /// <remarks>
    /// Empty for a plugin that does not offer the extension, and empty for one that offers it
    /// and then fails, which are the same thing to whoever asked: no patch, and the parameters
    /// alone to describe it. That is what every CLAP effect here was until now.
    ///
    /// The stream is a struct on the stack with a static function in it, and where the bytes
    /// actually go is a field on this thread. Both calls are main-thread by the standard and
    /// return before the field is cleared, so there is nothing to share.
    /// </remarks>
    public byte[] SaveState()
    {
        if (_disposed || _state == null || _state->Save == null) return Array.Empty<byte>();

        var writing = new System.IO.MemoryStream();
        var stream = new ClapOutputStream { Context = null, Write = &Written };

        _writing = writing;

        try
        {
            return _state->Save(_plugin, &stream) == 0 ? Array.Empty<byte>() : writing.ToArray();
        }
        catch (Exception)
        {
            return Array.Empty<byte>();
        }
        finally
        {
            _writing = null;
        }
    }

    /// <summary>Puts a saved lump back. A plugin that refuses it keeps what it had.</summary>
    public void LoadState(byte[]? state)
    {
        if (_disposed || state is not { Length: > 0 }) return;
        if (_state == null || _state->Load == null) return;

        var reading = new System.IO.MemoryStream(state, writable: false);
        var stream = new ClapInputStream { Context = null, Read = &Fetched };

        _reading = reading;

        try
        {
            _state->Load(_plugin, &stream);
        }
        catch (Exception)
        {
            // A patch that will not go back in is a plugin on its own defaults, which is what
            // it would have been without one.
        }
        finally
        {
            _reading = null;
        }
    }

    [ThreadStatic]
    private static System.IO.MemoryStream? _writing;

    [ThreadStatic]
    private static System.IO.MemoryStream? _reading;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static long Written(ClapOutputStream* stream, void* buffer, ulong size)
    {
        var writing = _writing;
        if (writing == null || buffer == null) return -1;

        int wanted = (int)Math.Min(size, int.MaxValue);
        if (wanted == 0) return 0;

        try
        {
            writing.Write(new ReadOnlySpan<byte>(buffer, wanted));
            return wanted;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static long Fetched(ClapInputStream* stream, void* buffer, ulong size)
    {
        var reading = _reading;
        if (reading == null || buffer == null) return -1;

        int wanted = (int)Math.Min(size, int.MaxValue);
        if (wanted == 0) return 0;

        try
        {
            // Nought is the end of it rather than a failure, which is how the plugin knows to
            // stop asking.
            return reading.Read(new Span<byte>(buffer, wanted));
        }
        catch (Exception)
        {
            return -1;
        }
    }

    public event Action<uint, double>? Edited;

    public event Action? Reloaded;

    /// <summary>The plugin says everything about it may have changed, which is a preset.</summary>
    internal void Reload()
    {
        if (_disposed) return;

        // Whatever was known about its knobs is out of date, so they are read again from
        // scratch rather than compared against what they used to be.
        _watched = null;
        _seen = null;

        Reloaded?.Invoke();
    }

    /// <summary>
    /// A knob turned in the plugin's own window. CLAP is one object rather than two, so the
    /// plugin already has the value and is only saying so; the host's part is to know that
    /// there is something worth saving.
    /// </summary>
    internal void Moved(uint id, double value) => Edited?.Invoke(id, value);

    /// <summary>
    /// Hands over any parameter moves now, without waiting for a block, and collects anything
    /// the plugin has to say back.
    /// </summary>
    /// <remarks>
    /// What CLAP's flush is for, and it goes both ways. Settings restored from a song have to
    /// take effect on a plugin that is not being played yet, or the knobs would read the
    /// plugin's defaults until something did. And a knob turned in the plugin's own window is
    /// only ever handed back during a block or a flush, so an idle plugin whose flush nobody
    /// calls can be turned all day without the host hearing a word of it. That is what
    /// <see cref="Poll"/> is for.
    /// </remarks>
    public void FlushParameters()
    {
        if (_disposed || _params == null || _params->Flush == null) return;

        // If the audio thread is inside the plugin, it is about to take the pending values
        // itself, and a flush now would be a second call into the plugin at the same time.
        if (!System.Threading.Monitor.TryEnter(_flush)) return;

        try
        {
            TakePending();

            _current = this;

            try
            {
                _params->Flush(_plugin, _inEvents, _outEvents);
            }
            finally
            {
                _current = null;
                _eventCount = 0;
            }
        }
        finally
        {
            System.Threading.Monitor.Exit(_flush);
        }
    }

    /// <summary>
    /// Asks the plugin what its own knobs are set to, and says so if any of them have moved.
    /// </summary>
    /// <remarks>
    /// The only way to know, for a plugin nobody is playing. A CLAP plugin hands a knob back
    /// through the events at the end of a block, and a plugin on a stopped track never gets a
    /// block. Flushing does not help: measured against ZamComp, the last thing it says arrives
    /// ten milliseconds before the audio stops and nothing after, though the flush is called
    /// thirty times a second all the while.
    ///
    /// So the values are read instead. Only the ones somebody can set: a compressor's gain
    /// reduction moves on its own and is not news. Called on the thread the plugin's window
    /// lives on, and only while it has one, since that is the only time a knob can be turned.
    /// </remarks>
    public void Poll()
    {
        if (_disposed) return;

        Ask();
    }

    /// <summary>
    /// Gives the plugin the chance to take in what its own window has done, from the thread
    /// CLAP says that has to happen on.
    /// </summary>
    /// <remarks>
    /// This is the piece that was missing, and the specification is explicit about it: while a
    /// plugin is switched on, its flush belongs to the audio thread. A plugin on a stopped
    /// track is still switched on, so calling its flush from the thread its window is on is
    /// not a small liberty, it is the wrong thread, and a knob turned in the plugin's own
    /// window never reaches the part of the plugin that holds the value.
    ///
    /// So a plugin nobody is playing gets an empty block's worth of attention on the right
    /// thread instead, often enough that a knob never feels late.
    /// </remarks>
    public void Idle()
    {
        if (_disposed) return;

        // Being played: the blocks are already carrying everything both ways.
        if (!IsIdle)
        {
            _wantsFlush = false;
            return;
        }

        _wantsFlush = false;

        FlushParameters();
    }

    private volatile bool _wantsFlush;

    /// <summary>The plugin asking to be given the chance to hand something over.</summary>
    public void WantsFlush() => _wantsFlush = true;

    /// <summary>True when the plugin has asked and has not been given it yet.</summary>
    public bool IsWaitingToSpeak => _wantsFlush;

    /// <summary>How many parameters are worth reading every time round. Past this it is not a panel.</summary>
    private const int MaxWatched = 512;

    private uint[]? _watched;
    private double[]? _seen;

    private void Ask()
    {
        if (_params == null || _params->GetValue == null) return;

        if (_watched == null)
        {
            var ids = new List<uint>();

            foreach (var parameter in Parameters())
            {
                if (parameter.IsReadOnly || parameter.IsBypass) continue;

                ids.Add(parameter.Id);

                if (ids.Count >= MaxWatched) break;
            }

            _watched = ids.ToArray();
            _seen = new double[_watched.Length];

            for (int index = 0; index < _watched.Length; index++) _seen[index] = ValueOf(_watched[index]);

            Diagnostics.Log.Write(Diagnostics.LogArea.Plugins, () =>
                "watching " + _watched!.Length + " knobs on " + Info.Name + ", which read " + Reading());

            return;
        }

        for (int index = 0; index < _watched.Length; index++)
        {
            double now = ValueOf(_watched[index]);

            if (Math.Abs(now - _seen![index]) < 0.000001) continue;

            _seen[index] = now;

            Moved(_watched[index], now);
        }
    }

    /// <summary>
    /// Every knob and what the plugin says it is set to, for a log line that settles whether
    /// the plugin is telling the host the truth about itself.
    /// </summary>
    public string Reading()
    {
        if (_watched == null) return "nothing being watched yet";

        var report = new System.Text.StringBuilder();

        foreach (uint id in _watched)
        {
            report.Append(id).Append('=').Append(ValueOf(id).ToString("0.###")).Append(' ');
        }

        return report.ToString();
    }

    /// <summary>Remembers a value the host set, so it does not come back as something the plugin did.</summary>
    private void Remember(uint id, double value)
    {
        if (_watched == null || _seen == null) return;

        for (int index = 0; index < _watched.Length; index++)
        {
            if (_watched[index] != id) continue;

            _seen[index] = value;
            return;
        }
    }

    /// <summary>How long since a block went through, for telling a running plugin from an idle one.</summary>
    private long _lastProcess;

    /// <summary>Longer than any block, short enough that a knob never feels late.</summary>
    private const long IdleMilliseconds = 200;

    private bool IsIdle => Environment.TickCount64 - _lastProcess > IdleMilliseconds;

    /// <summary>
    /// Held while handing parameters over outside a block, so two of those cannot overlap.
    /// The audio thread does not take it: it never waits on the UI thread.
    /// </summary>
    private readonly object _flush = new();

    /// <summary>Everything this plugin exposes, in the order it lists them.</summary>
    public IReadOnlyList<PluginParameter> Parameters()
    {
        var parameters = new List<PluginParameter>();
        if (_disposed || _params == null || _params->Count == null) return parameters;

        uint count = _params->Count(_plugin);
        var info = new ClapParamInfo();

        for (uint index = 0; index < count; index++)
        {
            if (_params->GetInfo(_plugin, index, &info) == 0) continue;

            parameters.Add(new PluginParameter(
                info.Id,
                ReadFixed(info.Name, ClapAbi.NameSize),
                info.MinValue,
                info.MaxValue,
                info.DefaultValue,
                (info.Flags & SteppedFlag) != 0 ? (int)Math.Round(info.MaxValue - info.MinValue) : 0,
                (info.Flags & HiddenFlag) != 0,
                (info.Flags & ReadOnlyFlag) != 0,
                (info.Flags & BypassFlag) != 0,
                Normalized: false));
        }

        return parameters;
    }

    /// <summary>What a parameter is set to right now, straight from the plugin.</summary>
    public double ValueOf(uint id)
    {
        if (_disposed || _params == null || _params->GetValue == null) return 0;

        double value = 0;
        return _params->GetValue(_plugin, id, &value) == 0 ? 0 : value;
    }

    /// <summary>How the plugin words a value: "-6.0 dB" rather than -6.</summary>
    public string TextFor(uint id, double value)
    {
        if (_disposed || _params == null || _params->ValueToText == null) return "";

        const int size = 128;
        byte* text = stackalloc byte[size];

        return _params->ValueToText(_plugin, id, value, text, size) == 0 ? "" : NativeText.Read(text);
    }

    /// <summary>
    /// Moves a parameter. The value is queued rather than written: the plugin expects to be
    /// told at the start of a block, on the audio thread, not whenever a knob is dragged.
    /// </summary>
    public void SetValue(uint id, double value)
    {
        if (_disposed) return;

        lock (_lock) _pending[id] = value;

        // Written down as already known, so that reading the values back does not report the
        // host's own change as something the plugin did.
        Remember(id, value);

        // A plugin that is not playing anything will never take the queue itself: a pad sitting
        // idle, or a track between takes. Without this the value waits, the plugin still
        // reports the old one, and the knob springs back to it.
        if (IsIdle) FlushParameters();
    }

    private static string ReadFixed(byte* text, int size)
    {
        int length = 0;
        while (length < size && text[length] != 0) length++;

        return length == 0 ? "" : System.Text.Encoding.UTF8.GetString(text, length);
    }

    /// <summary>
    /// Takes the plugin out of the mix. The instance is kept rather than destroyed, and the
    /// next slot that asks for the same plugin picks it up again.
    /// </summary>
    /// <remarks>
    /// Not destroyed because destroying is where plugins go wrong. Deactivating one of the
    /// plugins on this machine faults inside the plugin's own code, with three of its siblings
    /// surviving the same sequence, and there is nothing a host inside one process can do
    /// about that: the fault lands in our process and takes the app with it. Parking costs one
    /// idle instance per plugin that has ever been used, and the alternative costs the song
    /// somebody was working on.
    ///
    /// The proper fix is hosting plugins in a process of their own, where a crash costs the
    /// plugin and nothing else. That is a much larger piece of work.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_plugin != null && _plugin->StopProcessing != null) _plugin->StopProcessing(_plugin);

        lock (_lock) _pending.Clear();

        lock (ParkLock)
        {
            if (!Parked.TryGetValue(_key, out var waiting))
            {
                waiting = new Stack<ClapEffect>();
                Parked[_key] = waiting;
            }

            waiting.Push(this);
        }
    }

    /// <summary>
    /// The full teardown, for an instance that never started. Only safe here: a plugin that
    /// failed to activate has nothing to deactivate, so the path that faults is not taken.
    /// </summary>
    private void Retire()
    {
        if (_disposed) return;
        _disposed = true;

        if (_active)
        {
            if (_plugin->StopProcessing != null) _plugin->StopProcessing(_plugin);
            if (_plugin->Deactivate != null) _plugin->Deactivate(_plugin);
            _active = false;
        }

        ClapHostExtensions.Unbind(_host);

        if (_plugin != null && _plugin->Destroy != null) _plugin->Destroy(_plugin);

        Free(_inputData);
        Free(_outputData);
        Free(_inputChannels);
        Free(_outputChannels);
        Free(_inputBuffer);
        Free(_outputBuffer);
        Free(_events);
        Free(_inEvents);
        Free(_outEvents);
        Free(_process);

        if (_host != null) NativeMemory.Free(_host);

        // The rack goes last: freeing it while the plugin is still in it is a crash with
        // nothing to read in the stack.
        _bundle.Dispose();
    }

    private static void Free(void* memory)
    {
        if (memory != null) NativeMemory.Free(memory);
    }
}
