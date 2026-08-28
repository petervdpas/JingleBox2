using ManagedBass;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class BassPlugins : IBassPlugins
{
    /// <summary>
    /// What BASS reads with no help. Declared rather than asked for, because there is nothing
    /// to ask: these are built in and have no plugin to report them.
    /// </summary>
    private static readonly string[] BuiltIn = { ".wav", ".aiff", ".aif", ".mp3", ".mp2", ".mp1", ".mpga", ".ogg", ".oga" };

    /// <summary>
    /// Held while the add-ons are loaded, since two callers can arrive at once.
    /// </summary>
    /// <remarks>
    /// Static, along with what it guards, and deliberately. Whether an add-on is loaded is a
    /// fact about the process rather than about this object: loading one twice loads it twice,
    /// whichever object asked.
    /// </remarks>
    private static readonly object Gate = new();

    /// <summary>Whether the folder has been walked. It is walked once a session.</summary>
    private static bool _loaded;

    /// <summary>What the add-ons that did load say they read.</summary>
    private static string[] _added = Array.Empty<string>();

    /// <inheritdoc/>
    public void Load()
    {
        lock (Gate)
        {
            if (_loaded) return;

            _loaded = true;

            var reads = new List<string>();

            foreach (string library in Libraries())
            {
                int plugin = Bass.PluginLoad(library);

                if (plugin == 0) continue;

                if (Formats.TryGetValue(Named(library), out var kinds)) reads.AddRange(kinds);
            }

            _added = reads.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Kinds
    {
        get
        {
            Load();

            return BuiltIn.Concat(_added).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    /// <summary>
    /// The add-ons in the program's folder.
    /// </summary>
    /// <remarks>
    /// Everything called after BASS except BASS itself. One that turns out not to be an add-on,
    /// the loopback recorder among them, simply refuses to load and is passed over.
    /// </remarks>
    private IEnumerable<string> Libraries()
    {
        string home = AppContext.BaseDirectory;

        string pattern = OperatingSystem.IsWindows() ? "bass*.dll" : "libbass*.so";
        string core = OperatingSystem.IsWindows() ? "bass.dll" : "libbass.so";

        string[] found;

        try
        {
            found = Directory.GetFiles(home, pattern);
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }

        return found
            .Where(one => !string.Equals(Path.GetFileName(one), core, StringComparison.OrdinalIgnoreCase))
            .OrderBy(one => one, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// What each add-on reads, written out.
    /// </summary>
    /// <remarks>
    /// Declared rather than asked, because asking does not work: BASS reports a plugin's formats
    /// through an array of structs holding pointers to its strings, and the wrapper hands those
    /// back with the names and the extensions empty. Reading it properly would mean walking
    /// unmanaged memory for a list that changes about once a decade.
    ///
    /// So the door offers what is in this table, and no more. An add-on that is not in it still
    /// loads and a pad will still play what it reads: only the import picker is careful, because
    /// only the import picker is promising that what you pick will work.
    /// </remarks>
    private static readonly Dictionary<string, string[]> Formats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bass_aac"] = new[] { ".aac", ".m4a", ".m4b", ".mp4" },
        ["bassalac"] = new[] { ".m4a", ".mp4" },
        ["bassflac"] = new[] { ".flac", ".fla" },
        ["bassopus"] = new[] { ".opus" },
        ["basswv"] = new[] { ".wv" },
        ["bass_ape"] = new[] { ".ape", ".mac" },
        ["bass_ac3"] = new[] { ".ac3" },
        ["bassdsd"] = new[] { ".dsf", ".dff" },
        ["basswma"] = new[] { ".wma" },
        ["bass_mpc"] = new[] { ".mpc" },
        ["bass_tta"] = new[] { ".tta" },
        ["bass_spx"] = new[] { ".spx" },
    };

    /// <summary>What an add-on is called, with the platform's dressing taken off.</summary>
    private string Named(string library)
    {
        string name = Path.GetFileNameWithoutExtension(library);

        return name.StartsWith("lib", StringComparison.OrdinalIgnoreCase) ? name[3..] : name;
    }
}
