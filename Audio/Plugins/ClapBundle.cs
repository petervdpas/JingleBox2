using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using JingleBox2.Audio.Plugins.Enums;

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
    /// <summary>
    /// Every bundle open in this process, by its full path. Ordinal because a path is bytes
    /// rather than words, and two spellings that differ only in case are two files on Linux.
    /// </summary>
    private static readonly Dictionary<string, ClapBundle> Open_ = new(StringComparer.Ordinal);

    /// <summary>Held over the whole of an acquire, so two threads cannot both decide to load.</summary>
    private static readonly object Registry = new();

    /// <summary>The loaded shared library. Never freed: see <see cref="Dispose"/>.</summary>
    private readonly nint _library;

    /// <summary>The one exported symbol, which is where init and the factory come from.</summary>
    private readonly ClapPluginEntry* _entry;

    /// <summary>What lists and creates the plugins inside the bundle. Owned by the library.</summary>
    private readonly ClapPluginFactory* _factory;

    /// <summary>
    /// How many things are holding this bundle. What stops the same file being initialised
    /// twice, since a .clap that is deinitialised while still in use is a segmentation fault
    /// inside somebody else's code.
    /// </summary>
    private int _references;

    /// <summary>
    /// Private because a bundle is only ever reached through <see cref="Acquire"/>: one made any
    /// other way would be outside the count and could be initialised a second time.
    /// </summary>
    private ClapBundle(string path, nint library, ClapPluginEntry* entry, ClapPluginFactory* factory)
    {
        Path = path;
        _library = library;
        _entry = entry;
        _factory = factory;
    }

    /// <summary>Where the .clap is, as a full path. Also the key it is shared under.</summary>
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

    /// <summary>
    /// Opens a .clap and gets as far as its factory, or answers null for anything that is not
    /// one.
    /// </summary>
    /// <remarks>
    /// The plugin is handed the path it was loaded from, because some of them load resources
    /// next to themselves and have no other way of finding where they are.
    ///
    /// A library that is not a plugin, is built for another architecture, or simply refuses to
    /// load is one plugin the user does not get rather than a dead application, so every failure
    /// here unloads and answers null.
    /// </remarks>
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
            return Fail(library);
        }
    }

    /// <summary>
    /// Unloads a library that turned out not to be usable and answers null, so every failing
    /// path in <see cref="Load"/> is one line and cannot forget to free.
    /// </summary>
    private static ClapBundle? Fail(nint library)
    {
        if (library != 0) NativeLibrary.Free(library);
        return null;
    }

    /// <summary>What this bundle holds. Most hold one plugin; a suite holds dozens.</summary>
    public IReadOnlyList<PluginInfo> Plugins()
    {
        var plugins = new List<PluginInfo>();

        uint count = _factory->Count(_factory);

        for (uint index = 0; index < count; index++)
        {
            var descriptor = _factory->GetDescriptor(_factory, index);
            if (descriptor == null) continue;

            string id = NativeText.Read(descriptor->Id);
            if (string.IsNullOrEmpty(id)) continue;

            plugins.Add(new PluginInfo(
                id,
                NativeText.Read(descriptor->Name),
                NativeText.Read(descriptor->Vendor),
                NativeText.Read(descriptor->PluginVersion),
                Path,
                PluginFormat.Clap,
                IsInstrument(descriptor)));
        }

        return plugins;
    }

    /// <summary>
    /// Whether a plugin makes sound from notes rather than from audio. CLAP says so in the
    /// list of words a plugin describes itself with, so this costs nothing to ask.
    /// </summary>
    private static bool IsInstrument(ClapPluginDescriptor* descriptor)
    {
        if (descriptor->Features == null) return false;

        for (int index = 0; index < MaxFeatures; index++)
        {
            byte* feature = descriptor->Features[index];
            if (feature == null) break;

            string word = NativeText.Read(feature);
            if (string.Equals(word, "instrument", StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>Where the walk over a plugin's own description gives up, if it is not ended.</summary>
    private const int MaxFeatures = 64;

    /// <summary>
    /// Makes an instance of one of the plugins in this bundle, ready to be activated.
    /// </summary>
    /// <remarks>
    /// By id rather than by index, since a bundle is entitled to list its plugins in a different
    /// order next version and a saved chain names an id. The host struct handed over is kept by
    /// the plugin for its whole life and must not move.
    /// </remarks>
    internal ClapPlugin* Create(string id, ClapHost* host)
    {
        using var pluginId = new NativeText(id);
        return _factory->Create(_factory, host, pluginId.Pointer);
    }

    /// <summary>
    /// Gives back one reference. The rack itself stays loaded for the life of the process.
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
/// <remarks>
/// Every string handed into a plugin has to be UTF-8 and has to sit still, which rules out
/// anything the collector owns. A struct with a using around it is the cheapest way to say
/// "this lives exactly as long as the call".
///
/// It is only safe for a string the plugin reads and does not keep. Anything a plugin holds on
/// to, such as the host's own name, is allocated once and never freed instead.
/// </remarks>
internal unsafe readonly struct NativeText : IDisposable
{
    /// <summary>The unmanaged copy. Nought for a string that could not be allocated.</summary>
    private readonly nint _memory;

    /// <summary>Copies a string into unmanaged memory as UTF-8. Null is copied as empty.</summary>
    public NativeText(string text)
    {
        _memory = Marshal.StringToCoTaskMemUTF8(text ?? "");
    }

    /// <summary>The string as a plugin wants it: a pointer to null terminated UTF-8.</summary>
    public byte* Pointer => (byte*)_memory;

    /// <summary>Reads a C string the plugin owns. Null and unterminated both read as empty.</summary>
    public static string Read(byte* text) => text == null ? "" : Marshal.PtrToStringUTF8((nint)text) ?? "";

    /// <summary>Frees the copy. Safe to call on a default instance, which holds nothing.</summary>
    public void Dispose()
    {
        if (_memory != 0) Marshal.FreeCoTaskMem(_memory);
    }
}
