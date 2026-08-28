using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using JingleBox2.UI;

namespace JingleBox2;

/// <summary>
/// The application object: the styles everything is drawn with, and the one window.
/// </summary>
/// <remarks>
/// Nothing about the pads, the tracker or the sound lives here. This is the two moments the
/// toolkit offers before there is a window to hang anything on, which is why the plugin run
/// loop is pointed at the drawing thread here and nowhere else.
/// </remarks>
public partial class App : Application
{
    /// <summary>
    /// Loads the styles and puts the saved theme on before anything is drawn.
    /// </summary>
    /// <remarks>
    /// The theme goes on here rather than when the window opens, or the first frame would be
    /// drawn in the default colours and repainted in the chosen ones.
    /// </remarks>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        ThemeManager.Apply(ThemeManager.Default);
    }

    /// <summary>
    /// Points the plugin run loop at the drawing thread, then opens the window.
    /// </summary>
    /// <remarks>
    /// Plugins with interfaces of their own have to be called on the thread their windows live
    /// on. Until this is said, the run loop pumps on a thread of its own, which is right for
    /// effects with no window and wrong the moment one has. Said before the window is made,
    /// since a plugin can be loaded by the first song that opens.
    /// </remarks>
    public override void OnFrameworkInitializationCompleted()
    {
        JingleBox2.Audio.Plugins.PluginRunLoop.DriveWith(round => Dispatcher.UIThread.Post(round));

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://JingleBox2/Assets/icon.ico")))
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
