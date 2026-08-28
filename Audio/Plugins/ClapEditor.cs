using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// A CLAP plugin's own window: the interface the plugin draws, rather than the knobs the host
/// draws for it.
/// </summary>
/// <remarks>
/// The same idea as the VST3 side and a much shorter road to it, because CLAP is plain C: ask
/// for the gui extension, ask whether it can do windows of this platform's kind, and hand it
/// one. What the plugin does need first is somewhere for its clock and its X11 connection to
/// live, which is what <see cref="ClapHostExtensions"/> is: without those a plugin gets a
/// window it can never draw into, which looks exactly like a black rectangle.
/// </remarks>
public sealed unsafe class ClapEditor : IPluginEditor
{
    /// <summary>
    /// The effect this window belongs to. Held so the registration can be found again when the
    /// window goes, and so a resize request knows whose it is.
    /// </summary>
    private readonly ClapEffect _owner;

    /// <summary>The gui extension, owned by the plugin and valid while the plugin is loaded.</summary>
    private readonly ClapPluginGui* _gui;

    /// <summary>The plugin, which every call on the extension is about.</summary>
    private readonly ClapPlugin* _plugin;

    /// <summary>True once the plugin has built its interface. Nothing may be called before it.</summary>
    private bool _created;

    /// <summary>
    /// True once it has been taken down. Checked by every method, since the window can be closed
    /// while somebody is still holding the editor.
    /// </summary>
    private bool _closed;

