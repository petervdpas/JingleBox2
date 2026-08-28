using System;
using System.Runtime.InteropServices;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// The parts of the CLAP plugin ABI this host uses, as C# sees them.
/// </summary>
/// <remarks>
/// CLAP is a plain C ABI: structs of function pointers, no C++ vtables and no COM. That is
/// what makes it reachable from .NET without a native shim, and why it is the format this app
/// hosts rather than VST3.
///
/// Every struct here is laid out sequentially with the same field order and types as the C
/// header, so the runtime's natural alignment matches the C compiler's. Change a field's order
/// or width and the layout silently stops matching, which is a crash inside somebody else's
/// code with no clue where it came from. A C bool is one byte, so it is a byte here.
///
/// Every function pointer takes the owning struct as its first argument, which is how a C ABI
/// spells "this". The one exception is the entry point, which belongs to the library rather
/// than to any object.
/// </remarks>
internal static class ClapAbi
{
    /// <summary>The symbol every .clap file exports.</summary>
    public const string EntrySymbol = "clap_entry";

    /// <summary>
    /// The factory to ask the entry point for. There are others in the specification and none
    /// of them is used here.
    /// </summary>
    public const string PluginFactoryId = "clap.plugin-factory";

    /// <summary>The plugin's knobs: how many, what each is, and how to word a value.</summary>
    public const string ParamsExtension = "clap.params";

    /// <summary>
    /// How many channels the plugin wants in and out. Asked before it is activated, since the
    /// answer decides how the busses are built.
    /// </summary>
    public const string AudioPortsExtension = "clap.audio-ports";

    /// <summary>The plugin's own window, when it has one.</summary>
    public const string GuiExtension = "clap.gui";

    /// <summary>
    /// Everything the plugin keeps that its parameters do not describe. Its preset, in
    /// practice, and for anything with wavetables or samples inside it that is most of what
    /// somebody set up.
    /// </summary>
    /// <remarks>
    /// This was never implemented here until late, so CLAP effects had no state at all: only the
    /// parameters had ever been asked for, and nothing noticed because VST3 is the only format
    /// that can be an instrument here and an instrument is where a missing patch is loudest.
    /// </remarks>
    public const string StateExtension = "clap.state";

    /// <summary>A clock the plugin asks the host to hold, the same one VST3 calls a run loop.</summary>
    public const string TimerExtension = "clap.timer-support";

    /// <summary>Files the plugin wants watching. Its X11 connection, in practice.</summary>
    public const string PosixFdExtension = "clap.posix-fd-support";

    /// <summary>What a window is called on this platform, in CLAP's spelling.</summary>
    public static string WindowApi =>
        OperatingSystem.IsWindows() ? "win32" : OperatingSystem.IsMacOS() ? "cocoa" : "x11";

    /// <summary>Something to read on a watched file.</summary>
    public const uint PosixFdRead = 1 << 0;

    /// <summary>Fixed width name fields in the ABI.</summary>
    public const int NameSize = 256;

    /// <summary>
    /// The fixed width of a path field in the ABI, which is what a parameter's module name is
    /// carried in.
    /// </summary>
    public const int PathSize = 1024;

    /// <summary>Event space and type numbers, for a parameter move sent into a plugin.</summary>
    /// <remarks>
    /// A space is CLAP's room for event kinds nobody has invented yet. Everything this host
    /// sends is in the core space, which is nought.
    /// </remarks>
    public const ushort CoreEventSpace = 0;

    /// <summary>
    /// The core event that carries a parameter's new value. The number is the specification's
    /// and not an index into anything here.
    /// </summary>
    public const ushort ParamValueEvent = 5;
}

/// <summary>
/// A CLAP version, which is what both halves compare before either trusts the other's structs.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ClapVersion
{
    /// <summary>Breaking changes. Different majors mean the structs are not the same shape.</summary>
    public uint Major;

    /// <summary>Additions that keep the old layout. A higher minor is safe to load.</summary>
    public uint Minor;

    /// <summary>Fixes with no bearing on the layout at all.</summary>
    public uint Revision;
}

