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
    private readonly ClapEffect _owner;
    private readonly ClapPluginGui* _gui;
    private readonly ClapPlugin* _plugin;

    private bool _created;
    private bool _closed;

    private ClapEditor(ClapEffect owner, ClapPluginGui* gui, ClapPlugin* plugin)
    {
        _owner = owner;
        _gui = gui;
        _plugin = plugin;
    }

    public (int Width, int Height) Size { get; private set; } = (640, 480);

    public bool CanResize { get; private set; }

    public event Action<int, int>? ResizeRequested;

    /// <summary>
    /// Opens the plugin's interface, or gives back null when it has none it can show here.
    /// </summary>
    /// <remarks>
    /// A plugin is asked whether it can do this platform's kind of window before being told to
    /// make one. Refusing is a normal answer: plenty of plugins are parameters and nothing else.
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

        // Not floating: the window goes inside one of ours, the way an insert on a desk sits in
        // the rack rather than on the floor next to it.
        if (gui->Create(plugin, api.Pointer, 0) == 0) return null;

        var editor = new ClapEditor(owner, gui, plugin) { _created = true };

        editor.ReadSize();

        editor.CanResize = gui->CanResize != null && gui->CanResize(plugin) != 0;

        ClapHostExtensions.Watch(owner, editor);

        return editor;
    }

    private void ReadSize()
    {
        if (_gui->GetSize == null) return;

        uint width = 0;
        uint height = 0;

        if (_gui->GetSize(_plugin, &width, &height) == 0) return;

        if (width > 0 && height > 0) Size = ((int)width, (int)height);
    }

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

    public void Detach()
    {
        if (_closed || !_created) return;

        if (_gui->Hide != null) _gui->Hide(_plugin);
    }

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
    private static readonly Dictionary<nint, Registration> Registered = new();
    private static readonly object Gate = new();

    private static nint _next = 1;

    private static ClapHostGui* _gui;
    private static ClapHostTimerSupport* _timers;
    private static ClapHostPosixFd* _files;

    private sealed class Registration
    {
        /// <summary>Filled in once the plugin has finished loading, which is after it may
        /// first ask for a timer.</summary>
        public ClapEffect? Effect;
        public ClapEditor? Editor;
        public readonly Dictionary<uint, nint> Timers = new();
        public readonly Dictionary<int, nint> Files = new();
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

    private static ClapHostParams* _parameters;

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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Rescan(ClapHost* host, uint flags) { }

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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte ModifyFile(ClapHost* host, int file, uint flags) => 1;

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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void HintsChanged(ClapHost* host) { }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte RequestResize(ClapHost* host, uint width, uint height)
    {
        var registration = Find(host);

        var editor = registration?.Editor;
        if (editor == null) return 0;

        editor.Asked((int)width, (int)height);

        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte RequestShow(ClapHost* host) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte RequestHide(ClapHost* host) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Closed(ClapHost* host, byte wasDestroyed) { }
}
