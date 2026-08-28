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
    /// <summary>
    /// The folder's name under the user's application data.
    /// </summary>
    /// <remarks>
    /// Written down here rather than typed where it is wanted, because a plugin's own process
    /// has to arrive at the same folder without being told, and a second spelling of it would
    /// mean a log nobody could find.
    /// </remarks>
    public const string Name = "JingleBox2";

    /// <summary>
    /// The folder itself. Not created here: asking where something is is not making it.
    /// </summary>
    /// <param name="appName">
    /// Which folder, so a test can point the whole application at somewhere temporary. The
    /// application itself never passes it.
    /// </param>
    public static string Path(string appName = Name) =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);
}
