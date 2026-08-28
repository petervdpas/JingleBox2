using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class RecordingCategories : IRecordingCategories
{
    private const string FileName = "categories.json";

    private static readonly JsonSerializerOptions Layout = new() { WriteIndented = true };

    private readonly string _path;

    private Dictionary<string, string> _of = new(StringComparer.Ordinal);

    /// <summary>Reads the filing of the takes in the application's own recordings folder.</summary>
    public RecordingCategories()
        : this(Path.Combine(Config.AppFolder.Path(), "recordings")) { }

    /// <summary>Reads the filing of the takes in a named folder.</summary>
    /// <remarks>
    /// The folder rather than the file, since the file's name is this class's own business and a
    /// test that named it would be able to disagree with the application about where it is.
    /// </remarks>
    /// <param name="folder">Where the takes are, and where their filing sits beside them.</param>
    public RecordingCategories(string folder)
    {
        _path = Path.Combine(folder, FileName);

        Load();
    }

    /// <inheritdoc/>
    public string Of(string name) =>
        _of.TryGetValue(name, out string? category) ? category : "";

    /// <inheritdoc/>
    public void Put(string name, string? category)
    {
        string wanted = (category ?? "").Trim();

        if (string.Equals(Of(name), wanted, StringComparison.Ordinal)) return;

        if (wanted.Length == 0) _of.Remove(name);
        else _of[name] = wanted;

        Save();
    }

    /// <inheritdoc/>
    public void Renamed(string from, string to)
    {
        if (!_of.TryGetValue(from, out string? category)) return;

        _of.Remove(from);
        _of[to] = category;

        Save();
    }

    /// <inheritdoc/>
    public void Forget(string name)
    {
        if (!_of.Remove(name)) return;

        Save();
    }

    /// <summary>Reads the filing off the disc, and starts with none of it when it cannot.</summary>
    /// <remarks>
    /// A sorting nobody can read is a sorting rather than a session, so a damaged file costs the
    /// categories and nothing else.
    /// </remarks>
    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;

            var read = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path));

            if (read != null) _of = new Dictionary<string, string>(read, StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.LogArea.Audio, "Categories could not be read", ex);
        }
    }

    /// <summary>Writes the filing back beside the takes, after every change.</summary>
    private void Save()
    {
        try
        {
            string? folder = Path.GetDirectoryName(_path);

            if (folder != null) Directory.CreateDirectory(folder);

            File.WriteAllText(_path, JsonSerializer.Serialize(_of, Layout));
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.LogArea.Audio, "Categories could not be written", ex);
        }
    }
}
