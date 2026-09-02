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

    /// <inheritdoc cref="EntrySymbol"/>
    public static string ExitSymbol =>
        OperatingSystem.IsWindows() ? "ExitDll" : OperatingSystem.IsMacOS() ? "bundleExit" : "ModuleExit";

    /// <summary>
    /// The one symbol whose name is the same on every platform: the factory that lists what is
    /// inside the bundle.
    /// </summary>
    public const string FactorySymbol = "GetPluginFactory";

    /// <summary>The only class category this host cares about: something that makes audio.</summary>
    public const string AudioModuleCategory = "Audio Module Class";

    /// <summary>Nought, which is what a VST3 call answers when it did what it was asked.</summary>
    public const int ResultOk = 0;

    /// <summary>The same nought, spelled the way a call answering a yes or no question spells it.</summary>
    public const int ResultTrue = 0;

    /// <summary>
    /// One, which is no. The awkward part of this ABI: false is not nought, so a result can
    /// never be tested for truth by testing it against nought.
    /// </summary>
    public const int ResultFalse = 1;

    /// <summary>
    /// Refusing an interface. Windows uses the COM value for this and everything else uses
    /// Steinberg's own, which is the sort of difference that only shows up as a plugin quietly
    /// deciding the host is broken.
    /// </summary>
    public static int NoInterface => OperatingSystem.IsWindows() ? unchecked((int)0x80004002) : unchecked((int)0x80000004);

    /// <summary>
    /// Refusing a call outright, split across platforms the same way and for the same reason as
    /// <see cref="NoInterface"/>.
    /// </summary>
    public static int NotImplemented => OperatingSystem.IsWindows() ? unchecked((int)0x80004001) : unchecked((int)0x80000001);

    /// <summary>A bus carrying audio. One of Steinberg's MediaTypes.</summary>
    public const int MediaAudio = 0;

    /// <summary>A bus carrying notes and other events rather than samples.</summary>
    public const int MediaEvent = 1;

    /// <summary>A bus coming into the plugin. One of Steinberg's BusDirections.</summary>
    public const int DirectionInput = 0;

    /// <summary>A bus leaving it.</summary>
    public const int DirectionOutput = 1;

    /// <summary>The plugin's main bus, which is the only one this host connects. A BusType.</summary>
    public const int BusMain = 0;

    /// <summary>A side chain or other extra bus, left inactive here.</summary>
    public const int BusAux = 1;

    /// <summary>
    /// Single precision audio, which is what this host offers and what every plugin supports.
    /// One of Steinberg's SymbolicSampleSizes.
    /// </summary>
    public const int Sample32 = 0;

    /// <summary>A note starting. One of Event::EventTypes.</summary>
    public const ushort NoteOnEvent = 0;

    /// <summary>A note ending.</summary>
    public const ushort NoteOffEvent = 1;

    /// <summary>Event::kIsLive: played by hand rather than read off a timeline.</summary>
    public const ushort LiveEvent = 1 << 0;

    /// <summary>No note identity of its own, which is how a plain keyboard plays.</summary>
    public const int NoNoteId = -1;

    /// <summary>What a plugin's window is called on each platform.</summary>
    public static string PlatformWindowType =>
        OperatingSystem.IsWindows() ? "HWND" : OperatingSystem.IsMacOS() ? "NSView" : "X11EmbedWindowID";

    /// <summary>The name of the one view every plugin with an interface offers.</summary>
    public const string EditorView = "editor";

    /// <summary>
    /// Playing rather than rendering to a file, which is the only mode this host ever asks for.
    /// One of Steinberg's ProcessModes.
    /// </summary>
    public const int RealtimeMode = 0;

    /// <summary>Left and right, which is the only arrangement this host asks for.</summary>
    public const ulong StereoArrangement = 3;

    /// <summary>
    /// The parameter may be driven by the host. One of ParameterInfo::ParameterFlags, and the
    /// only one of them a plugin is really obliged to set honestly.
    /// </summary>
    public const int CanAutomate = 1 << 0;

    /// <summary>
    /// A reading rather than a control, such as a compressor's gain reduction. Excluded from the
    /// parameters polled back off a plugin with its window open: included, a song could never
    /// settle and so could never be saved.
    /// </summary>
    public const int ReadOnlyFlag = 1 << 1;

    /// <summary>Whole named positions rather than a sweep.</summary>
    public const int ListFlag = 1 << 3;

    /// <summary>The plugin asking that this one is not drawn.</summary>
    public const int HiddenFlag = 1 << 4;

    /// <summary>
    /// The parameter that picks the plugin's own preset. Moving it reloads a patch, which is why
    /// it is not treated as an ordinary knob.
    /// </summary>
    public const int ProgramChangeFlag = 1 << 15;

    /// <summary>The parameter the standard reserves for switching the plugin out of circuit.</summary>
    public const int BypassFlag = 1 << 16;

    /// <summary>The width of a plain ASCII name field in the ABI.</summary>
    public const int NameSize = 64;

    /// <summary>The width of a class's category field, which holds <see cref="AudioModuleCategory"/>.</summary>
    public const int CategorySize = 32;

    /// <summary>
    /// The width of the field holding a class's subcategories, which is the pipe separated list
    /// an instrument is told apart from an effect by.
    /// </summary>
    public const int SubCategoriesSize = 128;

    /// <summary>The width of a vendor field.</summary>
    public const int VendorSize = 64;

    /// <summary>The width of a version field, used for both the plugin's and the SDK's.</summary>
    public const int VersionSize = 64;

    /// <summary>The width of a URL field.</summary>
    public const int UrlSize = 256;

    /// <summary>The width of an email field.</summary>
    public const int EmailSize = 128;

    /// <summary>String128 is 128 UTF-16 characters, which is 256 bytes of struct.</summary>
    public const int String128Bytes = 256;

    /// <summary>How many bytes an interface id or a class id is.</summary>
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

    /// <summary>The root of every VST3 interface, which everything can be asked for.</summary>
    public static readonly byte[] FUnknownId = Uid(0x00000000, 0x00000000, 0xC0000000, 0x00000046);

    /// <summary>Being set up and taken down, which the two halves of a plugin share.</summary>
    public static readonly byte[] PluginBaseId = Uid(0x22888DDB, 0x156E45AE, 0x8358B348, 0x08190625);

    /// <summary>What lists the classes in a bundle. The first version, which every bundle has.</summary>
    public static readonly byte[] PluginFactoryId = Uid(0x7A4D811C, 0x52114A1F, 0xAED9D2EE, 0x0B43BF9F);

    /// <summary>
    /// The second version, which adds the vendor, the version and the subcategories. Asked for
    /// because that is where a plugin says whether it is an instrument.
    /// </summary>
    public static readonly byte[] PluginFactory2Id = Uid(0x0007B650, 0xF24B4C0B, 0xA464EDB9, 0xF00B2ABB);

    /// <summary>The third, which adds a host context and Unicode class names.</summary>
    public static readonly byte[] PluginFactory3Id = Uid(0x4555A2AB, 0xC1234E57, 0x9B122910, 0x36878931);

    /// <summary>The audio half of a plugin: busses, state, and being switched on.</summary>
    public static readonly byte[] ComponentId = Uid(0xE831FF31, 0xF2D54301, 0x928EBBEE, 0x25697802);

    /// <summary>The face of that half which actually renders a block.</summary>
    public static readonly byte[] AudioProcessorId = Uid(0x42043F99, 0xB7DA453C, 0xA569E79D, 0x9AAEC33D);

    /// <summary>The settings half: what the knobs are and where they stand.</summary>
    public static readonly byte[] EditControllerId = Uid(0xDCD7BBE3, 0x7742448D, 0xA874AACC, 0x979C759E);

    /// <summary>
    /// The door the two halves are wired together through. A host that does not wire them leaves
    /// a plugin whose window and whose sound know nothing about each other.
    /// </summary>
    public static readonly byte[] ConnectionPointId = Uid(0x70A4156F, 0x6E6E4026, 0x989148BF, 0xAA60D8D1);

    /// <summary>One thing a plugin's two halves say to each other.</summary>
    public static readonly byte[] MessageId = Uid(0x936F033B, 0xC6C047DB, 0xBB0882F8, 0x13C1E613);

    /// <summary>What is written on a message: named values of a few kinds.</summary>
    public static readonly byte[] AttributeListId = Uid(0x1E5F0AEB, 0xCC7F4533, 0xA2544011, 0x38AD5EE4);

    /// <summary>
    /// How a plugin tells the host a knob moved in its own window. Without one the plugin's
    /// interface is a picture: nothing the person does in it ever reaches the host.
    /// </summary>
    public static readonly byte[] ComponentHandlerId = Uid(0x93A0BEA3, 0x0BD045DB, 0x8E890B0C, 0xC1E46AC6);

    /// <summary>
    /// The host itself, which a plugin asks for a name and, more importantly, for somewhere to
    /// get a message envelope from.
    /// </summary>
    public static readonly byte[] HostApplicationId = Uid(0x58E595CC, 0xDB2D4969, 0x8B6AAF8C, 0x36A664E5);

    /// <summary>The parameter moves handed to a plugin at the start of a block.</summary>
    public static readonly byte[] ParameterChangesId = Uid(0xA4779663, 0x0BB64A56, 0xB44384A8, 0x466FEB9D);

    /// <summary>One parameter's worth of those moves, as points in time across the block.</summary>
    public static readonly byte[] ParamValueQueueId = Uid(0x01263A18, 0xED074F6F, 0x98C9D356, 0x4686F9BA);

    /// <summary>A stream, which is how a patch is handed over in both directions.</summary>
    public static readonly byte[] BStreamId = Uid(0xC3BF6EA2, 0x30994752, 0x9B6BF990, 0x1EE33E9B);

    /// <summary>
    /// A plugin's own window on Linux. X11 has no run loop of its own, so the host has to be
    /// one, which is what the three ids below are for.
    /// </summary>
    public static readonly byte[] PlugViewId = Uid(0x5BC32507, 0xD06049EA, 0xA6151B52, 0x2B755B29);

    /// <summary>The host's side of that window, which is where a resize request arrives.</summary>
    public static readonly byte[] PlugFrameId = Uid(0x367FAF01, 0xAFA94693, 0x8D4DA2A0, 0xED0882A3);

    /// <summary>
    /// The view's own face for being told how much the screen is scaled by.
    /// </summary>
    /// <remarks>
    /// Windows scales by telling each program a number rather than by giving it more pixels, so
    /// a plugin drawing its own interface has no way of knowing that 100 by 100 means 150 by 150
    /// on this display. The host has to say, and Steinberg's own guidance is to say it before
    /// the view is given a window.
    ///
    /// A plugin built on somebody else's toolkit usually reads the scaling itself and does not
    /// care; one that draws its own, as Arturia's whole range does, believes what it is told and
    /// nothing else. Told nothing, it lays out at a size that has no relation to the window it
    /// was handed, and what you get is a window that is up, active, answering the mouse, and
    /// blank.
    ///
    /// Optional, as the specification has it: a view that does not offer this face is a view
    /// that works the number out for itself, and the answer is to leave it alone.
    /// </remarks>
    public static readonly byte[] PlugViewContentScaleId = Uid(0x65ED9690, 0x8AC44525, 0x8AADEF7A, 0x72EA703F);

    /// <summary>The notes handed to a plugin at the start of a block.</summary>
    public static readonly byte[] EventListId = Uid(0x3A2C4214, 0x346349FE, 0xB2C4F397, 0xB9695A44);

    /// <summary>
    /// The clock and the doorbell, asked for by name off the host's frame. The same thing CLAP
    /// asks for as two separate extensions.
    /// </summary>
    public static readonly byte[] RunLoopId = Uid(0x18C35366, 0x97764F1A, 0x9C5B8385, 0x7A871389);

    /// <summary>What the run loop calls when a timer comes round.</summary>
    public static readonly byte[] TimerHandlerId = Uid(0x10BDD94F, 0x41424774, 0x821FAD8F, 0xECA72CA9);

    /// <summary>What the run loop calls when a watched file descriptor has something on it.</summary>
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

    /// <summary>
    /// One hex digit, upper case, because moduleinfo.json prints them that way and the two
    /// spellings have to compare equal.
    /// </summary>
    private static char Nibble(int value) => (char)(value < 10 ? '0' + value : 'A' + (value - 10));

    /// <summary>Turns a class id back into the sixteen bytes the factory wants.</summary>
    /// <returns>
    /// Null for anything that is not exactly thirty-two hex digits, since a saved chain can hold
    /// text from an older format or from a hand edit, and a wrong id handed to a factory is a
    /// call into somebody else's code with rubbish in it.
    /// </returns>
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
    /// <summary>
    /// Asking an object for another of its faces by interface id, and being handed a pointer to
    /// it. The reference count is already raised on whatever comes back, so it has to be
    /// released.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, byte*, void**, int> QueryInterface;

    /// <summary>One more holder. Answers the new count, which is for debugging and nothing else.</summary>
    public delegate* unmanaged[Cdecl]<void*, uint> AddRef;

    /// <summary>
    /// One fewer. The object frees itself when the count reaches nought, so the pointer is dead
    /// from that moment and nothing may touch it again.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, uint> Release;
}

