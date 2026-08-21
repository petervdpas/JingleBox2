using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using JingleBox2.Models;
using JingleBox2.ViewModels;
using JingleBox2.Waveform;
using System;
using System.ComponentModel;

namespace JingleBox2.Views;

public partial class RecordView : UserControl
{
    private static readonly WaveformViewport FullView = new();
    private static readonly IBrush WaveformBrush = new SolidColorBrush(Color.Parse("#3B82F6"));

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

        if (_onScreen) vm.StartInputMonitoring();
        else vm.StopInputMonitoring();
    }

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

        // Same outline builder the editor uses, at the default viewport: no zoom, no scroll.
        var waveformPath = new Path
        {
            Data = WaveformGeometry.Build(peakData, FullView, canvasWidth, canvasHeight),
            Fill = WaveformBrush,
            Opacity = 0.6
        };
        _recordWaveformCanvas.Children.Add(waveformPath);
    }
}
