using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.Models;
using JingleBox2.ViewModels;
using JingleBox2.Waveform;
using System;
using System.ComponentModel;

namespace JingleBox2.Views;

/// <summary>
/// Wiring only: pointer and button events in, redraws out. The viewport maths, the trim
/// rules, the outline building and the playback lifecycle all live in JingleBox2.Waveform.
/// </summary>
public partial class RecordingEditDialog : Window
{
    private const double TrimHandleWidth = 3;
    private const double TrimGrabTolerance = 10;
    private const double ClickSlop = 4; // a press moving less than this is a click, not a drag

    /// <summary>
    /// What the picture is painted with, read fresh on every redraw.
    /// </summary>
    /// <remarks>
    /// The same four parts the slice editor draws and in the same colours: the outline in the
    /// accent, the centre line in the muted text colour, the playhead in the text colour. The
    /// handles are the one exception, taking the theme's danger colour, because they are the
    /// only thing here that has to be found rather than looked at, and in a theme built out of
    /// one hue the playhead and the handle would otherwise be the same line twice.
    /// </remarks>
    private ThemePalette Palette => ThemePalette.From(this);
    private static readonly Cursor PanCursor = new(StandardCursorType.Hand);
    private static readonly Cursor ResizeCursor = new(StandardCursorType.SizeWestEast);

    private readonly WaveformViewport _viewport = new();
    private readonly TrimSelection _trim = new();
    private readonly WaveformPlayer _player = new();

    private Canvas? _canvas;
    private Button? _playButton;
    private Rectangle? _playheadMarker;
    private RecordViewModel? _vm;

    private TrimHandle _dragging = TrimHandle.None;
    private bool _panning;
    private double _pressX;
    private double _panStartScroll;

    /// <summary>Where playback starts, as a fraction of the file. Null means the trim start.</summary>
    private double? _playStart;

    /// <summary>Live position while playing. Null when stopped.</summary>
    private double? _playhead;

    /// <summary>Guards against a second Apply landing while the file is being rewritten.</summary>
    private bool _applying;

    /// <summary>The same, for a rename: the file is moving and cannot move twice.</summary>
    private bool _renaming;

    private double CanvasWidth => _canvas?.Width ?? 0;

    public RecordingEditDialog()
    {
        InitializeComponent();

        _player.PositionChanged += position =>
        {
            _playhead = position;
            UpdatePlayhead();
        };

        _player.Stopped += () =>
        {
            _playhead = null; // fall back to showing the play cursor
            UpdatePlayhead();
            SetPlayButtonContent("▶ Play");
        };

        Loaded += (_, _) =>
        {
            _playButton = this.FindControl<Button>("PlayButton");
            _canvas = this.FindControl<Canvas>("EditWaveformCanvas");

            if (_canvas == null) return;

            _canvas.PointerPressed += Canvas_PointerPressed;
            _canvas.PointerMoved += Canvas_PointerMoved;
            _canvas.PointerReleased += Canvas_PointerReleased;
            _canvas.PointerWheelChanged += Canvas_PointerWheelChanged;

            Redraw();
        };

        DataContextChanged += (_, _) =>
        {
            // Unsubscribe first: this fires again on every reassignment and would otherwise leak.
            if (_vm != null) _vm.PropertyChanged -= ViewModelPropertyChanged;

            _vm = DataContext as RecordViewModel;

            if (_vm != null) _vm.PropertyChanged += ViewModelPropertyChanged;
            Redraw();
        };

        Closing += (_, _) =>
        {
            _player.Dispose();
            if (_vm != null) _vm.PropertyChanged -= ViewModelPropertyChanged;
        };
    }

