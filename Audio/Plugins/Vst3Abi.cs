using System;
using System.Runtime.InteropServices;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// The VST3 binary interface, as far as hosting an effect needs it.
/// </summary>
/// <remarks>
/// VST3 is C++ rather than C: every interface is a class of pure virtual methods, which on
/// every compiler that matters is an object whose first word points at a table of function
/// pointers. So each interface here is two declarations, a table of the methods in the order
/// the header declares them, and an object holding a pointer to that table. Getting the order
/// wrong calls the wrong function with the wrong arguments, so the tables are written straight
/// down the header and not rearranged.
///
/// Structs are left at their natural alignment. The SDK pushes pack(16) on 64-bit Linux and
/// Windows, which is a ceiling rather than a floor, so nothing actually moves.
/// </remarks>
internal static class Vst3Abi
{
    /// <summary>What the platform calls to wake a module up, and to put it back to sleep.</summary>
    public static string EntrySymbol =>
        OperatingSystem.IsWindows() ? "InitDll" : OperatingSystem.IsMacOS() ? "bundleEntry" : "ModuleEntry";

    public static string ExitSymbol =>
        OperatingSystem.IsWindows() ? "ExitDll" : OperatingSystem.IsMacOS() ? "bundleExit" : "ModuleExit";

    public const string FactorySymbol = "GetPluginFactory";

    /// <summary>The only class category this host cares about: something that makes audio.</summary>
    public const string AudioModuleCategory = "Audio Module Class";

    public const int ResultOk = 0;
    public const int ResultTrue = 0;
    public const int ResultFalse = 1;

    /// <summary>
    /// Refusing an interface. Windows uses the COM value for this and everything else uses
    /// Steinberg's own, which is the sort of difference that only shows up as a plugin quietly
    /// deciding the host is broken.
    /// </summary>
    public static int NoInterface => OperatingSystem.IsWindows() ? unchecked((int)0x80004002) : unchecked((int)0x80000004);

    public static int NotImplemented => OperatingSystem.IsWindows() ? unchecked((int)0x80004001) : unchecked((int)0x80000001);

    // MediaTypes
    public const int MediaAudio = 0;
    public const int MediaEvent = 1;

    // BusDirections
    public const int DirectionInput = 0;
    public const int DirectionOutput = 1;

    // BusTypes
    public const int BusMain = 0;
    public const int BusAux = 1;

    // SymbolicSampleSizes
    public const int Sample32 = 0;

    // ProcessModes
    public const int RealtimeMode = 0;

    /// <summary>Left and right, which is the only arrangement this host asks for.</summary>
    public const ulong StereoArrangement = 3;

    // ParameterInfo::ParameterFlags
    public const int CanAutomate = 1 << 0;
    public const int ReadOnlyFlag = 1 << 1;
    public const int ListFlag = 1 << 3;
    public const int HiddenFlag = 1 << 4;
    public const int ProgramChangeFlag = 1 << 15;
    public const int BypassFlag = 1 << 16;

    public const int NameSize = 64;
    public const int CategorySize = 32;
    public const int SubCategoriesSize = 128;
    public const int VendorSize = 64;
    public const int VersionSize = 64;
    public const int UrlSize = 256;
    public const int EmailSize = 128;

    /// <summary>String128 is 128 UTF-16 characters, which is 256 bytes of struct.</summary>
    public const int String128Bytes = 256;

    public const int UidBytes = 16;

