using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// One .vst3 bundle, opened once and shared.
/// </summary>
/// <remarks>
/// Shared and counted for the same reason a CLAP bundle is: a module is woken and put back to
/// sleep through its entry points, and doing the second while somebody is still using it is a
/// crash inside the plugin's own code. See <see cref="ClapBundle"/> for why the rack itself
/// is never unloaded either.
/// </remarks>
public sealed unsafe class Vst3Module : IDisposable
{
    /// <summary>
    /// Every module open in this process, by its full path. Ordinal because a path is bytes
    /// rather than words, and two spellings that differ only in case are two files on Linux.
    /// </summary>
    private static readonly Dictionary<string, Vst3Module> Open_ = new(StringComparer.Ordinal);

    /// <summary>Held over the whole of an acquire, so two threads cannot both decide to load.</summary>
    private static readonly object Registry = new();

    /// <summary>The loaded shared library from inside the bundle. Never freed.</summary>
    private readonly nint _library;

    /// <summary>
    /// The factory as its first version, which is what every bundle has and what creates
    /// classes.
    /// </summary>
    private readonly IPluginFactory* _factory;

    /// <summary>
    /// The same factory queried for a later revision, or null for a bundle that stops at the
    /// first. Kept apart because the later entries in the table may only be called through a
    /// pointer that was asked for by that interface's own id: a factory that stops at the first
    /// version has a shorter table, and calling past its end jumps into whatever is next in
    /// memory.
    /// </summary>
    private readonly IPluginFactory* _factory2;

    /// <summary>
    /// How many things are holding this module. What stops the same bundle being woken twice,
    /// since a module put back to sleep while still in use is a crash inside the plugin's own
    /// code.
    /// </summary>
    private int _references;

    /// <summary>
    /// Private because a module is only ever reached through <see cref="Acquire"/>: one made any
    /// other way would be outside the count and could be woken a second time.
    /// </summary>
    private Vst3Module(string path, nint library, IPluginFactory* factory, IPluginFactory* factory2)
    {
        Path = path;
        _library = library;
        _factory = factory;
        _factory2 = factory2;
    }

    /// <summary>Where the .vst3 bundle is, as a full path. Also the key it is shared under.</summary>
    public string Path { get; }

    /// <summary>
    /// Takes a reference to a module, opening it if nothing else has. Give it back with
    /// <see cref="Dispose"/>.
    /// </summary>
    public static Vst3Module? Acquire(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        string key = System.IO.Path.GetFullPath(path);

        lock (Registry)
        {
            if (Open_.TryGetValue(key, out var existing))
            {
                existing._references++;
                return existing;
            }

            var module = Load(key);
            if (module == null) return null;

            module._references = 1;
            Open_[key] = module;

            return module;
        }
    }

    /// <summary>
    /// The shared library inside a bundle. A .vst3 is a folder with the binary for each
    /// platform under it, so that a bundle can be copied between machines whole.
    /// </summary>
    /// <remarks>
    /// A path that is a file rather than a folder is taken as the library itself: some builds
    /// ship the shared object named .vst3 with no bundle around it, and refusing those would
    /// lose plugins that work perfectly well.
    /// </remarks>
    public static string? BinaryIn(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        if (File.Exists(path)) return path;
        if (!Directory.Exists(path)) return null;

        string folder = System.IO.Path.Combine(path, "Contents", PlatformFolder());
        if (!Directory.Exists(folder)) return null;

        string pattern = OperatingSystem.IsWindows() ? "*.vst3" : OperatingSystem.IsMacOS() ? "*" : "*.so";

        foreach (var file in Directory.EnumerateFiles(folder, pattern))
        {
            return file;
        }

        return null;
    }

    /// <summary>What the bundle calls the folder for this machine.</summary>
    private static string PlatformFolder()
    {
        if (OperatingSystem.IsMacOS()) return "MacOS";

        string architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "aarch64",
            Architecture.X86 => "i386",
            _ => "x86_64"
        };

