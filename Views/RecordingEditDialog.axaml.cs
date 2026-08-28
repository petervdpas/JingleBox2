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
using JingleBox2.Machines.Ui;
using JingleBox2.Waveform.Enums;

namespace JingleBox2.Views;

/// <summary>
/// One take, its picture, and the two things that can be done to it: trimmed to what is
/// selected, and lifted to full level.
/// </summary>
/// <remarks>
/// Wiring only: pointer and button events in, redraws out. The viewport maths, the trim rules
/// and the playback lifecycle live in JingleBox2.Waveform, and the outline building in
/// JingleBox2.Machines.Ui, where the panel's own picture of a recording needs it too. That is
/// what makes those testable without a window; there is nothing here that could be.
///
/// Both edits rewrite the file where it lies rather than making a new take, so the window
/// stays open afterwards and the picture is drawn again from what is now on the disc.
/// </remarks>
public partial class RecordingEditDialog : Window
{
    /// <summary>How wide a trim handle is drawn. Narrow, because it is a boundary and not a control.</summary>
    private const double TrimHandleWidth = 3;

    /// <summary>
    /// How near the pointer has to be to a handle to take hold of it. Wider than the handle is
    /// drawn, because three pixels is not something a hand can be expected to hit.
    /// </summary>
    private const double TrimGrabTolerance = 10;

    /// <summary>
    /// How far a press may move and still count as a click rather than a drag. Without this,
    /// dropping the play cursor would be impossible: a hand moves a pixel or two on the way
    /// down and the release would be read as a pan of nothing.
    /// </summary>
    private const double ClickSlop = 4;

    /// <summary>How much closer the buttons take you. A step you can see in one press.</summary>
    private const double ButtonZoomStep = 1.5;

    /// <summary>
    /// How much one notch of the wheel takes you, which is gentler than a button, since a wheel
    /// is turned several notches at a time and a button is pressed once.
    /// </summary>
    private const double WheelZoomStep = 1.25;

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

    /// <summary>What the pointer looks like over a picture that can be dragged sideways.</summary>
    private static readonly Cursor PanCursor = new(StandardCursorType.Hand);

    /// <summary>What the pointer looks like over a trim handle, which moves in one axis only.</summary>
    private static readonly Cursor ResizeCursor = new(StandardCursorType.SizeWestEast);

    /// <summary>How much of the file is on screen and where, which every X on the canvas goes through.</summary>
    private readonly WaveformViewport _viewport = new();

    /// <summary>What would survive the cut, as two fractions of the file.</summary>
    private readonly TrimSelection _trim = new();

    /// <summary>What plays the preview, and what reports where it has got to.</summary>
    private readonly WaveformPlayer _player = new();

    /// <summary>
    /// Where the picture is drawn. Found once the window is up rather than in the constructor,
    /// since it does not exist until the template has been applied.
    /// </summary>
    private Canvas? _canvas;

    /// <summary>Kept because its wording is written to: it says Play or Stop as the preview runs.</summary>
    private Button? _playButton;

    /// <summary>
    /// The line showing where playback has got to, kept apart from the rest of the picture so
    /// it can be moved without the outline being built again.
    /// </summary>
    private Rectangle? _playheadMarker;

    /// <summary>
    /// The RECORD page's view model, which owns the take being edited. Kept so its changes can
    /// be let go of when the window is pointed at another one.
    /// </summary>
    private RecordViewModel? _vm;

    /// <summary>Which handle the hand has hold of, or none when it has hold of neither.</summary>
    private TrimHandle _dragging = TrimHandle.None;

    /// <summary>Whether the hand is dragging the picture sideways rather than a handle.</summary>
    private bool _panning;

    /// <summary>Where the press landed, which both the pan and the click test are measured from.</summary>
    private double _pressX;

    /// <summary>
    /// Where the window was when the pan began. The pan is worked out from the press rather
    /// than from the last move, so a drag that stops and starts again does not drift.
    /// </summary>
    private double _panStartScroll;

    /// <summary>Where playback starts, as a fraction of the file. Null means the trim start.</summary>
    private double? _playStart;

    /// <summary>
    /// Live position while playing. Null when stopped, which is what makes the marker fall back
    /// to showing the play cursor rather than freezing where the preview ended.
    /// </summary>
    private double? _playhead;

    /// <summary>Guards against a second Apply landing while the file is being rewritten.</summary>
    private bool _applying;

    /// <summary>The same, for a rename: the file is moving and cannot move twice.</summary>
    private bool _renaming;

    /// <summary>How wide the picture is, which is what every fraction is converted through.</summary>
    private double CanvasWidth => _canvas?.Width ?? 0;

    /// <summary>
    /// Builds the window and wires the picture up: the player's reports in, the pointer
    /// gestures out.
    /// </summary>
    /// <remarks>
    /// The canvas and the play button are found when the window loads rather than here, since
    /// neither exists until the template has been applied.
    ///
    /// The view model's changes are let go of before being taken again, because the data
    /// context announcement fires on every reassignment and would otherwise leave the window
    /// subscribed to every take it had ever shown.
    /// </remarks>
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
            _playhead = null;
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

