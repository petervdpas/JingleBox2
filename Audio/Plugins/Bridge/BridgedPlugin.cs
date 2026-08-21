using System;
using System.Collections.Generic;

namespace JingleBox2.Audio.Plugins.Bridge;

/// <summary>
/// A plugin that lives in another process, wearing the same face as one that does not.
/// </summary>
/// <remarks>
/// Everything above this class deals in <see cref="IPluginEffect"/> and
/// <see cref="IPluginInstrument"/> and has no idea where the plugin actually is. That is what
/// makes the isolation something that could be added: a chain, a track, a saved song and the
/// knob panel all carried on unchanged.
///
/// The one thing that is genuinely different is that a plugin can now go away while the
/// application is still running. When it does, an effect passes its audio through untouched and
/// an instrument goes quiet, both of which are what a missing box on a desk sounds like, and
/// <see cref="Stopped"/> is raised so somebody can say so and offer to start it again.
/// </remarks>
public sealed unsafe class BridgedPlugin : IPluginEffect, IPluginInstrument, IPluginWindowSource
{
    private readonly object _gate = new();
    private readonly int _sampleRate;
    private readonly int _maxFrames;
    private readonly bool _asInstrument;
    private readonly Dictionary<uint, double> _values = new();

    private PluginProcess? _process;
    private PluginParameter[] _parameters = Array.Empty<PluginParameter>();
    private BridgedEditor? _editor;
    private byte[]? _state;
    private bool _disposed;

    private BridgedPlugin(PluginInfo info, PluginProcess process, int sampleRate, int maxFrames, bool asInstrument)
    {
        Info = info;
        _sampleRate = sampleRate;
        _maxFrames = maxFrames;
        _asInstrument = asInstrument;

        Adopt(process);
    }

    public PluginInfo Info { get; }

    /// <summary>True while the plugin's process is up and taking audio.</summary>
    public bool IsActive => _process?.Alive == true;

    /// <summary>Which process this plugin actually is, for a log or a list of what is running.</summary>
    public int ProcessId => _process?.ProcessId ?? 0;

    /// <summary>True when the plugin has stopped and has not been started again.</summary>
    public bool HasStopped => _process?.Alive != true;

    /// <summary>Why it stopped, in words fit to put on a page.</summary>
    public string StoppedNote { get; private set; } = "";

    /// <summary>
    /// Raised when the plugin's process goes away on its own. Not raised when it is closed on
    /// purpose: that is not news.
    /// </summary>
    public event Action? Stopped;

    /// <summary>Opens a plugin in a process of its own.</summary>
    public static BridgedPlugin? Load(PluginInfo info, int sampleRate, int maxFrames, bool asInstrument)
    {
        var process = PluginProcess.Start(info, sampleRate, maxFrames, asInstrument);

        return process == null ? null : new BridgedPlugin(info, process, sampleRate, maxFrames, asInstrument);
    }

    private void Adopt(PluginProcess process)
    {
        _process = process;
        _parameters = process.Parameters;

        process.Died += OnDied;
        process.Edited += OnEdited;
    }

    /// <summary>
    /// The plugin moved one of its own knobs, over in its own window.
    /// </summary>
    /// <remarks>
    /// Written into the shadow copy as well as passed on, so that a plugin which has to be
    /// started again comes back with what its own window was last set to and not only with
    /// what was set from this side.
    /// </remarks>
    private void OnEdited(uint id, double value)
    {
        lock (_values) _values[id] = value;

        Edited?.Invoke(id, value);
    }

    /// <summary>Raised when the plugin moves one of its own knobs. Not on the drawing thread.</summary>
    public event Action<uint, double>? Edited;

    private void OnDied()
    {
        var process = _process;

        StoppedNote = Info.Name + " " + (process?.Epitaph.Length > 0 ? process.Epitaph : "stopped") + ".";

        var editor = _editor;
        if (editor != null) editor.Orphan();

        Stopped?.Invoke();
    }

    /// <summary>
    /// Starts the plugin again after it stopped, and puts back everything that was known about
    /// it: the patch it was last given, and every knob that has been moved since.
    /// </summary>
    /// <remarks>
    /// This cannot bring back what was only ever inside the plugin. A patch loaded from a song
    /// comes back exactly; a wavetable dragged in ten minutes ago and never saved does not.
    /// Which is why the message says settings rather than everything.
    /// </remarks>
    public bool Restart()
    {
        lock (_gate)
        {
            if (_disposed) return false;

            var old = _process;

            if (old != null)
            {
                old.Died -= OnDied;
                old.Edited -= OnEdited;
                old.Dispose();
            }

            _process = null;

            var fresh = PluginProcess.Start(Info, _sampleRate, _maxFrames, _asInstrument);
            if (fresh == null) return false;

            Adopt(fresh);

            StoppedNote = "";

            if (_state != null) Send(BridgeCall.LoadState, _state);

            foreach (var pair in _values) Send(BridgeCall.SetValue, BridgeBody.Number(pair.Key, pair.Value));

            Send(BridgeCall.Flush, null);

            return true;
        }
    }

