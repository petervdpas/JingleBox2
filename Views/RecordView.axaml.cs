using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using JingleBox2.Audio;
using JingleBox2.Machines.Ui;
using JingleBox2.Audio.Records;
using JingleBox2.ViewModels;
using System;
using System.ComponentModel;
using System.Linq;
using JingleBox2.Machines.Ui.Records;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Machines.Ui.Interfaces;

namespace JingleBox2.Views;

/// <summary>
/// The RECORD page: what is coming in, the take being made, and the shelf of takes there are.
/// </summary>
/// <remarks>
/// The shelf is what every other part of the application draws its recordings from, so a take
/// deleted here goes to <c>deleted/</c> rather than away, and undo on this page fetches back the
/// last one.
/// </remarks>
public partial class RecordView : UserControl
{
    /// <summary>A recording's outline, and which part of it a viewport is showing.</summary>
    private readonly IWaveformGeometry _shape = new WaveformGeometry();

    /// <summary>The one door recordings come in through. Holds nothing, so one is enough.</summary>
    private readonly IRecordingImport _import = new RecordingImport();

    /// <summary>
    /// The whole recording, no zoom and no scroll, which is the only view this page's picture
    /// ever shows. Windowing a take is the slice editor's job.
    /// </summary>
    private static readonly WaveformViewport FullView = new();

    /// <summary>How much of the accent the outline is painted with, as the slice editor does.</summary>
    private const double WaveformOpacity = 0.85;

    /// <summary>
    /// Where the shape of the current take is drawn. Found once the page is up rather than in
    /// the constructor, since it does not exist until the template has been applied.
    /// </summary>
    private Canvas? _recordWaveformCanvas;

    /// <summary>
    /// Builds the page, and keeps the picture and the input in step with what it is showing.
    /// </summary>
    /// <remarks>
    /// The canvas is stretch-sized, so the shape is drawn again whenever its layout size
    /// changes: a picture drawn once would be the right shape at the wrong width for the rest
    /// of the session.
    ///
    /// The data context can arrive after the page is already up, which is why the input is
    /// opened from here as well as from the attach. Getting that wrong is silent: the meter
    /// simply never moves.
    /// </remarks>
    public RecordView()
    {
        InitializeComponent();
        this.Loaded += (s, e) =>
        {
            _recordWaveformCanvas = this.FindControl<Canvas>("RecordWaveformCanvas");
            if (_recordWaveformCanvas != null)
            {
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

            UpdateMonitoring();
        };
    }

    /// <summary>
    /// The view model whose changes are being listened to, kept so the subscription can be
    /// dropped when the page is pointed at another one.
    /// </summary>
    private RecordViewModel? _subscribedVm;

    /// <summary>
    /// The view model currently holding the input open. Separate from <see cref="_subscribedVm"/>
    /// because a page can be listening to a view model without the input being open: the input
    /// follows whether the page is on screen, the subscription follows what it is showing.
    /// </summary>
    private RecordViewModel? _monitoring;

    /// <summary>Whether the page is up, which is what decides whether the input is held open.</summary>
    private bool _onScreen;

    /// <summary>
    /// A theme swap and other re-templating detach this page and put it straight back. Closing
    /// the input on the way out and opening it again on the way in would lose the routing every
    /// time, since the system wires a new capture stream to its own default, so a departure has
    /// to prove itself before the input is let go.
    /// </summary>
    private DispatcherTimer? _closing;

    /// <summary>
    /// How long a departure has to last before the input is really let go of. A second is long
    /// enough to outlast a re-template and short enough that nobody is left holding the
    /// microphone after they have walked away from the page.
    /// </summary>
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

    /// <summary>
    /// Leaving the page starts the clock on letting the input go, rather than closing it there
    /// and then. See <see cref="_closing"/> for why the departure has to prove itself.
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _onScreen = false;
        UpdateMonitoring();
    }

    /// <summary>
    /// Opens or closes the input to match whether the page is up and what it is showing.
    /// </summary>
    /// <remarks>
    /// A view model this page has let go of must not be left holding the input open, so it is
    /// stopped at once whatever the page is doing: the delayed close is about this page coming
    /// straight back, and a different view model is not that.
    /// </remarks>
    private void UpdateMonitoring()
    {
        var vm = DataContext as RecordViewModel;

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
    ///
    /// Sorted the way the folder reads, since a set of takes is nearly always named so that it
    /// sorts, and that order is the order they will be wanted in.
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
                    Patterns = _import.Kinds.Select(k => "*" + k).ToArray()
                }
            }
        });

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

    /// <summary>The shape of the take being shown, or nothing when there is none.</summary>
    private WaveformData? CurrentWaveform() => (this.DataContext as RecordViewModel)?.CurrentWaveform;

    /// <summary>
    /// Draws the picture again when the take being shown changes. Only that one property: a
    /// picture is expensive to build and nothing else on the page changes it.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(RecordViewModel.CurrentWaveform))
            DrawWaveform(CurrentWaveform());
    }

    /// <summary>
    /// Draws the take's shape across the canvas, or clears it when there is no take.
    /// </summary>
    /// <remarks>
    /// A null shape is an ordinary state rather than a fault: it is what is left after the
    /// recording being shown was deleted.
    ///
    /// Measured off <c>Bounds</c> and not off <c>Width</c> and <c>Height</c>, which are the
    /// requested sizes and are NaN unless XAML set them. This canvas is stretch-sized, so they
    /// always are.
    ///
    /// The palette is read at draw time rather than held in a field, because a theme swap has
    /// to reach this and the colour keys are only right once the new sheet is the one being
    /// asked. The outline is built by the same builder the slice editor uses, at the default
    /// viewport, so the two pictures of one take cannot disagree about its shape.
    /// </remarks>
    private void DrawWaveform(WaveformData? waveform)
    {
        if (_recordWaveformCanvas == null) return;

        _recordWaveformCanvas.Children.Clear();

        if (waveform == null) return;

        double canvasWidth = _recordWaveformCanvas.Bounds.Width;
        double canvasHeight = _recordWaveformCanvas.Bounds.Height;
        float[] peakData = waveform.PeakData;

        if (peakData.Length == 0 || canvasWidth <= 0 || canvasHeight <= 0) return;

        var palette = ThemePalette.From(this);

        var waveformPath = new Path
        {
            Data = _shape.Build(peakData, FullView, canvasWidth, canvasHeight),
            Fill = palette.AccentBrush,
            Opacity = WaveformOpacity
        };
        _recordWaveformCanvas.Children.Add(waveformPath);
    }
}