/// <summary>
/// The one symbol a .clap exports, and the whole of what a host has before it has asked for
/// anything.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapPluginEntry
{
    /// <summary>Which CLAP the library was built against.</summary>
    public ClapVersion Version;

    /// <summary>
    /// Wakes the library up, given the path it was loaded from. Called once per library and not
    /// once per plugin, and a plugin may legitimately answer false to say it will not run here.
    /// </summary>
    public delegate* unmanaged[Cdecl]<byte*, byte> Init;

    /// <summary>Puts the library back to sleep. The last thing called before it is unloaded.</summary>
    public delegate* unmanaged[Cdecl]<void> Deinit;

    /// <summary>
    /// Hands back a factory by name, or null for one the library does not have. The only name
    /// asked for here is <see cref="ClapAbi.PluginFactoryId"/>.
    /// </summary>
    public delegate* unmanaged[Cdecl]<byte*, void*> GetFactory;
}

/// <summary>
/// What is inside a .clap: one library can hold any number of plugins, and usually holds one.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapPluginFactory
{
    /// <summary>How many plugins this library offers.</summary>
    public delegate* unmanaged[Cdecl]<ClapPluginFactory*, uint> Count;

    /// <summary>
    /// What the plugin at that index is, without creating it. This is the whole of what a scan
    /// needs, which is why scanning costs no plugin instances.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapPluginFactory*, uint, ClapPluginDescriptor*> GetDescriptor;

    /// <summary>
    /// Makes one, by its id rather than by its index, given the host it is to talk to. Answers
    /// null when the plugin refuses to be created at all.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapPluginFactory*, ClapHost*, byte*, ClapPlugin*> Create;
}

/// <summary>Everything a picker needs about a plugin that has not been loaded.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapPluginDescriptor
{
    /// <summary>Which CLAP this plugin was built against.</summary>
    public ClapVersion Version;

    /// <summary>
    /// The plugin's identity, in reverse domain form. What a saved song stores, because a path
    /// is about this machine and an id is about the plugin.
    /// </summary>
    public byte* Id;

    /// <summary>What the plugin calls itself.</summary>
    public byte* Name;

    /// <summary>Who made it.</summary>
    public byte* Vendor;

    /// <summary>The vendor's page for it.</summary>
    public byte* Url;

    /// <summary>Where its manual is, if it says.</summary>
    public byte* ManualUrl;

    /// <summary>Where to ask for help with it, if it says.</summary>
    public byte* SupportUrl;

    /// <summary>The plugin's own version, as text and in whatever form it likes.</summary>
    public byte* PluginVersion;

    /// <summary>A sentence about what it does.</summary>
    public byte* Description;

    /// <summary>
    /// A null-terminated list of words the plugin uses to say what kind of thing it is, such as
    /// "instrument" or "audio-effect". The only way to tell an instrument from an effect before
    /// loading it.
    /// </summary>
    public byte** Features;
}

/// <summary>
/// The host, as the plugin holds it. Allocated unmanaged and never moved, because a plugin
/// keeps this pointer for as long as it is loaded.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapHost
{
    /// <summary>Which CLAP the host implements. A plugin may refuse to load against an older one.</summary>
    public ClapVersion Version;

    /// <summary>
    /// The host's own pointer, handed back to it in every callback. This is how a static
    /// callback finds out which of several plugins is calling it.
    /// </summary>
    public void* HostData;

    /// <summary>What the host calls itself.</summary>
    public byte* Name;

    /// <summary>Who wrote the host.</summary>
    public byte* Vendor;

    /// <summary>The host's page.</summary>
    public byte* Url;

    /// <summary>The host's own version.</summary>
    public byte* HostVersion;

    /// <summary>
    /// A plugin asking whether the host implements something, by name. Null is a legal answer to
    /// every extension there is, and is what this host gives to all but three of them.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapHost*, byte*, void*> GetExtension;

    /// <summary>A plugin asking to be deactivated and activated again, because something about it changed.</summary>
    public delegate* unmanaged[Cdecl]<ClapHost*, void> RequestRestart;

    /// <summary>A plugin asking to be given blocks again after it had stopped being processed.</summary>
    public delegate* unmanaged[Cdecl]<ClapHost*, void> RequestProcess;

    /// <summary>
    /// A plugin asking for a call on the main thread, which is where it may do work it cannot do
    /// on the audio one. Answered by <c>OnMainThread</c> on the plugin.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapHost*, void> RequestCallback;
}

