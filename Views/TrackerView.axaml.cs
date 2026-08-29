using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Reactive;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.Music;
using JingleBox2.UI;
using JingleBox2.Music.Interfaces;
using JingleBox2.UI.Interfaces;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <summary>
/// The TRACKER page: the pattern, its header, the order, the instruments beside it and the
/// strips that fold away underneath.
/// </summary>
/// <remarks>
/// Wiring only. Key handling maps a keystroke to one call on the view model; what the keystroke
/// means lives in <see cref="KeyboardNoteMap"/> and <see cref="PatternEdit"/>, which is what
/// makes both testable without a window.
///
/// Two things here exist because of the toolkit rather than because of the music. The grid is
/// measured inside the scroll viewer with no height limit and never learns how tall the hole it
/// is seen through is, so this page measures that and tells it. And the toolkit's own drag and
/// drop draws nothing at all on X11, so the picture in the hand is this page's own; see
/// <see cref="DragGhost"/>.
/// </remarks>
public partial class TrackerView : UserControl
{
    /// <summary>What a dragged track carries. Holds nothing, so one serves the page.</summary>
    private static readonly IDragPayload Tracks = new TrackDragData();

    /// <summary>And what a dragged instrument carries.</summary>
    private static readonly IDragPayload DraggedInstrument = new InstrumentDragData();

    /// <summary>A machine's colour mixed into the theme's. Holds nothing, so one is enough.</summary>
    private static readonly IMachineTint Tint = new MachineTint();

    /// <summary>Which letter sounds which note.</summary>
    private readonly IKeyboardNoteMap _keys = new KeyboardNoteMap();

    /// <summary>
    /// Which note each letter key is currently sounding, so its release names the same note.
    /// </summary>
    /// <remarks>
    /// Read back rather than worked out again from the key: the octave can be changed between
    /// a press and its release, and a release that named a different note would leave the first
    /// one held for the rest of the session and every note after it read as part of a chord.
    /// </remarks>
    private readonly Dictionary<Key, Note> _typed = new();

    /// <summary>Where the pattern has to sit for the cursor to stay on the middle.</summary>
    private readonly IViewportScroller _scroll = new ViewportScroller();

    /// <summary>
    /// The shelf of instruments, handed in from outside.
    /// </summary>
    /// <remarks>
    /// A property rather than something the tracker's own view model holds, because the rack
    /// is built with the tracker and would otherwise have to be handed back to it afterwards.
    /// The view needs it to show a page; the tracker itself does not need it at all.
    /// </remarks>
    public static readonly Avalonia.StyledProperty<MachineRackViewModel?> InstrumentsProperty =
        Avalonia.AvaloniaProperty.Register<TrackerView, MachineRackViewModel?>(nameof(Instruments));

    /// <inheritdoc cref="InstrumentsProperty"/>
    public MachineRackViewModel? Instruments
    {
        get => GetValue(InstrumentsProperty);
        set => SetValue(InstrumentsProperty, value);
    }

    /// <summary>What is in the hand while something is being dragged. See <see cref="DragGhost"/>.</summary>
    private readonly DragGhost _ghost;

