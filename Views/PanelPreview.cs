using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using JingleBox2.Audio.Plugins;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;
using System;
using System.ComponentModel;
using System.Linq;

namespace JingleBox2.Views;

/// <summary>
/// One machine's panel, in a window of its own, with nothing else running.
/// </summary>
/// <remarks>
/// A front panel is drawn code, and drawn code has to be looked at. Reaching it through the
/// application means starting the audio, opening a tab and picking an instrument before a
/// single pixel can be judged, which is a slow way to answer "is that knob the right size".
/// This opens the panel and nothing else: no engine, no library, no song.
///
/// Started with <c>--panel &lt;machine&gt;</c>. A way of looking rather than a way of working:
/// the controls move and the patch changes, but nothing is played and nothing is kept.
/// </remarks>
public static class PanelPreview
{
    public const string Argument = "--panel";

    /// <summary>Opens the panel as the library page shows it: nothing playing, lamps greyed.</summary>
    public const string Idle = "--idle";

    /// <summary>True when these arguments ask for a panel rather than the application.</summary>
    public static bool Claims(string[] args) =>
        args != null && args.Any(a => string.Equals(a, Argument, StringComparison.Ordinal));

    /// <summary>Which machine was asked for, or Ouroboros when none was named.</summary>
    private static Machine Wanted(string[] args)
    {
        int at = Array.IndexOf(args, Argument);
        string name = at >= 0 && at + 1 < args.Length ? args[at + 1] : "";

        return Machine.All.FirstOrDefault(m =>
            string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Machine.Ouroboros;
    }

    public static int Run(string[] args)
    {
        PreviewApp.Wanted = Wanted(args);
        PreviewApp.Playing = !args.Any(a => string.Equals(a, Idle, StringComparison.Ordinal));

        AppBuilder.Configure<PreviewApp>()
            .UsePlatformDetect()
            .WithInterFont()
            .StartWithClassicDesktopLifetime(Array.Empty<string>());

        return 0;
    }

    /// <summary>An audition that plays nothing, since a panel being looked at makes no sound.</summary>
    private sealed class Silent : IInstrumentAudition
    {
        public void Audition(TrackerInstrument instrument, Note note, int volume) { }

        public IPluginInstrument? PluginFor(TrackerInstrument instrument) => null;
    }

    /// <summary>
    /// A tracker that is not there, walking a playhead down a pattern so the LOCATION lamps
    /// have something to show. Nothing is playing; the lamps are the point.
    /// </summary>
    private sealed class Marching : ITrackerLocation
    {
        private readonly System.Timers.Timer _clock = new(180) { AutoReset = true };

        public Marching()
        {
            _clock.Elapsed += (_, _) =>
            {
                PlayingLine = (PlayingLine + 1) % PatternLines;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlayingLine)));
            };

            _clock.Start();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public int PlayingLine { get; private set; }

        public int PatternLines => 32;
    }

    private sealed class PreviewApp : Application
    {
        public static Machine Wanted { get; set; } = Machine.Ouroboros;

        /// <summary>False to see the panel with no track behind it, the way the library shows it.</summary>
        public static bool Playing { get; set; } = true;

        public override void Initialize()
        {
            Styles.Add(new FluentTheme());

            // The application's own resources, so the panel is drawn in the colours it would
            // be inside it. Looking at it in different colours would prove nothing.
            foreach (var theme in new[] { "Base", "Industrial" })
            {
                try
                {
                    Styles.Add(new StyleInclude(new Uri("avares://JingleBox2/"))
                    {
                        Source = new Uri("avares://JingleBox2/Themes/" + theme + ".axaml")
                    });
                }
                catch (Exception)
                {
                    // A theme that will not load is a panel in the wrong colours, not a crash.
                }
            }
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var instrument = TrackerInstrument.CreateOn(Wanted, Wanted.Name);
                var designer = new TrackInstrumentDesigner(
                    0, instrument, new Silent(), () => { }, null, Playing ? new Marching() : null);

                desktop.MainWindow = new Window
                {
                    Title = Wanted.Name + " panel",
                    SizeToContent = SizeToContent.WidthAndHeight,

                    // On top and in the corner, because the point of this window is to be
                    // photographed. A window the desktop is free to put behind something else
                    // is one the camera gets the something else of.
                    Topmost = true,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Position = new PixelPoint(0, 0),
                    Content = new ScrollViewer
                    {
                        Padding = new Thickness(18),
                        Content = new InstrumentEditor { DataContext = designer }
                    }
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