    public IReadOnlyList<PluginParameter> Parameters() => _parameters;

    public double ValueOf(uint id)
    {
        var answer = Ask(BridgeCall.ValueOf, BridgeBody.Number(id, 0));

        if (answer.Call != BridgeCall.Value)
        {
            return _values.TryGetValue(id, out double known) ? known : 0;
        }

        double value = BridgeBody.ReadDouble(answer.Payload);

        lock (_values) _values[id] = value;

        return value;
    }

    public string TextFor(uint id, double value)
    {
        var answer = Ask(BridgeCall.TextFor, BridgeBody.Number(id, value));

        if (answer.Call != BridgeCall.Text) return "";

        var words = BridgeBody.ReadWords(answer.Payload);

        return words.Length > 0 ? words[0] : "";
    }

    public void SetValue(uint id, double value)
    {
        lock (_values) _values[id] = value;

        Send(BridgeCall.SetValue, BridgeBody.Number(id, value));
    }

    public void FlushParameters() => Send(BridgeCall.Flush, null);

    public byte[] SaveState()
    {
        var answer = Ask(BridgeCall.SaveState, null);

        if (answer.Call != BridgeCall.State) return _state ?? Array.Empty<byte>();

        _state = answer.Payload;

        return answer.Payload;
    }

    public void LoadState(byte[]? state)
    {
        _state = state;

        if (state == null || state.Length == 0) return;

        Send(BridgeCall.LoadState, state);
    }

    public void NoteOn(int semitone, float velocity)
    {
        var process = _process;
        if (process?.Alive != true) return;

        process.Block.Queue(BridgeEvent.NoteOn, (uint)Math.Clamp(semitone, 0, 127), velocity);
    }

    public void NoteOff(int semitone)
    {
        var process = _process;
        if (process?.Alive != true) return;

        process.Block.Queue(BridgeEvent.NoteOff, (uint)Math.Clamp(semitone, 0, 127), 0);
    }

    public void AllNotesOff()
    {
        var process = _process;
        if (process?.Alive != true) return;

        process.Block.Queue(BridgeEvent.AllNotesOff, 0, 0);
    }

    /// <summary>
    /// Runs a block of a track's audio through the plugin, over in the other process.
    /// </summary>
    /// <remarks>
    /// A dead plugin leaves the buffer exactly as it found it, which is the track carrying on
    /// without the effect rather than the track going silent.
    /// </remarks>
    public void Process(float[] buffer, int frames)
    {
        var process = _process;
        if (process == null || frames <= 0) return;

        // Held open for the whole block, not just the crossing: the copies in and out are into
        // memory the other process shares, and that memory only goes when nobody is in it.
        if (!process.Enter()) return;

        try
        {
            Run(process, buffer, frames);
        }
        finally
        {
            process.Leave();
        }
    }

    private void Run(PluginProcess process, float[] buffer, int frames)
    {
        int done = 0;

        while (done < frames)
        {
            int chunk = Math.Min(_maxFrames, frames - done);

            var block = process.Block;
            int samples = chunk * PluginBridge.Channels;

            fixed (float* source = buffer)
            {
                Buffer.MemoryCopy(source + done * PluginBridge.Channels, block.Input, (long)samples * sizeof(float), (long)samples * sizeof(float));
            }

            if (!process.Render(chunk)) return;

            fixed (float* destination = buffer)
            {
                Buffer.MemoryCopy(block.Output, destination + done * PluginBridge.Channels, (long)samples * sizeof(float), (long)samples * sizeof(float));
            }

            done += chunk;
        }
    }

    /// <summary>
    /// Fills a block with what the instrument is playing. A dead plugin fills it with silence,
    /// because there is nothing else in it that could be right.
    /// </summary>
    public void Render(float[] buffer, int frames)
    {
        var process = _process;

        if (process == null || frames <= 0 || !process.Enter())
        {
            Array.Clear(buffer, 0, Math.Min(buffer.Length, Math.Max(0, frames) * PluginBridge.Channels));
            return;
        }

        try
        {
            Play(process, buffer, frames);
        }
        finally
        {
            process.Leave();
        }
    }