    /// <summary>
    /// Draws the picture again when the take being shown changes. Only that one property: the
    /// outline can carry thousands of points and nothing else on the page changes it.
    /// </summary>
    private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecordViewModel.CurrentWaveform))
            Redraw();
    }

    /// <summary>
    /// Builds the whole picture: the outline, the centre line, the selection, the two handles
    /// and the playhead.
    /// </summary>
    /// <remarks>
    /// The centre line goes on top of the fill rather than behind it. The outline is mirrored
    /// around that exact row, so behind it the line would be hidden everywhere except in true
    /// silence, which is the one place nobody needs it.
    ///
    /// The playhead is kept as a field so playback can move it without the outline being built
    /// again.
    /// </remarks>
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

    /// <summary>
    /// Tints what would survive the cut, clipped to the canvas so a selection running off
    /// either edge is drawn only as far as there is room for it.
    /// </summary>
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

    /// <summary>
    /// Takes hold of a handle if the press landed near one, and of the picture otherwise.
    /// </summary>
    /// <remarks>
    /// The pointer is captured either way, so a drag that leaves the canvas goes on being the
    /// same drag. A press on nothing, with nothing to pan, is left uncaptured: the release then
    /// reads as a click and drops the play cursor.
    /// </remarks>
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

    /// <summary>
    /// Moves whichever of the two the hand has hold of, and draws the picture again.
    /// </summary>
    /// <remarks>
    /// A pan to the right moves the window earlier, so the audio tracks the cursor rather than
    /// running away from it: the hand is dragging the recording, not the viewport.
    /// </remarks>
    private void Canvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_canvas == null) return;
        if (_dragging == TrimHandle.None && !_panning) return;

        var point = e.GetPosition(_canvas);

        if (_panning)
        {
            _viewport.ScrollTo(_panStartScroll - _viewport.PanDistance(point.X - _pressX, CanvasWidth));
        }
        else
        {
            _trim.Move(_dragging, _viewport.XToFraction(point.X, CanvasWidth), TrimSelection.MinGapFor(_viewport));
        }

        Redraw();
    }

    /// <summary>
    /// Lets go, and drops the play cursor if the press turned out to be a click.
    /// </summary>
    /// <remarks>
    /// A press that never moved a handle and travelled less than <see cref="ClickSlop"/> is a
    /// click, whether or not it was also panning: a pan of nothing has done nothing, and asking
    /// the hand to hold still to within a pixel would make the play cursor unusable.
    /// </remarks>
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

    /// <summary>
    /// The wheel zooms about the pointer, so what is under it stays under it.
    /// </summary>
    /// <remarks>
    /// Handled whether or not the zoom moved anything, since a wheel at the end of its range
    /// scrolling the window behind the picture is not what the gesture meant.
    /// </remarks>
    private void Canvas_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_canvas == null || CanvasWidth <= 0) return;

        var point = e.GetPosition(_canvas);
        double factor = e.Delta.Y > 0 ? WheelZoomStep : 1 / WheelZoomStep;

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

    /// <summary>
    /// Closer in, about the middle of what is showing rather than about the pointer, since a
    /// button press says nothing about where the pointer is on the picture.
    /// </summary>
    private void ZoomIn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewport.ZoomTo(_viewport.Zoom * ButtonZoomStep);
        Redraw();
    }

    /// <summary>Further out, by the same step, and stopping at the whole file.</summary>
    private void ZoomOut_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewport.ZoomTo(_viewport.Zoom / ButtonZoomStep);
        Redraw();
    }

    /// <summary>
    /// Plays what would survive the cut, from the play cursor, or stops what is playing.
    /// </summary>
    /// <remarks>
    /// One button for both, and its wording is written rather than bound, because the player is
    /// not a view model and its stopping is an event: it also ends on its own at the trim's end.
    /// </remarks>
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

    /// <summary>Writes the wording on the play button, which says what pressing it now would do.</summary>
    private void SetPlayButtonContent(string text)
    {
        if (_playButton != null) _playButton.Content = text;
    }

    /// <summary>
    /// Closes the window. Nothing is undone by it: trimming and normalising rewrite the file
    /// when they are pressed, so there is nothing pending for this to abandon.
    /// </summary>
    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    /// <summary>
    /// Gives the recording another name. The dialog stays open: renaming is not finishing, and
    /// the usual next thing is to trim what you have just named.
    /// </summary>
    /// <remarks>
    /// The preview is stopped first. Playing from inside the dialog holds the file open, and a
    /// file that is open is one that will not move on Windows.
    /// </remarks>
    private async void Rename_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm == null || _renaming) return;

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

    /// <summary>
    /// Cuts the file down to what is selected, and rewrites it.
    /// </summary>
    /// <remarks>
    /// Afterwards every stored position points at audio that no longer exists, so the trim, the
    /// play cursor, the playhead and the zoom are all put back to the whole file: what survived
    /// the cut is the whole file from here on.
    ///
    /// Both destructive buttons are switched off while it runs, and the preview is stopped
    /// first, since a file that is open is one that will not be rewritten on Windows.
    /// </remarks>
    private async void ApplyTrim_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm == null || _applying) return;

        _player.Stop();

        _applying = true;
        SetApplyEnabled(false);

        try
        {
            if (!await _vm.ApplyTrimAsync(_trim.Start, _trim.End)) return;

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