/// <summary>One loaded plugin, and everything a host does to it that is not an extension.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapPlugin
{
    /// <summary>What this plugin is, the same struct the factory offered before it was created.</summary>
    public ClapPluginDescriptor* Descriptor;

    /// <summary>The plugin's own pointer. The host never reads it and must never write it.</summary>
    public void* PluginData;

    /// <summary>
    /// Sets the plugin up, on the main thread, before anything else is asked of it. Extensions
    /// may not be fetched until this has answered true.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte> Init;

    /// <summary>Frees the plugin. The pointer is dead afterwards, this struct included.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, void> Destroy;

    /// <summary>
    /// Switches the plugin on for a sample rate and a block size range: rate, smallest block,
    /// largest block. The plugin allocates here, which is why it happens away from the audio
    /// thread and why a block bigger than the one promised is not allowed.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, double, uint, uint, byte> Activate;

    /// <summary>Switches it off again, undoing what activation allocated.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, void> Deactivate;

    /// <summary>Says that blocks are about to start arriving. Called on the audio thread.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte> StartProcessing;

    /// <summary>Says that they have stopped.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, void> StopProcessing;

    /// <summary>
    /// Throws away everything the plugin was holding about the sound so far: tails, filters,
    /// notes. What a transport jumping somewhere else should cause.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, void> Reset;

    /// <summary>One block of audio and the events that go with it. The audio thread, and only it.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, ClapProcess*, int> Process;

    /// <summary>
    /// Asking the plugin whether it implements something, by name. This is how the params, gui,
    /// state and audio-ports structs above are obtained.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte*, void*> GetExtension;

    /// <summary>The answer to a plugin that asked the host for a main thread call.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, void> OnMainThread;
}

/// <summary>One audio bus for one block, as CLAP describes it.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapAudioBuffer
{
    /// <summary>One pointer per channel. CLAP is deinterleaved; our busses are not.</summary>
    public float** Data32;

    /// <summary>
    /// The same thing at double precision. Exactly one of the two is non-null and this host
    /// always uses the float one, so this is always null here.
    /// </summary>
    public double** Data64;

    /// <summary>How many pointers <see cref="Data32"/> holds.</summary>
    public uint ChannelCount;

    /// <summary>How far behind this bus is, in frames, as the plugin reports it.</summary>
    public uint Latency;

    /// <summary>
    /// One bit per channel, set when that channel holds the same value for the whole block. A
    /// plugin may use it to skip silence. Left at nought here, which claims nothing and is
    /// always safe.
    /// </summary>
    public ulong ConstantMask;
}

/// <summary>Everything one call to <c>Process</c> is given.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapProcess
{
    /// <summary>
    /// A frame counter that never goes backwards, for a plugin that wants to know time has
    /// passed. Negative when the host does not keep one.
    /// </summary>
    public long SteadyTime;

    /// <summary>How many frames this block is, which may be fewer than the largest promised.</summary>
    public uint FramesCount;

    /// <summary>
    /// Where the song is and how fast, or null for a host that does not say. Null here: nothing
    /// in this application tells a plugin about the tracker's clock yet.
    /// </summary>
    public void* Transport;

    /// <summary>The busses coming in.</summary>
    public ClapAudioBuffer* AudioInputs;

    /// <summary>The busses going out.</summary>
    public ClapAudioBuffer* AudioOutputs;

    /// <summary>How many of the former there are.</summary>
    public uint AudioInputsCount;

    /// <summary>How many of the latter.</summary>
    public uint AudioOutputsCount;

    /// <summary>
    /// The events for this block, in order of time. Parameter moves reach a plugin here rather
    /// than through any setter, which is why a knob turn is queued and not written.
    /// </summary>
    public ClapInputEvents* InEvents;

    /// <summary>
    /// Somewhere for the plugin to put events of its own. This is the only place a CLAP plugin
    /// reports a knob it moved in its own window, and it is why one with its window open has to
    /// be read again forty times a second rather than being listened to.
    /// </summary>
    public ClapOutputEvents* OutEvents;
}

/// <summary>A list of events the host hands a plugin, read through two function pointers.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapInputEvents
{
    /// <summary>Whatever the host wants to find its own list with. Never read by the plugin.</summary>
    public void* Context;

    /// <summary>How many events there are.</summary>
    public delegate* unmanaged[Cdecl]<ClapInputEvents*, uint> Size;

    /// <summary>
    /// The event at that index, as a header the plugin then reads the rest of by its type. The
    /// memory belongs to the host and only lasts as long as the call.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapInputEvents*, uint, ClapEventHeader*> Get;
}

/// <summary>Somewhere for a plugin to put the events it produced during a block.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapOutputEvents
{
    /// <summary>Whatever the host wants to find its own list with.</summary>
    public void* Context;

    /// <summary>
    /// Takes one event, copying it: the plugin's memory is not kept. False means the host had
    /// no room, which the plugin is expected to survive rather than to treat as a fault.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapOutputEvents*, ClapEventHeader*, byte> TryPush;
}

