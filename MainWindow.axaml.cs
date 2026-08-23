using Avalonia.Controls;
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

        _audio = new BassAudioEngine(padCount: cfg.Rows * cfg.Columns);

        async Task<string?> PickFileAsync()
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                Title = "Select sample",
                FileTypeFilter =
                [
                    new FilePickerFileType("Audio")
                    {
                        Patterns = ["*.wav", "*.mp3", "*.ogg", "*.flac"]
                    }
                ]
            });

            return files.Count == 1 ? files[0].Path.LocalPath : null;
        }

        var vm = new MainViewModel(_audio, PickFileAsync, _store, cfg, _midi, _recording, _waveform, _routing);
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