    /// <summary>
    /// Builds one of the SDK's interface ids from the four words the header spells it with.
    /// </summary>
    /// <remarks>
    /// Windows lays the first three words out the way COM does, little-endian in groups, and
    /// every other platform lays all four out big-endian. Same id, different bytes, and a
    /// plugin compares the bytes.
    /// </remarks>
    public static byte[] Uid(uint l1, uint l2, uint l3, uint l4)
    {
        var uid = new byte[UidBytes];

        if (OperatingSystem.IsWindows())
        {
            uid[0] = (byte)(l1 & 0xFF);
            uid[1] = (byte)((l1 >> 8) & 0xFF);
            uid[2] = (byte)((l1 >> 16) & 0xFF);
            uid[3] = (byte)((l1 >> 24) & 0xFF);
            uid[4] = (byte)((l2 >> 16) & 0xFF);
            uid[5] = (byte)((l2 >> 24) & 0xFF);
            uid[6] = (byte)(l2 & 0xFF);
            uid[7] = (byte)((l2 >> 8) & 0xFF);
        }
        else
        {
            uid[0] = (byte)((l1 >> 24) & 0xFF);
            uid[1] = (byte)((l1 >> 16) & 0xFF);
            uid[2] = (byte)((l1 >> 8) & 0xFF);
            uid[3] = (byte)(l1 & 0xFF);
            uid[4] = (byte)((l2 >> 24) & 0xFF);
            uid[5] = (byte)((l2 >> 16) & 0xFF);
            uid[6] = (byte)((l2 >> 8) & 0xFF);
            uid[7] = (byte)(l2 & 0xFF);
        }

        uid[8] = (byte)((l3 >> 24) & 0xFF);
        uid[9] = (byte)((l3 >> 16) & 0xFF);
        uid[10] = (byte)((l3 >> 8) & 0xFF);
        uid[11] = (byte)(l3 & 0xFF);
        uid[12] = (byte)((l4 >> 24) & 0xFF);
        uid[13] = (byte)((l4 >> 16) & 0xFF);
        uid[14] = (byte)((l4 >> 8) & 0xFF);
        uid[15] = (byte)(l4 & 0xFF);

        return uid;
    }

    public static readonly byte[] FUnknownId = Uid(0x00000000, 0x00000000, 0xC0000000, 0x00000046);
    public static readonly byte[] PluginBaseId = Uid(0x22888DDB, 0x156E45AE, 0x8358B348, 0x08190625);
    public static readonly byte[] PluginFactoryId = Uid(0x7A4D811C, 0x52114A1F, 0xAED9D2EE, 0x0B43BF9F);
    public static readonly byte[] PluginFactory2Id = Uid(0x0007B650, 0xF24B4C0B, 0xA464EDB9, 0xF00B2ABB);
    public static readonly byte[] PluginFactory3Id = Uid(0x4555A2AB, 0xC1234E57, 0x9B122910, 0x36878931);
    public static readonly byte[] ComponentId = Uid(0xE831FF31, 0xF2D54301, 0x928EBBEE, 0x25697802);
    public static readonly byte[] AudioProcessorId = Uid(0x42043F99, 0xB7DA453C, 0xA569E79D, 0x9AAEC33D);
    public static readonly byte[] EditControllerId = Uid(0xDCD7BBE3, 0x7742448D, 0xA874AACC, 0x979C759E);
    public static readonly byte[] ConnectionPointId = Uid(0x70A4156F, 0x6E6E4026, 0x989148BF, 0xAA60D8D1);
    public static readonly byte[] ComponentHandlerId = Uid(0x93A0BEA3, 0x0BD045DB, 0x8E890B0C, 0xC1E46AC6);
    public static readonly byte[] HostApplicationId = Uid(0x58E595CC, 0xDB2D4969, 0x8B6AAF8C, 0x36A664E5);
    public static readonly byte[] ParameterChangesId = Uid(0xA4779663, 0x0BB64A56, 0xB44384A8, 0x466FEB9D);
    public static readonly byte[] ParamValueQueueId = Uid(0x01263A18, 0xED074F6F, 0x98C9D356, 0x4686F9BA);
    public static readonly byte[] BStreamId = Uid(0xC3BF6EA2, 0x30994752, 0x9B6BF990, 0x1EE33E9B);

    // Steinberg::Linux. X11 has no run loop of its own, so the host has to be one.
    public static readonly byte[] RunLoopId = Uid(0x18C35366, 0x97764F1A, 0x9C5B8385, 0x7A871389);
    public static readonly byte[] TimerHandlerId = Uid(0x10BDD94F, 0x41424774, 0x821FAD8F, 0xECA72CA9);
    public static readonly byte[] EventHandlerId = Uid(0x561E65C9, 0x13A0496F, 0x813A2C35, 0x654D7983);

    /// <summary>True when two interface ids name the same interface.</summary>
    public static unsafe bool SameId(byte* left, byte[] right)
    {
        if (left == null) return false;

        for (int index = 0; index < UidBytes; index++)
        {
            if (left[index] != right[index]) return false;
        }

        return true;
    }

    /// <summary>
    /// A class id as text, the same hex a bundle's moduleinfo.json prints. This is what a saved
    /// chain stores, because it outlives a path.
    /// </summary>
    public static unsafe string HexId(byte* uid)
    {
        if (uid == null) return "";

        var text = new char[UidBytes * 2];

        for (int index = 0; index < UidBytes; index++)
        {
            byte value = uid[index];
            text[index * 2] = Nibble(value >> 4);
            text[index * 2 + 1] = Nibble(value & 0xF);
        }

        return new string(text);
    }