    private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecordViewModel.CurrentWaveform))
            Redraw();
    }

    // ---- drawing ----------------------------------------------------------------

    private void Redraw()
    {
        if (_canvas == null) return;

        _canvas.Children.Clear();
        _playheadMarker = null;

        var waveform = _vm?.CurrentWaveform;
        double width = CanvasWidth;
        double height = _canvas.Height;

        if (waveform == null || waveform.PeakData.Length == 0 || width <= 0 || height <= 0) return;

        _canvas.Cursor = _viewport.CanPan ? PanCursor : Cursor.Default;

        var palette = Palette;

        _canvas.Children.Add(new Path
        {
            Data = WaveformGeometry.Build(waveform.PeakData, _viewport, width, height),
            Fill = palette.AccentBrush,
            Opacity = 0.85
        });

        // On top of the fill: the outline is mirrored around this exact row, so behind it
        // the line would be hidden everywhere except in true silence.
        _canvas.Children.Add(new Line
        {
            StartPoint = new Point(0, height / 2),
            EndPoint = new Point(width, height / 2),
            Stroke = palette.MutedBrush,
            StrokeThickness = 1,
            Opacity = 0.5
        });

        AddSelectionOverlay(width, height);

        double startX = _viewport.FractionToX(_trim.Start, width);
        double endX = _viewport.FractionToX(_trim.End, width);
        AddTrimHandle(startX, width, height);
        AddTrimHandle(endX - TrimHandleWidth, width, height);

        // Kept as a field so playback can move it without rebuilding the outline.
        _playheadMarker = new Rectangle
        {
            Fill = palette.TextBrush,
            Width = 1.5,
            Height = height,
            Opacity = 0.9,
            IsHitTestVisible = false
        };
        Canvas.SetTop(_playheadMarker, 0);
        _canvas.Children.Add(_playheadMarker);

        UpdatePlayhead();
    }

    private void AddSelectionOverlay(double width, double height)
    {
        double left = Math.Max(0, _viewport.FractionToX(_trim.Start, width));
        double right = Math.Min(width, _viewport.FractionToX(_trim.End, width));

        if (right <= left) return;

        var selection = new Rectangle
        {
            Fill = Palette.AccentBrush,
            Width = right - left,
            Height = height,
            Opacity = 0.2
        };
        Canvas.SetLeft(selection, left);
        Canvas.SetTop(selection, 0);
        _canvas!.Children.Add(selection);
    }

    /// <summary>
    /// Paints a handle only when its true position is on screen. One scrolled out of view
    /// stays where it belongs in the file rather than being pinned to the canvas edge, where
    /// it would look grabbable but point at the wrong sample.
    /// </summary>
    private void AddTrimHandle(double x, double width, double height)
    {
        if (x + TrimHandleWidth < 0 || x > width) return;

        var handle = new Rectangle
        {
            Fill = Palette.DangerBrush,
            Width = TrimHandleWidth,
            Height = height,
            Opacity = 0.9,
            Cursor = ResizeCursor
        };
        Canvas.SetLeft(handle, x);
        Canvas.SetTop(handle, 0);
        _canvas!.Children.Add(handle);
    }

    /// <summary>
    /// Moves the playhead without rebuilding the outline, which can carry thousands of points
    /// and would otherwise be regenerated ten times a second during playback.
    /// </summary>
    private void UpdatePlayhead()
    {
        if (_playheadMarker == null) return;

        double? fraction = _playhead ?? _playStart;
        if (fraction is null)
        {
            _playheadMarker.IsVisible = false;
            return;
        }

        double x = _viewport.FractionToX(fraction.Value, CanvasWidth);
        _playheadMarker.IsVisible = _viewport.IsOnScreen(x, CanvasWidth);
        Canvas.SetLeft(_playheadMarker, x);
    }

    // ---- pointer ----------------------------------------------------------------

    private void Canvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_canvas == null) return;

        var point = e.GetPosition(_canvas);
        _pressX = point.X;

        _dragging = _trim.HitTest(point.X, _viewport, CanvasWidth, TrimGrabTolerance);

        if (_dragging == TrimHandle.None && _viewport.CanPan)
        {
            _panning = true;
            _panStartScroll = _viewport.Scroll;
        }

        if (_dragging != TrimHandle.None || _panning)
            e.Pointer.Capture(_canvas);
    }

    private void Canvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_canvas == null) return;
        if (_dragging == TrimHandle.None && !_panning) return;

        var point = e.GetPosition(_canvas);

        if (_panning)
        {
            // Drag right to move the window earlier, so the audio tracks the cursor.
            _viewport.ScrollTo(_panStartScroll - _viewport.PanDistance(point.X - _pressX, CanvasWidth));
        }
        else
        {
            _trim.Move(_dragging, _viewport.XToFraction(point.X, CanvasWidth), TrimSelection.MinGapFor(_viewport));
        }

        Redraw();
    }

    private void Canvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_canvas != null && _dragging == TrimHandle.None)
        {
            var point = e.GetPosition(_canvas);
            if (Math.Abs(point.X - _pressX) <= ClickSlop)
                SetPlayStart(_viewport.XToFraction(point.X, CanvasWidth));
        }

        _dragging = TrimHandle.None;
        _panning = false;
        e.Pointer.Capture(null);
    }

    private void Canvas_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_canvas == null || CanvasWidth <= 0) return;

        var point = e.GetPosition(_canvas);
        double factor = e.Delta.Y > 0 ? 1.25 : 1 / 1.25;

        if (_viewport.ZoomAt(_viewport.Zoom * factor, point.X, CanvasWidth))
            Redraw();

        e.Handled = true;
    }

    /// <summary>
    /// Drops the play cursor, clamped into the trim region so Play always previews audio that
    /// will survive the cut. Seeks straight away if something is already playing.
    /// </summary>
    private void SetPlayStart(double fraction)
    {
        _playStart = _trim.Clamp(fraction);

        if (_player.IsPlaying)
            _player.SeekTo(_playStart.Value);
        else
            _playhead = null;

        UpdatePlayhead();
    }

    // ---- buttons ----------------------------------------------------------------

    private void ZoomIn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewport.ZoomTo(_viewport.Zoom * 1.5);
        Redraw();
    }

    private void ZoomOut_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewport.ZoomTo(_viewport.Zoom / 1.5);
        Redraw();
    }

    private void Play_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_player.IsPlaying)
        {
            _player.Stop();
            return;
        }

        if (_vm?.SelectedRecordingForEdit == null || _vm.CurrentWaveform == null) return;

        _player.Play(
            _vm.SelectedRecordingForEdit.FilePath,
            _trim.Clamp(_playStart ?? _trim.Start),
            _trim.End,
            _vm.CurrentWaveform.TotalSamples);

        if (_player.IsPlaying)
            SetPlayButtonContent("⏹ Stop");
    }

    private void SetPlayButtonContent(string text)
    {
        if (_playButton != null) _playButton.Content = text;
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    /// <summary>
    /// Gives the recording another name. The dialog stays open: renaming is not finishing, and
    /// the usual next thing is to trim what you have just named.
    /// </summary>
    private async void Rename_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm == null || _renaming) return;

        // Playing from inside the dialog holds the file open, and a file that is open is one
        // that will not move on Windows.
        _player.Stop();

        _renaming = true;

        try
        {
            await _vm.RenameAsync(_vm.EditName);
        }
        finally
        {
            _renaming = false;
        }
    }

    private async void ApplyTrim_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm == null || _applying) return;

        _player.Stop();

        _applying = true;
        SetApplyEnabled(false);

        try
        {
            if (!await _vm.ApplyTrimAsync(_trim.Start, _trim.End)) return;

            // The file has been rewritten, so every stored position now points at audio that
            // no longer exists. What survived the cut is the whole file from here on.
            _trim.Reset();
            _playStart = null;
            _playhead = null;
            _viewport.ZoomTo(WaveformViewport.MinZoom);

            Redraw();
        }
        finally
        {
            _applying = false;
            SetApplyEnabled(true);
        }
    }

    /// <summary>
    /// Lifts the file's level. The audio changes under every stored position but the timeline
    /// does not, so the trim region and the playhead stay where they are.
    /// </summary>
    private async void Normalize_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm == null || _applying) return;

        _player.Stop();

        _applying = true;
        SetApplyEnabled(false);

        try
        {
            if (await _vm.NormalizeAsync()) Redraw();
        }
        finally
        {
            _applying = false;
            SetApplyEnabled(true);
        }
    }

    /// <summary>
    /// Both destructive buttons go together: while the file is being rewritten, neither the
    /// trim nor the normalize may start a second write over the top of it.
    /// </summary>
    private void SetApplyEnabled(bool enabled)
    {
        var trim = this.FindControl<Button>("ApplyTrimButton");
        if (trim != null) trim.IsEnabled = enabled;

        var normalize = this.FindControl<Button>("NormalizeButton");
        if (normalize != null) normalize.IsEnabled = enabled;
    }
}