/// <summary>An object of the root interface: one word, pointing at its table.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct FUnknown
{
    /// <summary>The table of function pointers. Always the first word of a C++ object.</summary>
    public FUnknownVtbl* Vtbl;
}

/// <summary>
/// What lists the classes in a bundle, with the second and third versions' methods after it.
/// </summary>
/// <remarks>
/// The three versions are written as one table because that is how C++ single inheritance lays
/// them out: a factory that implements IPluginFactory3 has all seven entries in this order. The
/// last three may only be called after querying for the interface that adds them, since a
/// factory that stops at the first version has a shorter table and calling past its end is a
/// jump into whatever happened to be next in memory.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IPluginFactoryVtbl
{
    /// <summary>The root's three, which every table starts with.</summary>
    public FUnknownVtbl Base;

    /// <summary>Who made the bundle.</summary>
    public delegate* unmanaged[Cdecl]<void*, PFactoryInfo*, int> GetFactoryInfo;

    /// <summary>How many classes it holds, of every category and not only audio ones.</summary>
    public delegate* unmanaged[Cdecl]<void*, int> CountClasses;

    /// <summary>What the class at that index is: its id, its category and its name.</summary>
    public delegate* unmanaged[Cdecl]<void*, int, PClassInfo*, int> GetClassInfo;

    /// <summary>
    /// Makes one, by class id and by the interface id wanted back. This is where a component
    /// and a controller both come from.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, byte*, byte*, void**, int> CreateInstance;

    /// <summary>
    /// The richer class description IPluginFactory2 adds, which carries the vendor, the version
    /// and the subcategories. Reachable only after querying for that interface.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, int, PClassInfo2*, int> GetClassInfo2;

    /// <summary>
    /// The same again with wide strings, added by IPluginFactory3. Not read here, which is why
    /// the struct pointer is untyped.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, int, void*, int> GetClassInfoUnicode;

    /// <summary>
    /// Handing the factory the host, also IPluginFactory3. Some plugins ask the host for a
    /// message envelope during creation and have nowhere to ask without this.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, void*, int> SetHostContext;
}

