using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Midi;

/// <inheritdoc/>
/// <remarks>
/// One JSON file in the application folder, written whole through <see cref="ISafeFile"/> like
/// everything else here, and indented for the same reason the settings are: it is the first thing
/// anybody opens when a knob is doing something nobody expected.
/// </remarks>
public sealed class RemoteControlLinks : IRemoteControlLinks
{
    /// <summary>What the file is called under the application folder.</summary>
    public const string FileName = "remotecontrol-links.json";

    /// <summary>Where the application keeps its things.</summary>
    private readonly IAppFolder _app;

    /// <summary>Writing a file whole, so a half-written one cannot replace a good one.</summary>
    private readonly ISafeFile _files;

    /// <summary>Indented, since the file is meant to be readable by whoever has to look at it.</summary>
    private static readonly JsonSerializerOptions Layout = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Reads and writes the links.</summary>
    /// <param name="app">Where the application keeps its things.</param>
    /// <param name="files">How a file is written whole.</param>
    public RemoteControlLinks(IAppFolder? app = null, ISafeFile? files = null)
    {
        _app = app ?? new AppFolder();
        _files = files ?? new SafeFile();
    }

    /// <inheritdoc/>
    public string Path() => System.IO.Path.Combine(_app.Path(), FileName);

    /// <inheritdoc/>
    public string Written(IEnumerable<ControlMapping>? links) =>
        JsonSerializer.Serialize(links?.ToList() ?? new List<ControlMapping>(), Layout);

    /// <inheritdoc/>
    public void Keep(string written) => _files.Write(Path(), written);

    /// <inheritdoc/>
    public IReadOnlyList<ControlMapping> Read()
    {
        try
        {
            string path = Path();

            if (!File.Exists(path)) return Array.Empty<ControlMapping>();

            return JsonSerializer.Deserialize<List<ControlMapping>>(File.ReadAllText(path), Layout)
                   ?? (IReadOnlyList<ControlMapping>)Array.Empty<ControlMapping>();
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Midi, () => "links: the links file would not read: " + bad.Message);

            return Array.Empty<ControlMapping>();
        }
    }
}
