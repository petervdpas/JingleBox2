using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using JingleBox2.ViewModels;
using System.Linq;

namespace JingleBox2.Views;

/// <summary>
/// Where a machine is built.
/// </summary>
/// <remarks>
/// It opens a machine project, which is a folder on disc: the machine's own, kept wherever you
/// keep your work. The rack is the other side of it, what is installed and ready for a song to
/// take an instrument off, and that stays in the tracker where a song is written.
///
/// The pickers belong to the window, so they are opened here and only the answer goes to the
/// view model, the same arrangement the recordings importer uses.
/// </remarks>
public partial class MachineEditorView : UserControl
{
    public MachineEditorView()
    {
        InitializeComponent();

        // The preview is painted in the machine's own colours, so it is repainted whenever
        // another machine is opened, and mixed again when the theme moves under both.
        DataContextChanged += (_, _) => Watch();
        UI.ThemeManager.Changed += Later;
        DetachedFromVisualTree += (_, _) => UI.ThemeManager.Changed -= Later;
    }

    private System.ComponentModel.INotifyPropertyChanged? _watched;

    private void Watch()
    {
        if (_watched != null) _watched.PropertyChanged -= OnEditorChanged;

        _watched = Editor;

        if (_watched != null) _watched.PropertyChanged += OnEditorChanged;

        Retint();
    }

    private void OnEditorChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MachineEditorViewModel.Project)) Retint();
    }

    private void Later() => Avalonia.Threading.Dispatcher.UIThread.Post(Retint);

    /// <summary>Puts the machine's colours on the preview, so it looks like the box it is.</summary>
    private void Retint() =>
        MachineTint.Apply(this.FindControl<Border>("PanelPreview")!, Editor?.Project?.Theme);

    private MachineEditorViewModel? Editor => (DataContext as MainViewModel)?.MachineEditor;

    private async void Open_Click(object? sender, RoutedEventArgs e)
    {
        if (Editor is not { } editor) return;

        string? folder = await PickFolder("Open a machine");

        if (folder != null) editor.Open(folder);
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (Editor is not { } editor || editor.Project == null) return;

        if (!editor.NeedsFolder)
        {
            editor.Save();
            return;
        }

        string? folder = await PickFolder("Where to keep this machine");

        if (folder != null) editor.Save(folder);
    }

    private void Install_Click(object? sender, RoutedEventArgs e) => Editor?.Install();

    /// <summary>
    /// Picks a part up off the library.
    /// </summary>
    /// <remarks>
    /// Releasing without moving ends the drag with no effect, so this does not get in the way
    /// of clicking a part to put it inside whatever is picked. Both ways of adding one are
    /// wanted: dragging says where it goes, clicking is quicker when that is already right.
    /// </remarks>
    private async void Part_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control || control.Tag is not string kind) return;

        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed) return;

        var landed = await DragDrop.DoDragDropAsync(e, MachinePartDragData.For(kind), DragDropEffects.Copy);

        // Nowhere in particular, which is what pressing a part and letting go without moving
        // looks like. Taken as a click, so a part can be added without aiming: it goes inside
        // whatever is picked, the same as the panel would put it.
        if (landed == DragDropEffects.None) Editor?.AddElementCommand.Execute(kind);
    }

    private void Canvas_DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = MachinePartDragData.KindFrom(e.DataTransfer) == null
            ? DragDropEffects.None
            : DragDropEffects.Copy;

        e.Handled = true;
    }

    /// <summary>
    /// A part let go over the panel, put where it was let go.
    /// </summary>
    /// <remarks>
    /// The canvas is asked what is under the pointer rather than the drop being read off the
    /// control that took it, because what took it is a knob or a frame and what the panel is
    /// made of is elements. Let go over nothing in particular, it goes to the outermost element,
    /// which is the only place a first part can go anyway.
    /// </remarks>
    private void Canvas_Drop(object? sender, DragEventArgs e)
    {
        if (Editor is not { } editor) return;

        if (MachinePartDragData.KindFrom(e.DataTransfer) is not { } kind) return;

        editor.Drop(kind, PanelCanvas.ElementAt(e.Source));

        e.Handled = true;
    }

    private void Tree_DragOver(object? sender, DragEventArgs e) => Canvas_DragOver(sender, e);

    /// <summary>
    /// A part let go on a line of the panel list, put inside whatever that line stands for.
    /// </summary>
    /// <remarks>
    /// The list takes drops as well as the panel because a container with nothing in it is a
    /// container with nothing on screen: an empty row is a few pixels of gap, and a grid nobody
    /// has filled is the whole card. Aiming at the word is easier than aiming at the space.
    /// </remarks>
    private void Tree_Drop(object? sender, DragEventArgs e)
    {
        if (Editor is not { } editor) return;

        if (MachinePartDragData.KindFrom(e.DataTransfer) is not { } kind) return;

        editor.Drop(kind, Row(e.Source)?.Element);

        e.Handled = true;
    }

    /// <summary>Which element a line of the list stands for, or nothing when it is not a line.</summary>
    private static ViewModels.MachineElementViewModel? Row(object? source)
    {
        for (var at = source as Visual; at != null; at = Avalonia.VisualTree.VisualExtensions.GetVisualParent(at))
        {
            if (at is TreeViewItem { DataContext: ViewModels.MachineElementViewModel element }) return element;
        }

        return null;
    }

    /// <summary>
    /// A parameter chosen off the list, written into the element that is picked.
    /// </summary>
    /// <remarks>
    /// The box is not bound to anything. The list of keys is made afresh whenever a parameter is
    /// added, renamed or taken out, and a bound box empties its own selection when its list is
    /// replaced, which would wipe the parameter off the element that is picked.
    /// </remarks>
    private void Parameter_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_picking) return;

        if (sender is not ComboBox box || box.SelectedItem is not string key) return;

        _picking = true;

        try
        {
            if (Editor?.SelectedElement is { } element) element.Parameter = key;

            box.SelectedItem = null;
        }
        finally
        {
            _picking = false;
        }
    }

    /// <summary>True while a choice is being written, so the box cannot answer its own writing.</summary>
    private bool _picking;

    private async System.Threading.Tasks.Task<string?> PickFolder(string title)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (storage == null) return null;

        var picked = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return picked.Count == 0 ? null : picked[0].TryGetLocalPath();
    }
}
