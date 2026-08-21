using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// One .clap file, opened once and shared.
/// </summary>
/// <remarks>
/// Shared rather than opened per use, and counted. A .clap is initialised and deinitialised
/// through its entry point, and the two have to balance: opening the same file twice and
/// closing it twice deinitialises a library that is still in use, which is a segmentation
/// fault inside somebody else's code. So every user of a bundle takes a reference and gives
/// it back, and only the last one out turns the lights off.
/// </remarks>
public sealed unsafe class ClapBundle : IDisposable
{
    private static readonly Dictionary<string, ClapBundle> Open_ = new(StringComparer.Ordinal);
    private static readonly object Registry = new();

    private readonly nint _library;
    private readonly ClapPluginEntry* _entry;
    private readonly ClapPluginFactory* _factory;

    private int _references;

    private ClapBundle(string path, nint library, ClapPluginEntry* entry, ClapPluginFactory* factory)
    {
        Path = path;
        _library = library;
        _entry = entry;
        _factory = factory;
    }

    public string Path { get; }

    /// <summary>
    /// Takes a reference to a bundle, opening it if nothing else has. Give it back with
    /// <see cref="Dispose"/>; the file stays open until the last reference is returned.
    /// </summary>
    public static ClapBundle? Acquire(string path)
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

            var bundle = Load(key);
            if (bundle == null) return null;

            bundle._references = 1;
            Open_[key] = bundle;

            return bundle;
        }
    }

    private static ClapBundle? Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        nint library = 0;

        try
        {
            if (!NativeLibrary.TryLoad(path, out library)) return null;
            if (!NativeLibrary.TryGetExport(library, ClapAbi.EntrySymbol, out nint symbol)) return Fail(library);

            var entry = (ClapPluginEntry*)symbol;
            if (entry->Init == null || entry->GetFactory == null) return Fail(library);

            // The plugin is told where it lives; some load resources next to themselves.
            using var pathText = new NativeText(path);
            if (entry->Init(pathText.Pointer) == 0) return Fail(library);

            using var factoryId = new NativeText(ClapAbi.PluginFactoryId);
            var factory = (ClapPluginFactory*)entry->GetFactory(factoryId.Pointer);

            if (factory == null || factory->Count == null || factory->Create == null)
            {
                if (entry->Deinit != null) entry->Deinit();
                return Fail(library);
            }

            return new ClapBundle(path, library, entry, factory);
        }
        catch (Exception)
        {
            // A library that is not a plugin, is built for another architecture, or refuses to
            // load is one plugin the user does not get, not a dead application.
            return Fail(library);
        }
    }

    private static ClapBundle? Fail(nint library)
    {
        if (library != 0) NativeLibrary.Free(library);
        return null;
    }

    /// <summary>What this bundle holds. Most hold one plugin; a suite holds dozens.</summary>
    public IReadOnlyList<ClapPluginInfo> Plugins()
    {
        var plugins = new List<ClapPluginInfo>();

        uint count = _factory->Count(_factory);

        for (uint index = 0; index < count; index++)
        {
            var descriptor = _factory->GetDescriptor(_factory, index);
            if (descriptor == null) continue;

            string id = NativeText.Read(descriptor->Id);
            if (string.IsNullOrEmpty(id)) continue;

            plugins.Add(new ClapPluginInfo(
                id,
                NativeText.Read(descriptor->Name),
                NativeText.Read(descriptor->Vendor),
                NativeText.Read(descriptor->PluginVersion),
                Path));
        }

        return plugins;
    }

    /// <summary>
    /// Makes an instance of one of the plugins in this bundle, ready to be activated.
    /// </summary>
    internal ClapPlugin* Create(string id, ClapHost* host)
    {
        using var pluginId = new NativeText(id);
        return _factory->Create(_factory, host, pluginId.Pointer);
    }

    /// <summary>
    /// Gives back one reference. The library itself stays loaded for the life of the process.
    /// </summary>
    /// <remarks>
    /// Deliberately not unloaded. These libraries carry global state, thread-locals and exit
    /// handlers of their own, and unloading one after another has already gone leaves the
    /// second tearing down through the first one's remains: measured here as a segmentation
    /// fault while releasing the second plugin, with nothing of ours on the stack. Hosts do
    /// not unload plugin libraries for this reason, and the cost of keeping them is a few
    /// megabytes of address space until the app closes.
    ///
    /// The reference count is still kept, because it is what stops the same bundle being
    /// initialised twice, and it is what a rescan would need if this is ever revisited.
    /// </remarks>
    public void Dispose()
    {
        lock (Registry)
        {
            if (_references > 0) _references--;
        }
    }
}

/// <summary>A C string in unmanaged memory, freed when it goes out of scope.</summary>
internal unsafe readonly struct NativeText : IDisposable
{
    private readonly nint _memory;

    public NativeText(string text)
    {
        _memory = Marshal.StringToCoTaskMemUTF8(text ?? "");
    }

    public byte* Pointer => (byte*)_memory;

    /// <summary>Reads a C string the plugin owns. Null and unterminated both read as empty.</summary>
    public static string Read(byte* text) => text == null ? "" : Marshal.PtrToStringUTF8((nint)text) ?? "";

    public void Dispose()
    {
        if (_memory != 0) Marshal.FreeCoTaskMem(_memory);
    }
}
