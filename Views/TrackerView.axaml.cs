using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Reactive;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

/// <summary>
/// Wiring only. Key handling maps a keystroke to one call on the view model; what the
/// keystroke means lives in <see cref="KeyboardNoteMap"/> and <see cref="PatternEdit"/>.
/// </summary>
public partial class TrackerView : UserControl
{
    /// <summary>
    /// The shelf of instruments, handed in from outside.
    /// </summary>
    /// <remarks>
    /// A property rather than something the tracker's own view model holds, because the library
    /// is built with the tracker and would otherwise have to be handed back to it afterwards.
    /// The view needs it to show a page; the tracker itself does not need it at all.
    /// </remarks>
    public static readonly Avalonia.StyledProperty<object?> InstrumentsProperty =
        Avalonia.AvaloniaProperty.Register<TrackerView, object?>(nameof(Instruments));

    public object? Instruments
    {
        get => GetValue(InstrumentsProperty);
        set => SetValue(InstrumentsProperty, value);
    }

    public TrackerView()
    {
        InitializeComponent();

        Grid.CursorMoved += (_, cursor) => ViewModel?.SetCursor(cursor);
        AddHandler(KeyDownEvent, OnGridKeyDown, RoutingStrategies.Tunnel);

        // The header sits outside the scroll area, so it has to be told how far the pattern
        // has scrolled sideways and what character width the grid settled on.
        Header.TrackClicked += (_, track) => SelectTrack(track);

        SetUpDragAndDrop();
        GridScroll.GetObservable(ScrollViewer.OffsetProperty)
            .Subscribe(new AnonymousObserver<Vector>(offset => Header.ScrollOffset = offset.X));

        Grid.LayoutUpdated += (_, _) =>
        {
            var metrics = Grid.Metrics;
            if (metrics.CharWidth > 0) Header.CharWidth = metrics.CharWidth;
            Header.RowHeight = Grid.RowHeight;
        };

        // Follow the cursor, and follow the player while it is running.
        Grid.GetObservable(PatternGrid.EditCursorProperty)
            .Subscribe(new AnonymousObserver<PatternCursor>(FollowCursor));

        Grid.GetObservable(PatternGrid.PlayingLineProperty)
            .Subscribe(new AnonymousObserver<int>(FollowPlayhead));
    }

    private TrackerViewModel? ViewModel => DataContext as TrackerViewModel;

    /// <summary>
    /// The two things that can be dragged here.
    /// </summary>
    /// <remarks>
    /// An instrument dragged from the list onto a track points that track at it, and offers to
    /// bring the notes already written there along. A track dragged by its own header moves the
    /// whole track: its notes, its instrument, its effects and its mixer strip.
    ///
    /// Both land on the same two surfaces, so which one a drop means comes from the format the
    /// drag carries rather than from where it was let go.
    /// </remarks>
    private void SetUpDragAndDrop()
    {
        // Bubble with handledEventsToo: the ListBox marks the press handled once it has
        // updated the selection, which is exactly the state the drag needs to read.
        InstrumentList.AddHandler(PointerPressedEvent, OnInstrumentPointerPressed,
            RoutingStrategies.Bubble, handledEventsToo: true);

        // The header marks the press handled once it has selected the track, so this reads it
        // the same way and for the same reason.
        Header.AddHandler(PointerPressedEvent, OnHeaderPointerPressed,
            RoutingStrategies.Bubble, handledEventsToo: true);

        // The whole track column takes a drop, not just its header.
        DragDrop.SetAllowDrop(Header, true);
        Header.AddHandler(DragDrop.DragOverEvent, OnHeaderDragOver);
        Header.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        Header.AddHandler(DragDrop.DropEvent, OnHeaderDrop);

        DragDrop.SetAllowDrop(Grid, true);
        Grid.AddHandler(DragDrop.DragOverEvent, OnGridDragOver);
        Grid.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        Grid.AddHandler(DragDrop.DropEvent, OnGridDrop);
    }

