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

    /// <summary>Opens the panel as the rack page shows it: nothing playing, lamps greyed.</summary>
    public const string Idle = "--idle";

    /// <summary>True when these arguments ask for a panel rather than the application.</summary>
    public static bool Claims(string[] args) =>
        args != null && args.Any(a => string.Equals(a, Argument, StringComparison.Ordinal));

    /// <summary>Which machine was asked for, or the first one installed when none was named.</summary>
    private static Machine Wanted(string[] args)
    {
        int at = Array.IndexOf(args, Argument);
        string name = at >= 0 && at + 1 < args.Length ? args[at + 1] : "";

        return Machine.All.FirstOrDefault(m =>
                   string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? Machine.Installed.FirstOrDefault()
               ?? Machine.Plugin;
    }

    public static int Run(string[] args)
    {
        // The machines have to be read before one can be asked for by name. The application
        // does this at startup; nothing here did, so every --panel showed the plugin panel
        // whatever was typed after it.
        Tracker.Machines.MachineProjects.Keep(Tracker.Machines.MachineRegistry.Load());

        PreviewApp.Wanted = Wanted(args);
        PreviewApp.Playing = !args.Any(a => string.Equals(a, Idle, StringComparison.Ordinal));

        AppBuilder.Configure<PreviewApp>()
            .UsePlatformDetect()
            .WithInterFont()
            .StartWithClassicDesktopLifetime(Array.Empty<string>());

        return 0;
    }

    /// <summary>The recordings on the shelf, so the take pickers on a panel have something in them.</summary>
    private static System.Collections.ObjectModel.ObservableCollection<JingleBox2.Models.Recording> Takes()
    {
        var takes = new System.Collections.ObjectModel.ObservableCollection<JingleBox2.Models.Recording>();

        try
        {
            string home = JingleBox2.Audio.RecordingImport.Directory;

            if (!System.IO.Directory.Exists(home)) return takes;

            foreach (string path in System.IO.Directory.EnumerateFiles(home).OrderBy(p => p))
            {
                if (!JingleBox2.Audio.RecordingImport.Playable(path)) continue;

                takes.Add(new JingleBox2.Models.Recording
                {
                    Id = System.IO.Path.GetFileNameWithoutExtension(path),
                    FilePath = path,
                    Name = System.IO.Path.GetFileNameWithoutExtension(path)
                });
            }
        }
        catch (Exception)
        {
            // No shelf, or one that will not be read: an empty picker, not a crash.
        }

        return takes;
    }

    /// <summary>An audition that plays nothing, since a panel being looked at makes no sound.</summary>
    private sealed class Silent : IInstrumentAudition
    {
        public double Audition(TrackerInstrument instrument, Note note, int volume) => 0;

        public void Let(TrackerInstrument instrument, Note note) { }

        public void Silence(TrackerInstrument instrument) { }

        /// <summary>
        /// A cursor walking across the recording, the way Marching walks a playhead down a
        /// pattern. Nothing is sounding; the line is the point.
        /// </summary>
        public double SamplePosition(int track)
        {
            _step = (_step + 1) % Steps;

            return _step / (double)Steps;
        }

        /// <summary>Two seconds at the panel's forty milliseconds.</summary>
        private const int Steps = 50;

        private int _step;

        public IPluginInstrument? PluginFor(TrackerInstrument instrument) => null;
    }

    /// <summary>
    /// A tracker that is not there, walking a playhead down a pattern so the LOCATION lamps
    /// have something to show. Nothing is playing; the lamps are the point.
    /// </summary>
    private sealed class Marching : ITrackerPanel
    {
        private readonly System.Timers.Timer _clock = new(180) { AutoReset = true };

        public Marching()
        {
            _clock.Elapsed += (_, _) =>
            {
                PlayingLine = (PlayingLine + 1) % PatternLines;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlayingLine)));

                // A tune of sorts, so the keyboard has something to light while it is looked at.
                NotePlayed?.Invoke(this, (0, new Note(Octave * 12 + Steps[PlayingLine % Steps.Length]), 0d));
            };

            _clock.Start();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public int PlayingLine { get; private set; }

        public int PatternLines => 32;

        public int Octave { get; set; } = 4;

        public void FollowOctave(int octave)
        {
            Octave = octave;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Octave)));
        }

        public event EventHandler<(int Track, Note Note, double Seconds)>? NotePlayed;

        /// <summary>
        /// Somewhere for the keys to go: a line that wanders far enough out of the three
        /// octaves on show that the keyboard has to move to keep up with it.
        /// </summary>
        private static readonly int[] Steps = { 0, 7, 3, 10, 5, 12, 3, 8, 40, 43, 38, -20, -13, -17, 24, 19 };
    }

    private sealed class PreviewApp : Application
    {
        public static Machine Wanted { get; set; } = Machine.Plugin;

        /// <summary>False to see the panel with no track behind it, the way the rack shows it.</summary>
        public static bool Playing { get; set; } = true;

        public override void Initialize()
        {
            Styles.Add(new FluentTheme());

            // The application's own resources, so the panel is drawn in the colours it would
            // be inside it. Looking at it in different colours would prove nothing.
            foreach (var sheet in new[] { UI.ThemeManager.BaseSheet, UI.ThemeManager.SheetFor("Industrial Dark") })
            {
                try
                {
                    Styles.Add(new StyleInclude(new Uri("avares://JingleBox2/"))
                    {
                        Source = new Uri(sheet)
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

                // The machines that cut a recording into pieces have a picture of one on their
                // panel, and a panel with no picture on it cannot be judged. So the preview
                // reads takes the way the application does. It only ever reads them.
                var shelf = Takes();

                var designer = new TrackInstrumentDesigner(
                    0, instrument, new Silent(), () => { },
                    new JingleBox2.Audio.WaveformService(),
                    Playing ? new Marching() : null,
                    null,
                    shelf);

                // A machine that cuts recordings up, looked at with nothing on it, is a panel
                // with a blank rectangle in the middle of it. So the first take on the shelf is
                // put on it and chopped, the way Marching plays a tune so the lamps have
                // something to show. Put on the machine, not handed to the chop editor: that is
                // the only way in, here as anywhere else.
                if (shelf.Count > 0)
                {
                    string take = shelf[0].FilePath;

                    // A machine that holds one recording has neither zones nor pads: the take
                    // is a setting on it like any other, written the way its own panel writes
                    // it. Without this the Recording machine was the one machine the preview
                    // showed empty.
                    designer.Editor?.Values?.SetText("take", take);

                    designer.Editor?.Zones?.Selected?.Take(take);
                    designer.Editor?.Kit?.Selected?.Take(take);
                    designer.Editor?.Slices?.SliceCommand.Execute(null);
                }

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
                        Content = new InstrumentPanel { DataContext = designer }
                    }
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
