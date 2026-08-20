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
        };
    }

    private RecordViewModel? _subscribedVm;

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
