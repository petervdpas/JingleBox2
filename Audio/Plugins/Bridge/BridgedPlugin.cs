using System;
using System.Collections.Generic;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Audio.Plugins.Interfaces;

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
    /// <summary>
    /// Held while the process underneath is being changed or let go: starting again, opening a
    /// window, disposing. Not held for audio, which is why <see cref="PluginProcess.Enter"/>
    /// exists.
    /// </summary>
    private readonly object _gate = new();

    /// <summary>Kept so the plugin can be started again on the same terms it was loaded on.</summary>
    private readonly int _sampleRate;

    /// <summary>The same, and also the size of the shared block, so a restart maps the same shape.</summary>
    private readonly int _maxFrames;

    /// <summary>Whether it was loaded to play notes or to work on audio. A restart must agree.</summary>
    private readonly bool _asInstrument;

    /// <summary>
    /// A shadow of every parameter that has been moved, from either side.
    /// </summary>
    /// <remarks>
    /// Two jobs. It is what a knob reads when the plugin cannot be asked, so a dead plugin's
    /// panel still shows what it was set to rather than a page of noughts. And it is what is
    /// poured back in on a restart, which is why moves the plugin made in its own window are
    /// written here as well as passed on.
    /// </remarks>
    private readonly Dictionary<uint, double> _values = new();

    /// <summary>The process the plugin is in, or null while there is none.</summary>
    private PluginProcess? _process;

    /// <summary>
    /// What the plugin exposes, read once when it loaded and kept. Asking a plugin in another
    /// process for its list is a round trip, and Serum answers with 2622 of them.
    /// </summary>
    private PluginParameter[] _parameters = Array.Empty<PluginParameter>();

    /// <summary>The window that is open on the plugin, if one is.</summary>
    private BridgedEditor? _editor;

    /// <summary>
    /// The last patch known about: what was loaded in, or what was last read out. Put back
    /// first on a restart, since a patch moves every parameter at once.
    /// </summary>
    private byte[]? _state;

    /// <summary>Set once this has been let go, so a restart cannot raise the dead.</summary>
    private bool _disposed;

    /// <summary>Takes a process that has already loaded the plugin and said hello.</summary>
    private BridgedPlugin(PluginInfo info, PluginProcess process, int sampleRate, int maxFrames, bool asInstrument)
    {
        Info = info;
        _sampleRate = sampleRate;
        _maxFrames = maxFrames;
        _asInstrument = asInstrument;

        Adopt(process);
    }

    /// <inheritdoc/>
    public PluginInfo Info { get; }

    /// <inheritdoc/>
    /// <remarks>True while the plugin's process is up and taking audio.</remarks>
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
    /// <remarks>
    /// Null when the process would not start or the plugin would not load in it, which are the
    /// same answer here: there is no plugin. Nothing is thrown, because a plugin that will not
    /// load is an ordinary thing to find on somebody's machine.
    /// </remarks>
    /// <param name="info">Which plugin, and in which format.</param>
    /// <param name="sampleRate">What the audio is running at over here.</param>
    /// <param name="maxFrames">The most frames one crossing carries, and the size of the shared block.</param>
    /// <param name="asInstrument">
    /// True to play notes, false to work on audio that is handed to it. A plugin that can do
    /// both is told which of them it is being used as.
    /// </param>
    public static BridgedPlugin? Load(PluginInfo info, int sampleRate, int maxFrames, bool asInstrument)
    {
        var process = PluginProcess.Start(info, sampleRate, maxFrames, asInstrument);

        return process == null ? null : new BridgedPlugin(info, process, sampleRate, maxFrames, asInstrument);
    }

    /// <summary>
    /// Takes up a freshly started process: its parameters, and the three things it says without
    /// being asked. Used by the constructor and by <see cref="Restart"/>, so a plugin started
    /// again is wired exactly as one started for the first time.
    /// </summary>
    private void Adopt(PluginProcess process)
    {
        _process = process;
        _parameters = process.Parameters;

        process.Died += OnDied;
        process.Edited += OnEdited;
        process.Reloaded += OnReloaded;
    }

    /// <summary>Everything about the plugin may have changed, which is a preset having arrived.</summary>
    /// <remarks>
    /// Nothing that was known about its knobs is worth keeping: a preset moves all of them at
    /// once, so the shadow is emptied and the values are asked for again on the next look
    /// rather than compared against what they used to be.
    /// </remarks>
    private void OnReloaded()
    {
        lock (_values) _values.Clear();

        Reloaded?.Invoke();
    }

    /// <summary>Raised when the plugin loads a whole new sound. Not on the drawing thread.</summary>
    public event Action? Reloaded;

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

    /// <summary>
    /// The process has gone. Writes down why in words fit for a page, tells the window there is
    /// nothing behind it any more, and says so.
    /// </summary>
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
                old.Reloaded -= OnReloaded;
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

    /// <inheritdoc/>
    /// <remarks>Answered from the list read when the plugin loaded, not asked for again.</remarks>
    public IReadOnlyList<PluginParameter> Parameters() => _parameters;

    /// <inheritdoc/>
    /// <remarks>
    /// A round trip to another process, so it is asked with the short patience: this is called
    /// from the thread that draws. A plugin that will not answer falls back to the shadow copy,
    /// which is the last value anybody set or the plugin reported, and that is a better answer
    /// for a panel than nought.
    /// </remarks>
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

    /// <inheritdoc/>
    /// <remarks>
    /// Only the plugin knows how it words a value, so there is nothing to fall back to: a
    /// plugin that cannot be asked gives an empty string and the panel prints the number.
    /// </remarks>
    public string TextFor(uint id, double value)
    {
        var answer = Ask(BridgeCall.TextFor, BridgeBody.Number(id, value));

        if (answer.Call != BridgeCall.Text) return "";

        var words = BridgeBody.ReadWords(answer.Payload);

        return words.Length > 0 ? words[0] : "";
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Written into the shadow copy before it is sent, so it survives the plugin dying on the
    /// way and comes back with it.
    /// </remarks>
    public void SetValue(uint id, double value)
    {
        lock (_values) _values[id] = value;

        Send(BridgeCall.SetValue, BridgeBody.Number(id, value));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Asks the other side to hand everything queued to the plugin now rather than waiting for
    /// the next block, which matters for a plugin on a track nobody is playing.
    /// </remarks>
    public void FlushParameters() => Send(BridgeCall.Flush, null);

    /// <inheritdoc/>
    /// <remarks>
    /// The slow one: a patch is hundreds of kilobytes for some plugins and is read whole across
    /// the wire, so it gets the long patience. What comes back is kept, both as the answer and
    /// as what a restart pours back in. A plugin that will not answer gives up the last patch
    /// known about rather than nothing, since nothing would be written into a song as a plugin
    /// with no sound in it.
    /// </remarks>
    public byte[] SaveState()
    {
        var answer = Ask(BridgeCall.SaveState, null);

        if (answer.Call != BridgeCall.State) return _state ?? Array.Empty<byte>();

        _state = answer.Payload;

        return answer.Payload;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Kept here as well as sent, so a plugin that has to be started again comes back with the
    /// patch it was given. An empty lump is remembered and not sent: there is nothing in it to
    /// put back.
    /// </remarks>
    public void LoadState(byte[]? state)
    {
        _state = state;

        if (state == null || state.Length == 0) return;

        Send(BridgeCall.LoadState, state);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Queued into the shared block rather than sent as a message, so it costs no round trip
    /// and is applied by the other side immediately before the block it belongs to. A dead
    /// plugin swallows it.
    /// </remarks>
    public void NoteOn(int semitone, float velocity)
    {
        var process = _process;
        if (process?.Alive != true) return;

        Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Tracker, () =>
            Info.Name + " note on " + semitone + " at " + velocity.ToString("0.##"));

        process.Block.Queue(BridgeEvent.NoteOn, (uint)Math.Clamp(semitone, 0, 127), velocity);
    }

    /// <inheritdoc/>
    /// <remarks>Queued the same way as the press, so the two halves of a key cannot cross.</remarks>
    public void NoteOff(int semitone)
    {
        var process = _process;
        if (process?.Alive != true) return;

        process.Block.Queue(BridgeEvent.NoteOff, (uint)Math.Clamp(semitone, 0, 127), 0);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// One event rather than one per note, since the plugin knows what it is sounding and this
    /// side does not.
    /// </remarks>
    public void AllNotesOff()
    {
        var process = _process;
        if (process?.Alive != true) return;

        Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Tracker, () => Info.Name + " all notes off");

        process.Block.Queue(BridgeEvent.AllNotesOff, 0, 0);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Runs the block through the plugin over in the other process. A dead plugin leaves the
    /// buffer exactly as it found it, which is the track carrying on without the effect rather
    /// than the track going silent.
    ///
    /// The process is held open for the whole block and not only for the crossing: the copies
    /// in and out are into memory the other process shares, and that memory is only freed once
    /// nobody is inside it.
    /// </remarks>
    public void Process(float[] buffer, int frames)
    {
        var process = _process;
        if (process == null || frames <= 0) return;

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

    /// <summary>
    /// The crossing itself, for an effect: copy in, ask for a block, copy out.
    /// </summary>
    /// <remarks>
    /// Broken into chunks no larger than the shared block, since a caller may ask for more
    /// frames at once than the block was made for. A crossing that fails leaves the rest of the
    /// buffer as it was, which for an effect is the audio going past untouched.
    /// </remarks>
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

    /// <inheritdoc/>
    /// <remarks>
    /// Fills the block with what the instrument is playing. A dead plugin fills it with
    /// silence, because there is nothing else in it that could be right: whatever was in the
    /// buffer before is somebody else's audio.
    /// </remarks>
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

    /// <summary>
    /// The crossing for an instrument: the input is cleared rather than copied, since an
    /// instrument is given nothing and hands back what it is playing.
    /// </summary>
    /// <remarks>
    /// A crossing that fails clears the rest of the buffer. Everything that has already come
    /// back is kept, so an instrument whose process dies part way through a block ends the
    /// block rather than repeating whatever was in the buffer.
    /// </remarks>
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

    /// <inheritdoc/>
    /// <remarks>
    /// Opens the plugin's own interface over in the process where the plugin is, and hands back
    /// something that speaks to it. Null for a plugin that draws no window of its own, which is
    /// the panel of knobs being used instead.
    ///
    /// A second call takes the first window's editor away from it: the other side keeps one
    /// interface per plugin and puts it into whichever window asks, so two live editors here
    /// would both believe they had it.
    /// </remarks>
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

    /// <summary>
    /// Told by a window that it has closed. Only the current one is forgotten, so a window that
    /// was already replaced cannot take the new one down with it on its way out.
    /// </summary>
    internal void Forget(BridgedEditor editor)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_editor, editor)) _editor = null;
        }
    }

    /// <summary>
    /// Asks the plugin something and waits for the answer.
    /// </summary>
    /// <remarks>
    /// Two patiences, because the questions are not alike. A knob's value is asked from the
    /// thread that draws and has to come back inside the time a window has before it is called
    /// frozen; a patch being handed over is worth waiting properly for. See
    /// <see cref="PluginBridge.QuickTimeoutMilliseconds"/>.
    /// </remarks>
    private (BridgeCall Call, byte[] Payload) Ask(BridgeCall call, byte[]? payload)
    {
        var process = _process;

        return process?.Alive != true
            ? (BridgeCall.Fail, Array.Empty<byte>())
            : process.Call(call, payload, Patience(call));
    }

    /// <summary>
    /// How long this question is worth waiting for.
    /// </summary>
    /// <remarks>
    /// A patch is hundreds of kilobytes for some plugins and is read and written whole, so it
    /// gets the long patience. Everything else is a number or a short string, asked while
    /// somebody is looking at the window it is going into, and gets the short one.
    /// </remarks>
    private static int Patience(BridgeCall call) => call switch
    {
        BridgeCall.SaveState or BridgeCall.LoadState => PluginBridge.CallTimeoutMilliseconds,

        _ => PluginBridge.QuickTimeoutMilliseconds
    };

    /// <summary>
    /// Says something to the plugin and throws the answer away. Still a round trip: the other
    /// side answers everything, and reading the answer is what keeps the two in step.
    /// </summary>
    private void Send(BridgeCall call, byte[]? payload) => Ask(call, payload);

    /// <inheritdoc/>
    /// <remarks>
    /// Closing on purpose, so <see cref="Stopped"/> is not raised: a plugin taken off a track
    /// is not news. The window goes first, since a plugin drawing into a window whose plugin
    /// has been disposed is a crash inside somebody else's toolkit.
    /// </remarks>
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
                process.Reloaded -= OnReloaded;
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
    /// <summary>The plugin this window belongs to, told when the window has gone.</summary>
    private readonly BridgedPlugin _owner;

    /// <summary>The process to say it to. Held directly, since every call here is one message.</summary>
    private readonly PluginProcess _process;

    /// <summary>
    /// Set once there is nothing left to talk to, whether the window was closed or the plugin
    /// died underneath it. Everything after that is refused rather than sent into a dead socket.
    /// </summary>
    private bool _closed;

    /// <summary>
    /// Takes the size the plugin asked for when it was opened.
    /// </summary>
    /// <remarks>
    /// A plugin that gives no size gets 640 by 480, which is a window somebody can find and
    /// resize rather than a strip of nothing. Handing a plugin a window of no size at all is
    /// close to handing it the one-pixel window Avalonia makes before its first layout, which
    /// is what used to kill Serum.
    /// </remarks>
    internal BridgedEditor(BridgedPlugin owner, PluginProcess process, int width, int height)
    {
        _owner = owner;
        _process = process;

        Size = (width > 0 ? width : 640, height > 0 ? height : 480);

        _process.ResizeRequested += OnResizeRequested;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// What the plugin last said it wanted, or last agreed to. It changes underneath the window
    /// when a plugin resizes itself, which is a preset with a bigger panel arriving.
    /// </remarks>
    public (int Width, int Height) Size { get; private set; }

    /// <inheritdoc/>
    /// <remarks>
    /// Answered by the plugin when its interface was opened, and carried across with the size.
    /// </remarks>
    public bool CanResize { get; internal init; }

    /// <inheritdoc/>
    /// <remarks>Raised on the bridge's reader thread. Whoever listens gets to their own.</remarks>
    public event Action<int, int>? ResizeRequested;

    /// <summary>
    /// The plugin asking for a size, passed on once it has been written down here. A window
    /// that has already closed says nothing: the plugin is talking to a window that is gone.
    /// </summary>
    private void OnResizeRequested(int width, int height)
    {
        if (_closed || width <= 0 || height <= 0) return;

        Size = (width, height);

        ResizeRequested?.Invoke(width, height);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The handle crosses to the other process and the plugin draws straight into it, which is
    /// what X11 allows and what makes a plugin's interface possible from out here at all. The
    /// size the plugin settles on comes back with the answer, since a plugin often has its own
    /// opinion once it has really drawn.
    /// </remarks>
    public bool Attach(nint window)
    {
        if (_closed) return false;

        var answer = _process.Call(BridgeCall.Attach, BridgeBody.Handle(window), PluginBridge.WindowTimeoutMilliseconds);

        if (answer.Call != BridgeCall.Ok) return false;

        var size = BridgeBody.ReadPair(answer.Payload);

        if (size.First > 0 && size.Second > 0) Size = (size.First, size.Second);

        return true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Takes the interface back out of the window. The interface itself is left standing over
    /// there on purpose: see the other side, where building a second one is what silences a
    /// plugin.
    /// </remarks>
    public void Detach()
    {
        if (_closed) return;

        _process.Call(BridgeCall.Detach, null, PluginBridge.WindowTimeoutMilliseconds);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The window was dragged, so the plugin is told to lay itself out again. Sent even to a
    /// plugin that says it cannot resize, since it is the plugin's business what to do about it.
    /// </remarks>
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

    /// <inheritdoc/>
    /// <remarks>
    /// A window that was orphaned has nothing left to tell: the plugin it was showing is gone,
    /// so the owner is told and no message is sent. Otherwise the interface is put away over
    /// there and then the owner is told, in that order, so the plugin stops drawing before
    /// anything else lets go of the window.
    /// </remarks>
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