    private static char Nibble(int value) => (char)(value < 10 ? '0' + value : 'A' + (value - 10));

    /// <summary>Turns a class id back into the sixteen bytes the factory wants.</summary>
    public static byte[]? ParseHexId(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length != UidBytes * 2) return null;

        var uid = new byte[UidBytes];

        for (int index = 0; index < UidBytes; index++)
        {
            if (!byte.TryParse(text.AsSpan(index * 2, 2), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out uid[index]))
            {
                return null;
            }
        }

        return uid;
    }
}

/// <summary>Every VST3 interface starts here: ask for another face, count me in, count me out.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct FUnknownVtbl
{
    public delegate* unmanaged[Cdecl]<void*, byte*, void**, int> QueryInterface;
    public delegate* unmanaged[Cdecl]<void*, uint> AddRef;
    public delegate* unmanaged[Cdecl]<void*, uint> Release;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct FUnknown
{
    public FUnknownVtbl* Vtbl;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IPluginFactoryVtbl
{
    public FUnknownVtbl Base;

    public delegate* unmanaged[Cdecl]<void*, PFactoryInfo*, int> GetFactoryInfo;
    public delegate* unmanaged[Cdecl]<void*, int> CountClasses;
    public delegate* unmanaged[Cdecl]<void*, int, PClassInfo*, int> GetClassInfo;
    public delegate* unmanaged[Cdecl]<void*, byte*, byte*, void**, int> CreateInstance;

    // IPluginFactory2 adds this one. Reachable only after querying for that interface.
    public delegate* unmanaged[Cdecl]<void*, int, PClassInfo2*, int> GetClassInfo2;

    // IPluginFactory3 adds these two.
    public delegate* unmanaged[Cdecl]<void*, int, void*, int> GetClassInfoUnicode;
    public delegate* unmanaged[Cdecl]<void*, void*, int> SetHostContext;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IPluginFactory
{
    public IPluginFactoryVtbl* Vtbl;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct PFactoryInfo
{
    public fixed byte Vendor[Vst3Abi.NameSize];
    public fixed byte Url[Vst3Abi.UrlSize];
    public fixed byte Email[Vst3Abi.EmailSize];
    public int Flags;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct PClassInfo
{
    public fixed byte Cid[Vst3Abi.UidBytes];
    public int Cardinality;
    public fixed byte Category[Vst3Abi.CategorySize];
    public fixed byte Name[Vst3Abi.NameSize];
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct PClassInfo2
{
    public fixed byte Cid[Vst3Abi.UidBytes];
    public int Cardinality;
    public fixed byte Category[Vst3Abi.CategorySize];
    public fixed byte Name[Vst3Abi.NameSize];
    public uint ClassFlags;
    public fixed byte SubCategories[Vst3Abi.SubCategoriesSize];
    public fixed byte Vendor[Vst3Abi.VendorSize];
    public fixed byte Version[Vst3Abi.VersionSize];
    public fixed byte SdkVersion[Vst3Abi.VersionSize];
}

/// <summary>The audio half of a plugin: busses, state, and being switched on.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IComponentVtbl
{
    public FUnknownVtbl Base;

    // IPluginBase
    public delegate* unmanaged[Cdecl]<void*, void*, int> Initialize;
    public delegate* unmanaged[Cdecl]<void*, int> Terminate;

    // IComponent
    public delegate* unmanaged[Cdecl]<void*, byte*, int> GetControllerClassId;
    public delegate* unmanaged[Cdecl]<void*, int, int> SetIoMode;
    public delegate* unmanaged[Cdecl]<void*, int, int, int> GetBusCount;
    public delegate* unmanaged[Cdecl]<void*, int, int, int, BusInfo*, int> GetBusInfo;
    public delegate* unmanaged[Cdecl]<void*, void*, void*, int> GetRoutingInfo;
    public delegate* unmanaged[Cdecl]<void*, int, int, int, byte, int> ActivateBus;
    public delegate* unmanaged[Cdecl]<void*, byte, int> SetActive;
    public delegate* unmanaged[Cdecl]<void*, void*, int> SetState;
    public delegate* unmanaged[Cdecl]<void*, void*, int> GetState;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IComponent
{
    public IComponentVtbl* Vtbl;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct BusInfo
{
    public int MediaType;
    public int Direction;
    public int ChannelCount;
    public fixed byte Name[Vst3Abi.String128Bytes];
    public int BusType;
    public uint Flags;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IAudioProcessorVtbl
{
    public FUnknownVtbl Base;

    public delegate* unmanaged[Cdecl]<void*, ulong*, int, ulong*, int, int> SetBusArrangements;
    public delegate* unmanaged[Cdecl]<void*, int, int, ulong*, int> GetBusArrangement;
    public delegate* unmanaged[Cdecl]<void*, int, int> CanProcessSampleSize;
    public delegate* unmanaged[Cdecl]<void*, uint> GetLatencySamples;
    public delegate* unmanaged[Cdecl]<void*, ProcessSetup*, int> SetupProcessing;
    public delegate* unmanaged[Cdecl]<void*, byte, int> SetProcessing;
    public delegate* unmanaged[Cdecl]<void*, ProcessData*, int> Process;
    public delegate* unmanaged[Cdecl]<void*, uint> GetTailSamples;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IAudioProcessor
{
    public IAudioProcessorVtbl* Vtbl;
}

[StructLayout(LayoutKind.Sequential)]
internal struct ProcessSetup
{
    public int ProcessMode;
    public int SymbolicSampleSize;
    public int MaxSamplesPerBlock;
    public double SampleRate;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AudioBusBuffers
{
    public int NumChannels;
    public ulong SilenceFlags;
    public float** ChannelBuffers32;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ProcessData
{
    public int ProcessMode;
    public int SymbolicSampleSize;
    public int NumSamples;
    public int NumInputs;
    public int NumOutputs;
    public AudioBusBuffers* Inputs;
    public AudioBusBuffers* Outputs;
    public void* InputParameterChanges;
    public void* OutputParameterChanges;
    public void* InputEvents;
    public void* OutputEvents;
    public void* ProcessContext;
}

/// <summary>The settings half of a plugin: what the knobs are and where they stand.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IEditControllerVtbl
{
    public FUnknownVtbl Base;

    // IPluginBase
    public delegate* unmanaged[Cdecl]<void*, void*, int> Initialize;
    public delegate* unmanaged[Cdecl]<void*, int> Terminate;

    // IEditController
    public delegate* unmanaged[Cdecl]<void*, void*, int> SetComponentState;
    public delegate* unmanaged[Cdecl]<void*, void*, int> SetState;
    public delegate* unmanaged[Cdecl]<void*, void*, int> GetState;
    public delegate* unmanaged[Cdecl]<void*, int> GetParameterCount;
    public delegate* unmanaged[Cdecl]<void*, int, ParameterInfo*, int> GetParameterInfo;
    public delegate* unmanaged[Cdecl]<void*, uint, double, char*, int> GetParamStringByValue;
    public delegate* unmanaged[Cdecl]<void*, uint, char*, double*, int> GetParamValueByString;
    public delegate* unmanaged[Cdecl]<void*, uint, double, double> NormalizedParamToPlain;
    public delegate* unmanaged[Cdecl]<void*, uint, double, double> PlainParamToNormalized;
    public delegate* unmanaged[Cdecl]<void*, uint, double> GetParamNormalized;
    public delegate* unmanaged[Cdecl]<void*, uint, double, int> SetParamNormalized;
    public delegate* unmanaged[Cdecl]<void*, void*, int> SetComponentHandler;
    public delegate* unmanaged[Cdecl]<void*, byte*, void*> CreateView;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IEditController
{
    public IEditControllerVtbl* Vtbl;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ParameterInfo
{
    public uint Id;
    public fixed byte Title[Vst3Abi.String128Bytes];
    public fixed byte ShortTitle[Vst3Abi.String128Bytes];
    public fixed byte Units[Vst3Abi.String128Bytes];
    public int StepCount;
    public double DefaultNormalizedValue;
    public int UnitId;
    public int Flags;
}

/// <summary>How the two halves of a plugin talk to each other.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IConnectionPointVtbl
{
    public FUnknownVtbl Base;

    public delegate* unmanaged[Cdecl]<void*, void*, int> Connect;
    public delegate* unmanaged[Cdecl]<void*, void*, int> Disconnect;
    public delegate* unmanaged[Cdecl]<void*, void*, int> Notify;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IConnectionPoint
{
    public IConnectionPointVtbl* Vtbl;
}