/// <summary>
/// The front of every CLAP event, whatever kind it is. The size in it is how a reader steps to
/// the next one and how a kind added later can be skipped over rather than misread.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ClapEventHeader
{
    /// <summary>How many bytes the whole event is, this header included.</summary>
    public uint Size;

    /// <summary>Which frame of the block it happens on, counted from its start.</summary>
    public uint Time;

    /// <summary>
    /// Which set of type numbers <see cref="Type"/> is from. Everything sent here is
    /// <see cref="ClapAbi.CoreEventSpace"/>.
    /// </summary>
    public ushort SpaceId;

    /// <summary>What kind of event this is, within its space.</summary>
    public ushort Type;

    /// <summary>
    /// Flags about the event, such as whether it is part of a gesture somebody is still making.
    /// Nought is the ordinary case and is what is sent here.
    /// </summary>
    public uint Flags;
}

/// <summary>A parameter move, which is how a knob reaches a plugin: through the event queue.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ClapEventParamValue
{
    /// <summary>The common front, with the type set to <see cref="ClapAbi.ParamValueEvent"/>.</summary>
    public ClapEventHeader Header;

    /// <summary>Which parameter, by the plugin's own id rather than by an index.</summary>
    public uint ParamId;

    /// <summary>
    /// The pointer the plugin handed out with the parameter's description, so it can find the
    /// parameter without a lookup. Passed back untouched, and nought when the host kept none.
    /// </summary>
    public nint Cookie;

    /// <summary>
    /// Which port the move is for, or -1 for all of them. Minus one everywhere here: nothing in
    /// this application moves a parameter for one port only.
    /// </summary>
    public short PortIndex;

    /// <summary>Which MIDI channel, or -1 for all.</summary>
    public short Channel;

    /// <summary>Which key, or -1 for all. This is what makes per-note parameter moves possible.</summary>
    public short Key;

    /// <summary>Which sounding note, or -1 for all.</summary>
    public int NoteId;

    /// <summary>The new value, in the parameter's own units and inside its own range.</summary>
    public double Value;
}

/// <summary>The plugin's knobs, as the <c>clap.params</c> extension offers them.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapPluginParams
{
    /// <summary>How many parameters there are.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint> Count;

    /// <summary>What the parameter at that index is. An index here, and an id in everything after.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint, ClapParamInfo*, byte> GetInfo;

    /// <summary>Where a parameter stands, by id, in the plugin's own units.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint, double*, byte> GetValue;

    /// <summary>
    /// How the plugin words a value: id, value, a buffer to write into and its size. The only
    /// way to print a parameter whose units the host does not know.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint, double, byte*, uint, byte> ValueToText;

    /// <summary>The other way round, for a value somebody typed.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte*, uint, double*, byte> TextToValue;

    /// <summary>
    /// Hands parameter events over outside a block, for a plugin nothing is being played
    /// through. Without it a knob turned on an idle chain would sit in the queue until somebody
    /// pressed play.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, ClapInputEvents*, ClapOutputEvents*, void> Flush;
}

/// <summary>One parameter as the plugin describes it.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapParamInfo
{
    /// <summary>
    /// The number the plugin knows this parameter by, and what everything after
    /// <c>GetInfo</c> names it with. Not the index it was found at.
    /// </summary>
    public uint Id;

    /// <summary>
    /// What kind of parameter it is: stepped, hidden, read only, whether it can be automated,
    /// and so on. Read by mask rather than compared.
    /// </summary>
    public uint Flags;

    /// <summary>
    /// The plugin's own pointer for this parameter, to be handed back in every event about it.
    /// An optimisation the plugin offers and the host is free to ignore.
    /// </summary>
    public void* Cookie;

    /// <summary>The name, as a fixed field of bytes rather than a pointer. UTF-8, null terminated.</summary>
    public fixed byte Name[ClapAbi.NameSize];

    /// <summary>
    /// Where the plugin would file this parameter in a tree of its own, as a slash separated
    /// path. Not used here: the parameters are drawn in the order the plugin lists them.
    /// </summary>
    public fixed byte Module[ClapAbi.PathSize];

    /// <summary>The bottom of the range, in the plugin's own units.</summary>
    public double MinValue;

    /// <summary>The top of the range, in the same units.</summary>
    public double MaxValue;

    /// <summary>Where the parameter sits before anybody touches it.</summary>
    public double DefaultValue;
}