/// <summary>A factory object: one word, pointing at its table.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IPluginFactory
{
    /// <summary>The table of function pointers.</summary>
    public IPluginFactoryVtbl* Vtbl;
}

/// <summary>Who made a bundle, as the factory describes itself.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct PFactoryInfo
{
    /// <summary>The vendor's name, ASCII in a fixed field.</summary>
    public fixed byte Vendor[Vst3Abi.NameSize];

    /// <summary>The vendor's page.</summary>
    public fixed byte Url[Vst3Abi.UrlSize];

    /// <summary>Where to write to about it.</summary>
    public fixed byte Email[Vst3Abi.EmailSize];

    /// <summary>
    /// What the vendor claims about the bundle: whether it is signed, whether it is classic
    /// rather than component based. Not read here.
    /// </summary>
    public int Flags;
}

/// <summary>One class inside a bundle, as the first factory version describes it.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct PClassInfo
{
    /// <summary>The class id, which is what a saved chain writes down.</summary>
    public fixed byte Cid[Vst3Abi.UidBytes];

    /// <summary>
    /// How many of this class may exist at once. Nearly always "as many as you like", and not
    /// enforced here.
    /// </summary>
    public int Cardinality;

    /// <summary>
    /// What kind of class it is. Only <see cref="Vst3Abi.AudioModuleCategory"/> matters here:
    /// the rest are controllers and test classes a host does not create directly.
    /// </summary>
    public fixed byte Category[Vst3Abi.CategorySize];

    /// <summary>What the class calls itself, which is what a person reads in a list.</summary>
    public fixed byte Name[Vst3Abi.NameSize];
}

