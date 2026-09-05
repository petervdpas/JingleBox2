using System;
using System.IO;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.SoundDevices.Interfaces;

namespace JingleBox2.SoundDevices;

/// <inheritdoc/>
public sealed class SoundDeviceHelp(ISafeFile? files = null) : ISoundDeviceHelp
{
    /// <summary>How a file is written whole, so a save that fails leaves the old page there.</summary>
    private readonly ISafeFile _files = files ?? new SafeFile();

    /// <inheritdoc/>
    /// <remarks>
    /// Written out rather than built, so the one file name this depends on can be found by
    /// looking for it, here and in any device folder anybody opens.
    /// </remarks>
    public string FileName => "help.md";

    /// <inheritdoc/>
    public string Read(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return "";

        try
        {
            string path = Path.Combine(folder, FileName);

            return File.Exists(path) ? File.ReadAllText(path) : "";
        }
        catch (Exception ex)
        {
            Log.Fault(LogArea.Machines, "A device's help could not be read from " + folder, ex);

            return "";
        }
    }

    /// <inheritdoc/>
    public void Write(string? folder, string? text)
    {
        if (string.IsNullOrWhiteSpace(folder)) return;

        string path = Path.Combine(folder, FileName);

        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                if (File.Exists(path)) File.Delete(path);

                return;
            }

            Directory.CreateDirectory(folder);

            _files.Write(path, text!);
        }
        catch (Exception ex)
        {
            Log.Fault(LogArea.Machines, "A device's help could not be written to " + path, ex);
        }
    }
}
