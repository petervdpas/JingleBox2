using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using JingleBox2.Audio;
using JingleBox2.Audio.Routing;
using JingleBox2.Config;
using JingleBox2.Midi;
using JingleBox2.ViewModels;
using System;
using System.Reflection;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker.Machines;
using JingleBox2.Tracker.Machines.Interfaces;

namespace JingleBox2;

/// <summary>
/// The one window, and the place every long-lived service in the application is made.
/// </summary>
/// <remarks>
/// The services are built here rather than injected because there is nothing above this to
/// inject them from: the window is what the application lifetime hands control to. They are all
/// disposed when it closes, which is the only shutdown this program has.
///
/// The window itself answers three kinds of key, and the reason all three are here rather than
/// on a page is the same each time: they are worth pressing from wherever you happen to be. The
/// space bar works the transport, Ctrl+R records whatever the page you are on records, and the
/// shortcut map delivers save, delete, undo and redo to whatever has the keyboard.
/// </remarks>
public partial class MainWindow : Window
{
    /// <summary>The machines folder on disc.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly IMachineRegistry Registry = new MachineRegistry();

    /// <summary>The settings file, read once on the way up and written whenever something moves.</summary>
    private readonly ConfigStore _store = new("JingleBox2");

    /// <summary>The pads' sound. The tracker shares its device rather than opening a second one.</summary>
    private readonly BassAudioEngine _audio;

    /// <summary>The MIDI ports, whose messages reach the pads, the tracker, or both.</summary>
    private readonly IMidiService _midi = new MidiService();

    /// <summary>What makes a take, and what puts one on the shelf.</summary>
    private readonly IRecordingService _recording = new RecordingService();

    /// <summary>What turns a recording into the picture of one.</summary>
    private readonly IWaveformService _waveform = new WaveformService();

    /// <summary>
    /// What the machine can be asked to record: a graph to patch on Linux, an output's playback
    /// on Windows, and nothing at all elsewhere, in which case the RECORD page hides the picker.
    /// </summary>
    private readonly IAudioRouting _routing;

    /// <summary>
    /// The settings as they stand, kept so the window's own size can be written back into them
    /// without reading the file again.
    /// </summary>
    private AppConfig? _cfg;

    /// <summary>
    /// Gathers a drag of the window's edge into one write. Resizing announces itself
    /// continuously, and a settings file written per pixel is a settings file written a
    /// thousand times for one gesture.
    /// </summary>
    private DispatcherTimer? _saveWindowTimer;

    /// <summary>Set once the startup size has been applied, so layout does not trigger saves.</summary>
    private bool _windowRestored;

    /// <summary>What is being held down, so a held key is one press. See <see cref="UI.HeldKeys"/>.</summary>
    private readonly UI.HeldKeys _held = new();

    /// <summary>
    /// Set while the transport has the space bar, so the key coming up again is swallowed too.
    /// </summary>
    /// <remarks>
    /// A button clicks on the space key coming up, and does not check that it ever saw the key
    /// go down. Taking only the key-down therefore stops the button pressing and still lets it
    /// click: open a song, press space, and the song plays and the picker opens behind it.
    /// </remarks>
    private bool _tookSpace;

    /// <summary>How much of the window is spent above the pads: the theme, the device and the tabs.</summary>
    private const double HeaderHeight = 140;

    /// <summary>How big a pad wants to be, which is what keeps the matrix roughly square.</summary>
    private const double PadSize = 120;

    /// <summary>The room around each pad, counted into the size the matrix asks for.</summary>
    private const double PadMargin = 20;

    /// <summary>What the window itself takes at the edges, over and above the matrix.</summary>
    private const double WindowPadding = 48;

    /// <summary>
    /// The floor the window opens at.
    /// </summary>
    /// <remarks>
    /// The matrix alone gives a cramped window for the small grids, which are the common ones:
    /// two by two is four pads and a window nobody could work in. Larger matrices still grow
    /// past it.
    /// </remarks>
    private const double DefaultWidth = 800;

    /// <inheritdoc cref="DefaultWidth"/>
    private const double DefaultHeight = 800;