/// <summary>
/// The same class, as the second factory version describes it. The first four fields repeat
/// <see cref="PClassInfo"/> in the same order, because the struct is the older one with more
/// on the end.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct PClassInfo2
{
    /// <inheritdoc cref="PClassInfo.Cid"/>
    public fixed byte Cid[Vst3Abi.UidBytes];

    /// <inheritdoc cref="PClassInfo.Cardinality"/>
    public int Cardinality;

    /// <inheritdoc cref="PClassInfo.Category"/>
    public fixed byte Category[Vst3Abi.CategorySize];

    /// <inheritdoc cref="PClassInfo.Name"/>
    public fixed byte Name[Vst3Abi.NameSize];

    /// <summary>Whether the plugin is distributable and whether it can be used simply. Not read here.</summary>
    public uint ClassFlags;

    /// <summary>
    /// A pipe separated list of what the plugin is: "Instrument|Synth", "Fx|Dynamics". The only
    /// place a scan can learn that a plugin takes notes rather than audio.
    /// </summary>
    public fixed byte SubCategories[Vst3Abi.SubCategoriesSize];

    /// <summary>Who made this class, which may differ from who made the bundle.</summary>
    public fixed byte Vendor[Vst3Abi.VendorSize];

    /// <summary>The plugin's own version, in whatever form it likes.</summary>
    public fixed byte Version[Vst3Abi.VersionSize];

    /// <summary>Which SDK it was built against.</summary>
    public fixed byte SdkVersion[Vst3Abi.VersionSize];
}