    /// <summary>
    /// Builds the page and joins the pattern grid, the header, the scroll viewer and the ghost
    /// layer to each other.
    /// </summary>
    /// <remarks>
    /// The header sits outside the scroll area, so it has to be told how far the pattern has
    /// scrolled sideways and what character width and row height the grid settled on. Without
    /// that the names would stand still while the columns under them moved.
    ///
    /// The viewport is only real once the window has laid itself out, and it changes again
    /// whenever the window is resized or a strip under the pattern grows, which is why the half
    /// view is measured from an announcement rather than worked out once.
    ///
    /// The view follows the cursor always, and the playhead only while the transport is
    /// running.
    /// </remarks>
    public TrackerView()
    {
        InitializeComponent();

        _ghost = new DragGhost(GhostLayer);

        Grid.CursorMoved += (_, cursor) => ViewModel?.SetCursor(cursor);
        Grid.Clicked += (_, cursor) => FollowCursor(cursor);
        AddHandler(KeyDownEvent, OnGridKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnGridKeyUp, RoutingStrategies.Tunnel);
        Grid.LostFocus += OnGridLostFocus;

        Header.TrackClicked += (_, track) => SelectTrack(track);

        SetUpDragAndDrop();
        GridScroll.GetObservable(ScrollViewer.OffsetProperty)
            .Subscribe(new AnonymousObserver<Vector>(offset => Header.ScrollOffset = offset.X));

        Grid.LayoutUpdated += (_, _) =>
        {
            var metrics = Grid.Metrics;
            if (metrics.CharWidth > 0) Header.CharWidth = metrics.CharWidth;
            Header.RowHeight = Grid.RowHeight;
            Header.Columns = metrics.Columns;
        };

        GridScroll.GetObservable(ScrollViewer.ViewportProperty)
            .Subscribe(new AnonymousObserver<Size>(_ => MeasureHalfView()));

        Grid.GetObservable(PatternGrid.EditCursorProperty)
            .Subscribe(new AnonymousObserver<PatternCursor>(FollowCursor));

        Grid.GetObservable(PatternGrid.PlayingLineProperty)
            .Subscribe(new AnonymousObserver<int>(FollowPlayhead));
    }

    /// <summary>How many beats a page key moves. A bar in four four, which is what a page means here.</summary>
    private const int PageBeats = 4;

    /// <summary>
    /// How far a scroll offset has to differ before it is worth writing.
    /// </summary>
    /// <remarks>
    /// Half a pixel, which is below anything anybody can see. Writing an unchanged offset would
    /// restart the scroll animation on every keystroke, so a cursor moved down one line would
    /// jump rather than step.
    /// </remarks>
    private const double ScrollSlop = 0.5;

    /// <summary>The song and everything about it, or nothing before the page has been given one.</summary>
    private TrackerViewModel? ViewModel => DataContext as TrackerViewModel;

