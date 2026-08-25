using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using JingleBox2.Audio;
using JingleBox2.Audio.Routing;
using JingleBox2.Config;
using JingleBox2.Midi;
using JingleBox2.ViewModels;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace JingleBox2;

public partial class MainWindow : Window
{
    private readonly ConfigStore _store = new("JingleBox2");
    private readonly BassAudioEngine _audio;
    private readonly IMidiService _midi = new MidiService();
    private readonly IRecordingService _recording = new RecordingService();
    private readonly IWaveformService _waveform = new WaveformService();

    // What the machine can be asked to record: a graph to patch on Linux, an output's playback
    // on Windows, and nothing at all elsewhere, in which case the RECORD page hides the picker.
    private readonly IAudioRouting _routing;

    private AppConfig? _cfg;
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

    // Constants for window sizing
    private const double HeaderHeight = 140; // Theme, device, tabs
    private const double PadSize = 120;      // Target pad size in pixels
    private const double PadMargin = 20;     // Margin around each pad

    // The matrix alone gives a cramped window for small grids, so treat this as the floor
    // the window opens at. Larger matrices still grow past it.
    private const double DefaultWidth = 800;
    private const double DefaultHeight = 800;

    public MainWindow()
    {
        InitializeComponent();

        _routing = AudioRouting.Create(_recording);

        var cfg = _store.LoadOrCreateDefault();

        // Before anything else that might have something to say. Off unless the setting says
        // otherwise, and free when off. See JingleBox2.Diagnostics.Log.
        Diagnostics.Log.Open(Config.AppFolder.Path(), cfg.WriteLog);
        Diagnostics.Log.Write(Diagnostics.LogArea.App, () =>
            "settings read from " + _store.ConfigPath + ", " + cfg.Rows + " by " + cfg.Columns + " pads");

        // Whether the log is on or off. A run that ends badly is nearly always a run nobody
        // was logging, which is the whole reason this keeps its own short account.
        Diagnostics.CrashReport.Watch(Config.AppFolder.Path());
        Diagnostics.CrashReport.Note("started, " + cfg.Rows + " by " + cfg.Columns + " pads");

        // The machines this installation has. Read before anything shows one, since what the
        // rack is called and what colour it wears come off the machines themselves now.
        var machines = Tracker.Machines.MachineRegistry.Load();

        // Kept rather than counted and dropped: a panel drawn from a machine's own description
        // asks for the machine, and this is the one moment the disc is read.
        Tracker.Machines.MachineProjects.Keep(machines);

        Diagnostics.Log.Write(Diagnostics.LogArea.App,
            () => machines.Count + " machine" + (machines.Count == 1 ? "" : "s") + " read from disc");

        _audio = new BassAudioEngine(padCount: cfg.Rows * cfg.Columns);

        var vm = new MainViewModel(_audio, _store, cfg, _midi, _recording, _waveform, _routing);
        DataContext = vm;

        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(version))
        {
            // Strip git hash suffix if present (e.g. "1.0.12+abc123def")
            var plus = version.IndexOf('+');
            if (plus > 0) version = version[..plus];
            Title = $"JingleBox2 v{version}";
        }

        // The space bar works the transport from wherever you are in the window, and is taken
        // on the way down so that nothing else can spend it first.
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnWindowKeyUp, RoutingStrategies.Tunnel);

        // Keys held while the window loses focus are released somewhere else, and this window
        // never hears about it.
        Deactivated += (_, _) => _held.Forget();

        // Subscribe to matrix size changes to resize window
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
    /// Space starts the transport when it is stopped and stops it when it is running.
    /// </summary>
    /// <remarks>
    /// Where every tracker and every desk puts it, and the reason the transport is on the
    /// window rather than on a page: it is worth starting from wherever you happen to be.
    ///
    /// Taken on the way down, before the focused control sees it, because otherwise the last
    /// button you pressed keeps the key: click Open and space opens the song again instead of
    /// playing it, which is exactly what a space bar must never do. Space belongs to the
    /// transport the way it does on a desk. Enter still works every button, so nothing that
    /// could be reached by keyboard becomes unreachable.
    ///
    /// Two things are left alone: a text box, where a space is a space, and a combo box with
    /// its list open, where it picks the row that is lit.
    /// </remarks>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Every key, so the record of what is down is the whole keyboard and the next shortcut
        // added here is one press too.
        bool first = _held.Pressed(e.Key);

        if (e.Handled || e.Key != Key.Space || e.KeyModifiers != KeyModifiers.None) return;

        switch (FocusManager?.GetFocusedElement())
        {
            // Somebody is typing, and a space is part of what they are typing.
            case TextBox: return;

            // The list is down and space is how a row is taken.
            case ComboBox { IsDropDownOpen: true }: return;
        }

        // Ours from here, both halves of it.
        _tookSpace = true;

        // Held down, not pressed again. Swallowed rather than passed on, so a leant-on space
        // does nothing at all rather than something else.
        if (!first)
        {
            e.Handled = true;
            return;
        }

        if (DataContext is not MainViewModel vm) return;

        // Whatever the caps are working on this page. See TransportSwitch.
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

        // Resizing fires continuously during a drag, so coalesce into one write at the end.
        _saveWindowTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
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

    private void ScheduleWindowSave()
    {
        if (!_windowRestored || _saveWindowTimer == null) return;

        _saveWindowTimer.Stop();
        _saveWindowTimer.Start();
    }

    private void SaveWindowSize()
    {
        if (_cfg == null) return;

        _cfg.WindowMaximized = WindowState == WindowState.Maximized;

        // Only record a normal-state size; storing the maximized dimensions would make them
        // the size the window restores down to.
        if (WindowState == WindowState.Normal && Width > 0 && Height > 0)
        {
            _cfg.WindowWidth = Width;
            _cfg.WindowHeight = Height;
        }

        _store.Save(_cfg);
    }

    private void OnMatrixSizeChanged(int rows, int columns)
    {
        var (width, height) = MatrixSize(rows, columns);

        // A row of pads asks for as much width as it likes, and a long thin matrix asks for
        // more than any screen has. Grown past the screen the window is simply half off it.
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;

        if (screen != null)
        {
            width = Math.Min(width, screen.WorkingArea.Width / screen.Scaling);
            height = Math.Min(height, screen.WorkingArea.Height / screen.Scaling);
        }

        // Only grow. A deliberate resize should survive a change of matrix, but the pads
        // must never end up clipped.
        if (Width < width) Width = width;
        if (Height < height) Height = height;

        ScheduleWindowSave();
    }

    /// <summary>Size that keeps the pads roughly square, never below the first-run default.</summary>
    private static (double Width, double Height) MatrixSize(int rows, int columns)
    {
        double padTotalSize = PadSize + PadMargin;
        double width = columns * padTotalSize + 48;  // 48 for window padding
        double height = rows * padTotalSize + HeaderHeight + 48;

        return (Math.Max(width, DefaultWidth), Math.Max(height, DefaultHeight));
    }
}