/// <summary>The audio half of a plugin: busses, state, and being switched on.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IComponentVtbl
{
    /// <summary>The root's three.</summary>
    public FUnknownVtbl Base;

    /// <summary>
    /// IPluginBase: sets the half up, given the host. Whatever is handed over here is what the
    /// plugin asks for a message envelope from later, so a null costs the plugins that do not
    /// check.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, void*, int> Initialize;

    /// <summary>IPluginBase: takes it down again, before it is released.</summary>
    public delegate* unmanaged[Cdecl]<void*, int> Terminate;

    /// <summary>
    /// Which class the plugin's other half is, so the host can create it off the same factory.
    /// A plugin whose two halves are one object answers with a failure and that is legal.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, byte*, int> GetControllerClassId;

    /// <summary>What the host wants of the plugin's input and output layout. Not used here.</summary>
    public delegate* unmanaged[Cdecl]<void*, int, int> SetIoMode;

    /// <summary>How many busses of a media type in a direction.</summary>
    public delegate* unmanaged[Cdecl]<void*, int, int, int> GetBusCount;

    /// <summary>What the bus at that index is: channels, name, whether it is the main one.</summary>
    public delegate* unmanaged[Cdecl]<void*, int, int, int, BusInfo*, int> GetBusInfo;

    /// <summary>How a plugin's inputs feed its outputs internally. Not read here.</summary>
    public delegate* unmanaged[Cdecl]<void*, void*, void*, int> GetRoutingInfo;

    /// <summary>
    /// Switches one bus on or off. A bus nobody activates gets no memory and is not processed,
    /// which is how a side chain stays out of the way.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, int, int, int, byte, int> ActivateBus;

    /// <summary>
    /// Switches the whole half on. The plugin allocates here, so it happens away from the audio
    /// thread and everything about the setup has to be settled first.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, byte, int> SetActive;

    /// <summary>
    /// Pours a patch back into the audio half, from a stream. The other half is given the same
    /// bytes through its own SetComponentState.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, void*, int> SetState;

    /// <summary>
    /// Reads the patch out. This is the lump a song keeps, and for a plugin with wavetables
    /// inside it that lump is most of the song's size.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, void*, int> GetState;
}

/// <summary>The audio half as an object: one word, pointing at its table.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IComponent
{
    /// <summary>The table of function pointers.</summary>
    public IComponentVtbl* Vtbl;
}

/// <summary>One bus as the plugin describes it, before any audio has run.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct BusInfo
{
    /// <summary>Audio or events. See <see cref="Vst3Abi.MediaAudio"/>.</summary>
    public int MediaType;

    /// <summary>In or out. See <see cref="Vst3Abi.DirectionInput"/>.</summary>
    public int Direction;

    /// <summary>How many channels it carries. Two is what this host asks for.</summary>
    public int ChannelCount;

    /// <summary>What the plugin calls the bus, as UTF-16 in a fixed field.</summary>
    public fixed byte Name[Vst3Abi.String128Bytes];

    /// <summary>Main or auxiliary. See <see cref="Vst3Abi.BusMain"/>.</summary>
    public int BusType;

    /// <summary>Whether the bus is on by default, and whether it is a control voltage bus.</summary>
    public uint Flags;
}

/// <summary>The face of the audio half that actually renders a block.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IAudioProcessorVtbl
{
    /// <summary>The root's three.</summary>
    public FUnknownVtbl Base;

    /// <summary>
    /// The host saying what each bus is to carry, as a speaker arrangement per bus in each
    /// direction. A plugin may refuse and offer what it will take instead.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, ulong*, int, ulong*, int, int> SetBusArrangements;

    /// <summary>Asking what a bus ended up carrying, after the plugin had its say.</summary>
    public delegate* unmanaged[Cdecl]<void*, int, int, ulong*, int> GetBusArrangement;

    /// <summary>
    /// Whether the plugin will take 32 or 64 bit audio. Answered with the ABI's awkward true,
    /// which is nought.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, int, int> CanProcessSampleSize;

    /// <summary>How far behind the plugin puts the sound, in samples. Not compensated for here.</summary>
    public delegate* unmanaged[Cdecl]<void*, uint> GetLatencySamples;

    /// <summary>
    /// Rate, block size and mode, all settled before the half is switched on. A block bigger
    /// than the size promised here is not allowed.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, ProcessSetup*, int> SetupProcessing;

    /// <summary>Says that blocks are about to start arriving, or have stopped.</summary>
    public delegate* unmanaged[Cdecl]<void*, byte, int> SetProcessing;

    /// <summary>One block of audio, with its parameter moves and its notes. The audio thread only.</summary>
    public delegate* unmanaged[Cdecl]<void*, ProcessData*, int> Process;

    /// <summary>
    /// How long the plugin goes on making sound after its input stops, in samples. Why a chain
    /// has to go on being given blocks while nothing is playing.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, uint> GetTailSamples;
}

/// <summary>The renderer as an object: one word, pointing at its table.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IAudioProcessor
{
    /// <summary>The table of function pointers.</summary>
    public IAudioProcessorVtbl* Vtbl;
}

/// <summary>Everything settled before a plugin is switched on.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ProcessSetup
{
    /// <summary>Playing rather than rendering. See <see cref="Vst3Abi.RealtimeMode"/>.</summary>
    public int ProcessMode;

    /// <summary>32 or 64 bit audio. See <see cref="Vst3Abi.Sample32"/>.</summary>
    public int SymbolicSampleSize;

    /// <summary>The largest block the plugin will ever be handed. It allocates against this.</summary>
    public int MaxSamplesPerBlock;

    /// <summary>The sample rate, which everything time based inside the plugin is worked out from.</summary>
    public double SampleRate;
}

