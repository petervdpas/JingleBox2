using Avalonia;
using System;
using System.IO;

namespace JingleBox2;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Started as a plugin's process rather than as the application. There is no window, no
        // audio device and no configuration in this mode: it loads one plugin, serves it, and
        // goes away. See JingleBox2.Audio.Plugins.Bridge.PluginHostProcess.
        if (Audio.Plugins.Bridge.PluginHostProcess.Claims(args))
        {
            Environment.Exit(Audio.Plugins.Bridge.PluginHostProcess.Run(args));
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

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
