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
