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
/// crash inside the plugin's own code. See <see cref="ClapBundle"/> for why the library itself
/// is never unloaded either.
/// </remarks>
public sealed unsafe class Vst3Module : IDisposable
{
    private static readonly Dictionary<string, Vst3Module> Open_ = new(StringComparer.Ordinal);
    private static readonly object Registry = new();

    private readonly nint _library;
    private readonly IPluginFactory* _factory;
    private readonly IPluginFactory* _factory2;

    private int _references;

    private Vst3Module(string path, nint library, IPluginFactory* factory, IPluginFactory* factory2)
    {
        Path = path;
        _library = library;
        _factory = factory;
        _factory2 = factory2;
    }

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
    public static string? BinaryIn(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        // Some builds ship the library itself named .vst3, with no bundle around it.
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

    private static Vst3Module? Load(string path)
    {
        string? binary = BinaryIn(path);
        if (binary == null) return null;

        nint library = 0;

        try
        {
            if (!NativeLibrary.TryLoad(binary, out library)) return null;

            // The module is woken before anything else is asked of it. Linux hands it the
            // library handle; the other platforms take nothing.
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

            // The factory is told who is hosting it before any class is made. Some plugins,
            // Serum among them, will not start without being able to ask the host its name.
            var factory2 = Extend(factory);

            return new Vst3Module(path, library, factory, factory2);
        }
        catch (Exception)
        {
            // A bundle built for another architecture, or one that is not a plugin at all, is
            // one plugin the user does not get rather than a dead application.
            return null;
        }
    }

    /// <summary>
    /// Asks the factory for its later revisions, which is where the vendor, the version and
    /// the host context live. The pointer is usually the same object; asking is what makes it
    /// safe to call the entries those revisions added.
    /// </summary>
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

            // A class is allowed to leave its vendor blank, and Serum does. Who made the
            // bundle is the next best answer and is never blank in practice.
            string maker = detailed ? Text(full.Vendor, Vst3Abi.VendorSize) : "";
            if (string.IsNullOrWhiteSpace(maker)) maker = vendor;

            // The class says what it is, so an instrument can be told apart without opening
            // it. Serum ships both in the one bundle: a synth and an effect rack.
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
    internal IEditController* CreateController(byte* classId)
    {
        void* result = null;

        fixed (byte* face = Vst3Abi.EditControllerId)
        {
            if (_factory->Vtbl->CreateInstance(_factory, classId, face, &result) != Vst3Abi.ResultOk) return null;
        }

        return (IEditController*)result;
    }

    private static string Text(byte* fixedText, int size)
    {
        int length = 0;
        while (length < size && fixedText[length] != 0) length++;

        return length == 0 ? "" : System.Text.Encoding.UTF8.GetString(fixedText, length);
    }

    /// <summary>
    /// Gives back one reference. The library stays loaded for the life of the process, for the
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
