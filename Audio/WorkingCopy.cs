using System;
using System.IO;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// A folder under the application folder, beside the recorder's own scratchpad and for the same
/// reasons: a take is hundreds of megabytes, it has to be on the same disc as everything else
/// here, and somebody looking for what a crash interrupted would look beside their recordings.
///
/// The file carries the process's own number, so a run that ended badly can be told from one
/// that is going on now. What is swept on the way in is everything that is not this run's, which
/// is exactly the leavings of a crash.
/// </remarks>
public sealed class WorkingCopy : IWorkingCopy
{
    /// <summary>What the folder is called under the application folder.</summary>
    public const string FolderName = "edits";

    /// <summary>Where the copies live.</summary>
    private readonly string _folder;

    /// <summary>How a file is written over another one without ever leaving half of it.</summary>
    private readonly ISafeFile _files;

    /// <summary>The take on the shelf this copy came from.</summary>
    private string? _take;

    /// <inheritdoc/>
    public string? Path { get; private set; }

    /// <summary>Names the folder under the application folder.</summary>
    /// <param name="folder">Where the application keeps its things, defaulted to the real one.</param>
    /// <param name="files">How a file is written whole, defaulted to the real one.</param>
    public WorkingCopy(IAppFolder? folder = null, ISafeFile? files = null)
    {
        _folder = System.IO.Path.Combine((folder ?? new AppFolder()).Path(), FolderName);
        _files = files ?? new SafeFile();

        Directory.CreateDirectory(_folder);
    }

    /// <inheritdoc/>
    public bool IsOpen => Path != null && File.Exists(Path);

    /// <inheritdoc/>
    public string? Open(string takePath)
    {
        Close();

        if (string.IsNullOrWhiteSpace(takePath) || !File.Exists(takePath)) return null;

        Sweep();

        Directory.CreateDirectory(_folder);

        string copy = System.IO.Path.Combine(
            _folder, "edit-" + Environment.ProcessId + System.IO.Path.GetExtension(takePath));

        File.Copy(takePath, copy, overwrite: true);

        _take = takePath;
        Path = copy;

        return copy;
    }

    /// <inheritdoc/>
    public bool Fresh()
    {
        if (_take == null || Path == null || !File.Exists(_take)) return false;

        File.Copy(_take, Path, overwrite: true);

        return true;
    }

    /// <inheritdoc/>
    public bool Keep()
    {
        if (_take == null || Path == null || !File.Exists(Path)) return false;

        _files.Write(_take, into =>
        {
            using var copy = File.OpenRead(Path);

            copy.CopyTo(into);
        });

        return true;
    }

    /// <inheritdoc/>
    public void Close()
    {
        if (Path != null)
        {
            try { File.Delete(Path); }
            catch (Exception) { }
        }

        Path = null;
        _take = null;
    }

    /// <summary>
    /// Clears out what another run left behind.
    /// </summary>
    /// <remarks>
    /// A file carrying another run's number is a copy nobody is working on any more, since this
    /// folder is emptied on the way out of every take: what is in it belongs to a run that ended
    /// badly. A file that will not delete is passed over, because a folder that could not be
    /// tidied is not a reason to refuse to open a take.
    /// </remarks>
    private void Sweep()
    {
        if (!Directory.Exists(_folder)) return;

        string mine = "edit-" + Environment.ProcessId;

        foreach (string file in Directory.GetFiles(_folder))
        {
            if (System.IO.Path.GetFileNameWithoutExtension(file) == mine) continue;

            try { File.Delete(file); }
            catch (Exception) { }
        }
    }
}
