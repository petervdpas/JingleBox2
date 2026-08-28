using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Controllers.Interfaces;

namespace JingleBox2.Controllers;

/// <inheritdoc/>
public sealed class ControllerFolder : IControllerFolder
{
    /// <inheritdoc cref="IControllerFolder.Name"/>
    public const string Name = "controllers";

    /// <inheritdoc/>
    string IControllerFolder.Name => Name;

    /// <inheritdoc/>
    public string Shipped => Path.Combine(AppContext.BaseDirectory, Name);

    /// <inheritdoc/>
    public string Installed => Path.Combine(new Files.AppFolder().Path(), Name);

    /// <summary>What has already been offered, so a new file can arrive and a deleted one cannot.</summary>
    private const string OfferedName = "offered.txt";

    /// <inheritdoc/>
    public void FirstRun()
    {
        try
        {
            bool fresh = !Directory.Exists(Installed);

            Directory.CreateDirectory(Installed);

            if (!Directory.Exists(Shipped)) return;

            var offered = Offered(fresh);
            bool moved = false;

            foreach (string file in Directory.GetFiles(Shipped))
            {
                string name = Path.GetFileName(file);

                if (string.Equals(name, OfferedName, StringComparison.OrdinalIgnoreCase)) continue;
                if (!offered.Add(name)) continue;

                moved = true;

                try
                {
                    File.Copy(file, Path.Combine(Installed, name), overwrite: false);

                    Log.Write(LogArea.Midi, () => "controllers: '" + name + "' is new, so it has been put in");
                }
                catch (Exception bad)
                {
                    Log.Write(LogArea.Midi, () => "controllers: '" + name + "' could not be copied: " + bad.Message);
                }
            }

            if (moved) Remember(offered);
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Midi, () => "controllers: cannot prepare '" + Installed + "': " + bad.Message);
        }
    }

    /// <summary>
    /// The files this installation has been offered before now.
    /// </summary>
    /// <remarks>
    /// A folder with no record is a folder from before there was one, and everything in it
    /// counts as offered. Otherwise the first start after this lands would put back every file
    /// anybody had ever deleted.
    /// </remarks>
    private HashSet<string> Offered(bool fresh)
    {
        var offered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string record = Path.Combine(Installed, OfferedName);

        try
        {
            if (File.Exists(record))
            {
                foreach (string line in File.ReadAllLines(record))
                    if (line.Trim() is { Length: > 0 } one) offered.Add(one);

                return offered;
            }

            if (!fresh)
                foreach (string file in Directory.GetFiles(Installed))
                    offered.Add(Path.GetFileName(file));
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Midi, () => "controllers: cannot read what has been offered: " + bad.Message);
        }

        return offered;
    }

    /// <summary>Writes down what has been offered, so the next start does not offer it again.</summary>
    /// <remarks>
    /// Sorted, because this is a file a person may open when they want to know why a codec they
    /// deleted has not come back.
    /// </remarks>
    private void Remember(HashSet<string> offered)
    {
        try
        {
            File.WriteAllLines(Path.Combine(Installed, OfferedName), offered.OrderBy(one => one, StringComparer.Ordinal));
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Midi, () => "controllers: cannot write what has been offered: " + bad.Message);
        }
    }

    /// <inheritdoc/>
    public bool Like(string pattern, string text)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(text)) return false;

        var parts = pattern.Split('*');

        if (parts.Length == 1)
            return text.Contains(pattern, StringComparison.OrdinalIgnoreCase);

        int at = 0;

        for (int part = 0; part < parts.Length; part++)
        {
            if (parts[part].Length == 0) continue;

            int found = text.IndexOf(parts[part], at, StringComparison.OrdinalIgnoreCase);
            if (found < 0) return false;
            if (part == 0 && found != 0) return false;

            at = found + parts[part].Length;
        }

        return parts[^1].Length == 0 || text.EndsWith(parts[^1], StringComparison.OrdinalIgnoreCase);
    }
}
