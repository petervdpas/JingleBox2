using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using JingleBox2.Models;
using JingleBox2.ViewModels;
using System;
using System.ComponentModel;

namespace JingleBox2.Views;

public partial class RecordView : UserControl
{
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
        if (waveform == null || _recordWaveformCanvas == null) return;

        _recordWaveformCanvas.Children.Clear();

        // Control.Width/Height are the *requested* sizes and are NaN unless set in XAML.
        // The rendered size lives in Bounds, so use that.
        double canvasWidth = _recordWaveformCanvas.Bounds.Width;
        double canvasHeight = _recordWaveformCanvas.Bounds.Height;
        float[] peakData = waveform.PeakData;

        if (peakData.Length == 0 || canvasWidth <= 0 || canvasHeight <= 0) return;

        // Draw waveform as filled area (mirrored top/bottom)
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            double pixelWidth = canvasWidth / peakData.Length;
            double centerY = canvasHeight / 2;

            // Top half
            ctx.BeginFigure(new Point(0, centerY), true);
            for (int i = 0; i < peakData.Length; i++)
            {
                double x = i * pixelWidth + pixelWidth / 2;
                double peakHeight = peakData[i] * centerY;
                double y = centerY - peakHeight;
                ctx.LineTo(new Point(x, y));
            }

            // Bottom half (mirror)
            for (int i = peakData.Length - 1; i >= 0; i--)
            {
                double x = i * pixelWidth + pixelWidth / 2;
                double peakHeight = peakData[i] * centerY;
                double y = centerY + peakHeight;
                ctx.LineTo(new Point(x, y));
            }

            ctx.EndFigure(true);
        }

        var waveformPath = new Path
        {
            Data = geometry,
            Fill = new SolidColorBrush(Color.Parse("#3B82F6")),
            Opacity = 0.6
        };
        _recordWaveformCanvas.Children.Add(waveformPath);
    }
}
