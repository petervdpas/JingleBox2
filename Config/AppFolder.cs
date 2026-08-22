using System;
using System.IO;

namespace JingleBox2.Config;

/// <summary>
/// Where everything the app keeps lives: settings, songs, instruments, presets, the log.
/// </summary>
/// <remarks>
/// On its own, and knowing nothing, because things that are not the settings need it too. A
/// plugin's own process has to find the same folder to write to the same log, and it has no
/// settings to read and no business loading any.
/// </remarks>
public static class AppFolder
{
    public const string Name = "JingleBox2";

    /// <summary>The folder itself. Not created here: asking where something is is not making it.</summary>
    public static string Path(string appName = Name) =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);
}
