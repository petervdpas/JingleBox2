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
    /// Anything thrown on the way up is written to startup.log before it is rethrown, because
    /// a failure this early has no window to report itself in and would otherwise be a process
    /// that started and vanished.
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

        File.AppendAllText("startup.log", $"Main entered {DateTime.Now:O}{Environment.NewLine}");

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            File.AppendAllText("startup.log", $"FATAL: {ex}{Environment.NewLine}");
            throw;
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