    private void Play(PluginProcess process, float[] buffer, int frames)
    {
        int done = 0;

        while (done < frames)
        {
            int chunk = Math.Min(_maxFrames, frames - done);
            int samples = chunk * PluginBridge.Channels;

            var block = process.Block;

            new Span<float>(block.Input, samples).Clear();

            if (!process.Render(chunk))
            {
                Array.Clear(buffer, done * PluginBridge.Channels, Math.Min(buffer.Length - done * PluginBridge.Channels, (frames - done) * PluginBridge.Channels));
                return;
            }

            fixed (float* destination = buffer)
            {
                Buffer.MemoryCopy(block.Output, destination + done * PluginBridge.Channels, (long)samples * sizeof(float), (long)samples * sizeof(float));
            }

            done += chunk;
        }
    }

    /// <summary>Opens the plugin's own interface, over in the process where the plugin is.</summary>
    public IPluginEditor? OpenEditor()
    {
        lock (_gate)
        {
            var process = _process;
            if (process?.Alive != true || !process.HasOwnWindow) return null;

            var answer = process.Call(BridgeCall.OpenEditor, null, PluginBridge.WindowTimeoutMilliseconds);
            if (answer.Call != BridgeCall.Ok) return null;

            var size = BridgeBody.ReadThree(answer.Payload);

            _editor?.Orphan();
            _editor = new BridgedEditor(this, process, size.First, size.Second) { CanResize = size.Third != 0 };

            return _editor;
        }
    }

    internal void Forget(BridgedEditor editor)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_editor, editor)) _editor = null;
        }
    }

    private (BridgeCall Call, byte[] Payload) Ask(BridgeCall call, byte[]? payload)
    {
        var process = _process;

        return process?.Alive != true
            ? (BridgeCall.Fail, Array.Empty<byte>())
            : process.Call(call, payload);
    }

    private void Send(BridgeCall call, byte[]? payload) => Ask(call, payload);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;

            _editor?.Orphan();
            _editor = null;

            var process = _process;
            _process = null;

            if (process != null)
            {
                process.Died -= OnDied;
                process.Edited -= OnEdited;
                process.Dispose();
            }
        }
    }
}

/// <summary>
/// The plugin's own window, drawn by the plugin, in a window this process lends it.
/// </summary>
/// <remarks>
/// X11 lets a window belong to one program and its parent belong to another, which is the whole
/// reason a plugin's interface can be brought back from the other side of a process boundary
/// and still sit inside our own window, in the right place, with the right size. The plugin
/// draws straight into it; nothing is copied and nothing is a picture of anything.
/// </remarks>
public sealed class BridgedEditor : IPluginEditor
{
    private readonly BridgedPlugin _owner;
    private readonly PluginProcess _process;

    private bool _closed;

    internal BridgedEditor(BridgedPlugin owner, PluginProcess process, int width, int height)
    {
        _owner = owner;
        _process = process;

        Size = (width > 0 ? width : 640, height > 0 ? height : 480);

        _process.ResizeRequested += OnResizeRequested;
    }

    public (int Width, int Height) Size { get; private set; }

    /// <summary>True when the plugin will follow a window being dragged bigger.</summary>
    public bool CanResize { get; internal init; }

    public event Action<int, int>? ResizeRequested;

    private void OnResizeRequested(int width, int height)
    {
        if (_closed || width <= 0 || height <= 0) return;

        Size = (width, height);

        ResizeRequested?.Invoke(width, height);
    }

    public bool Attach(nint window)
    {
        if (_closed) return false;

        var answer = _process.Call(BridgeCall.Attach, BridgeBody.Handle(window), PluginBridge.WindowTimeoutMilliseconds);

        if (answer.Call != BridgeCall.Ok) return false;

        var size = BridgeBody.ReadPair(answer.Payload);

        if (size.First > 0 && size.Second > 0) Size = (size.First, size.Second);

        return true;
    }

    public void Detach()
    {
        if (_closed) return;

        _process.Call(BridgeCall.Detach, null, PluginBridge.WindowTimeoutMilliseconds);
    }

    public void Resized(int width, int height)
    {
        if (_closed || width <= 0 || height <= 0) return;

        _process.Call(BridgeCall.Resized, BridgeBody.Pair(width, height), PluginBridge.WindowTimeoutMilliseconds);
    }

    /// <summary>Called when the plugin died underneath the window. There is nothing left to tell.</summary>
    internal void Orphan()
    {
        _closed = true;
        _process.ResizeRequested -= OnResizeRequested;
    }

    public void Dispose()
    {
        if (_closed)
        {
            _owner.Forget(this);
            return;
        }

        _closed = true;
        _process.ResizeRequested -= OnResizeRequested;

        _process.Call(BridgeCall.CloseEditor, null, PluginBridge.WindowTimeoutMilliseconds);

        _owner.Forget(this);
    }
}
