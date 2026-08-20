using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.Models;
using JingleBox2.ViewModels;
using ManagedBass;
using System;
using System.ComponentModel;

namespace JingleBox2.Views;

public partial class RecordingEditDialog : Window
{
    private Canvas? _editWaveformCanvas;
    private double _trimHandleWidth = 8;
    private double _leftTrimPos = 0;
    private double _rightTrimPos = 1;
    private bool _draggingLeft = false;
    private bool _draggingRight = false;
    private double _zoomLevel = 1.0;
    private int _playbackChannel = 0;
    private bool _isPlaying = false;
    private Button? _playButton;

    public RecordingEditDialog()
    {
        InitializeComponent();
        this.Loaded += (s, e) =>
        {
            _editWaveformCanvas = this.FindControl<Canvas>("EditWaveformCanvas");
            if (_editWaveformCanvas != null)
            {
                _editWaveformCanvas.PointerPressed += EditCanvas_PointerPressed;
                _editWaveformCanvas.PointerMoved += EditCanvas_PointerMoved;
                _editWaveformCanvas.PointerReleased += EditCanvas_PointerReleased;

                // Force draw if data context already has waveform
                if (this.DataContext is RecordViewModel vm && vm.CurrentWaveform != null)
                {
                    DrawWaveform(vm.CurrentWaveform);
                }
            }
        };

        this.Closing += (s, e) =>
        {
            // Stop playback when dialog closes
            StopPlayback();
        };

        this.DataContextChanged += (s, e) =>
        {
            if (this.DataContext is RecordViewModel vm)
            {
                vm.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(vm.CurrentWaveform))
                        DrawWaveform(vm.CurrentWaveform);
                };

                // Also draw immediately if waveform is already loaded
                if (vm.CurrentWaveform != null)
                    DrawWaveform(vm.CurrentWaveform);
            }
        };
    }

    private void DrawWaveform(WaveformData? waveform)
    {
        if (waveform == null || _editWaveformCanvas == null) return;

        _editWaveformCanvas.Children.Clear();

        double canvasWidth = _editWaveformCanvas.Width;
        double canvasHeight = _editWaveformCanvas.Height;
        float[] peakData = waveform.PeakData;

        if (peakData.Length == 0) return;

        // Calculate pixel width based on zoom level
        double basePixelWidth = canvasWidth / peakData.Length;
        double pixelWidth = basePixelWidth * _zoomLevel;

        // Calculate visible range based on zoom
        int startSample = 0;
        int endSample = peakData.Length;

        if (_zoomLevel > 1)
        {
            // When zoomed in, show only a portion
            int visibleSamples = (int)(peakData.Length / _zoomLevel);
            endSample = Math.Min(startSample + visibleSamples, peakData.Length);
        }

        // Draw waveform as filled area
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            double centerY = canvasHeight / 2;

            // Top half
            ctx.BeginFigure(new Point(0, centerY), true);
            for (int i = startSample; i < endSample; i++)
            {
                double x = (i - startSample) * pixelWidth + pixelWidth / 2;
                if (x > canvasWidth) break;

                double peakHeight = peakData[i] * centerY;
                double y = centerY - peakHeight;
                ctx.LineTo(new Point(x, y));
            }

            // Bottom half (mirror)
            for (int i = endSample - 1; i >= startSample; i--)
            {
                double x = (i - startSample) * pixelWidth + pixelWidth / 2;
                if (x > canvasWidth) continue;

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
        _editWaveformCanvas.Children.Add(waveformPath);

        // Trim handle positions, in the same coordinate space the pointer handlers use
        double leftX = FractionToX(_leftTrimPos, canvasWidth);
        double rightX = FractionToX(_rightTrimPos, canvasWidth);

        // Draw selection overlay
        double selectionWidth = Math.Max(0, rightX - leftX - _trimHandleWidth);
        var selection = new Rectangle
        {
            Fill = new SolidColorBrush(Color.Parse("#3B82F6")),
            Width = selectionWidth,
            Height = canvasHeight,
            Opacity = 0.2
        };
        Canvas.SetLeft(selection, Math.Max(0, leftX + _trimHandleWidth));
        Canvas.SetTop(selection, 0);
        _editWaveformCanvas.Children.Add(selection);

        // Draw left trim handle
        var leftHandle = new Rectangle
        {
            Fill = new SolidColorBrush(Color.Parse("#EF4444")),
            Width = _trimHandleWidth,
            Height = canvasHeight,
            Opacity = 0.9,
            Cursor = new Cursor(StandardCursorType.SizeWestEast)
        };
        Canvas.SetLeft(leftHandle, leftX);
        Canvas.SetTop(leftHandle, 0);
        _editWaveformCanvas.Children.Add(leftHandle);

        // Draw right trim handle
        var rightHandle = new Rectangle
        {
            Fill = new SolidColorBrush(Color.Parse("#EF4444")),
            Width = _trimHandleWidth,
            Height = canvasHeight,
            Opacity = 0.9,
            Cursor = new Cursor(StandardCursorType.SizeWestEast)
        };
        Canvas.SetLeft(rightHandle, rightX - _trimHandleWidth);
        Canvas.SetTop(rightHandle, 0);
        _editWaveformCanvas.Children.Add(rightHandle);
    }

    // Zoomed in, the canvas shows the leading 1/_zoomLevel of the file stretched across its
    // full width. Drawing, hit-testing and dragging all go through this one mapping so a
    // handle always responds where it is painted.

    /// <summary>Fraction of the whole recording (0..1) to an x offset on the canvas.</summary>
    private double FractionToX(double fraction, double canvasWidth)
        => Math.Clamp(fraction * canvasWidth * _zoomLevel, 0, canvasWidth);

    /// <summary>An x offset on the canvas back to a fraction of the whole recording.</summary>
    private double XToFraction(double x, double canvasWidth)
        => canvasWidth > 0 ? x / (canvasWidth * _zoomLevel) : 0;

    private void EditCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_editWaveformCanvas == null) return;

        var point = e.GetPosition(_editWaveformCanvas);
        double canvasWidth = _editWaveformCanvas.Width;
        double leftX = FractionToX(_leftTrimPos, canvasWidth);
        double rightX = FractionToX(_rightTrimPos, canvasWidth);

        if (point.X >= leftX && point.X <= leftX + _trimHandleWidth)
            _draggingLeft = true;
        else if (point.X >= rightX - _trimHandleWidth && point.X <= rightX)
            _draggingRight = true;

        if (_draggingLeft || _draggingRight)
            e.Pointer.Capture(_editWaveformCanvas);
    }

    private void EditCanvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_draggingLeft && !_draggingRight || _editWaveformCanvas == null) return;

        var point = e.GetPosition(_editWaveformCanvas);
        double canvasWidth = _editWaveformCanvas.Width;
        double newPos = Math.Clamp(XToFraction(point.X, canvasWidth), 0, 1);

        if (_draggingLeft && newPos < _rightTrimPos - 0.05)
            _leftTrimPos = newPos;
        else if (_draggingRight && newPos > _leftTrimPos + 0.05)
            _rightTrimPos = newPos;

        if (this.DataContext is RecordViewModel vm)
            DrawWaveform(vm.CurrentWaveform);
    }

    private void EditCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _draggingLeft = false;
        _draggingRight = false;
        if (_editWaveformCanvas != null)
            e.Pointer.Capture(null);
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        this.Close();
    }

    private async void ApplyTrim_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        StopPlayback();

        if (this.DataContext is RecordViewModel vm)
            await vm.ApplyTrimAsync(_leftTrimPos, _rightTrimPos);

        this.Close();
    }

    private void ZoomIn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _zoomLevel = Math.Min(_zoomLevel * 1.5, 10);
        if (this.DataContext is RecordViewModel vm)
            DrawWaveform(vm.CurrentWaveform);
    }

    private void ZoomOut_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _zoomLevel = Math.Max(_zoomLevel / 1.5, 1);
        if (this.DataContext is RecordViewModel vm)
            DrawWaveform(vm.CurrentWaveform);
    }

    private void Play_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _playButton = sender as Button;

        if (_isPlaying)
        {
            StopPlayback();
            return;
        }

        if (this.DataContext is not RecordViewModel vm || vm.SelectedRecordingForEdit == null)
            return;

        try
        {
            long trimStartSample = (long)(_leftTrimPos * (vm.CurrentWaveform?.TotalSamples ?? 0));
            long trimEndSample = (long)(_rightTrimPos * (vm.CurrentWaveform?.TotalSamples ?? 0));

            StartPlayback(vm.SelectedRecordingForEdit.FilePath, trimStartSample, trimEndSample);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Playback error: {ex.Message}");
        }
    }

    private void StartPlayback(string filePath, long startSample, long endSample)
    {
        try
        {
            // Load the file
            _playbackChannel = Bass.CreateStream(filePath, 0, 0, BassFlags.Default);
            if (_playbackChannel == 0)
                return;

            // Set start position (BASS uses bytes, need to calculate from samples)
            var info = Bass.ChannelGetInfo(_playbackChannel);
            long startBytes = (startSample * info.Channels * 2); // 2 bytes per sample (16-bit)
            Bass.ChannelSetPosition(_playbackChannel, startBytes);

            // Play
            Bass.ChannelPlay(_playbackChannel);
            _isPlaying = true;
            if (_playButton != null)
                _playButton.Content = "⏹ Stop";

            // Monitor playback (stop when we reach end position)
            var timer = new System.Timers.Timer(100);
            long endBytes = (endSample * info.Channels * 2);
            timer.Elapsed += (s, e) =>
            {
                long currentPos = Bass.ChannelGetPosition(_playbackChannel);
                if (currentPos >= endBytes || !Bass.ChannelIsActive(_playbackChannel).HasFlag(PlaybackState.Playing))
                {
                    timer.Stop();
                    StopPlayback();
                }
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Playback error: {ex.Message}");
            _isPlaying = false;
        }
    }

    private void StopPlayback()
    {
        if (_playbackChannel != 0)
        {
            Bass.ChannelStop(_playbackChannel);
            Bass.StreamFree(_playbackChannel);
            _playbackChannel = 0;
        }
        _isPlaying = false;
        if (_playButton != null)
            _playButton.Content = "▶ Play";
    }
}