/// <summary>One bus for one block: how many channels, and where each one's samples are.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AudioBusBuffers
{
    /// <summary>How many channel pointers there are.</summary>
    public int NumChannels;

    /// <summary>
    /// One bit per channel, set when that channel is silent for the whole block. Left at nought,
    /// which claims nothing and is always safe.
    /// </summary>
    public ulong SilenceFlags;

    /// <summary>
    /// One pointer per channel. VST3 is deinterleaved and our busses are not, so a block is
    /// unpacked into these on the way in and packed back on the way out.
    /// </summary>
    public float** ChannelBuffers32;
}

/// <summary>Everything one call to <c>Process</c> is given.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ProcessData
{
    /// <summary>Playing rather than rendering, repeated from the setup.</summary>
    public int ProcessMode;

    /// <summary>32 or 64 bit audio, repeated from the setup.</summary>
    public int SymbolicSampleSize;

    /// <summary>How many frames this block is, which may be fewer than the largest promised.</summary>
    public int NumSamples;

    /// <summary>How many input busses are described.</summary>
    public int NumInputs;

    /// <summary>How many output busses are described.</summary>
    public int NumOutputs;

    /// <summary>The busses coming in.</summary>
    public AudioBusBuffers* Inputs;

    /// <summary>The busses going out.</summary>
    public AudioBusBuffers* Outputs;

    /// <summary>
    /// The parameter moves for this block, as an IParameterChanges. Where a knob turn reaches a
    /// plugin: there is no setter on the audio half at all.
    /// </summary>
    public void* InputParameterChanges;

    /// <summary>
    /// Somewhere for the plugin to put parameter moves of its own. Not read here: VST3 reports a
    /// knob moved in a plugin's own window at once, through the component handler, rather than
    /// waiting for the end of a block the way CLAP does.
    /// </summary>
    public void* OutputParameterChanges;

    /// <summary>The notes for this block, as an IEventList. Null for an effect.</summary>
    public void* InputEvents;

    /// <summary>Somewhere for the plugin to put notes of its own. Null here.</summary>
    public void* OutputEvents;

    /// <summary>
    /// Where the song is and how fast. Null here: nothing in this application tells a plugin
    /// about the tracker's clock yet.
    /// </summary>
    public void* ProcessContext;
}

/// <summary>The settings half of a plugin: what the knobs are and where they stand.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IEditControllerVtbl
{
    /// <summary>The root's three.</summary>
    public FUnknownVtbl Base;

    /// <summary>IPluginBase: sets the half up, given the host.</summary>
    public delegate* unmanaged[Cdecl]<void*, void*, int> Initialize;

    /// <summary>IPluginBase: takes it down again.</summary>
    public delegate* unmanaged[Cdecl]<void*, int> Terminate;

    /// <summary>
    /// Hands this half the audio half's patch, so the knobs agree with the sound. The same bytes
    /// the component was given, read from the start again.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, void*, int> SetComponentState;

    /// <summary>This half's own state, which is window size and such rather than sound.</summary>
    public delegate* unmanaged[Cdecl]<void*, void*, int> SetState;

    /// <inheritdoc cref="SetState"/>
    public delegate* unmanaged[Cdecl]<void*, void*, int> GetState;

    /// <summary>How many parameters there are.</summary>
    public delegate* unmanaged[Cdecl]<void*, int> GetParameterCount;

    /// <summary>
    /// What the parameter at that index is. An index here, and an id in everything after, which
    /// is the trap: the two are not the same number and plenty of plugins scatter their ids.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, int, ParameterInfo*, int> GetParameterInfo;

    /// <summary>
    /// How the plugin words a value, into 128 wide characters. The only way to print a VST3
    /// parameter at all, since every one of them is nought to one whatever it means.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, uint, double, char*, int> GetParamStringByValue;

    /// <summary>The other way round, for a value somebody typed.</summary>
    public delegate* unmanaged[Cdecl]<void*, uint, char*, double*, int> GetParamValueByString;

    /// <summary>Nought to one into the parameter's real units.</summary>
    public delegate* unmanaged[Cdecl]<void*, uint, double, double> NormalizedParamToPlain;

    /// <summary>Real units back to nought to one.</summary>
    public delegate* unmanaged[Cdecl]<void*, uint, double, double> PlainParamToNormalized;

    /// <summary>Where a parameter stands, as this half believes it.</summary>
    public delegate* unmanaged[Cdecl]<void*, uint, double> GetParamNormalized;

    /// <summary>
    /// Moves it here. This half only: the audio half hears about it through the block's
    /// parameter changes, and a host that writes to one and not the other leaves a plugin whose
    /// window and whose sound disagree.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, uint, double, int> SetParamNormalized;

    /// <summary>
    /// Hands the plugin somewhere to report a knob it moved itself. Without one the plugin's own
    /// window is a picture the host learns nothing from.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, void*, int> SetComponentHandler;

    /// <summary>
    /// Makes the plugin's own window, by name. The only name asked for is
    /// <see cref="Vst3Abi.EditorView"/>, and null back means the plugin has no picture and gets
    /// the host's knobs.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, byte*, void*> CreateView;
}

