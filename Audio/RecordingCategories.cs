using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace JingleBox2.Audio;

/// <summary>
/// What each take is filed under, kept in one file beside the takes.
/// </summary>
/// <remarks>
/// A note about a recording rather than a folder to put it in, because the recording itself is
/// a path somebody wrote down: an instrument plays that file, a pad fires it, a song names it.
/// Filing a take into a folder would move it out from under all three, and out from under the
/// pad profiles that are not even loaded. A line in a file moves nothing.
///
/// It lives in the recordings folder, so copying the takes somewhere copies how they were
/// sorted with them. Lose the file and what is lost is the sorting, not a second of audio.
///
/// Keyed by name, which for a recording is its file name. A take renamed on this page is
/// followed; one renamed behind the app's back loses its category rather than inheriting
/// somebody else's.
/// </remarks>
public sealed class RecordingCategories
{
    private const string FileName = "categories.json";

    private static readonly JsonSerializerOptions Layout = new() { WriteIndented = true };

    private readonly string _path;

    private Dictionary<string, string> _of = new(StringComparer.Ordinal);

    public RecordingCategories()
        : this(Path.Combine(Config.AppFolder.Path(), "recordings")) { }

    public RecordingCategories(string folder)
    {
        _path = Path.Combine(folder, FileName);

        Load();
    }

    /// <summary>What that take is filed under, or empty when it is filed under nothing.</summary>
    public string Of(string name) =>
        _of.TryGetValue(name, out string? category) ? category : "";

    /// <summary>Files a take, or takes it out of its category when given nothing.</summary>
    public void Put(string name, string? category)
    {
        string wanted = (category ?? "").Trim();

        if (string.Equals(Of(name), wanted, StringComparison.Ordinal)) return;

        if (wanted.Length == 0) _of.Remove(name);
        else _of[name] = wanted;

        Save();
    }

    /// <summary>The take is called something else now, and keeps what it was filed under.</summary>
    public void Renamed(string from, string to)
    {
        if (!_of.TryGetValue(from, out string? category)) return;

        _of.Remove(from);
        _of[to] = category;

        Save();
    }

    /// <summary>The take is gone, so the line about it is too.</summary>
    public void Forget(string name)
    {
        if (!_of.Remove(name)) return;

        Save();
    }

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
            // A sorting nobody can read is a sorting, not a session. Start with none of it.
            Diagnostics.Log.Fault(Diagnostics.LogArea.Audio, "Categories could not be read", ex);
        }
    }

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