    /// <summary>
    /// How long a resize has to settle before it is written down. Long enough to outlast a
    /// drag, short enough that letting go and quitting still keeps the size.
    /// </summary>
    private static readonly TimeSpan WindowSaveDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Reads the settings, builds every service the application has, and puts the window on
    /// screen at the size it was last left at.
    /// </summary>
    /// <remarks>
    /// The order here is the point of it. The log is opened before anything else that might
    /// have something to say, off unless the setting asks for it and free when off; the crash
    /// account is watched next, because a run that ends badly is nearly always a run nobody was
    /// logging, which is the whole reason it keeps its own short record. The machines are read
    /// before anything shows one, since what a rack row is called and what colour it wears come
    /// off the machines themselves, and they are kept rather than counted and dropped: a panel
    /// drawn from a machine's own description asks for the machine, and this is the one moment
    /// the disc is read.
    ///
    /// The three key handlers are all put on the window: the space bar and the key-up are
    /// tunnelled so nothing focused can spend them first, and the shortcut map and the pointer
    /// mode are listened for on every window this application opens, dialogs included, because
    /// a dialog is where the focus is and so is where the answer should come from.
    ///
    /// Keys held while the window loses focus are released somewhere else and this window never
    /// hears about it, so what is held is forgotten on deactivation rather than left set for
    /// ever.
    ///
    /// The title carries the version with anything after a plus cut off, since a build made
    /// from a working tree names itself "1.0.12+abc123def" and a commit hash in a title bar is
    /// nothing anybody reading it can use.
    /// </remarks>
    public MainWindow()
    {
        InitializeComponent();

        _routing = new AudioRoutingFactory().Create(_recording);

        var cfg = _store.LoadOrCreateDefault();

        Audio.RealtimeThread.Wants(cfg.RealtimeAudio);

        Diagnostics.Log.Open(new Files.AppFolder().Path(), cfg.WriteLog, Areas(cfg));
        Diagnostics.Log.Write(Diagnostics.Enums.LogArea.App, () =>
            "settings read from " + _store.ConfigPath + ", " + cfg.Rows + " by " + cfg.Columns + " pads");

        Diagnostics.CrashReport.Watch(new Files.AppFolder().Path());
        Diagnostics.CrashReport.Note("started, " + cfg.Rows + " by " + cfg.Columns + " pads");

        var machines = Registry.Load();

        var projects = new Tracker.Machines.MachineProjects();

        projects.Keep(machines);

        Diagnostics.Log.Write(Diagnostics.Enums.LogArea.App,
            () => machines.Count + " machine" + (machines.Count == 1 ? "" : "s") + " read from disc");

        _audio = new BassAudioEngine(
            padCount: cfg.Rows * cfg.Columns,
            deviceRate: cfg.EngineSampleRate);

        var vm = new MainViewModel(_audio, _store, cfg, _midi, _recording, _waveform, _routing, projects);
        DataContext = vm;

        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(version))
        {
            var plus = version.IndexOf('+');
            if (plus > 0) version = version[..plus];
            Title = $"JingleBox2 v{version}";
        }

        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);

        Views.LinkKey.Listen(this);

        Shortcuts.ShortcutKeys.Map.Take(cfg.Shortcuts);
        Shortcuts.ShortcutKeys.Listen(this);
        AddHandler(KeyUpEvent, OnWindowKeyUp, RoutingStrategies.Tunnel);

        Deactivated += (_, _) => _held.Forget();

        vm.MatrixSizeChanged += OnMatrixSizeChanged;

        _cfg = cfg;
        RestoreWindowSize(cfg);

        Closed += (_, __) =>
        {
            vm.MatrixSizeChanged -= OnMatrixSizeChanged;
            _midi.Dispose();
            _audio.Dispose();
        };
    }

    /// <summary>
    /// Which parts of the app the settings ask for, with nothing said meaning all of them.
    /// </summary>
    private static Diagnostics.Enums.LogArea Areas(Config.AppConfig cfg) =>
        cfg.LogAreas == 0 ? Diagnostics.Enums.LogArea.Everything : (Diagnostics.Enums.LogArea)cfg.LogAreas;

    /// <summary>
    /// The two keys the window answers itself: space works the transport, Ctrl+R records.
    /// </summary>
    /// <remarks>
    /// Space starts the transport when it is stopped and stops it when it is running, which is
    /// where every tracker and every desk puts it, and is the reason the transport is on the
    /// window rather than on a page: it is worth starting from wherever you happen to be.
    ///
    /// It is taken on the way down, before the focused control sees it, because otherwise the
    /// last button you pressed keeps the key: click Open and space opens the song again instead
    /// of playing it, which is exactly what a space bar must never do. Enter still works every
    /// button, so nothing reachable by keyboard becomes unreachable. Two things are left alone:
    /// a text box, where a space is a space, and a combo box with its list open, where space
    /// takes the row that is lit. A space held down is swallowed rather than passed on, so a
    /// leant-on key does nothing at all rather than something else.
    ///
    /// Ctrl+R records, and what that means is whatever the page you are on says it means: the
    /// transport is patched to the page's own deck. On RECORD it takes a take, on TRACKER it
    /// arms the pattern for typing, and on the pages that record nothing the deck says it
    /// cannot and the keystroke passes through. The cap at the top of the window is this same
    /// thing pressed with a mouse. Pressing it again is the other half of the same key: on
    /// RECORD that ends the take, and on TRACKER, where recording is an armed state rather than
    /// a running one, the deck reads it as disarming. Not while somebody is typing a name into
    /// something.
    ///
    /// Every key goes through <see cref="_held"/>, not only these two, so the record of what is
    /// down is the whole keyboard and the next shortcut added here is one press too.
    /// </remarks>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        bool first = _held.Pressed(e.Key);

        if (e.Handled) return;

        if (first && e.Key == Key.R && e.KeyModifiers == KeyModifiers.Control)
        {
            if (FocusManager?.GetFocusedElement() is TextBox) return;

            if (DataContext is not MainViewModel deck || deck.Transport is not { } transport) return;

            if (transport.IsRecording) transport.StopCommand.Execute(null);
            else if (transport.CanRecord) transport.RecordCommand.Execute(null);
            else return;

            e.Handled = true;
            return;
        }

        if (e.Key != Key.Space || e.KeyModifiers != KeyModifiers.None) return;

        switch (FocusManager?.GetFocusedElement())
        {
            case TextBox: return;

            case ComboBox { IsDropDownOpen: true }: return;
        }

        _tookSpace = true;

        if (!first)
        {
            e.Handled = true;
            return;
        }

        if (DataContext is not MainViewModel vm) return;

        vm.Transport.Toggle();

        e.Handled = true;
    }

    /// <summary>
    /// The key is up, so the next time it goes down is a press again.
    /// </summary>
    /// <remarks>
    /// A space the transport took is swallowed on the way up as well. Buttons click on the
    /// key coming up rather than going down, and do it whether or not they saw the press, so
    /// half a space bar is enough to work the last button you clicked.
    /// </remarks>
    private void OnWindowKeyUp(object? sender, KeyEventArgs e)
    {
        _held.Released(e.Key);

        if (e.Key != Key.Space || !_tookSpace) return;

        _tookSpace = false;
        e.Handled = true;
    }

    /// <summary>
    /// Uses the size the window was last left at, falling back to the pad matrix on first run.
    /// </summary>
    /// <remarks>
    /// The watching starts here rather than in the constructor, and only after the size has
    /// been applied: putting the saved size on is itself a resize, and a watcher already
    /// running would write it straight back and would do so before the settings had finished
    /// being read.
    /// </remarks>
    private void RestoreWindowSize(AppConfig cfg)
    {
        if (cfg.WindowWidth > 0 && cfg.WindowHeight > 0)
        {
            Width = cfg.WindowWidth;
            Height = cfg.WindowHeight;
        }
        else
        {
            var (width, height) = MatrixSize(cfg.Rows, cfg.Columns);
            Width = width;
            Height = height;
        }

        if (cfg.WindowMaximized)
            WindowState = WindowState.Maximized;

        _windowRestored = true;

        _saveWindowTimer = new DispatcherTimer { Interval = WindowSaveDelay };
        _saveWindowTimer.Tick += (_, _) =>
        {
            _saveWindowTimer.Stop();
            SaveWindowSize();
        };

        PropertyChanged += (_, e) =>
        {
            if (e.Property == ClientSizeProperty || e.Property == WindowStateProperty)
                ScheduleWindowSave();
        };
    }

    /// <summary>
    /// Starts the clock on writing the window's size down, restarting it if it was already
    /// running, so a drag of a hundred steps costs one write at the end of it.
    /// </summary>
    private void ScheduleWindowSave()
    {
        if (!_windowRestored || _saveWindowTimer == null) return;

        _saveWindowTimer.Stop();
        _saveWindowTimer.Start();
    }

    /// <summary>
    /// Writes the window's size and whether it is maximised into the settings.
    /// </summary>
    /// <remarks>
    /// Only a normal-state size is recorded. Storing the maximised dimensions would make them
    /// the size the window restores down to, so unmaximising would do nothing visible and the
    /// window could never be got back to a workable size.
    /// </remarks>
    private void SaveWindowSize()
    {
        if (_cfg == null) return;

        _cfg.WindowMaximized = WindowState == WindowState.Maximized;

        if (WindowState == WindowState.Normal && Width > 0 && Height > 0)
        {
            _cfg.WindowWidth = Width;
            _cfg.WindowHeight = Height;
        }

        _store.Save(_cfg);
    }

    /// <summary>
    /// Grows the window when the pad matrix has grown past what it can show.
    /// </summary>
    /// <remarks>
    /// Only grows. A deliberate resize should survive a change of matrix, but the pads must
    /// never end up clipped.
    ///
    /// Capped at the screen, because a row of pads asks for as much width as it likes and a
    /// long thin matrix asks for more than any screen has. Grown past the screen the window is
    /// simply half off it, which is worse than a matrix that has to be scrolled.
    /// </remarks>
    private void OnMatrixSizeChanged(int rows, int columns)
    {
        var (width, height) = MatrixSize(rows, columns);

        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;

        if (screen != null)
        {
            width = Math.Min(width, screen.WorkingArea.Width / screen.Scaling);
            height = Math.Min(height, screen.WorkingArea.Height / screen.Scaling);
        }

        if (Width < width) Width = width;
        if (Height < height) Height = height;

        ScheduleWindowSave();
    }

    /// <summary>Size that keeps the pads roughly square, never below the first-run default.</summary>
    private static (double Width, double Height) MatrixSize(int rows, int columns)
    {
        double padTotalSize = PadSize + PadMargin;
        double width = columns * padTotalSize + WindowPadding;
        double height = rows * padTotalSize + HeaderHeight + WindowPadding;

        return (Math.Max(width, DefaultWidth), Math.Max(height, DefaultHeight));
    }
}