    /// <summary>
    /// Private because an editor is only ever made by <see cref="Open"/>, which is the only
    /// place that knows the plugin has agreed to draw on this platform.
    /// </summary>
    private ClapEditor(ClapEffect owner, ClapPluginGui* gui, ClapPlugin* plugin)
    {
        _owner = owner;
        _gui = gui;
        _plugin = plugin;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Starts at a plain window's worth, so a plugin that will not say how big it wants to be
    /// still gets something a person can work in rather than nothing.
    /// </remarks>
    public (int Width, int Height) Size { get; private set; } = (640, 480);

    /// <inheritdoc/>
    /// <remarks>Read once when the window is built, since it is a fact about the plugin.</remarks>
    public bool CanResize { get; private set; }

    /// <inheritdoc/>
    public event Action<int, int>? ResizeRequested;

    /// <summary>
    /// Opens the plugin's interface, or gives back null when it has none it can show here.
    /// </summary>
    /// <remarks>
    /// A plugin is asked whether it can do this platform's kind of window before being told to
    /// make one. Refusing is a normal answer: plenty of plugins are parameters and nothing else.
    ///
    /// The window is made not floating: it goes inside one of ours, the way an insert on a desk
    /// sits in the rack rather than on the floor next to it.
    /// </remarks>
    internal static ClapEditor? Open(ClapEffect owner)
    {
        var plugin = owner.Handle;
        if (plugin == null || plugin->GetExtension == null) return null;

        using var name = new NativeText(ClapAbi.GuiExtension);

        var gui = (ClapPluginGui*)plugin->GetExtension(plugin, name.Pointer);
        if (gui == null || gui->Create == null || gui->SetParent == null) return null;

        using var api = new NativeText(ClapAbi.WindowApi);

        if (gui->IsApiSupported != null && gui->IsApiSupported(plugin, api.Pointer, 0) == 0) return null;

        if (gui->Create(plugin, api.Pointer, 0) == 0) return null;

        var editor = new ClapEditor(owner, gui, plugin) { _created = true };

        editor.ReadSize();

        editor.CanResize = gui->CanResize != null && gui->CanResize(plugin) != 0;

        ClapHostExtensions.Watch(owner, editor);

        return editor;
    }

    /// <summary>
    /// Asks the plugin how big it wants to be, and keeps what it says only if it is a real size.
    /// A plugin that refuses, or answers nought, keeps whatever was there before.
    /// </summary>
    private void ReadSize()
    {
        if (_gui->GetSize == null) return;

        uint width = 0;
        uint height = 0;

        if (_gui->GetSize(_plugin, &width, &height) == 0) return;

        if (width > 0 && height > 0) Size = ((int)width, (int)height);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The size is read again after the plugin has been given the window, because plenty of
    /// plugins lay themselves out at that moment and only then know what they are.
    /// </remarks>
    public bool Attach(nint window)
    {
        if (_closed || !_created || window == 0) return false;

        using var api = new NativeText(ClapAbi.WindowApi);

        var frame = new ClapWindow { Api = api.Pointer, Handle = window };

        if (_gui->SetParent(_plugin, &frame) == 0) return false;

        ReadSize();

        if (_gui->Show != null) _gui->Show(_plugin);

        return true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Hidden rather than destroyed. CLAP has no way to take a window back out of its parent, so
    /// the interface stays built and is taken down for good in <see cref="Dispose"/>.
    /// </remarks>
    public void Detach()
    {
        if (_closed || !_created) return;

        if (_gui->Hide != null) _gui->Hide(_plugin);
    }

    /// <inheritdoc/>
    public void Resized(int width, int height)
    {
        if (_closed || !_created || width <= 0 || height <= 0 || _gui->SetSize == null) return;

        _gui->SetSize(_plugin, (uint)width, (uint)height);

        Size = (width, height);
    }

    /// <summary>The plugin asking for a different size, passed on to whoever owns the window.</summary>
    internal void Asked(int width, int height)
    {
        if (_closed || width <= 0 || height <= 0) return;

        Size = (width, height);

        ResizeRequested?.Invoke(width, height);
    }

    /// <summary>
    /// Hides the interface and has the plugin destroy it, in that order, and stops the host from
    /// forwarding any more resize requests to an editor that has gone.
    /// </summary>
    public void Dispose()
    {
        if (_closed) return;
        _closed = true;

        ClapHostExtensions.Unwatch(_owner);

        if (!_created) return;

        _created = false;

        if (_gui->Hide != null) _gui->Hide(_plugin);
        if (_gui->Destroy != null) _gui->Destroy(_plugin);
    }
}

/// <summary>
/// The parts of itself the host offers a CLAP plugin: a clock, a way to have files watched,
/// and somewhere to send a request to be resized.
/// </summary>
/// <remarks>
/// A plugin gets at these through the host struct, which is plain C and static, so the way back
/// from a callback to the plugin it belongs to is the host pointer. Each loaded plugin gets its
/// own host struct, and this keeps a note of which plugin each one is.
///
/// Without the clock and the watched file a Linux CLAP plugin cannot draw: its toolkit sits on
/// an X11 connection waiting to be told there is something on it, and nothing else in the
/// process is ever going to look. The same job the VST3 side calls a run loop, and the same
/// pump underneath. See <see cref="PluginRunLoop"/>.
/// </remarks>
internal static unsafe class ClapHostExtensions
{
    /// <summary>
    /// One entry per loaded plugin, keyed by its own host struct, which is the only thing a
    /// plain C callback is handed that can identify who is calling.
    /// </summary>
    private static readonly Dictionary<nint, Registration> Registered = new();

    /// <summary>Held over every read and write of the registrations and the shared structs.</summary>
    private static readonly object Gate = new();

    /// <summary>
    /// The next key to hand the run loop. One counter for timers and files both, so a key names
    /// exactly one thing however it was asked for.
    /// </summary>
    private static nint _next = 1;

    /// <summary>
    /// The three host extension structs, made on first use and shared by every plugin. They hold
    /// only function pointers, and which plugin is calling comes from the host pointer each
    /// function is handed, so one of each is enough.
    /// </summary>
    private static ClapHostGui* _gui;

    /// <inheritdoc cref="_gui"/>
    private static ClapHostTimerSupport* _timers;

    /// <inheritdoc cref="_gui"/>
    private static ClapHostPosixFd* _files;

    /// <summary>What is known about one loaded plugin: who it is, and what it has asked for.</summary>
    private sealed class Registration
    {
        /// <summary>Filled in once the plugin has finished loading, which is after it may
        /// first ask for a timer.</summary>
        public ClapEffect? Effect;
        /// <summary>Where a resize request goes, or null while the plugin has no window up.</summary>
        public ClapEditor? Editor;

        /// <summary>
        /// The plugin's own timer numbers against the run loop's keys. Two dictionaries because
        /// the plugin counts from one per plugin and the run loop counts once for the process.
        /// </summary>
        public readonly Dictionary<uint, nint> Timers = new();

        /// <summary>The file descriptors being watched for this plugin, against their keys.</summary>
        public readonly Dictionary<int, nint> Files = new();

        /// <summary>
        /// The next timer number to give this plugin. Never reused, so a timer that rings after
        /// being given back rings on nothing rather than on somebody else's timer.
        /// </summary>
        public uint NextTimer = 1;
    }

    /// <summary>
    /// Puts a host struct on the list before the plugin behind it exists.
    /// </summary>
    /// <remarks>
    /// A plugin is allowed to ask for a timer while it is still loading, which is before there
    /// is anything to call when that timer comes round. So the note is made first and filled in
    /// afterwards, and a timer that rings in between rings on nothing.
    /// </remarks>
    public static void Reserve(ClapHost* host)
    {
        if (host == null) return;

        lock (Gate) Registered[(nint)host] = new Registration();
    }

    /// <summary>Notes which plugin a host struct belongs to, so a callback can find its way back.</summary>
    public static void Bind(ClapHost* host, ClapEffect effect)
    {
        if (host == null || effect == null) return;

        lock (Gate)
        {
            if (Registered.TryGetValue((nint)host, out var existing)) existing.Effect = effect;
            else Registered[(nint)host] = new Registration { Effect = effect };
        }
    }

    /// <summary>Forgets one, and takes back every timer and file it had asked for.</summary>
    public static void Unbind(ClapHost* host)
    {
        if (host == null) return;

        Registration? going;

        lock (Gate)
        {
            if (!Registered.Remove((nint)host, out going)) return;
        }

        foreach (var key in going.Timers.Values) PluginRunLoop.Drop(key);
        foreach (var key in going.Files.Values) PluginRunLoop.Unwatch(key);
    }

    /// <summary>Says which editor a resize request should reach.</summary>
    /// <remarks>
    /// Walked rather than looked up, because the caller has the effect and not the host struct
    /// behind it. There is one registration per plugin and a handful of plugins, so the walk
    /// costs nothing and happens once per window.
    /// </remarks>
    public static void Watch(ClapEffect effect, ClapEditor editor)
    {
        lock (Gate)
        {
            foreach (var registration in Registered.Values)
            {
                if (ReferenceEquals(registration.Effect, effect)) registration.Editor = editor;
            }
        }
    }

    /// <summary>
    /// Stops sending resize requests to an editor that has gone. Walked rather than looked up,
    /// for the same reason as <see cref="Watch"/>: the caller has the effect and not its host.
    /// </summary>
    public static void Unwatch(ClapEffect effect)
    {
        lock (Gate)
        {
            foreach (var registration in Registered.Values)
            {
                if (ReferenceEquals(registration.Effect, effect)) registration.Editor = null;
            }
        }
    }

    /// <summary>
    /// Which plugin a callback is about. Null for a host struct that has already been forgotten,
    /// which is what a callback arriving during teardown looks like.
    /// </summary>
    private static Registration? Find(ClapHost* host)
    {
        if (host == null) return null;

        lock (Gate) return Registered.TryGetValue((nint)host, out var registration) ? registration : null;
    }

    /// <summary>
    /// The answer to a plugin asking what the host can do. Everything not listed here is
    /// answered with null, which is a legal answer to every extension there is.
    /// </summary>
    public static void* Extension(byte* id)
    {
        if (id == null) return null;

        string? name = Marshal.PtrToStringUTF8((nint)id);

        if (name == ClapAbi.ParamsExtension) return Params();
        if (name == ClapAbi.GuiExtension) return Gui();
        if (name == ClapAbi.TimerExtension) return Timers();
        if (name == ClapAbi.PosixFdExtension) return Files();

        return null;
    }

    /// <inheritdoc cref="_gui"/>
    private static ClapHostParams* _parameters;

    /// <summary>The parameters extension, built on first use and never freed.</summary>
    private static void* Params()
    {
        lock (Gate)
        {
            if (_parameters != null) return _parameters;

            _parameters = (ClapHostParams*)NativeMemory.AllocZeroed(1, (nuint)sizeof(ClapHostParams));

            _parameters->Rescan = &Rescan;
            _parameters->Clear = &Clear;
            _parameters->RequestFlush = &RequestFlush;

            return _parameters;
        }
    }

    /// <summary>
    /// The plugin asking the host to read its parameters again, which is what loading a preset
    /// in its own window comes through as.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Rescan(ClapHost* host, uint flags)
    {
        try
        {
            Find(host)?.Effect?.Reload();
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// The plugin asking the host to throw away what it had queued for a parameter. Nothing is
    /// queued here between blocks, so there is nothing to throw away.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Clear(ClapHost* host, uint id, uint flags) { }

    /// <summary>
    /// The plugin saying it has something for the host and asking to be given the chance to
    /// hand it over.
    /// </summary>
    /// <remarks>
    /// Which is what a knob turned in the plugin's own window looks like from this side. It is
    /// written down rather than acted on: the handing over has to happen on the audio thread,
    /// and this is not it.
    /// </remarks>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void RequestFlush(ClapHost* host)
    {
        Find(host)?.Effect?.WantsFlush();
    }

    /// <summary>The gui extension, built on first use and never freed.</summary>
    private static void* Gui()
    {
        lock (Gate)
        {
            if (_gui != null) return _gui;

            _gui = (ClapHostGui*)NativeMemory.AllocZeroed(1, (nuint)sizeof(ClapHostGui));

            _gui->ResizeHintsChanged = &HintsChanged;
            _gui->RequestResize = &RequestResize;
            _gui->RequestShow = &RequestShow;
            _gui->RequestHide = &RequestHide;
            _gui->Closed = &Closed;

            return _gui;
        }
    }

    /// <summary>The timer extension, built on first use and never freed.</summary>
    private static void* Timers()
    {
        lock (Gate)
        {
            if (_timers != null) return _timers;

            _timers = (ClapHostTimerSupport*)NativeMemory.AllocZeroed(1, (nuint)sizeof(ClapHostTimerSupport));

            _timers->RegisterTimer = &RegisterTimer;
            _timers->UnregisterTimer = &UnregisterTimer;

            return _timers;
        }
    }

    /// <summary>The file watching extension, built on first use and never freed.</summary>
    private static void* Files()
    {
        lock (Gate)
        {
            if (_files != null) return _files;

            _files = (ClapHostPosixFd*)NativeMemory.AllocZeroed(1, (nuint)sizeof(ClapHostPosixFd));

            _files->RegisterFd = &RegisterFile;
            _files->ModifyFd = &ModifyFile;
            _files->UnregisterFd = &UnregisterFile;

            return _files;
        }
    }

    /// <summary>
    /// A plugin asking for a clock. The number it is given is its own and counts from one; the
    /// key the run loop is given is the process's and never repeats, so the two cannot be
    /// confused when several plugins are open.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte RegisterTimer(ClapHost* host, uint milliseconds, uint* id)
    {
        var registration = Find(host);
        if (registration == null || id == null) return 0;

        uint given;
        nint key;

        lock (Gate)
        {
            given = registration.NextTimer++;
            key = _next++;

            registration.Timers[given] = key;
        }

        *id = given;

        PluginRunLoop.Keep(key, milliseconds, () => registration.Effect?.RingTimer(given));

        return 1;
    }

    /// <summary>Giving a clock back. A number nobody knows is refused rather than ignored.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte UnregisterTimer(ClapHost* host, uint id)
    {
        var registration = Find(host);
        if (registration == null) return 0;

        nint key;

        lock (Gate)
        {
            if (!registration.Timers.Remove(id, out key)) return 0;
        }

        PluginRunLoop.Drop(key);

        return 1;
    }

    /// <summary>
    /// A plugin asking the host to watch a file descriptor, which is its X11 connection. Without
    /// this its toolkit sits waiting to be told there is something on it and nothing else in the
    /// process is ever going to look, so the window is a black rectangle.
    /// </summary>
    /// <remarks>
    /// The same descriptor asked for twice replaces the first watch rather than adding a second,
    /// since two watches on one connection would deliver every event twice.
    /// </remarks>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte RegisterFile(ClapHost* host, int file, uint flags)
    {
        var registration = Find(host);
        if (registration == null) return 0;

        nint key;

        lock (Gate)
        {
            if (registration.Files.TryGetValue(file, out key))
            {
                PluginRunLoop.Unwatch(key);
            }

            key = _next++;
            registration.Files[file] = key;
        }

        PluginRunLoop.Watching(key, file, ready => registration.Effect?.RingFile(ready, ClapAbi.PosixFdRead));

        return 1;
    }

    /// <summary>
    /// A plugin changing what it wants a descriptor watched for. Accepted and ignored: the watch
    /// here is for something to read, which is the only thing an X11 connection ever wants.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte ModifyFile(ClapHost* host, int file, uint flags) => 1;

    /// <summary>Asking the host to stop watching one. A descriptor nobody is watching is refused.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte UnregisterFile(ClapHost* host, int file)
    {
        var registration = Find(host);
        if (registration == null) return 0;

        nint key;

        lock (Gate)
        {
            if (!registration.Files.Remove(file, out key)) return 0;
        }

        PluginRunLoop.Unwatch(key);

        return 1;
    }

    /// <summary>
    /// The plugin saying the shapes it will accept have changed. Nothing here reads those hints,
    /// so there is nothing to read again.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void HintsChanged(ClapHost* host) { }

    /// <summary>
    /// The plugin asking to be a different size, which is how one with a fold-out panel opens
    /// it. Refused when no window is up, since there would be nothing to resize.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte RequestResize(ClapHost* host, uint width, uint height)
    {
        var registration = Find(host);

        var editor = registration?.Editor;
        if (editor == null) return 0;

        editor.Asked((int)width, (int)height);

        return 1;
    }

    /// <summary>
    /// The plugin asking to be shown or hidden. Refused: the window belongs to whoever opened
    /// it, and a plugin putting its own interface on screen unasked is not wanted here.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte RequestShow(ClapHost* host) => 0;

    /// <inheritdoc cref="RequestShow"/>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte RequestHide(ClapHost* host) => 0;

    /// <summary>
    /// The plugin saying its window was closed from inside. Nothing is done: the window here is
    /// only ever closed by the host, so this is a plugin reporting something that did not happen.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Closed(ClapHost* host, byte wasDestroyed) { }
}
