using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Reactive;
using Avalonia.Interactivity;
using JingleBox2.Models;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

/// <summary>
/// Wiring only. Key handling maps a keystroke to one call on the view model; what the
/// keystroke means lives in <see cref="KeyboardNoteMap"/> and <see cref="PatternEdit"/>.
/// </summary>
public partial class TrackerView : UserControl
{
    public TrackerView()
    {
        InitializeComponent();

        Grid.CursorMoved += (_, cursor) => ViewModel?.SetCursor(cursor);
        AddHandler(KeyDownEvent, OnGridKeyDown, RoutingStrategies.Tunnel);

        // The header sits outside the scroll area, so it has to be told how far the pattern
        // has scrolled sideways and what character width the grid settled on.
        Header.TrackClicked += (_, track) => SelectTrack(track);
        GridScroll.GetObservable(ScrollViewer.OffsetProperty)
            .Subscribe(new AnonymousObserver<Vector>(offset => Header.ScrollOffset = offset.X));

        Grid.LayoutUpdated += (_, _) =>
        {
            var metrics = Grid.Metrics;
            if (metrics.CharWidth > 0) Header.CharWidth = metrics.CharWidth;
            Header.RowHeight = Grid.RowHeight;
        };
    }

    private TrackerViewModel? ViewModel => DataContext as TrackerViewModel;

    private void SelectTrack(int track)
    {
        var vm = ViewModel;
        if (vm == null) return;

        vm.SetCursor(vm.Cursor with { Track = track });
        Grid.Focus();
    }

    private void AddInstrument_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null && RecordingPicker.SelectedItem is Recording recording)
            ViewModel.AddInstrumentCommand.Execute(recording);
    }

    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null || !Grid.IsFocused) return;

        switch (e.Key)
        {
            case Key.Up: vm.MoveCursor(-1, 0, 0); e.Handled = true; return;
            case Key.Down: vm.MoveCursor(1, 0, 0); e.Handled = true; return;
            case Key.Left: vm.MoveCursor(0, 0, -1); e.Handled = true; return;
            case Key.Right: vm.MoveCursor(0, 0, 1); e.Handled = true; return;
            case Key.PageUp: vm.MoveCursor(-vm.LinesPerBeat * 4, 0, 0); e.Handled = true; return;
            case Key.PageDown: vm.MoveCursor(vm.LinesPerBeat * 4, 0, 0); e.Handled = true; return;
            case Key.Tab:
                vm.MoveCursor(0, e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1, 0);
                e.Handled = true;
                return;
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
