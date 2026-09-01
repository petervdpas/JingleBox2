using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;
using System;
using System.ComponentModel;
using System.Linq;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.ViewModels.Interfaces;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Machines;
using JingleBox2.Tracker.Machines.Interfaces;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio;

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
    /// <summary>The one door recordings come in through. Holds nothing, so one is enough.</summary>
    private static readonly IRecordingImport _import = new RecordingImport();

    /// <summary>The machines folder on disc.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly IMachineRegistry Registry = new MachineRegistry();

    /// <summary>The machines this preview has, filled once before anything is drawn.</summary>
    /// <remarks>
    /// The preview is its own little application, so it composes its own rather than being handed
    /// the one the main window builds. It is read by whichever panel is being previewed, which is
    /// not the method that fills it, so it cannot be a local.
    /// </remarks>
    private static readonly IMachineProjects Projects = new MachineProjects();

    /// <summary>The switch that takes over startup, followed by the machine's name.</summary>
    public const string Argument = "--panel";

    /// <summary>Opens the panel as the rack page shows it: nothing playing, lamps greyed.</summary>
    public const string Idle = "--idle";

    /// <summary>
    /// Opens it in the other mouse mode, with a couple of parameters already spoken for.
    /// </summary>
    /// <remarks>
    /// For looking at what pointing at a control does, which is a thing to be photographed
    /// rather than described. There is no controller here and nothing is written down: the
    /// links are made up so that the rings have something to be around.
    /// </remarks>
    public const string Link = "--link";

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

    /// <summary>
    /// Opens the window and runs until it is closed. Nothing else in the application is started.
    /// </summary>
    /// <remarks>
    /// The machines are read first, because one cannot be asked for by name until they are. The
    /// application does this at startup and nothing here did, so every <c>--panel</c> showed the
    /// plugin panel whatever was typed after it.
    ///
    /// With <see cref="Link"/> two of the machine's own parameters are made up as links, so the
    /// quiet rings have somewhere to be. There is no controller here and nothing is written down.
    /// </remarks>
    public static int Run(string[] args)
    {
        Projects.Keep(Registry.Load());

        PreviewApp.Wanted = Wanted(args);
        PreviewApp.Playing = !args.Any(a => string.Equals(a, Idle, StringComparison.Ordinal));

        if (args.Any(a => string.Equals(a, Link, StringComparison.Ordinal)))
        {
            var pretend = new System.Collections.Generic.List<Midi.ControlMapping>();

            foreach (var parameter in Projects.For(PreviewApp.Wanted.SlotId)
                                          ?.Parameters.Take(2) ?? System.Linq.Enumerable.Empty<JingleBox2.Machines.MachineParameter>())
            {
                pretend.Add(new Midi.ControlMapping
                {
                    Kind = Midi.Enums.ControlKind.Instrument,
                    Machine = PreviewApp.Wanted.SlotId,
                    Key = parameter.Key
                });
            }

            var link = new Midi.ControlLink(pretend, () => { });
            link.UseThis();
            link.IsLinking = true;
        }

        AppBuilder.Configure<PreviewApp>()
            .UsePlatformDetect()
            .WithInterFont()
            .StartWithClassicDesktopLifetime(Array.Empty<string>());

        return 0;
    }

    /// <summary>
    /// The recordings on the shelf, so the take pickers on a panel have something in them.
    /// </summary>
    /// <remarks>
    /// Only ever read. No shelf, or one that will not be read, is an empty picker rather than a
    /// crash: this window exists to be looked at, and a panel with no takes on it is still a
    /// panel.
    /// </remarks>
    private static System.Collections.ObjectModel.ObservableCollection<JingleBox2.Audio.Records.Recording> Takes()
    {
        var takes = new System.Collections.ObjectModel.ObservableCollection<JingleBox2.Audio.Records.Recording>();

        try
        {
            string home = _import.Directory;

            if (!System.IO.Directory.Exists(home)) return takes;

            foreach (string path in System.IO.Directory.EnumerateFiles(home).OrderBy(p => p))
            {
                if (!_import.Playable(path)) continue;

                takes.Add(new JingleBox2.Audio.Records.Recording
                {
                    Id = System.IO.Path.GetFileNameWithoutExtension(path),
                    FilePath = path,
                    Name = System.IO.Path.GetFileNameWithoutExtension(path)
                });
            }
        }
        catch (Exception)
        {
        }

        return takes;
    }

    /// <summary>An audition that plays nothing, since a panel being looked at makes no sound.</summary>
    private sealed class Silent : IInstrumentAudition
    {
        /// <inheritdoc/>
        /// <remarks>Nothing sounds, and nothing is held, so the length is nought.</remarks>
        public double Audition(TrackerInstrument instrument, Note note, int volume) => 0;

        /// <inheritdoc/>
        public void Let(TrackerInstrument instrument, Note note) { }

        /// <inheritdoc/>
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

        /// <summary>How far round the walk has got, since there is no recording to be inside.</summary>
        private int _step;

        /// <inheritdoc/>
        /// <remarks>No plugin is ever loaded here, so a panel that asks gets nothing.</remarks>
        public IPluginParameters? PluginFor(TrackerInstrument instrument) => null;
    }

    /// <summary>
    /// A tracker that is not there, walking a playhead down a pattern so the LOCATION lamps
    /// have something to show. Nothing is playing; the lamps are the point.
    /// </summary>
    private sealed class Marching : ITrackerPanel
    {
        /// <summary>Slow enough to be watched, which is the only thing this is for.</summary>
        private readonly System.Timers.Timer _clock = new(180) { AutoReset = true };

        /// <summary>
        /// Starts the walk and a tune of sorts, so the keyboard has something to light while it
        /// is being looked at.
        /// </summary>
        public Marching()
        {
            _clock.Elapsed += (_, _) =>
            {
                PlayingLine = (PlayingLine + 1) % PatternLines;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlayingLine)));

                NotePlayed?.Invoke(this, (0, new Note(Octave * 12 + Steps[PlayingLine % Steps.Length]), 0d));
            };

            _clock.Start();
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <inheritdoc/>
        public int PlayingLine { get; private set; }

        /// <inheritdoc/>
        /// <remarks>A pattern length like any other, since nothing here holds a song.</remarks>
        public int PatternLines => 32;

        /// <inheritdoc/>
        public int Octave { get; set; } = 4;

        /// <inheritdoc/>
        public void FollowOctave(int octave)
        {
            Octave = octave;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Octave)));
        }

        /// <inheritdoc/>
        public event EventHandler<(int Track, Note Note, double Seconds)>? NotePlayed;

        /// <summary>
        /// Somewhere for the keys to go: a line that wanders far enough out of the three
        /// octaves on show that the keyboard has to move to keep up with it.
        /// </summary>
        private static readonly int[] Steps = { 0, 7, 3, 10, 5, 12, 3, 8, 40, 43, 38, -20, -13, -17, 24, 19 };
    }

    /// <summary>The application this window runs inside: the theme, and one window in it.</summary>
    private sealed class PreviewApp : Application
    {
        /// <summary>Which machine's panel is opened, set before the application is built.</summary>
        public static Machine Wanted { get; set; } = Machine.Plugin;

        /// <summary>False to see the panel with no track behind it, the way the rack shows it.</summary>
        public static bool Playing { get; set; } = true;

        /// <summary>
        /// Loads the application's own resources, so the panel is drawn in the colours it would
        /// be inside it. Looking at it in different colours would prove nothing.
        /// </summary>
        /// <remarks>
        /// A sheet that will not load leaves the panel in the wrong colours rather than stopping
        /// the window, which is still worth looking at.
        /// </remarks>
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());

            foreach (var sheet in new[] { UI.ThemeSwitch.BaseSheet, UI.ThemeSwitch.SheetFor("Industrial Dark") })
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
                }
            }
        }

        /// <summary>
        /// Builds the one window: the machine as an instrument, on a designer with nothing
        /// behind it, inside a scroll viewer so a tall panel can still be reached.
        /// </summary>
        /// <remarks>
        /// The machines that cut a recording into pieces have a picture of one on their panel,
        /// and a panel with no picture on it cannot be judged, so the preview reads takes the way
        /// the application does and puts the first one on the machine. Put on the machine rather
        /// than handed to the chop editor: that is the only way in, here as anywhere else. A
        /// machine that holds one recording has neither zones nor pads, and the take is a setting
        /// on it like any other; without that line the Recording machine was the one machine the
        /// preview showed empty.
        ///
        /// On top and in the corner, because the point of this window is to be photographed. A
        /// window the desktop is free to put behind something else is one the camera gets the
        /// something else of.
        /// </remarks>
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var instrument = TrackerInstrument.CreateOn(Wanted, Wanted.Name);

                var shelf = Takes();

                var designer = new TrackInstrumentDesigner(
                    0, instrument, Projects, new Silent(), () => { },
                    new JingleBox2.Audio.WaveformService(),
                    Playing ? new Marching() : null,
                    null,
                    shelf);

                if (shelf.Count > 0)
                {
                    string take = shelf[0].FilePath;

                    designer.Editor?.Values?.SetText("take", take);

                    designer.Editor?.Zones?.Selected?.Take(take);
                    designer.Editor?.Kit?.Selected?.Take(take);
                    designer.Editor?.Slices?.SliceCommand.Execute(null);
                }

                desktop.MainWindow = new Window
                {
                    Title = Wanted.Name + " panel",
                    SizeToContent = SizeToContent.WidthAndHeight,

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
