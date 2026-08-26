using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using JingleBox2.Audio;
using JingleBox2.Machines.Ui;
using JingleBox2.Models;
using JingleBox2.ViewModels;
using System;
using System.ComponentModel;
using System.Linq;

namespace JingleBox2.Views;

public partial class RecordView : UserControl
{
    private static readonly WaveformViewport FullView = new();

    /// <summary>How much of the accent the outline is painted with, as the slice editor does.</summary>
    private const double WaveformOpacity = 0.85;

    private Canvas? _recordWaveformCanvas;

    public RecordView()
    {
        InitializeComponent();
        this.Loaded += (s, e) =>
        {
            _recordWaveformCanvas = this.FindControl<Canvas>("RecordWaveformCanvas");
            if (_recordWaveformCanvas != null)
            {
                // The canvas is stretch-sized, so redraw whenever its layout size changes.
                _recordWaveformCanvas.SizeChanged += (_, _) => DrawWaveform(CurrentWaveform());
                DrawWaveform(CurrentWaveform());
            }
        };

        this.DataContextChanged += (s, e) =>
        {
            if (_subscribedVm != null)
                _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;

            _subscribedVm = this.DataContext as RecordViewModel;

            if (_subscribedVm != null)
            {
                _subscribedVm.PropertyChanged += OnViewModelPropertyChanged;
                DrawWaveform(_subscribedVm.CurrentWaveform);
            }

            // The data context can arrive after the page is already up, so the input is opened
            // from here as well as from the attach.
            UpdateMonitoring();
        };
    }

    private RecordViewModel? _subscribedVm;
    private RecordViewModel? _monitoring;
    private bool _onScreen;

    /// <summary>
    /// A theme swap and other re-templating detach this page and put it straight back. Closing
    /// the input on the way out and opening it again on the way in would lose the routing every
    /// time, since the system wires a new capture stream to its own default, so a departure has
    /// to prove itself before the input is let go.
    /// </summary>
    private DispatcherTimer? _closing;

    private static readonly TimeSpan CloseDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The input is watched while this page is up, so the meter reads before a take rather
    /// than only during one. It is closed again on the way out: holding a capture device open
    /// for a tab nobody is looking at is rude to whatever else wants the microphone.
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _onScreen = true;
        UpdateMonitoring();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _onScreen = false;
        UpdateMonitoring();
    }

    private void UpdateMonitoring()
    {
        var vm = DataContext as RecordViewModel;

        // A view model this page has let go of must not be left holding the input open.
        if (!ReferenceEquals(_monitoring, vm)) _monitoring?.StopInputMonitoring();

        _monitoring = vm;

        if (vm == null) return;

        if (_onScreen)
        {
            _closing?.Stop();
            vm.StartInputMonitoring();
            return;
        }

        ScheduleClose(vm);
    }

    /// <summary>Lets go of the input only if the page is still gone a moment later.</summary>
    private void ScheduleClose(RecordViewModel vm)
    {
        _closing ??= new DispatcherTimer { Interval = CloseDelay };
        _closing.Stop();

        void Close(object? sender, EventArgs e)
        {
            _closing!.Tick -= Close;
            _closing.Stop();

            if (!_onScreen) vm.StopInputMonitoring();
        }

        _closing.Tick += Close;
        _closing.Start();
    }

    /// <summary>
    /// Brings recordings in from the disc onto the shelf of takes.
    /// </summary>
    /// <remarks>
    /// The picker belongs to the window, so it is opened here and only the answer goes to the
    /// view model, which is the same arrangement the instrument designer's importer uses.
    /// </remarks>
    private async void Import_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not RecordViewModel vm) return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import recordings",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Recordings")
                {
                    Patterns = RecordingImport.Kinds.Select(k => "*" + k).ToArray()
                }
            }
        });

        // Sorted the way the folder reads, since a set of takes is nearly always named so that
        // it sorts, and that order is the order they will be wanted in.
        vm.Import(picked
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList());
    }

    /// <summary>
    /// Opening the list is the moment it has to be right: a program only appears in the graph
    /// while it is playing, so what was true a minute ago usually is not.
    /// </summary>
    private void Routes_DropDownOpened(object? sender, EventArgs e) =>
        (DataContext as RecordViewModel)?.RefreshRoutesCommand.Execute(null);

    private WaveformData? CurrentWaveform() => (this.DataContext as RecordViewModel)?.CurrentWaveform;

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(RecordViewModel.CurrentWaveform))
            DrawWaveform(CurrentWaveform());
    }

    private void DrawWaveform(WaveformData? waveform)
    {
        if (_recordWaveformCanvas == null) return;

        _recordWaveformCanvas.Children.Clear();

        if (waveform == null) return; // cleared, e.g. after the recording was deleted

        // Control.Width/Height are the *requested* sizes and are NaN unless set in XAML.
        // The rendered size lives in Bounds, so use that.
        double canvasWidth = _recordWaveformCanvas.Bounds.Width;
        double canvasHeight = _recordWaveformCanvas.Bounds.Height;
        float[] peakData = waveform.PeakData;

        if (peakData.Length == 0 || canvasWidth <= 0 || canvasHeight <= 0) return;

        // Read at draw time rather than held in a field: a theme swap has to reach this, and
        // the colour keys are only right once the new sheet is the one being asked.
        var palette = ThemePalette.From(this);

        // Same outline builder the editor uses, at the default viewport: no zoom, no scroll.
        var waveformPath = new Path
        {
            Data = WaveformGeometry.Build(peakData, FullView, canvasWidth, canvasHeight),
            Fill = palette.AccentBrush,
            Opacity = WaveformOpacity
        };
        _recordWaveformCanvas.Children.Add(waveformPath);
    }
}