/// <summary>The settings half as an object: one word, pointing at its table.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IEditController
{
    /// <summary>The table of function pointers.</summary>
    public IEditControllerVtbl* Vtbl;
}

/// <summary>One parameter as the settings half describes it.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ParameterInfo
{
    /// <summary>
    /// The number the plugin knows this parameter by, and what everything else names it with.
    /// Not the index it was found at.
    /// </summary>
    public uint Id;

    /// <summary>The name, as UTF-16 in a fixed field.</summary>
    public fixed byte Title[Vst3Abi.String128Bytes];

    /// <summary>A shorter name, for a display with seven characters on it.</summary>
    public fixed byte ShortTitle[Vst3Abi.String128Bytes];

    /// <summary>What the units are called, empty for the many plugins that do not say.</summary>
    public fixed byte Units[Vst3Abi.String128Bytes];

    /// <summary>
    /// How many gaps there are between the positions, nought for a continuous sweep. One means
    /// two positions, which is a switch.
    /// </summary>
    public int StepCount;

    /// <summary>Where the parameter sits before anybody touches it, nought to one like every value here.</summary>
    public double DefaultNormalizedValue;

    /// <summary>Which of the plugin's own groups it belongs to. Not used here.</summary>
    public int UnitId;

    /// <summary>
    /// What kind of parameter it is, as a mask. See <see cref="Vst3Abi.ReadOnlyFlag"/> and the
    /// flags beside it.
    /// </summary>
    public int Flags;
}

/// <summary>How the two halves of a plugin talk to each other.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IConnectionPointVtbl
{
    /// <summary>The root's three.</summary>
    public FUnknownVtbl Base;

    /// <summary>
    /// Points one half at the other. Both halves are connected, each to the other, and a host
    /// that skips this leaves a plugin whose window changes nothing.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, void*, int> Connect;

    /// <summary>Unpoints it, before either half is taken down.</summary>
    public delegate* unmanaged[Cdecl]<void*, void*, int> Disconnect;

    /// <summary>
    /// One half posting a message to the other. The message is an envelope the plugin asked the
    /// host to supply, which is why a host with no envelope to give crashes the plugins that do
    /// not check.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, void*, int> Notify;
}

/// <summary>A connection point as an object: one word, pointing at its table.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IConnectionPoint
{
    /// <summary>The table of function pointers.</summary>
    public IConnectionPointVtbl* Vtbl;
}


/// <summary>
/// One thing that happens at a point in a block: a note starting, a note ending.
/// </summary>
/// <remarks>
/// The layout is a C union, so the note-on and note-off fields sit on top of each other and
/// which set means anything is decided by <see cref="Type"/>. Written out with the offsets
/// spelled rather than left to the compiler, because the union has to land at 24 whatever is
/// in it, and a byte out here is a note at the wrong pitch or a plugin reading rubbish.
/// </remarks>
[StructLayout(LayoutKind.Explicit, Size = 48)]
internal struct Vst3Event
{
    /// <summary>Which event bus the note is on. Nought, since one is all a plugin is given here.</summary>
    [FieldOffset(0)] public int BusIndex;

    /// <summary>Which frame of the block it happens on, counted from its start.</summary>
    [FieldOffset(4)] public int SampleOffset;

    /// <summary>Where it falls in the song, in quarter notes. Nought, since no transport is supplied.</summary>
    [FieldOffset(8)] public double PpqPosition;

    /// <summary>See <see cref="Vst3Abi.LiveEvent"/>: played by hand rather than off a timeline.</summary>
    [FieldOffset(16)] public ushort Flags;

    /// <summary>Which of the two sets of fields below means anything. See <see cref="Vst3Abi.NoteOnEvent"/>.</summary>
    [FieldOffset(18)] public ushort Type;

    /// <summary>Note on: the MIDI channel, counted from nought.</summary>
    [FieldOffset(24)] public short OnChannel;

    /// <summary>Note on: the key, as a MIDI note number.</summary>
    [FieldOffset(26)] public short OnPitch;

    /// <summary>Note on: how far off that key to play, in cents over a semitone. Nought here.</summary>
    [FieldOffset(28)] public float OnTuning;

    /// <summary>Note on: how hard, nought to one.</summary>
    [FieldOffset(32)] public float OnVelocity;

    /// <summary>Note on: how long the note is to last, in samples, or nought for "until told".</summary>
    [FieldOffset(36)] public int OnLength;

    /// <summary>
    /// Note on: an identity for this sounding note, so a later event can name it. Set to
    /// <see cref="Vst3Abi.NoNoteId"/>, which is how a plain keyboard plays.
    /// </summary>
    [FieldOffset(40)] public int OnNoteId;

