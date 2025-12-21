using Avalonia;
using System;
using System.IO;

namespace JingleBox2;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
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
