using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JingleBox2.Audio.Plugins;

/// <summary>One plugin parameter, as the host sees it.</summary>
public sealed record ClapParameter(uint Id, string Name, double Minimum, double Maximum, double Default, uint Flags)
{
    // From the CLAP parameter flags, declared rather than worked out from a shift each time.
    private const uint SteppedFlag = 1 << 0;
    private const uint HiddenFlag = 1 << 2;
    private const uint ReadOnlyFlag = 1 << 3;
    private const uint BypassFlag = 1 << 4;

    /// <summary>Whole numbers only: a mode or a count rather than a dial.</summary>
    public bool IsStepped => (Flags & SteppedFlag) != 0;

    /// <summary>Not meant to be shown at all.</summary>
    public bool IsHidden => (Flags & HiddenFlag) != 0;

    /// <summary>
    /// The plugin talking rather than listening: a gain reduction or an output level. Shown as
    /// a reading, never as something to drag.
    /// </summary>
    public bool IsReadOnly => (Flags & ReadOnlyFlag) != 0;

    /// <summary>The plugin's own bypass, which the host offers in its own way.</summary>
    public bool IsBypass => (Flags & BypassFlag) != 0;
}

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
public sealed unsafe class ClapEffect : IAudioInsert, IDisposable
{
    /// <summary>Stereo in, stereo out. Wider plugins are fed and read on their first two.</summary>
    public const int Channels = 2;

    private readonly ClapBundle _bundle;
    private readonly ClapPlugin* _plugin;
    private readonly ClapHost* _host;
    private readonly ClapPluginParams* _params;
    private readonly ClapPluginAudioPorts* _ports;

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

    private ClapEffect(ClapBundle bundle, ClapPlugin* plugin, ClapHost* host, ClapPluginInfo info)
    {
        _bundle = bundle;
        _plugin = plugin;
        _host = host;
        Info = info;

        using var parameters = new NativeText(ClapAbi.ParamsExtension);
        _params = (ClapPluginParams*)plugin->GetExtension(plugin, parameters.Pointer);

        using var ports = new NativeText(ClapAbi.AudioPortsExtension);
        _ports = (ClapPluginAudioPorts*)plugin->GetExtension(plugin, ports.Pointer);

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

    public ClapPluginInfo Info { get; }

    public bool IsActive => _active;

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

    private static ClapPluginInfo? FindPlugin(ClapBundle bundle, string pluginId)
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
        _outEvents->TryPush = &IgnoreEvent;

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
    /// Plugins report their own parameter changes back this way. Nothing here listens yet, so
    /// they are accepted and dropped rather than refused, which some plugins treat as an error.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static byte IgnoreEvent(ClapOutputEvents* list, ClapEventHeader* header) => 1;

    /// <summary>
    /// Hands over any parameter moves now, without waiting for a block. What CLAP's flush is
    /// for: settings restored from a song have to take effect on a plugin that is not being
    /// played yet, or the knobs would read the plugin's defaults until something did.
    /// </summary>
    public void FlushParameters()
    {
        if (_disposed || _params == null || _params->Flush == null) return;

        // If the audio thread is inside the plugin, it is about to take the pending values
        // itself, and a flush now would be a second call into the plugin at the same time.
        if (!System.Threading.Monitor.TryEnter(_flush)) return;

        try
        {
            TakePending();
            if (_eventCount == 0) return;

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
    public IReadOnlyList<ClapParameter> Parameters()
    {
        var parameters = new List<ClapParameter>();
        if (_disposed || _params == null || _params->Count == null) return parameters;

        uint count = _params->Count(_plugin);
        var info = new ClapParamInfo();

        for (uint index = 0; index < count; index++)
        {
            if (_params->GetInfo(_plugin, index, &info) == 0) continue;

            parameters.Add(new ClapParameter(
                info.Id,
                ReadFixed(info.Name, ClapAbi.NameSize),
                info.MinValue,
                info.MaxValue,
                info.DefaultValue,
                info.Flags));
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

        // The library goes last: freeing it while the plugin is still in it is a crash with
        // nothing to read in the stack.
        _bundle.Dispose();
    }

    private static void Free(void* memory)
    {
        if (memory != null) NativeMemory.Free(memory);
    }
}