    private async void OnInstrumentPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(InstrumentList).Properties.IsLeftButtonPressed) return;
        if (InstrumentList.SelectedItem is not InstrumentSlot slot) return;

        // Releasing without moving simply ends the drag with no effect, so this does not get
        // in the way of clicking a row to select it.
        await DragDrop.DoDragDropAsync(e, InstrumentDragData.For(slot.Index), DragDropEffects.Link);
    }

    /// <summary>
    /// Picks a track up. Releasing without moving ends the drag with no effect, so this does
    /// not get in the way of clicking a header to select the track.
    /// </summary>
    private async void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Header).Properties.IsLeftButtonPressed) return;

        int track = Header.TrackAtPoint(e.GetPosition(Header));
        if (track < 0) return;

        await DragDrop.DoDragDropAsync(e, TrackDragData.For(track), DragDropEffects.Move);
    }

    private void OnHeaderDragOver(object? sender, DragEventArgs e) =>
        HandleDragOver(e, Header.TrackAtPoint(e.GetPosition(Header)));

    private void OnGridDragOver(object? sender, DragEventArgs e) =>
        HandleDragOver(e, Grid.TrackAtPoint(e.GetPosition(Grid)));

    private void OnHeaderDrop(object? sender, DragEventArgs e) =>
        HandleDrop(e, Header.TrackAtPoint(e.GetPosition(Header)));

    private void OnGridDrop(object? sender, DragEventArgs e) =>
        HandleDrop(e, Grid.TrackAtPoint(e.GetPosition(Grid)));

    private void HandleDragOver(DragEventArgs e, int track)
    {
        int moving = TrackDragData.IndexFrom(e.DataTransfer);

        if (moving >= 0)
        {
            bool somewhere = track >= 0 && track != moving;

            // Both surfaces light up together, so the header names the column being targeted.
            ShowDropTarget(somewhere ? track : -1);

            e.DragEffects = somewhere ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
            return;
        }

        int instrument = InstrumentDragData.IndexFrom(e.DataTransfer);
        bool valid = instrument >= 0 && track >= 0;

        ShowDropTarget(valid ? track : -1);

        e.DragEffects = valid ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private async void HandleDrop(DragEventArgs e, int track)
    {
        ShowDropTarget(-1);

        // A track being moved and an instrument being pointed at a track arrive the same way
        // and mean different things, so which it is comes from the format it carries.
        int moving = TrackDragData.IndexFrom(e.DataTransfer);

        if (moving >= 0)
        {
            e.Handled = true;
            if (track >= 0) ViewModel?.MoveTrack(moving, track);
            return;
        }

        int instrument = InstrumentDragData.IndexFrom(e.DataTransfer);
        if (instrument < 0 || track < 0) return;

        e.Handled = true;

        // Awaited rather than left running: binding an instrument to a track asks whether the
        // notes already on it should follow, and the dialog cannot be raised from inside the
        // drop itself.
        var model = ViewModel;
        if (model != null) await model.AssignInstrumentToTrack(track, instrument);
    }

    private void OnDragLeave(object? sender, DragEventArgs e) => ShowDropTarget(-1);

    private void ShowDropTarget(int track)
    {
        Header.DropTargetTrack = track;
        Grid.DropTargetTrack = track;
    }

    private void FollowCursor(PatternCursor cursor)
    {
        ScrollToRow(cursor.Line);
        ScrollToTrack(cursor.Track);
    }

    private void FollowPlayhead(int line)
    {
        // Only chase the playhead while it is actually moving, or stopping would yank the
        // view away from wherever the cursor was left.
        if (line >= 0 && ViewModel?.IsPlaying == true) ScrollToRow(line);
    }

    private void ScrollToRow(int row)
    {
        var pattern = Grid.Pattern;
        if (pattern == null) return;

        double offset = ViewportScroller.KeepRowVisible(
            GridScroll.Offset.Y, GridScroll.Viewport.Height, Grid.Metrics, row, pattern.Lines);

        SetScrollOffset(offset, GridScroll.Offset.X);
    }

    private void ScrollToTrack(int track)
    {
        if (Grid.Pattern == null) return;

        double offset = ViewportScroller.KeepTrackVisible(
            GridScroll.Offset.X, GridScroll.Viewport.Width, Grid.Metrics, track);

        SetScrollOffset(GridScroll.Offset.Y, offset);
    }

    private void SetScrollOffset(double y, double x)
    {
        // Writing an unchanged offset would restart the scroll animation on every keystroke.
        if (Math.Abs(x - GridScroll.Offset.X) < 0.5 && Math.Abs(y - GridScroll.Offset.Y) < 0.5) return;

        GridScroll.Offset = new Vector(x, y);
    }

    private void SelectTrack(int track)
    {
        var vm = ViewModel;
        if (vm == null) return;

        vm.SetCursor(vm.Cursor with { Track = track });
        Grid.Focus();
    }

    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null || !Grid.IsFocused) return;

        // Shift with a movement key grows the block instead of moving away from it.
        bool extend = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        switch (e.Key)
        {
            case Key.Up: vm.MoveCursor(-1, 0, 0, extend); e.Handled = true; return;
            case Key.Down: vm.MoveCursor(1, 0, 0, extend); e.Handled = true; return;
            case Key.Left: vm.MoveCursor(0, extend ? -1 : 0, extend ? 0 : -1, extend); e.Handled = true; return;
            case Key.Right: vm.MoveCursor(0, extend ? 1 : 0, extend ? 0 : 1, extend); e.Handled = true; return;
            case Key.PageUp: vm.MoveCursor(-vm.LinesPerBeat * 4, 0, 0, extend); e.Handled = true; return;
            case Key.PageDown: vm.MoveCursor(vm.LinesPerBeat * 4, 0, 0, extend); e.Handled = true; return;
            case Key.Tab:
                vm.MoveCursor(0, extend ? -1 : 1, 0);
                e.Handled = true;
                return;
            case Key.A when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                vm.SelectAll();
                e.Handled = true;
                return;
            case Key.C when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                vm.CopySelection();
                e.Handled = true;
                return;
            case Key.X when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                vm.CutSelection();
                e.Handled = true;
                return;
            case Key.V when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                vm.Paste();
                e.Handled = true;
                return;
            case Key.Escape: vm.ClearSelection(); e.Handled = true; return;
            case Key.Delete: vm.ClearAtCursor(); e.Handled = true; return;
            case Key.Insert: vm.InsertLine(); e.Handled = true; return;
            case Key.Back: vm.DeleteLine(); e.Handled = true; return;
        }

        string key = e.Key.ToString();

        if (KeyboardNoteMap.IsNoteOff(key))
        {
            vm.EnterNoteOff();
            e.Handled = true;
            return;
        }

        if (vm.Cursor.Column == CellColumn.Note)
        {
            // Only here: on the other columns the digit row types values.
            if (KeyboardNoteMap.IsNoteOffInNotes(key))
            {
                vm.EnterNoteOff();
                e.Handled = true;
                return;
            }

            if (KeyboardNoteMap.NoteFor(key, vm.Octave) is Note note)
            {
                vm.EnterNote(note);
                e.Handled = true;
            }
            return;
        }

        // Hex entry for the instrument, volume, and effect parameter columns.
        char typed = KeyToChar(e.Key);
        if (typed != '\0')
        {
            if (vm.Cursor.Column == CellColumn.Effect && char.IsLetter(typed) && !IsHexLetter(typed))
                vm.EnterEffectCommand(typed);
            else
                vm.EnterHexDigit(typed);

            e.Handled = true;
        }
    }

    private static bool IsHexLetter(char c) => c is >= 'A' and <= 'F';

    private static char KeyToChar(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => (char)('0' + (key - Key.D0)),
        >= Key.NumPad0 and <= Key.NumPad9 => (char)('0' + (key - Key.NumPad0)),
        >= Key.A and <= Key.Z => (char)('A' + (key - Key.A)),
        _ => '\0'
    };
}
