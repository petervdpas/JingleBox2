using Avalonia.Controls;
using Avalonia.Platform.Storage;
using JingleBox2.Audio;
using JingleBox2.Config;
using JingleBox2.Midi;
using JingleBox2.ViewModels;
using System;
using System.Threading.Tasks;

namespace JingleBox2;

public partial class MainWindow : Window
{
    private readonly ConfigStore _store = new("JingleBox2");
    private readonly BassAudioEngine _audio;
    private readonly IMidiService _midi = new MidiService();

    // Constants for window sizing
    private const double HeaderHeight = 140; // Theme, device, tabs
    private const double PadSize = 120;      // Target pad size in pixels
    private const double PadMargin = 20;     // Margin around each pad

    public MainWindow()
    {
        InitializeComponent();

        var cfg = _store.LoadOrCreateDefault();
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

        var vm = new MainViewModel(_audio, PickFileAsync, _store, cfg, _midi);
        DataContext = vm;

        // Subscribe to matrix size changes to resize window
        vm.MatrixSizeChanged += OnMatrixSizeChanged;

        // Set initial window size based on config
        AdjustWindowSize(cfg.Rows, cfg.Columns);

        Closed += (_, __) =>
        {
            vm.MatrixSizeChanged -= OnMatrixSizeChanged;
            _midi.Dispose();
            _audio.Dispose();
        };
    }

    private void OnMatrixSizeChanged(int rows, int columns)
    {
        AdjustWindowSize(rows, columns);
    }

    private void AdjustWindowSize(int rows, int columns)
    {
        // Calculate window size to keep pads approximately square
        double padTotalSize = PadSize + PadMargin;
        double width = columns * padTotalSize + 48;  // 48 for window padding
        double height = rows * padTotalSize + HeaderHeight + 48;

        // Apply minimum constraints
        width = Math.Max(width, 400);
        height = Math.Max(height, 400);

        Width = width;
        Height = height;
    }
}