/// <summary>How many audio busses the plugin wants, and what each one is.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapPluginAudioPorts
{
    /// <summary>How many busses on that side. The byte is 1 for inputs and 0 for outputs.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte, uint> Count;

    /// <summary>The bus at that index on that side.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint, byte, ClapAudioPortInfo*, byte> Get;
}

/// <summary>One audio bus as the plugin describes it, before any audio has run.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapAudioPortInfo
{
    /// <summary>The plugin's own number for this bus.</summary>
    public uint Id;

    /// <summary>What the plugin calls it, for a host that draws its routing.</summary>
    public fixed byte Name[ClapAbi.NameSize];

    /// <summary>
    /// What kind of bus it is: whether it is the main one, whether 64 bit audio is supported,
    /// whether it can be processed in place.
    /// </summary>
    public uint Flags;

    /// <summary>How many channels it carries. Two is what this host asks for and expects.</summary>
    public uint ChannelCount;

    /// <summary>
    /// What the channels mean, as one of CLAP's own names: mono, stereo, surround. Null when the
    /// plugin does not say.
    /// </summary>
    public byte* PortType;

    /// <summary>
    /// The bus on the other side this one may share memory with, or an invalid id when it may
    /// not. Not used here: the busses are separate.
    /// </summary>
    public uint InPlacePair;
}

/// <summary>A window handed to a plugin: what kind it is, and which one.</summary>
/// <remarks>
/// The second field is a union in the header, of a pointer on Windows and macOS and an
/// unsigned long on X11. Both are eight bytes on the machines this runs on, so one native
/// integer covers all three.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapWindow
{
    /// <summary>
    /// Which windowing system the handle belongs to, as one of CLAP's own names. See
    /// <see cref="ClapAbi.WindowApi"/>.
    /// </summary>
    public byte* Api;

    /// <summary>The window itself: an X11 window id, an HWND, or an NSView.</summary>
    public nint Handle;
}

/// <summary>The plugin's window, as the plugin offers it.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapPluginGui
{
    /// <summary>
    /// Whether the plugin can draw into that kind of window. The byte says whether the window is
    /// to float rather than be embedded.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte*, byte, byte> IsApiSupported;

    /// <summary>Which kind the plugin would rather have, for a host that can offer more than one.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte**, byte*, byte> GetPreferredApi;

    /// <summary>Builds the interface, for that kind of window, floating or embedded.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte*, byte, byte> Create;

    /// <summary>Takes it down again. Called before the host's window goes away, never after.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, void> Destroy;

    /// <summary>Tells the plugin the display's scaling, so it can draw at the right size.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, double, byte> SetScale;

    /// <summary>How big the plugin wants to be, in pixels.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint*, uint*, byte> GetSize;

    /// <summary>Whether it will follow a window being dragged bigger.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte> CanResize;

    /// <summary>
    /// What shapes it will accept: an aspect ratio to keep, a step to snap to. Not read here,
    /// which is why the pointer is untyped.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, void*, byte> GetResizeHints;

    /// <summary>Rounds a size the host is considering to one the plugin would actually take.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint*, uint*, byte> AdjustSize;

    /// <summary>Tells the plugin the window it was given is now this size.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint, uint, byte> SetSize;

    /// <summary>
    /// Puts the plugin's interface inside a window the host owns. The window has to really be on
    /// screen at its full size first: handing over the one-pixel window a toolkit makes before
    /// its first layout is what killed Serum.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, ClapWindow*, byte> SetParent;

    /// <summary>The other arrangement: the plugin's own window kept in front of the host's.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, ClapWindow*, byte> SetTransient;

    /// <summary>What the host would like the plugin's floating window to be called.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte*, void> SuggestTitle;

    /// <summary>Makes the interface visible, once it has been given somewhere to be.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte> Show;

    /// <summary>Hides it again without taking it down.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte> Hide;
}

/// <summary>The plugin's end of the clock: it is told which of its timers came round.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapPluginTimerSupport
{
    /// <summary>The timer with that id has come round. Always on the main thread.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint, void> OnTimer;
}

/// <summary>The plugin's end of the doorbell: it is told a file has something on it.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapPluginPosixFd
{
    /// <summary>
    /// That file descriptor is ready, for those reasons. In practice it is the plugin's X11
    /// connection with events waiting on it, and this call is how they get drawn.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, int, uint, void> OnFd;
}

