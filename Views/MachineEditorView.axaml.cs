using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using JingleBox2.ViewModels;
using System;
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

        // A take is picked the way it is picked everywhere else in the app, by the dialog with
        // the categories and the search in it. The panel only says which setting wants one.
        PanelCanvas.TakeWanted += PickTake;

        // A handle dragged on the panel writes the size onto the machine itself, so the rest of
        // the page has to hear about it: the property rows are showing the size it used to be.
        PanelCanvas.Resized += (_, element) => Editor?.Resized(element);

        // The drag is ours from press to release, so the moving and the letting go are watched
        // here rather than left to the system's own drag and drop.
        AddHandler(PointerMovedEvent, Carrying_PointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, Carrying_PointerReleased, RoutingStrategies.Tunnel);

        // The preview is painted in the machine's own colours, so it is repainted whenever
        // another machine is opened, and mixed again when the theme moves under both.
        DataContextChanged += (_, _) => Watch();
        UI.ThemeManager.Changed += Later;
        DetachedFromVisualTree += (_, _) => UI.ThemeManager.Changed -= Later;
    }

    /// <summary>What the tint is following, so it can stop following the machine before it.</summary>
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

    /// <summary>Puts the machine's colours on the plate, so it looks like the box it is.</summary>
    private void Retint() =>
        MachineTint.Apply(this.FindControl<Border>("PanelPreview")!, Editor?.Project?.Theme);

    /// <summary>
    /// Puts one recording on the one line that asked for it.
    /// </summary>
    /// <remarks>
    /// The other half of loading a folder of samples: this is how a single pad is filled, which
    /// is what somebody fixing one drum in a kit wants. Brought into the machine on the way, so
    /// what the preset names is a file that travels with it.
    /// </remarks>
    private async void PresetWave_Click(object? sender, RoutedEventArgs e)
    {
        if (Editor is not { } editor) return;

        if (sender is not Control control || control.DataContext is not PresetLine line) return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (storage == null) return;

        var found = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "A recording for " + line.Name,
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Recordings") { Patterns = new[] { "*.wav" } } },
        });

        if (found.FirstOrDefault()?.TryGetLocalPath() is not { Length: > 0 } path) return;

        if (editor.PresetDesk.Bring(path) is { Length: > 0 } named) line.Text = named;
    }

    /// <summary>
    /// Brings recordings into the machine and puts them on this preset.
    /// </summary>
    /// <remarks>
    /// The panel's own way of loading samples puts them on your shelf, which is right for an
    /// instrument in a song and wrong for a preset: a preset has to travel, so what it plays is
    /// copied into the machine's own folder and named from there.
    /// </remarks>
    private async void PresetWaves_Click(object? sender, RoutedEventArgs e)
    {
        if (Editor is not { } editor) return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (storage == null) return;

        var found = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Recordings to put on this preset",
            AllowMultiple = true,
            FileTypeFilter = new[] { new FilePickerFileType("Recordings") { Patterns = new[] { "*.wav" } } },
        });

        var paths = found
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(path => path!)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (paths.Count == 0) return;

        editor.PresetDesk.Fill(paths);
    }


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

    /// <summary>
    /// Writes the machine out where the pointer says.
    /// </summary>
    /// <remarks>
    /// A save picker rather than a folder one: what leaves here is a single file, and where it
    /// goes is wherever you send machines from. The name is offered from the machine's own, so
    /// the file is recognisable on a desktop full of zips.
    /// </remarks>
    private async void Export_Click(object? sender, RoutedEventArgs e)
    {
        if (Editor is not { CanExport: true } editor) return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (storage == null) return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export the machine",
            SuggestedFileName = editor.ExportName,
            DefaultExtension = "zip",
            FileTypeChoices = new[] { MachinePack }
        });

        string? path = file?.TryGetLocalPath();

        if (path != null) editor.Export(path);
    }

    /// <summary>What a machine looks like on disc once it has left here.</summary>
    private static readonly FilePickerFileType MachinePack = new("Machine")
    {
        Patterns = new[] { "*.zip" }
    };

    /// <summary>
    /// Puts a picture on the machine, on whichever picture element is being worked on.
    /// </summary>
    /// <remarks>
    /// The file is copied into the machine and renamed there, so what is chosen here is a file
    /// on somebody's disc and what the machine keeps is its own copy under its own name. Only
    /// the path goes to the view model, which is where all of that happens.
    /// </remarks>
    private async void Picture_Click(object? sender, RoutedEventArgs e)
    {
        if (Editor is not { PicturePicked: true } editor) return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (storage == null) return;

        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a picture",
            AllowMultiple = false,
            FileTypeFilter = new[] { Pictures }
        });

        string? path = picked.Count == 0 ? null : picked[0].TryGetLocalPath();

        if (path != null) editor.PutPicture(path);
    }

    /// <summary>
    /// What a machine will take as a picture.
    /// </summary>
    /// <remarks>
    /// The drawing is the one worth choosing where there is a choice. A panel is laid out at
    /// whatever size suits it and a logo made of lines is drawn at that size, while one made of
    /// pixels is stretched to it.
    /// </remarks>
    private static readonly FilePickerFileType Pictures = new("Picture")
    {
        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.svg" }
    };

    /// <summary>
    /// A recording put on the machine that is being laid out.
    /// </summary>
    /// <remarks>
    /// Written into the panel's own settings, not into the project. Which take a machine plays
    /// belongs to an instrument in a song, and the machine being built here has no instrument:
    /// this is the same trying of the controls as turning one of the knobs, and it is thrown
    /// away with the rest of the preview.
    /// </remarks>
    private async void PickTake(object? sender, string key)
    {
        if (DataContext is not MainViewModel main) return;

        var take = await TakeDialog.PickAsync(main.Takes);

        if (take == null || take.FilePath.Length == 0) return;

        main.MachineEditor.Values.SetText(key, take.FilePath);
        main.MachineEditor.Redraw();
    }

    /// <summary>
    /// Picks a part up off the library and carries it.
    /// </summary>
    /// <remarks>
    /// Letting go without moving is a press rather than a drag, and adds the part where the
    /// selection is: dragging says where it goes, pressing is quicker when that is already right.
    /// </remarks>
    private void Part_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control || control.Tag is not string kind) return;

        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed) return;

        Carry(new Carrying(kind, null, e.GetPosition(this)), e);
    }

    /// <summary>
    /// Picks the line that was pressed, and carries what it stands for.
    /// </summary>
    /// <remarks>
    /// A left press on a line already picks it, through the tree's own selection. A right press
    /// does not, and a menu about the line under the pointer that acts on a different line is
    /// worse than no menu, so the pick is made here before anything else happens. The outermost
    /// element is picked but not carried: it is what everything else is inside.
    /// </remarks>
    private void Row_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control row || row.DataContext is not MachineElementViewModel element) return;

        if (Editor is not { } editor) return;

        editor.SelectedElement = element;

        if (element.Parent == null) return;

        if (!e.GetCurrentPoint(row).Properties.IsLeftButtonPressed) return;

        Carry(new Carrying(null, element, e.GetPosition(this)), e);
    }

    /// <summary>
    /// What is in the hand: a kind out of the library, or something already on the machine.
    /// </summary>
    /// <remarks>
    /// One or the other, never both. Where it started is kept so that a press that never really
    /// moved can be told from a drag, since letting go without moving means something different
    /// for both of them: a part is added where the selection is, and an element stays where it is.
    /// </remarks>
    private sealed record Carrying(string? Kind, MachineElementViewModel? Element, Point From)
    {
        public bool Moved { get; set; }
    }

    private Carrying? _carrying;

    /// <summary>How far the hand has to move before it is carrying something rather than pressing it.</summary>
    private const double Threshold = 4;

    /// <summary>
    /// Picks a thing up and follows the hand with it until it is let go.
    /// </summary>
    /// <remarks>
    /// The pointer is captured and the whole drag is ours, rather than handed to the system's own
    /// drag and drop. Three things come of that, and all three are the reason: what is being
    /// carried can be drawn as the thing itself rather than as a cursor, the drag works the same
    /// on every platform the app runs on, and it can be tested without a hand on a mouse.
    /// </remarks>
    private void Carry(Carrying carrying, PointerPressedEventArgs e)
    {
        _carrying = carrying;

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <summary>The ghost follows the hand, and whatever it is over is marked.</summary>
    private void Carrying_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_carrying is not { } carrying) return;

        var at = e.GetPosition(this);

        if (!carrying.Moved)
        {
            // A press that has not really moved is a press. Starting the ghost on the first
            // pixel would mean a click on a part flashing a picture of itself across the page.
            if (Math.Abs(at.X - carrying.From.X) < Threshold &&
                Math.Abs(at.Y - carrying.From.Y) < Threshold) return;

            carrying.Moved = true;

            ShowGhost(carrying);
        }

        MoveGhost(at);

        var (onPanel, onList) = Under(at);

        Mark(onPanel, onList);

        // The line between two parts, which is the whole of how somebody says "after that one".
        PanelCanvas.Landing(Within(this.InputHitTest(at) as Visual, PanelCanvas) ? Inside(at) : null);
    }

    /// <summary>Puts down what was being carried, wherever the hand let go.</summary>
    private void Carrying_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_carrying is not { } carrying) return;

        _carrying = null;

        e.Pointer.Capture(null);

        HideGhost();
        Mark(null, null);

        PanelCanvas.Landing(null);

        if (Editor is not { } editor) return;

        var at = e.GetPosition(this);
        var (onPanel, onList) = Under(at);

        // Let go without ever moving. A part out of the library is added where the selection is,
        // which is how one gets added without aiming; something already on the machine stays
        // exactly where it is, since the press was somebody picking it to work on.
        if (!carrying.Moved)
        {
            if (carrying.Kind is { } picked) editor.AddElementCommand.Execute(picked);

            return;
        }

        // On the panel the hand says where among the others, which is what the line was showing.
        // On the list it says which container, and the part goes at the end of it.
        var (into, place) = Within(this.InputHitTest(at) as Visual, PanelCanvas)
            ? PanelCanvas.Where(Inside(at))
            : (onList?.Element, -1);

        into ??= onPanel;

        if (carrying.Kind is { } kind) editor.Drop(kind, into, place);
        else if (carrying.Element is { } moved) editor.MoveInto(moved.Element, into, place);
    }

    /// <summary>What the hand is over: an element on the panel, or a line of the list.</summary>
    private (Machines.MachineElement?, ViewModels.MachineElementViewModel?) Under(Point at)
    {
        var hit = this.InputHitTest(at) as Visual;

        if (hit == null) return (null, null);

        if (Row(hit) is { } row) return (null, row);

        // Over the panel but over nothing in particular means the machine itself, which is where
        // a part let go over open space goes.
        if (Within(hit, PanelCanvas)) return (PanelCanvas.ElementAt(hit) ?? Editor?.Project?.Panel.Root, null);

        return (null, null);
    }

    private static bool Within(Visual? at, Visual holder)
    {
        for (; at != null; at = Avalonia.VisualTree.VisualExtensions.GetVisualParent(at))
        {
            if (ReferenceEquals(at, holder)) return true;
        }

        return false;
    }

    /// <summary>
    /// Draws what is being carried, as the thing itself.
    /// </summary>
    /// <remarks>
    /// A part is drawn by the same control the library draws it with, so what is in the hand and
    /// what was picked up are the same picture. Something already on the machine is drawn as its
    /// kind with its name beside it, since the element itself is on the panel and cannot be in
    /// two places at once.
    /// </remarks>
    private void ShowGhost(Carrying carrying)
    {
        Control inside;

        if (carrying.Kind is { } kind)
        {
            inside = new Machines.Ui.MachinePartSample { Kind = kind, Width = 62, Height = 44 };
        }
        else
        {
            var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };

            row.Children.Add(new Machines.Ui.MachinePartSample
            {
                Kind = carrying.Element?.Kind ?? "",
                Width = 44,
                Height = 32,
            });

            row.Children.Add(new TextBlock
            {
                Text = carrying.Element?.Display ?? "",
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            });

            inside = row;
        }

        _ghost = new Border
        {
            Classes = { "card" },
            Padding = new Thickness(6),
            Opacity = 0.85,
            Child = inside,
        };

        GhostLayer.Children.Add(_ghost);
    }

    private Border? _ghost;

    /// <summary>Below and right of the hand, so what is being aimed at is not under the picture.</summary>
    private void MoveGhost(Point at)
    {
        if (_ghost == null) return;

        Canvas.SetLeft(_ghost, at.X + 12);
        Canvas.SetTop(_ghost, at.Y + 12);
    }

    /// <summary>The same point, said in the panel's own coordinates.</summary>
    /// <remarks>
    /// The carry is followed in this page's coordinates, because that is where the ghost is
    /// drawn, and the panel is somewhere inside the page with a card and a scroll around it.
    /// Asking the panel about a point in the page's terms would be off by wherever it sits.
    /// </remarks>
    private Point Inside(Point at) =>
        this.TranslatePoint(at, PanelCanvas) ?? at;

    private void HideGhost()
    {
        if (_ghost == null) return;

        GhostLayer.Children.Remove(_ghost);

        _ghost = null;
    }


    /// <summary>Outlines what would take the part, on the panel and on the list at once.</summary>
    /// <remarks>
    /// Both, because a drag crosses from one to the other and whichever is left holding a mark
    /// is showing something that is no longer true. Clearing is the same call with nothing in it.
    /// </remarks>
    private void Mark(Machines.MachineElement? onPanel, ViewModels.MachineElementViewModel? onList)
    {
        PanelCanvas.Marked = onPanel;

        if (Editor is not { } editor) return;

        foreach (var row in editor.Every()) row.IsDropTarget = ReferenceEquals(row, onList);
    }

    /// <summary>The outermost element, which is where a part let go over nothing goes.</summary>
    private Machines.MachineElement? Root() => Editor?.Project?.Panel.Root;



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
