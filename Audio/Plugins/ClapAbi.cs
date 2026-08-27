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
/// </remarks>
internal static class ClapAbi
{
    /// <summary>The symbol every .clap file exports.</summary>
    public const string EntrySymbol = "clap_entry";

    public const string PluginFactoryId = "clap.plugin-factory";

    public const string ParamsExtension = "clap.params";

    public const string AudioPortsExtension = "clap.audio-ports";

    /// <summary>The plugin's own window, when it has one.</summary>
    public const string GuiExtension = "clap.gui";

    /// <summary>
    /// Everything the plugin keeps that its parameters do not describe. Its preset, in
    /// practice, and for anything with wavetables or samples inside it that is most of what
    /// somebody set up.
    /// </summary>
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

    public const int PathSize = 1024;

    /// <summary>Event space and type numbers, for a parameter move sent into a plugin.</summary>
    public const ushort CoreEventSpace = 0;

    public const ushort ParamValueEvent = 5;
}

[StructLayout(LayoutKind.Sequential)]
internal struct ClapVersion
{
    public uint Major;
    public uint Minor;
    public uint Revision;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapPluginEntry
{
    public ClapVersion Version;
    public delegate* unmanaged[Cdecl]<byte*, byte> Init;
    public delegate* unmanaged[Cdecl]<void> Deinit;
    public delegate* unmanaged[Cdecl]<byte*, void*> GetFactory;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapPluginFactory
{
    public delegate* unmanaged[Cdecl]<ClapPluginFactory*, uint> Count;
    public delegate* unmanaged[Cdecl]<ClapPluginFactory*, uint, ClapPluginDescriptor*> GetDescriptor;
    public delegate* unmanaged[Cdecl]<ClapPluginFactory*, ClapHost*, byte*, ClapPlugin*> Create;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapPluginDescriptor
{
    public ClapVersion Version;
    public byte* Id;
    public byte* Name;
    public byte* Vendor;
    public byte* Url;
    public byte* ManualUrl;
    public byte* SupportUrl;
    public byte* PluginVersion;
    public byte* Description;
    public byte** Features;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapHost
{
    public ClapVersion Version;
    public void* HostData;
    public byte* Name;
    public byte* Vendor;
    public byte* Url;
    public byte* HostVersion;
    public delegate* unmanaged[Cdecl]<ClapHost*, byte*, void*> GetExtension;
    public delegate* unmanaged[Cdecl]<ClapHost*, void> RequestRestart;
    public delegate* unmanaged[Cdecl]<ClapHost*, void> RequestProcess;
    public delegate* unmanaged[Cdecl]<ClapHost*, void> RequestCallback;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapPlugin
{
    public ClapPluginDescriptor* Descriptor;
    public void* PluginData;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte> Init;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, void> Destroy;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, double, uint, uint, byte> Activate;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, void> Deactivate;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte> StartProcessing;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, void> StopProcessing;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, void> Reset;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, ClapProcess*, int> Process;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte*, void*> GetExtension;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, void> OnMainThread;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapAudioBuffer
{
    /// <summary>One pointer per channel. CLAP is deinterleaved; our busses are not.</summary>
    public float** Data32;

    public double** Data64;
    public uint ChannelCount;
    public uint Latency;
    public ulong ConstantMask;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapProcess
{
    public long SteadyTime;
    public uint FramesCount;
    public void* Transport;
    public ClapAudioBuffer* AudioInputs;
    public ClapAudioBuffer* AudioOutputs;
    public uint AudioInputsCount;
    public uint AudioOutputsCount;
    public ClapInputEvents* InEvents;
    public ClapOutputEvents* OutEvents;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapInputEvents
{
    public void* Context;
    public delegate* unmanaged[Cdecl]<ClapInputEvents*, uint> Size;
    public delegate* unmanaged[Cdecl]<ClapInputEvents*, uint, ClapEventHeader*> Get;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapOutputEvents
{
    public void* Context;
    public delegate* unmanaged[Cdecl]<ClapOutputEvents*, ClapEventHeader*, byte> TryPush;
}

[StructLayout(LayoutKind.Sequential)]
internal struct ClapEventHeader
{
    public uint Size;
    public uint Time;
    public ushort SpaceId;
    public ushort Type;
    public uint Flags;
}

/// <summary>A parameter move, which is how a knob reaches a plugin: through the event queue.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ClapEventParamValue
{
    public ClapEventHeader Header;
    public uint ParamId;
    public nint Cookie;
    public short PortIndex;
    public short Channel;
    public short Key;
    public int NoteId;
    public double Value;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapPluginParams
{
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint> Count;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint, ClapParamInfo*, byte> GetInfo;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint, double*, byte> GetValue;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint, double, byte*, uint, byte> ValueToText;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte*, uint, double*, byte> TextToValue;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, ClapInputEvents*, ClapOutputEvents*, void> Flush;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapParamInfo
{
    public uint Id;
    public uint Flags;
    public void* Cookie;
    public fixed byte Name[ClapAbi.NameSize];
    public fixed byte Module[ClapAbi.PathSize];
    public double MinValue;
    public double MaxValue;
    public double DefaultValue;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapPluginAudioPorts
{
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte, uint> Count;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint, byte, ClapAudioPortInfo*, byte> Get;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapAudioPortInfo
{
    public uint Id;
    public fixed byte Name[ClapAbi.NameSize];
    public uint Flags;
    public uint ChannelCount;
    public byte* PortType;
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
    public byte* Api;
    public nint Handle;
}

/// <summary>The plugin's window, as the plugin offers it.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapPluginGui
{
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte*, byte, byte> IsApiSupported;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte**, byte*, byte> GetPreferredApi;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte*, byte, byte> Create;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, void> Destroy;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, double, byte> SetScale;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint*, uint*, byte> GetSize;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte> CanResize;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, void*, byte> GetResizeHints;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint*, uint*, byte> AdjustSize;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint, uint, byte> SetSize;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, ClapWindow*, byte> SetParent;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, ClapWindow*, byte> SetTransient;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte*, void> SuggestTitle;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte> Show;
    public delegate* unmanaged[Cdecl]<ClapPlugin*, byte> Hide;
}

/// <summary>The plugin's end of the clock: it is told which of its timers came round.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapPluginTimerSupport
{
    public delegate* unmanaged[Cdecl]<ClapPlugin*, uint, void> OnTimer;
}

/// <summary>The plugin's end of the doorbell: it is told a file has something on it.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapPluginPosixFd
{
    public delegate* unmanaged[Cdecl]<ClapPlugin*, int, uint, void> OnFd;
}

/// <summary>What the host offers a plugin about its window.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapHostGui
{
    public delegate* unmanaged[Cdecl]<ClapHost*, void> ResizeHintsChanged;
    public delegate* unmanaged[Cdecl]<ClapHost*, uint, uint, byte> RequestResize;
    public delegate* unmanaged[Cdecl]<ClapHost*, byte> RequestShow;
    public delegate* unmanaged[Cdecl]<ClapHost*, byte> RequestHide;
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
    public delegate* unmanaged[Cdecl]<ClapHost*, uint, void> Rescan;
    public delegate* unmanaged[Cdecl]<ClapHost*, uint, uint, void> Clear;
    public delegate* unmanaged[Cdecl]<ClapHost*, void> RequestFlush;
}

/// <summary>What the host offers a plugin about timers.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapHostTimerSupport
{
    public delegate* unmanaged[Cdecl]<ClapHost*, uint, uint*, byte> RegisterTimer;
    public delegate* unmanaged[Cdecl]<ClapHost*, uint, byte> UnregisterTimer;
}

/// <summary>What the host offers a plugin about files it wants watching.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapHostPosixFd
{
    public delegate* unmanaged[Cdecl]<ClapHost*, int, uint, byte> RegisterFd;
    public delegate* unmanaged[Cdecl]<ClapHost*, int, uint, byte> ModifyFd;
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
    public delegate* unmanaged[Cdecl]<ClapPlugin*, ClapOutputStream*, byte> Save;
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
    public void* Context;
    public delegate* unmanaged[Cdecl]<ClapInputStream*, void*, ulong, long> Read;
}

/// <summary>Somewhere for a plugin to write its own state to.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ClapOutputStream
{
    public void* Context;
    public delegate* unmanaged[Cdecl]<ClapOutputStream*, void*, ulong, long> Write;
}