    /// <summary>
    /// Writes the open song somewhere of the user's choosing, with its recordings inside it.
    /// </summary>
    /// <remarks>
    /// Somewhere of their choosing, and never the songs folder, because this is not a save. It
    /// is the copy that leaves: an archive, or a file going to somebody who has none of your
    /// takes. The song being worked on is untouched by it.
    /// </remarks>
    private async void Pack_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } tracker) return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Pack song",
            SuggestedFileName = tracker.SongName,
            DefaultExtension = "jibx",
            FileTypeChoices = new[] { PackedSong }
        });

        string? path = file?.TryGetLocalPath();

        if (path != null) tracker.Pack(path);
    }

    /// <summary>What a song looks like on disc once it has left here.</summary>
    private static readonly FilePickerFileType PackedSong = new("Song")
    {
        Patterns = new[] { "*.jibx" }
    };

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
    ///
    /// Both presses are bubbled with handledEventsToo, because the list marks the press handled
    /// once it has updated its selection and the header does the same once it has picked the
    /// track, and that state is exactly what the drag needs to read.
    ///
    /// The whole track column takes a drop, not just its header, so an instrument can be let go
    /// over the notes it is about to play.
    ///
    /// The page takes one too, so the picture in the hand keeps following it over the order
    /// list, the instruments and the bar at the bottom. Nothing lands there: it only runs where
    /// neither of the other two has already answered, which is exactly the places where letting
    /// go would do nothing.
    /// </remarks>
    private void SetUpDragAndDrop()
    {
        InstrumentList.AddHandler(PointerPressedEvent, OnInstrumentPointerPressed,
            RoutingStrategies.Bubble, handledEventsToo: true);

        Header.AddHandler(PointerPressedEvent, OnHeaderPointerPressed,
            RoutingStrategies.Bubble, handledEventsToo: true);

        DragDrop.SetAllowDrop(Header, true);
        Header.AddHandler(DragDrop.DragOverEvent, OnHeaderDragOver);
        Header.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        Header.AddHandler(DragDrop.DropEvent, OnHeaderDrop);

        DragDrop.SetAllowDrop(Grid, true);
        Grid.AddHandler(DragDrop.DragOverEvent, OnGridDragOver);
        Grid.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        Grid.AddHandler(DragDrop.DropEvent, OnGridDrop);

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnPageDragOver);
    }

    /// <summary>
    /// The hand is somewhere on this page that will not take what it is holding.
    /// </summary>
    /// <remarks>
    /// Reached only when the grid and the header have both let the event past, since each of
    /// them marks it handled. Without it the picture would simply stop being drawn the moment
    /// the pointer left the pattern, which reads as the drag having failed rather than as the
    /// place being the wrong one.
    /// </remarks>
    private void OnPageDragOver(object? sender, DragEventArgs e)
    {
        Carry(e);

        _ghost.Refused = true;
        ShowDropTarget(-1);

        e.DragEffects = DragDropEffects.None;
    }

    /// <summary>
    /// Picks an instrument up off the list.
    /// </summary>
    /// <remarks>
    /// Releasing without moving simply ends the drag with no effect, so this does not get in the
    /// way of clicking a row to select it. The picture in the hand is put down in the
    /// <c>finally</c>, which is the one moment that is always reached: see <see cref="LetGo"/>.
    /// </remarks>
    private async void OnInstrumentPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(InstrumentList).Properties.IsLeftButtonPressed) return;
        if (InstrumentList.SelectedItem is not InstrumentSlot slot) return;

        try
        {
            await DragDrop.DoDragDropAsync(e, DraggedInstrument.For(slot.Index), DragDropEffects.Link);
        }
        finally
        {
            LetGo();
        }
    }

    /// <summary>
    /// The drag is over, however it ended.
    /// </summary>
    /// <remarks>
    /// Here rather than on the drop, because a drag is just as often abandoned: let go over the
    /// order list, or off the window entirely, and no drop is ever raised. The await above ends
    /// either way, which is the one moment that is always true.
    /// </remarks>
    private void LetGo()
    {
        _ghost.Hide();
        ShowDropTarget(-1);
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

        try
        {
            await DragDrop.DoDragDropAsync(e, Tracks.For(track), DragDropEffects.Move);
        }
        finally
        {
            LetGo();
        }
    }

    /// <summary>The hand is over the header, so the track is read off the column it is on.</summary>
    private void OnHeaderDragOver(object? sender, DragEventArgs e)
    {
        Carry(e);
        HandleDragOver(e, Header.TrackAtPoint(e.GetPosition(Header)));
    }

    /// <summary>
    /// The hand is over the notes, which names the same track the column above it does.
    /// </summary>
    private void OnGridDragOver(object? sender, DragEventArgs e)
    {
        Carry(e);
        HandleDragOver(e, Grid.TrackAtPoint(e.GetPosition(Grid)));
    }

    /// <summary>
    /// Keeps the picture of what is being dragged under the hand.
    /// </summary>
    /// <remarks>
    /// In the page's own coordinates, since that is where the layer is, rather than the header's
    /// or the grid's: a drag crosses from one to the other and the picture must not jump when it
    /// does. Drawn once and then only moved, and not taken away when the pointer leaves either
    /// surface, or crossing the line between them would blink it.
    /// </remarks>
    private void Carry(DragEventArgs e)
    {
        if (!_ghost.IsShowing)
        {
            if (Carried(e) is not { } picture) return;

            _ghost.Show(picture);
        }

        _ghost.MoveTo(e.GetPosition(this));
    }

    /// <summary>What to draw in the hand, or null for a drag from somewhere else entirely.</summary>
    /// <remarks>
    /// The same picture as the thing that was picked up, which is what makes it read as the
    /// thing rather than as a label about it: an instrument keeps its machine's colour down the
    /// side and the sentence under its name, exactly as its row in the list has them.
    /// </remarks>
    private Control? Carried(DragEventArgs e)
    {
        int moving = Tracks.IndexFrom(e.DataTransfer);

        if (moving >= 0)
            return Picture("Track " + (moving + 1).ToString("00", CultureInfo.InvariantCulture), "", "");

        int instrument = DraggedInstrument.IndexFrom(e.DataTransfer);
        if (instrument < 0) return null;

        var slot = ViewModel?.Instruments.FirstOrDefault(s => s.Index == instrument);

        return slot == null ? null : Picture(slot.Name, slot.DetailText, slot.Colour);
    }

    /// <summary>
    /// Builds the card that is drawn in the hand: the name, the sentence under it, and the
    /// machine's colour down the side.
    /// </summary>
    /// <remarks>
    /// Built here rather than templated, because the ghost layer is a canvas over the page and
    /// takes a control rather than a data context. A track being moved has no colour and no
    /// sentence, so it comes out as the name alone.
    /// </remarks>
    private static Control Picture(string name, string detail, string colour)
    {
        var lines = new StackPanel { Spacing = 1 };

        lines.Children.Add(new TextBlock { Text = name, FontWeight = FontWeight.SemiBold });

        if (detail.Length > 0)
            lines.Children.Add(new TextBlock { Text = detail, FontSize = 11, Opacity = 0.75 });

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
        };

        if (Tint.Hue(colour, out var hue))
        {
            row.Children.Add(new Border
            {
                Width = 3,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(hue),
            });
        }

        row.Children.Add(lines);

        return row;
    }

    /// <summary>Let go over the header, onto the track that column names.</summary>
    private void OnHeaderDrop(object? sender, DragEventArgs e) =>
        HandleDrop(e, Header.TrackAtPoint(e.GetPosition(Header)));

    /// <summary>Let go over the notes, onto the track that column names.</summary>
    private void OnGridDrop(object? sender, DragEventArgs e) =>
        HandleDrop(e, Grid.TrackAtPoint(e.GetPosition(Grid)));

    /// <summary>
    /// Says whether this track would take what is in the hand, and shows it on both surfaces.
    /// </summary>
    /// <remarks>
    /// Which of the two drags this is comes from the format it carries rather than from where it
    /// is: a track being moved and an instrument being pointed at a track land on the same two
    /// surfaces. A track cannot be dropped on itself, which is the one refusal that is about
    /// what is being carried rather than about where the hand is.
    ///
    /// Both surfaces light up together, so the header names the column being targeted even while
    /// the hand is down among the notes. The event is marked handled either way, which is what
    /// keeps <see cref="OnPageDragOver"/> to the places where letting go would really do nothing.
    /// </remarks>
    private void HandleDragOver(DragEventArgs e, int track)
    {
        int moving = Tracks.IndexFrom(e.DataTransfer);

        if (moving >= 0)
        {
            bool somewhere = track >= 0 && track != moving;

            ShowDropTarget(somewhere ? track : -1);

            _ghost.Refused = !somewhere;

            e.DragEffects = somewhere ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
            return;
        }

        int instrument = DraggedInstrument.IndexFrom(e.DataTransfer);
        bool valid = instrument >= 0 && track >= 0;

        ShowDropTarget(valid ? track : -1);

        _ghost.Refused = !valid;

        e.DragEffects = valid ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// Does what letting go there means: moves a track, or points a track at an instrument.
    /// </summary>
    /// <remarks>
    /// A track being moved and an instrument being pointed at a track arrive the same way and
    /// mean different things, so which it is comes from the format it carries.
    ///
    /// The instrument case is awaited rather than left running: pointing a track at an
    /// instrument asks whether the notes already on it should follow, and that dialog cannot be
    /// raised from inside the drop itself.
    /// </remarks>
    private async void HandleDrop(DragEventArgs e, int track)
    {
        ShowDropTarget(-1);

        int moving = Tracks.IndexFrom(e.DataTransfer);

        if (moving >= 0)
        {
            e.Handled = true;
            if (track >= 0) ViewModel?.MoveTrack(moving, track);
            return;
        }

        int instrument = DraggedInstrument.IndexFrom(e.DataTransfer);
        if (instrument < 0 || track < 0) return;

        e.Handled = true;

        var model = ViewModel;
        if (model != null) await model.AssignInstrumentToTrack(track, instrument);
    }

    /// <summary>
    /// The hand has left a surface, so nothing on it is a target any more. The picture in the
    /// hand is deliberately left alone: it is taken away when the drag itself ends.
    /// </summary>
    private void OnDragLeave(object? sender, DragEventArgs e) => ShowDropTarget(-1);

    /// <summary>
    /// Lights one track's column on both surfaces, or none when given a number below nought.
    /// </summary>
    private void ShowDropTarget(int track)
    {
        Header.DropTargetTrack = track;
        Grid.DropTargetTrack = track;
    }

    /// <summary>
    /// Keeps the cursor on the middle of the screen and its track in view.
    /// </summary>
    /// <remarks>
    /// Not while the hand has hold of the pattern. There the pointer is what is moving the
    /// cursor, so scrolling to put that cursor back under the middle pulls the content out from
    /// under the hand: the next movement lands several lines further on than it was aimed at,
    /// that moves the cursor again, and the block runs away down the pattern on its own. The
    /// press alone was enough to do it, before any movement at all. The grid says so again when
    /// the button comes up, which is when this catches up.
    /// </remarks>
    private void FollowCursor(PatternCursor cursor)
    {
        if (Grid.Grabbed) return;

        ScrollToRow(cursor.Line);
        ScrollToTrack(cursor.Track);
    }

    /// <summary>
    /// Runs the pattern under the playhead while the transport is going.
    /// </summary>
    /// <remarks>
    /// Only while it is actually moving. Chasing it when it is not would yank the view away from
    /// wherever the cursor was left the moment the transport stopped.
    /// </remarks>
    private void FollowPlayhead(int line)
    {
        if (line >= 0 && ViewModel?.IsPlaying == true) ScrollToRow(line);
    }

    /// <summary>
    /// Tells the grid how far the middle of the screen is from its edges, which is how much of
    /// the pattern either side of this one is worth drawing.
    /// </summary>
    /// <remarks>
    /// Here rather than in the grid because the grid is measured inside the scroll viewer with
    /// no height limit and never learns how tall the hole it is seen through is. Re-centred
    /// afterwards, or resizing the window would leave the cursor wherever the taller pattern
    /// happened to put it.
    /// </remarks>
    private void MeasureHalfView()
    {
        double half = Math.Max(0, (GridScroll.Viewport.Height - Grid.RowHeight) / 2);

        if (Math.Abs(half - Grid.HalfView) < ScrollSlop) return;

        Grid.HalfView = half;

        if (ViewModel is { } model) ScrollToRow(model.Cursor.Line);
    }

    /// <summary>
    /// Puts a line on the middle of the screen, which is where the line being worked on always
    /// sits. Line 00 is on the middle exactly as any other row is, with blank above it.
    /// </summary>
    private void ScrollToRow(int row)
    {
        var pattern = Grid.Pattern;
        if (pattern == null) return;

        double offset = _scroll.CentreRow(
            GridScroll.Viewport.Height, Grid.Metrics, row, pattern.Lines);

        SetScrollOffset(offset, GridScroll.Offset.X);
    }

    /// <summary>
    /// Brings a track into view sideways, moving as little as will do it: the pattern is not
    /// centred horizontally, since a track's neighbours are worth seeing.
    /// </summary>
    private void ScrollToTrack(int track)
    {
        if (Grid.Pattern == null) return;

        double offset = _scroll.KeepTrackVisible(
            GridScroll.Offset.X, GridScroll.Viewport.Width, Grid.Metrics, track);

        SetScrollOffset(GridScroll.Offset.Y, offset);
    }

    /// <summary>
    /// Moves the view, and does nothing at all when it is already there. See
    /// <see cref="ScrollSlop"/> for why an unchanged offset must not be written.
    /// </summary>
    private void SetScrollOffset(double y, double x)
    {
        if (Math.Abs(x - GridScroll.Offset.X) < ScrollSlop && Math.Abs(y - GridScroll.Offset.Y) < ScrollSlop) return;

        GridScroll.Offset = new Vector(x, y);
    }

    /// <summary>
    /// Puts the cursor on a track and gives the pattern the keyboard back, so clicking a header
    /// leaves you able to type straight away.
    /// </summary>
    private void SelectTrack(int track)
    {
        var vm = ViewModel;
        if (vm == null) return;

        vm.SetCursor(vm.Cursor with { Track = track });
        Grid.Focus();
    }

    /// <summary>
    /// Everything typed into the pattern: moving about, the block, and what goes in a cell.
    /// </summary>
    /// <remarks>
    /// Only while the grid has the keyboard, so the same keys on the bar or in a box are that
    /// control's. Shift with a movement key grows the block instead of moving away from it.
    ///
    /// What a key means depends on the column the cursor is in. In the note column the piano
    /// layout applies and a letter is a note; everywhere else the digit row and A to F type hex
    /// values, and in the effect column a letter that is not a hex digit is the command itself.
    /// The rules are in <see cref="KeyboardNoteMap"/>, so this only decides which of them to
    /// ask.
    /// </remarks>
    /// <summary>
    /// A letter key coming up, which is what tells the view model a chord is over.
    /// </summary>
    /// <remarks>
    /// The note path has no release of its own: a letter typed into the pattern sounds a note
    /// that lets itself go after a moment, and nothing ever said the key had come up. That was
    /// enough while a track held one note. It is not enough now, because a note pressed while
    /// another is held is a chord and goes into the next note column, and without a release the
    /// first chord anybody typed would go on filling columns for ever.
    ///
    /// It does not stop the sound. A note played by hand runs its own length here, which is the
    /// rule everywhere in this application that a key can be clicked rather than held.
    /// </remarks>
    /// <summary>
    /// The keyboard has gone somewhere else, so every key it was holding is forgotten.
    /// </summary>
    /// <remarks>
    /// The release will be delivered wherever the keys went instead and this will never hear
    /// it. Without this the next note typed here would be read as part of a chord begun before
    /// somebody clicked away, and would land in the second note column of whatever track the
    /// cursor happened to be in.
    /// </remarks>
    private void OnGridLostFocus(object? sender, RoutedEventArgs e)
    {
        _typed.Clear();
        ViewModel?.LetAllNotes();
    }

    private void OnGridKeyUp(object? sender, KeyEventArgs e)
    {
        if (!_typed.Remove(e.Key, out var note)) return;

        ViewModel?.LetNote(note);
    }

    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null || !Grid.IsFocused) return;

        bool extend = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        switch (e.Key)
        {
            case Key.Up: vm.MoveCursor(-1, 0, 0, extend); e.Handled = true; return;
            case Key.Down: vm.MoveCursor(1, 0, 0, extend); e.Handled = true; return;
            case Key.Left: vm.MoveCursor(0, extend ? -1 : 0, extend ? 0 : -1, extend); e.Handled = true; return;
            case Key.Right: vm.MoveCursor(0, extend ? 1 : 0, extend ? 0 : 1, extend); e.Handled = true; return;
            case Key.PageUp: vm.MoveCursor(-vm.LinesPerBeat * PageBeats, 0, 0, extend); e.Handled = true; return;
            case Key.PageDown: vm.MoveCursor(vm.LinesPerBeat * PageBeats, 0, 0, extend); e.Handled = true; return;
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

        if (_keys.IsNoteOff(key))
        {
            vm.EnterNoteOff();
            e.Handled = true;
            return;
        }

        if (vm.Cursor.Column == CellColumn.Note)
        {
            if (_keys.IsNoteOffInNotes(key))
            {
                vm.EnterNoteOff();
                e.Handled = true;
                return;
            }

            if (_keys.NoteFor(key, vm.Octave) is Note note)
            {
                _typed[e.Key] = note;
                vm.EnterNote(note);
                e.Handled = true;
            }
            return;
        }

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

    /// <summary>Whether a letter is one of the six that are also digits in hex.</summary>
    private static bool IsHexLetter(char c) => c is >= 'A' and <= 'F';

    /// <summary>
    /// The character a key stands for, or nought when it stands for none.
    /// </summary>
    /// <remarks>
    /// Read off the key rather than off the text the toolkit produces, because the pattern takes
    /// the same characters whatever layout the keyboard is set to: a cell holds hex, and hex is
    /// the same six letters everywhere.
    /// </remarks>
    private static char KeyToChar(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => (char)('0' + (key - Key.D0)),
        >= Key.NumPad0 and <= Key.NumPad9 => (char)('0' + (key - Key.NumPad0)),
        >= Key.A and <= Key.Z => (char)('A' + (key - Key.A)),
        _ => '\0'
    };
}
