namespace JingleBox2.Files.Interfaces;

/// <summary>
/// Where everything the app keeps lives: settings, songs, instruments, presets, the log.
/// </summary>
/// <remarks>
/// On its own, and knowing nothing, because things that are not the settings need it too. A
/// plugin's own process has to find the same folder to write to the same log, and it has no
/// settings to read and no business loading any.
///
/// It asks the operating system where a user's application data is, which is the whole reason
/// it is a seam: the answer is different on the two systems this runs on and is different again
/// under a test, so a caller that reaches for the real folder cannot be asked what it would do
/// with another one.
/// </remarks>
public interface IAppFolder
{
    /// <summary>
    /// The folder's name under the user's application data.
    /// </summary>
    /// <remarks>
    /// Written down in one place rather than typed where it is wanted, because a plugin's own
    /// process has to arrive at the same folder without being told, and a second spelling of it
    /// would mean a log nobody could find.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// The folder itself. Not created here: asking where something is is not making it.
    /// </summary>
    /// <param name="appName">
    /// Which folder, so a test can point the whole application at somewhere temporary. The
    /// application itself never passes it.
    /// </param>
    string Path(string appName);

    /// <summary>The folder under <see cref="Name"/>, which is what the application always asks for.</summary>
    string Path();
}
