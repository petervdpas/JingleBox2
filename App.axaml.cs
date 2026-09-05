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

        ThemeSwitch.Apply(ThemeSwitch.Default);
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
            var splash = new Views.SplashWindow();
            splash.Show();

            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            var since = DateTime.UtcNow;

            Dispatcher.UIThread.Post(() => Open(desktop, splash, since), DispatcherPriority.Background);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Builds the one window, puts it up, and takes the splash down.
    /// </summary>
    /// <remarks>
    /// Posted at <see cref="DispatcherPriority.Background"/> rather than called, and that is the
    /// whole of what makes the splash worth having. Building the window is a long stretch of
    /// work on the drawing thread, so called here it would run before the splash had been
    /// painted once and the splash would appear as the window did: a flash, and a startup that
    /// still looked like nothing was happening. Posted behind the frame that draws it, the
    /// splash is up first and stays up for as long as the work takes.
    ///
    /// The splash is closed after the window is shown rather than before it is built. Closing
    /// the last window is what ends the application, so the two overlap on purpose, and
    /// <see cref="ShutdownMode.OnMainWindowClose"/> is set for the same reason: with it left on
    /// the last window, a splash that closed a moment early would take the application with it.
    ///
    /// Anything thrown while the window is built takes the splash down before it goes up, so a
    /// startup that fails is not a purple rectangle sitting there for ever. What it throws is
    /// left alone: <c>Program.Main</c> writes it to startup.log, which is where a failure this
    /// early is read.
    /// </remarks>
    /// <param name="desktop">The lifetime, which is told which window is the main one.</param>
    /// <param name="splash">What is on screen until there is a window to replace it.</param>
    /// <param name="since">When the splash went up, which is what the shortest stay is measured from.</param>
    private static void Open(
        IClassicDesktopStyleApplicationLifetime desktop, Views.SplashWindow splash, DateTime since)
    {
        MainWindow main;

        try
        {
            main = new MainWindow(splash)
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://JingleBox2/Assets/icon.ico")))
            };

            desktop.MainWindow = main;
        }
        catch (Exception)
        {
            splash.Close();
            throw;
        }

        splash.Doing("Ready");

        var left = SplashLeast - (DateTime.UtcNow - since);

        if (left <= TimeSpan.Zero)
        {
            Swap(main, splash);
            return;
        }

        DispatcherTimer.RunOnce(() => Swap(main, splash), left);
    }

    /// <summary>
    /// The shortest the splash stays up, however quickly the application is ready.
    /// </summary>
    /// <remarks>
    /// A floor and not a wait. The window is built while this runs, so on a machine that takes
    /// longer than this the splash is gone the moment it is ready and nothing has been added to
    /// the startup; it only costs anything on a machine fast enough that the splash would
    /// otherwise be a flash.
    ///
    /// Which is the whole reason for it: a splash nobody can read is worse than none, since it
    /// reads as the window having flickered. Long enough to be looked at rather than glimpsed.
    /// </remarks>
    private static readonly TimeSpan SplashLeast = TimeSpan.FromSeconds(2.4);

    /// <summary>
    /// Puts the window up and takes the splash down, in that order.
    /// </summary>
    /// <remarks>
    /// That order and not the other one. Closing the last window is what ends the application,
    /// so the two are on screen together for an instant on purpose.
    /// </remarks>
    /// <param name="main">The window everything is in.</param>
    /// <param name="splash">What has been standing in for it.</param>
    private static void Swap(Window main, Window splash)
    {
        main.Show();
        splash.Close();
    }
}