        return OperatingSystem.IsWindows()
            ? (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64-win" : "x86_64-win")
            : architecture + "-linux";
    }

    /// <summary>
    /// Opens a bundle and gets as far as its factory, or answers null for anything that is not
    /// one.
    /// </summary>
    /// <remarks>
    /// The module is woken before anything else is asked of it. Linux hands the entry point the
    /// library handle and the other two platforms take nothing, which is the one place the three
    /// spellings of the entry point differ in more than their name.
    ///
    /// A bundle built for another architecture, or one that is not a plugin at all, is one
    /// plugin the user does not get rather than a dead application.
    /// </remarks>
    private static Vst3Module? Load(string path)
    {
        string? binary = BinaryIn(path);
        if (binary == null) return null;

        nint library = 0;

        try
        {
            if (!NativeLibrary.TryLoad(binary, out library)) return null;

            if (NativeLibrary.TryGetExport(library, Vst3Abi.EntrySymbol, out nint entry))
            {
                if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
                {
                    var start = (delegate* unmanaged[Cdecl]<byte>)entry;
                    if (start() == 0) return null;
                }
                else
                {
                    var start = (delegate* unmanaged[Cdecl]<void*, byte>)entry;
                    if (start((void*)library) == 0) return null;
                }
            }

            if (!NativeLibrary.TryGetExport(library, Vst3Abi.FactorySymbol, out nint symbol)) return null;

            var get = (delegate* unmanaged[Cdecl]<IPluginFactory*>)symbol;
            var factory = get();

            if (factory == null || factory->Vtbl == null) return null;

            var factory2 = Extend(factory);

            return new Vst3Module(path, library, factory, factory2);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Asks the factory for its later revisions, which is where the vendor, the version and
    /// the host context live. The pointer is usually the same object; asking is what makes it
    /// safe to call the entries those revisions added.
    /// </summary>
    /// <remarks>
    /// The third revision also gets told who is hosting it, before any class is made. Some
    /// plugins, Serum among them, will not start without being able to ask the host its name,
    /// and the host context is the only place they can ask.
    /// </remarks>
    private static IPluginFactory* Extend(IPluginFactory* factory)
    {
        IPluginFactory* factory3 = Query(factory, Vst3Abi.PluginFactory3Id);

        if (factory3 != null)
        {
            factory3->Vtbl->SetHostContext(factory3, Vst3Host.Application());
            return factory3;
        }

        return Query(factory, Vst3Abi.PluginFactory2Id);
    }

    /// <summary>
    /// Asks a factory for another of its faces. Null for one it does not have, which is an
    /// ordinary answer rather than a fault.
    /// </summary>
    private static IPluginFactory* Query(IPluginFactory* factory, byte[] id)
    {
        void* result = null;

        fixed (byte* wanted = id)
        {
            if (factory->Vtbl->Base.QueryInterface(factory, wanted, &result) != Vst3Abi.ResultOk) return null;
        }

        return (IPluginFactory*)result;
    }

    /// <summary>What this bundle holds. Only the classes that make audio are listed.</summary>
    /// <remarks>
    /// A class is allowed to leave its vendor blank and Serum does, so who made the bundle is
    /// used instead: that is never blank in practice.
    ///
    /// What kind of thing a class is comes off its subcategories, so an instrument is told apart
    /// from an effect without opening either. Serum ships both in the one bundle, a synth and an
    /// effect rack, which is why the question has to be asked per class rather than per file.
    /// </remarks>
    public IReadOnlyList<PluginInfo> Plugins()
    {
        var plugins = new List<PluginInfo>();
        if (_factory == null) return plugins;

        string vendor = FactoryVendor();

        int count = _factory->Vtbl->CountClasses(_factory);

        for (int index = 0; index < count; index++)
        {
            var full = new PClassInfo2();
            bool detailed = _factory2 != null &&
                            _factory2->Vtbl->GetClassInfo2(_factory2, index, &full) == Vst3Abi.ResultOk;

            var plain = new PClassInfo();
            if (!detailed && _factory->Vtbl->GetClassInfo(_factory, index, &plain) != Vst3Abi.ResultOk) continue;

            string category = detailed
                ? Text(full.Category, Vst3Abi.CategorySize)
                : Text(plain.Category, Vst3Abi.CategorySize);

            if (!string.Equals(category, Vst3Abi.AudioModuleCategory, StringComparison.Ordinal)) continue;

            string id = detailed ? Vst3Abi.HexId(full.Cid) : Vst3Abi.HexId(plain.Cid);
            string name = detailed ? Text(full.Name, Vst3Abi.NameSize) : Text(plain.Name, Vst3Abi.NameSize);

            string maker = detailed ? Text(full.Vendor, Vst3Abi.VendorSize) : "";
            if (string.IsNullOrWhiteSpace(maker)) maker = vendor;

            string kinds = detailed ? Text(full.SubCategories, Vst3Abi.SubCategoriesSize) : "";
            bool instrument = kinds.Contains("Instrument", StringComparison.OrdinalIgnoreCase) ||
                              kinds.Contains("Synth", StringComparison.OrdinalIgnoreCase);

            plugins.Add(new PluginInfo(
                id,
                name,
                maker,
                detailed ? Text(full.Version, Vst3Abi.VersionSize) : "",
                Path,
                PluginFormat.Vst3,
                instrument));
        }

        return plugins;
    }

    /// <summary>Who made the bundle, used for the classes that do not say themselves.</summary>
    private string FactoryVendor()
    {
        var info = new PFactoryInfo();
        if (_factory->Vtbl->GetFactoryInfo(_factory, &info) != Vst3Abi.ResultOk) return "";

        return Text(info.Vendor, Vst3Abi.NameSize);
    }

    /// <summary>
    /// Makes the audio half of one of the classes in this bundle, ready to be initialised.
    /// </summary>
    /// <remarks>
    /// By class id rather than by index, since a bundle is entitled to list its classes in a
    /// different order next version and a saved chain names an id. Null for an id that is not
    /// thirty-two hex digits, rather than handing rubbish to the factory.
    /// </remarks>
    internal IComponent* CreateComponent(string classId)
    {
        var cid = Vst3Abi.ParseHexId(classId);
        if (cid == null || _factory == null) return null;

        void* result = null;

        fixed (byte* wanted = cid)
        fixed (byte* face = Vst3Abi.ComponentId)
        {
            if (_factory->Vtbl->CreateInstance(_factory, wanted, face, &result) != Vst3Abi.ResultOk) return null;
        }

        return (IComponent*)result;
    }

    /// <summary>
    /// Makes the settings half, when the audio half says it lives in a class of its own.
    /// </summary>
    /// <remarks>
    /// The id comes from the component itself rather than from a saved chain, which is why it is
    /// raw bytes here. A plugin whose two halves are one object never reaches this.
    /// </remarks>
    internal IEditController* CreateController(byte* classId)
    {
        void* result = null;

        fixed (byte* face = Vst3Abi.EditControllerId)
        {
            if (_factory->Vtbl->CreateInstance(_factory, classId, face, &result) != Vst3Abi.ResultOk) return null;
        }

        return (IEditController*)result;
    }

    /// <summary>
    /// Reads one of the ABI's fixed width name fields. The terminator is looked for and the size
    /// is the ceiling, because a field that is exactly full carries no terminator at all and
    /// reading past it would run into the next field.
    /// </summary>
    private static string Text(byte* fixedText, int size)
    {
        int length = 0;
        while (length < size && fixedText[length] != 0) length++;

        return length == 0 ? "" : System.Text.Encoding.UTF8.GetString(fixedText, length);
    }

    /// <summary>
    /// Gives back one reference. The rack stays loaded for the life of the process, for the
    /// reasons written out on <see cref="ClapBundle.Dispose"/>.
    /// </summary>
    public void Dispose()
    {
        lock (Registry)
        {
            if (_references > 0) _references--;
        }
    }
}
