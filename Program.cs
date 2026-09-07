using Avalonia;
using System;
using System.IO;

namespace JingleBox2;

/// <summary>
/// Where the executable starts, and where it decides which of three things it is this time.
/// </summary>
/// <remarks>
/// The same binary is the application, one plugin's host process, and one machine's panel on
/// its own. Two of those are asked about before anything else happens, because neither wants a
/// window, a sound card or the configuration, and building any of that first would cost a
/// plugin process its startup time and would have a preview fighting the application over the
/// application folder.
/// </remarks>
class Program
{
    /// <summary>
    /// Works out what this process is for, and runs it.
    /// </summary>
    /// <remarks>
    /// The first claim wins, and the two special modes exit rather than returning, so nothing
    /// below them can run by accident. Started as a plugin's process there is no window, no
    /// audio device and no configuration: it loads one plugin, serves it, and goes away
    /// (see <see cref="Audio.Plugins.Bridge.PluginHostProcess"/>). Started as a panel preview
    /// it draws one machine's front panel and nothing else.
    ///
    /// Anything thrown on the way up is written down before it is rethrown, because a failure
    /// this early has no window to report itself in and would otherwise be a process that
    /// started and vanished. Where it is written down is <see cref="Note"/>, which cannot fail.
    /// </remarks>
    [STAThread]
    public static void Main(string[] args)
    {
        if (Audio.Plugins.Bridge.PluginHostProcess.Claims(args))
        {
            Environment.Exit(Audio.Plugins.Bridge.PluginHostProcess.Run(args));
            return;
        }

        if (Views.PanelPreview.Claims(args))
        {
            Environment.Exit(Views.PanelPreview.Run(args));
            return;
        }

        Note($"Main entered {DateTime.Now:O}");

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Note($"FATAL: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Writes one line into <c>startup.log</c> in the application folder, and never throws.
    /// </summary>
    /// <remarks>
    /// It was written to <c>startup.log</c> with no folder in front of it, which is a path
    /// relative to wherever the process happened to be started, and that is not somewhere this
    /// program may write. Installed from a package the program lives under <c>/opt</c>, which is
    /// the system's; a desktop launcher starts it at the root of the disc or at the user's home,
    /// neither of which is where a log belongs and the first of which is refused outright. So
    /// the first line the application wrote threw <see cref="UnauthorizedAccessException"/>
    /// before the toolkit had been asked for anything, and the whole of the symptom was a
    /// program that would not start. It ran from a checkout throughout, because a build tree is
    /// somewhere its owner can write.
    ///
    /// The application folder is where everything else this program keeps already lives, it is
    /// the same folder whatever started the process and from where, and
    /// <see cref="Files.AppFolder"/> knows it without reading the settings, which is what lets
    /// it be asked this early.
    ///
    /// Nothing here is allowed to throw, which is the other half of the same fault: a note
    /// about starting was the reason the application did not start. A folder that cannot be
    /// made, a disc that is full and a file somebody else holds open are all the same answer,
    /// which is that this run goes unrecorded and the application carries on. The catch is
    /// deliberately over everything rather than over the write alone, since working out where
    /// to write reads the environment and that can fail on its own.
    /// </remarks>
    /// <param name="line">What to write down, with no line ending on it.</param>
    private static void Note(string line)
    {
        try
        {
            string folder = new Files.AppFolder().Path();
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "startup.log"), line + Environment.NewLine);
        }
        catch
        {
        }
    }

    /// <summary>
    /// The toolkit's configuration, kept apart from <see cref="Main"/> because the designer
    /// calls it by name to build a preview without running the application.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