/// <summary>What the host offers a plugin about its window.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapHostGui
{
    /// <summary>The plugin saying the shapes it will accept have changed.</summary>
    public delegate* unmanaged[Cdecl]<ClapHost*, void> ResizeHintsChanged;

    /// <summary>
    /// The plugin asking for a different size, which is how one with a fold-out panel opens it.
    /// The host is expected to make its window that size.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapHost*, uint, uint, byte> RequestResize;

    /// <summary>The plugin asking to be shown.</summary>
    public delegate* unmanaged[Cdecl]<ClapHost*, byte> RequestShow;

    /// <summary>The plugin asking to be hidden.</summary>
    public delegate* unmanaged[Cdecl]<ClapHost*, byte> RequestHide;

    /// <summary>
    /// The plugin saying its window was closed from inside. The byte says whether the plugin has
    /// already destroyed it, which decides whether the host still has to.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapHost*, byte, void> Closed;
}

/// <summary>
/// What the host offers a plugin about its parameters.
/// </summary>
/// <remarks>
/// The one that matters here is the last: a plugin whose own window has changed something says
/// so by asking for a flush, and a host that does not offer this has nowhere for that to go.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapHostParams
{
    /// <summary>
    /// The plugin saying its parameters have changed in some way, with a mask saying how: their
    /// values, their names, or the list itself.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapHost*, uint, void> Rescan;

    /// <summary>The plugin asking the host to throw away what it had queued for a parameter.</summary>
    public delegate* unmanaged[Cdecl]<ClapHost*, uint, uint, void> Clear;

    /// <summary>
    /// The plugin asking to be flushed, which is how it reports a knob moved in its own window
    /// while nothing is playing through it.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapHost*, void> RequestFlush;
}

/// <summary>What the host offers a plugin about timers.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapHostTimerSupport
{
    /// <summary>
    /// The plugin asking for a clock at that many milliseconds, and being handed back an id to
    /// know it by. Both standards ask for this in different words and it is the same clock.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapHost*, uint, uint*, byte> RegisterTimer;

    /// <summary>Giving one back.</summary>
    public delegate* unmanaged[Cdecl]<ClapHost*, uint, byte> UnregisterTimer;
}

/// <summary>What the host offers a plugin about files it wants watching.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapHostPosixFd
{
    /// <summary>
    /// The plugin asking the host to watch a file descriptor for those reasons. It is its X11
    /// connection, and without this the plugin's window never draws.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapHost*, int, uint, byte> RegisterFd;

    /// <summary>Changing what the host is watching a descriptor for.</summary>
    public delegate* unmanaged[Cdecl]<ClapHost*, int, uint, byte> ModifyFd;

    /// <summary>Asking the host to stop watching one.</summary>
    public delegate* unmanaged[Cdecl]<ClapHost*, int, byte> UnregisterFd;
}

/// <summary>What a plugin keeps beyond its knobs, written to and read from a host stream.</summary>
/// <remarks>
/// Both are main thread calls, and both answer false for a plugin that could not do it, which
/// is not the same as one that had nothing to say.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapPluginState
{
    /// <summary>The plugin writing everything about itself into a stream the host owns.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, ClapOutputStream*, byte> Save;

    /// <summary>The plugin reading it back. Every parameter may move as a result.</summary>
    public delegate* unmanaged[Cdecl]<ClapPlugin*, ClapInputStream*, byte> Load;
}

/// <summary>
/// Somewhere for a plugin to read its own state from. The host owns both the memory and the
/// function.
/// </summary>
/// <remarks>
/// Nought back means there is no more, which is how a plugin knows it has the whole lump; a
/// negative number is a failure. A plugin is entitled to ask for it in as many pieces as it
/// likes and several do.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapInputStream
{
    /// <summary>Whatever the host wants to find the real stream with. Never read by the plugin.</summary>
    public void* Context;

    /// <summary>
    /// Fills a buffer up to that many bytes, and answers how many it really put there. Nought is
    /// the end and a negative number is a failure.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapInputStream*, void*, ulong, long> Read;
}

/// <summary>Somewhere for a plugin to write its own state to.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapOutputStream
{
    /// <summary>Whatever the host wants to find the real stream with.</summary>
    public void* Context;

    /// <summary>
    /// Takes up to that many bytes and answers how many it took. A short write is allowed and
    /// the plugin is expected to come round again with the rest.
    /// </summary>
    public delegate* unmanaged[Cdecl]<ClapOutputStream*, void*, ulong, long> Write;
}