    /// <summary>Note off: the channel, on the same ground as the note-on fields.</summary>
    [FieldOffset(24)] public short OffChannel;

    /// <summary>Note off: the key.</summary>
    [FieldOffset(26)] public short OffPitch;

    /// <summary>Note off: how fast the key came up. Most plugins ignore it.</summary>
    [FieldOffset(28)] public float OffVelocity;

    /// <summary>Note off: which sounding note this ends, or <see cref="Vst3Abi.NoNoteId"/> for the pitch.</summary>
    [FieldOffset(32)] public int OffNoteId;

    /// <summary>Note off: the same detune the note started with.</summary>
    [FieldOffset(36)] public float OffTuning;
}


/// <summary>Where a plugin's window is and how big it is, in pixels on X11 and Windows.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ViewRect
{
    /// <summary>The left edge, which is nought for a window a host has just lent out.</summary>
    public int Left;

    /// <summary>The top edge.</summary>
    public int Top;

    /// <summary>The right edge, one past the last pixel.</summary>
    public int Right;

    /// <summary>The bottom edge, one past the last pixel.</summary>
    public int Bottom;

    /// <summary>How wide, which is what a host actually wants and the struct does not carry.</summary>
    public int Width => Right - Left;

    /// <summary>How tall.</summary>
    public int Height => Bottom - Top;
}

/// <summary>
/// The view's face for the screen's scaling, which is the root's three and one more.
/// </summary>
/// <remarks>
/// The factor is a single, not a double: the specification's <c>ScaleFactor</c> is a float, and
/// handing over eight bytes where four are expected puts rubbish in the plugin's register.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IPlugViewContentScaleVtbl
{
    /// <summary>The root's three.</summary>
    public FUnknownVtbl Base;

    /// <summary>
    /// How much the screen this view is on is scaled by: 1 for none, 1.5 for 150 per cent.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, float, int> SetContentScaleFactor;
}

/// <summary>A plugin's own interface: a window it draws itself, inside one the host lends it.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IPlugViewVtbl
{
    /// <summary>The root's three.</summary>
    public FUnknownVtbl Base;

    /// <summary>
    /// Whether the plugin can draw into that kind of window, by the platform's name for one. See
    /// <see cref="Vst3Abi.PlatformWindowType"/>.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, byte*, int> IsPlatformTypeSupported;

    /// <summary>
    /// Puts the plugin's interface inside a window the host owns, given the handle and the
    /// platform's name for it. The window has to really be on screen at its full size first:
    /// handing over the one-pixel window a toolkit makes before its first layout is what killed
    /// Serum.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, void*, byte*, int> Attached;

    /// <summary>Takes it back out. Called before the host's window goes away, never after.</summary>
    public delegate* unmanaged[Cdecl]<void*, int> Removed;

    /// <summary>A wheel turned over the view, for a host that delivers input itself. Not used here.</summary>
    public delegate* unmanaged[Cdecl]<void*, float, int> OnWheel;

    /// <summary>A key pressed, likewise not delivered by this host: X11 gives it to the plugin directly.</summary>
    public delegate* unmanaged[Cdecl]<void*, char, short, short, int> OnKeyDown;

    /// <summary>A key released.</summary>
    public delegate* unmanaged[Cdecl]<void*, char, short, short, int> OnKeyUp;

    /// <summary>How big the plugin wants to be, which is asked before the window is made.</summary>
    public delegate* unmanaged[Cdecl]<void*, ViewRect*, int> GetSize;

    /// <summary>Tells the plugin the window it was given is now this size.</summary>
    public delegate* unmanaged[Cdecl]<void*, ViewRect*, int> OnSize;

    /// <summary>Tells it whether it has the keyboard.</summary>
    public delegate* unmanaged[Cdecl]<void*, byte, int> OnFocus;

    /// <summary>
    /// Hands the plugin the host's frame, which is where a resize request goes and, on Linux,
    /// where the run loop is asked for. A plugin with no frame has no clock and never draws.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, void*, int> SetFrame;

    /// <summary>
    /// Whether the plugin will follow a window being dragged bigger. Answers the ABI's awkward
    /// true, which is nought.
    /// </summary>
    public delegate* unmanaged[Cdecl]<void*, int> CanResize;

    /// <summary>Rounds a size the host is considering to one the plugin would actually take.</summary>
    public delegate* unmanaged[Cdecl]<void*, ViewRect*, int> CheckSizeConstraint;
}

/// <summary>A view as an object: one word, pointing at its table.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IPlugView
{
    /// <summary>The table of function pointers.</summary>
    public IPlugViewVtbl* Vtbl;
}

/// <summary>A view asked how much the screen it is on is scaled by.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct IPlugViewContentScale
{
    /// <summary>The table of function pointers.</summary>
    public IPlugViewContentScaleVtbl* Vtbl;
}
