using System;
using System.IO;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// A folder under the application folder rather than the system's temporary one, deliberately.
/// A take here is hundreds of megabytes on a long session and it is read back by the waveform,
/// the preview and the trim, so it wants to be on the same disc as everything else this
/// application keeps; and where somebody has to go looking for a take a crash interrupted,
/// beside their own recordings is where they would think to look.
/// </remarks>
public sealed class TakeScratch : ITakeScratch
{
    /// <summary>What the folder is called under the application folder.</summary>
    public const string FolderName = "scratch";

    /// <summary>Backs <see cref="Folder"/>.</summary>
    private readonly string _folder;

    /// <summary>Names the scratchpad under the application folder.</summary>
    /// <param name="folder">Where the application keeps its things, defaulted to the real one.</param>
    public TakeScratch(IAppFolder? folder = null)
    {
        _folder = Path.Combine((folder ?? new AppFolder()).Path(), FolderName);

        Directory.CreateDirectory(_folder);
    }

    /// <inheritdoc/>
    public string Folder
    {
        get
        {
            Directory.CreateDirectory(_folder);
            return _folder;
        }
    }

    /// <inheritdoc/>
    public void Sweep()
    {
        if (!Directory.Exists(_folder)) return;

        foreach (string file in Directory.GetFiles(_folder))
        {
            try { File.Delete(file); }
            catch (Exception) { }
        }
    }

    /// <inheritdoc/>
    public string? Keep(string from, string folder, string name)
    {
        if (string.IsNullOrWhiteSpace(from) || !File.Exists(from)) return null;
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(name)) return null;

        Directory.CreateDirectory(folder);

        string to = Path.Combine(folder, name + ".wav");

        if (File.Exists(to)) return null;

        File.Move(from, to);

        return to;
    }

    /// <inheritdoc/>
    public void Drop(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try { File.Delete(path); }
        catch (Exception) { }
    }
}
