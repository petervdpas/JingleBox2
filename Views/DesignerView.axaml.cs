using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.IO;
using JingleBox2.ViewModels;
using System;
using System.Linq;
using JingleBox2.Views.Interfaces;

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
public partial class DesignerView : UserControl, Shortcuts.Interfaces.IShortcutContext
{
    /// <summary>A machine's colour mixed into the theme's. Holds nothing, so one is enough.</summary>
    private readonly IPanelTint _tint = new PanelTint();

    /// <summary>What is in the hand. See <see cref="DragGhost"/>.</summary>
    private readonly DragGhost _ghost;

    /// <summary>
    /// Builds the page and wires the panel preview, the drag, and the tinting.
    /// </summary>
    /// <remarks>
    /// A take is picked the way it is picked everywhere else in the application, by the dialog
    /// with the categories and the search in it. The panel only says which setting wants one.
    ///
    /// A handle dragged on the panel writes the size onto the machine itself, so the rest of the
    /// page has to hear about it: the property rows are showing the size it used to be.
    ///
    /// The drag is this page's own from press to release, which is why the moving and the
    /// letting go are watched here rather than left to the toolkit's drag and drop. See
    /// <see cref="Carry"/> for the three reasons.
    ///
    /// The preview is painted in the machine's own colours, so it is repainted whenever another
    /// machine is opened and mixed again when the theme moves under both.
    /// </remarks>
    public DesignerView()
    {
        InitializeComponent();

        _ghost = new DragGhost(GhostLayer);

        PanelCanvas.TakeWanted += PickTake;

        PanelCanvas.Resized += (_, element) => Editor?.Resized(element);

        AddHandler(PointerMovedEvent, Carrying_PointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, Carrying_PointerReleased, RoutingStrategies.Tunnel);

        DataContextChanged += (_, _) => Watch();
        UI.ThemeSwitch.Changed += Later;
        DetachedFromVisualTree += (_, _) => UI.ThemeSwitch.Changed -= Later;
    }

    /// <summary>What the tint is following, so it can stop following the machine before it.</summary>
    private System.ComponentModel.INotifyPropertyChanged? _watched;

    /// <summary>
    /// Listens to whichever editor the page has been given, and lets go of the one before, so
    /// the tint follows the machine that is open rather than every machine ever opened.
    /// </summary>
    private void Watch()
    {
        if (_watched != null) _watched.PropertyChanged -= OnEditorChanged;

        _watched = Editor;

        if (_watched != null) _watched.PropertyChanged += OnEditorChanged;

        Retint();
    }

    /// <summary>
    /// Repaints the preview when the machine changes, or when its colour does.
    /// </summary>
    /// <remarks>
    /// The colour as well as the machine, because the colour is picked here: the panel beside
    /// the picker is the whole of the feedback, and a panel that only recoloured on opening
    /// would mean choosing a colour by saving and looking.
    /// </remarks>
    private void OnEditorChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DesignerViewModel.Project)
            or nameof(DesignerViewModel.Accent)) Retint();
    }

    /// <summary>
    /// Repainted after the theme swap has settled rather than during it.
    /// </summary>
    /// <remarks>
    /// The shades are mixed against the theme's own colours, and read in the middle of the swap
    /// those are still the old theme's: the preview came out of a light theme still wearing
    /// light cards on a dark page.
    /// </remarks>
    private void Later() => Avalonia.Threading.Dispatcher.UIThread.Post(Retint);

    /// <summary>Puts the machine's colours on the plate, so it looks like the box it is.</summary>
    private void Retint() =>
        _tint.Repaint(this.FindControl<Border>("PanelPreview")!, Editor?.Project?.Theme);

    /// <summary>
    /// Opens the colours, and puts back whatever comes out of them.
    /// </summary>
    /// <remarks>
    /// The whole theme rather than the seven, because the dialog holds the colour too: somebody
    /// in there to make the face lighter may well move the colour while looking at it, and
    /// keeping half of what they did would be worse than keeping none.
    /// </remarks>
    private async void Colours_Click(object? sender, RoutedEventArgs e)
    {
        if (Editor is not { Project: not null } editor) return;

        var wanted = await PanelColoursDialog.AskAsync(editor.Project.Name, editor.Theme);

        if (wanted == null) return;

        editor.Dressed(wanted);

        Retint();
    }

    /// <summary>
    /// Points the level tool at a folder of recordings anywhere on the disc.
    /// </summary>
    /// <remarks>
    /// The whole point of that scope: a pack downloaded this morning is levelled where it lies,
    /// before any of it is brought into a machine, rather than one file at a time afterwards.
    /// </remarks>
    private async void LevelFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (Editor is not { } editor) return;

        string? folder = await PickFolder("A folder of recordings to level");

        if (folder != null) editor.Utilities.Pick(folder);
    }

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


    /// <summary>
    /// The machine being built, reached through the application's view model because this page
    /// is shown inside it rather than given an editor of its own.
    /// </summary>
    private DesignerViewModel? Editor => DataContext as DesignerViewModel;

    /// <summary>Opens a machine project, which is a folder on somebody's disc.</summary>
    private async void Open_Click(object? sender, RoutedEventArgs e)
    {
        if (Editor is not { } editor) return;

        string? folder = await PickFolder("Open a " + editor.Word);

        if (folder != null) editor.Open(folder);
    }

    /// <summary>
    /// What the keyboard can ask of the machine editor.
    /// </summary>
    /// <remarks>
    /// Answered by the view rather than by its view model, because saving a machine that has
    /// never had a folder has to ask where to put it, and asking is a window's job. The
    /// dispatcher walks outwards from whatever has the keyboard and takes the first thing that
    /// says yes, so this is reached while the editor is on screen and not otherwise.
    /// </remarks>
    bool Shortcuts.Interfaces.IShortcutContext.Can(Shortcuts.Enums.ShortcutAction action) => action switch
    {
        Shortcuts.Enums.ShortcutAction.Save => Editor?.Project != null,
        Shortcuts.Enums.ShortcutAction.Delete => Editor?.RemoveElementCommand.CanExecute(null) == true,
        Shortcuts.Enums.ShortcutAction.Undo => Editor?.History.CanUndo == true,
        Shortcuts.Enums.ShortcutAction.Redo => Editor?.History.CanRedo == true,
        _ => false
    };

    /// <summary>
    /// Does what the keyboard asked of the machine editor.
    /// </summary>
    /// <remarks>
    /// Undo and redo put the step back into the project that is already open rather than
    /// replacing it, so every wrapper on screen is still pointed at the elements that were there
    /// a moment ago; hanging them off the tree again is what makes the panel show what the
    /// machine now says.
    /// </remarks>
    void Shortcuts.Interfaces.IShortcutContext.Do(Shortcuts.Enums.ShortcutAction action)
    {
        if (Editor is not { } editor) return;

        switch (action)
        {
            case Shortcuts.Enums.ShortcutAction.Save:
                Save_Click(this, new RoutedEventArgs());
                break;

            case Shortcuts.Enums.ShortcutAction.Delete:
                editor.RemoveElementCommand.Execute(null);
                break;

            case Shortcuts.Enums.ShortcutAction.Undo when editor.History.Undo(editor.Project):
                editor.Rewrap();
                break;

            case Shortcuts.Enums.ShortcutAction.Redo when editor.History.Redo(editor.Project):
                editor.Rewrap();
                break;
        }
    }

    /// <summary>
    /// One of the machine's own fields was typed into and left.
    /// </summary>
    /// <remarks>
    /// The name, what it is, who made it and its version bind straight through to the project,
    /// which is a plain object with nothing to say when it changes. So nothing knew a machine
    /// had been renamed: the Save button stayed cold and undo could not take it back, while
    /// dropping a knob on the panel did both. Told here, once, when the box is left.
    /// </remarks>
    private void Machine_Changed(object? sender, RoutedEventArgs e) => Editor?.Redraw();

    /// <summary>
    /// Reads the machine back off the disc as it was last saved, asked first.
    /// </summary>
    /// <remarks>
    /// The undo history goes with it, which the question says, because the steps are the
    /// machine's own JSON and putting one back after the file has been reread would restore a
    /// version of a machine nobody is looking at any more.
    /// </remarks>
    private async void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        if (Editor is not { } editor || !editor.CanCancelChanges) return;

        bool confirmed = await ConfirmDialog.AskAsync(
            "Cancel the changes",
            $"Throw away everything done to '{editor.Project!.Name}' since it was last saved, and "
                + "read it back as it was? What you have undone and redone goes with it.",
            "Cancel changes");

        if (confirmed) editor.CancelChanges();
    }

    /// <summary>
    /// Writes the machine to its folder, asking for one the first time.
    /// </summary>
    /// <remarks>
    /// The asking is why saving is answered by the view rather than by the view model: a machine
    /// that has never had a folder has to be asked where to go, and asking is a window's job.
    /// </remarks>
    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (Editor is not { } editor || editor.Project == null) return;

        if (!editor.NeedsFolder)
        {
            editor.Save();
            return;
        }

        string? folder = await PickFolder("Where to keep this " + editor.Word);

        if (folder != null) editor.Save(folder);
    }

    /// <summary>
    /// Writes the whole machine into a folder somebody chooses, and works there afterwards.
    /// </summary>
    /// <remarks>
    /// Asked here rather than in the view model for the same reason saving is: choosing a folder
    /// is a window's job.
    ///
    /// A folder already holding a different machine is asked about first, naming both. The case
    /// this exists for is writing an edited machine back over the copy that ships beside the
    /// program, so overwriting has to be allowed; landing on somebody else's machine by picking
    /// the wrong folder is the same gesture and would silently bury it.
    ///
    /// Nothing in the folder is deleted either way, so a machine written over another leaves
    /// behind whatever the other had and this one has not. That is the registry's rule for a
    /// shipped machine being updated and it is right here too: what else is in that folder is
    /// not this machine's business.
    /// </remarks>
    private async void SaveAs_Click(object? sender, RoutedEventArgs e)
    {
        if (Editor is not { CanExport: true } editor || editor.Project == null) return;

        string? folder = await PickFolder("Where to keep this " + editor.Word + " from now on");

        if (folder == null) return;

        if (Holder(folder) is { Length: > 0 } other)
        {
            bool confirmed = await ConfirmDialog.AskAsync(
                "Write over that machine",
                $"'{other}' is already in that folder. Write '{editor.Project.Name}' over it? "
                    + "Files that only the other machine has are left where they are.",
                "Write over it");

            if (!confirmed) return;
        }

        editor.SaveAs(folder);
    }

    /// <summary>
    /// The name of the machine already in that folder, when it is a different one.
    /// </summary>
    /// <remarks>
    /// By id rather than by name, since the name is the part somebody is free to change and the
    /// id is the part that cannot be. A folder holding this same machine is not a collision: it
    /// is the ordinary case of saving an edited copy back over where it came from, and asking
    /// about it every time would train somebody to press the button without reading it.
    ///
    /// A folder with no manifest, or one that will not read, is nothing to warn about: there is
    /// no machine there to lose.
    /// </remarks>
    private string? Holder(string folder)
    {
        if (Editor?.Project is not { } mine) return null;

        var there = Editor?.Read(folder);

        if (there == null || string.Equals(there.Id, mine.Id, StringComparison.OrdinalIgnoreCase)) return null;

        return there.Name.Length > 0 ? there.Name : there.Id;
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
            Title = "Export the " + editor.Word,
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
        if (Editor is not { } editor) return;

        if (editor.Browse is not { } shelf) return;

        var take = await TakeDialog.PickAsync(shelf);

        if (take == null || take.FilePath.Length == 0) return;

        editor.Values.SetText(key, take.FilePath);
        editor.Redraw();
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
        if (sender is not Control row || row.DataContext is not PanelElementViewModel element) return;

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
    private sealed record Carrying(string? Kind, PanelElementViewModel? Element, Point From)
    {
        /// <summary>
        /// Set once the hand has travelled far enough for this to be a drag. Mutable on a
        /// record because the answer changes during one gesture and the rest of it does not.
        /// </summary>
        public bool Moved { get; set; }
    }

    /// <summary>What the hand has hold of, or nothing when it has hold of nothing.</summary>
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
    /// <remarks>
    /// A press that has not really moved is still a press: starting the ghost on the first pixel
    /// would mean a click on a part flashing a picture of itself across the page. See
    /// <see cref="Threshold"/>.
    ///
    /// Whether letting go here would land is asked while the hand is still moving, so the
    /// picture can say the answer rather than leaving it to be found out by letting go. It is
    /// the same <see cref="Takes"/> the release asks.
    ///
    /// The line the panel draws is where among the others the part would go, which is the whole
    /// of how somebody says "after that one".
    /// </remarks>
    private void Carrying_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_carrying is not { } carrying) return;

        var at = e.GetPosition(this);

        if (!carrying.Moved)
        {
            if (Math.Abs(at.X - carrying.From.X) < Threshold &&
                Math.Abs(at.Y - carrying.From.Y) < Threshold) return;

            carrying.Moved = true;

            ShowGhost(carrying);
        }

        MoveGhost(at);

        _ghost.Refused = !Takes(at);

        var (onPanel, onList) = Under(at);

        Mark(onPanel, onList);

        PanelCanvas.Landing(Within(this.InputHitTest(at) as Visual, PanelCanvas) ? Inside(at) : null);
    }

    /// <summary>Puts down what was being carried, wherever the hand let go.</summary>
    /// <remarks>
    /// Let go without ever moving, a part out of the library is added where the selection is,
    /// which is how one gets added without aiming; something already on the machine stays exactly
    /// where it is, since the press was somebody picking it to work on.
    ///
    /// Let go over neither the machine nor the list, the drop is off. A part that quietly went to
    /// the root because the hand was over the parameters would be a part somebody has to find and
    /// take out again, so it says so instead.
    ///
    /// On the panel the hand says where among the others, which is what the line was showing. On
    /// the list it says which container, and the part goes at the end of it.
    /// </remarks>
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

        if (!carrying.Moved)
        {
            if (carrying.Kind is { } picked) editor.AddElementCommand.Execute(picked);

            return;
        }

        var landed = this.InputHitTest(at) as Visual;

        if (!Takes(at))
        {
            editor.Status = "Dropped nowhere, so nothing moved.";

            return;
        }

        var (into, place) = Within(landed, PanelCanvas)
            ? PanelCanvas.Where(Inside(at))
            : (onList?.Element, -1);

        into ??= onPanel;

        if (carrying.Kind is { } kind) editor.Drop(kind, into, place);
        else if (carrying.Element is { } moved) editor.MoveInto(moved.Element, into, place);
    }

    /// <summary>
    /// True where letting go would put the part somewhere: the machine's own picture, or the
    /// list of what is on it.
    /// </summary>
    /// <remarks>
    /// One test, asked twice: while the hand moves, so the picture in it can say whether this
    /// will land, and again when it is let go. Two spellings of it would eventually disagree,
    /// and the way that fails is a ghost promising a drop that the release then refuses.
    /// </remarks>
    private bool Takes(Point at)
    {
        var landed = this.InputHitTest(at) as Visual;

        return Within(landed, PanelCanvas) || Within(landed, PanelTree);
    }

    /// <summary>What the hand is over: an element on the panel, or a line of the list.</summary>
    /// <remarks>
    /// Over the panel but over nothing in particular means the machine itself, which is where a
    /// part let go over open space goes.
    /// </remarks>
    private (Rack.SoundDevices.Faces.PanelElement?, ViewModels.PanelElementViewModel?) Under(Point at)
    {
        var hit = this.InputHitTest(at) as Visual;

        if (hit == null) return (null, null);

        if (Row(hit) is { } row) return (null, row);

        if (Within(hit, PanelCanvas)) return (PanelCanvas.ElementAt(hit) ?? Editor?.Project?.Panel.Root, null);

        return (null, null);
    }

    /// <summary>
    /// Whether one visual is inside another, walked up the tree.
    /// </summary>
    /// <remarks>
    /// Asked rather than hit-testing the holder directly, because what is under the pointer is a
    /// knob or a row deep inside the panel, and the question being asked is which of the two
    /// surfaces it belongs to.
    /// </remarks>
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
            inside = new Rack.Controls.PartSample { Kind = kind, Width = 62, Height = 44 };
        }
        else
        {
            var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };

            row.Children.Add(new Rack.Controls.PartSample
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

        _ghost.Show(inside);
    }

    /// <summary>Moves the picture in the hand, in the page's own coordinates, where the layer is.</summary>
    private void MoveGhost(Point at) => _ghost.MoveTo(at);

    /// <summary>The same point, said in the panel's own coordinates.</summary>
    /// <remarks>
    /// The carry is followed in this page's coordinates, because that is where the ghost is
    /// drawn, and the panel is somewhere inside the page with a card and a scroll around it.
    /// Asking the panel about a point in the page's terms would be off by wherever it sits.
    /// </remarks>
    private Point Inside(Point at) =>
        this.TranslatePoint(at, PanelCanvas) ?? at;

    /// <summary>Takes the picture out of the hand, whether the drag landed or was abandoned.</summary>
    private void HideGhost() => _ghost.Hide();


    /// <summary>Outlines what would take the part, on the panel and on the list at once.</summary>
    /// <remarks>
    /// Both, because a drag crosses from one to the other and whichever is left holding a mark
    /// is showing something that is no longer true. Clearing is the same call with nothing in it.
    /// </remarks>
    private void Mark(Rack.SoundDevices.Faces.PanelElement? onPanel, ViewModels.PanelElementViewModel? onList)
    {
        PanelCanvas.Marked = onPanel;

        if (Editor is not { } editor) return;

        foreach (var row in editor.Every()) row.IsDropTarget = ReferenceEquals(row, onList);
    }

    /// <summary>The outermost element, which is where a part let go over nothing goes.</summary>
    private Rack.SoundDevices.Faces.PanelElement? Root() => Editor?.Project?.Panel.Root;



    /// <summary>Which element a line of the list stands for, or nothing when it is not a line.</summary>
    private static ViewModels.PanelElementViewModel? Row(object? source)
    {
        for (var at = source as Visual; at != null; at = Avalonia.VisualTree.VisualExtensions.GetVisualParent(at))
        {
            if (at is TreeViewItem { DataContext: ViewModels.PanelElementViewModel element }) return element;
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

    /// <summary>
    /// Asks for a folder, starting where the machines are rather than where the system was last.
    /// </summary>
    /// <remarks>
    /// Opening one is not opening a file. The system offers wherever you last were, which after
    /// an afternoon of anything else is a folder with none of these in it and three levels to
    /// climb out of. So it starts beside the one already open, which is the folder its
    /// neighbours are in, and at this world's own installed folder when there is none: the
    /// effects tab starts in the effects, which is what the world is asked for.
    ///
    /// Where the machine landed is remembered, so a second machine opened out of somebody else's
    /// folder does not send you back to this one.
    /// </remarks>
    private async System.Threading.Tasks.Task<string?> PickFolder(string title)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (storage == null) return null;

        var picked = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = await Among(storage),
        });

        string? landed = picked.Count == 0 ? null : picked[0].TryGetLocalPath();

        if (landed is { Length: > 0 } && Path.GetDirectoryName(landed) is { Length: > 0 } home) _lastHome = home;

        return landed;
    }

    /// <summary>Where machines were last seen: beside the one open, or the installed ones.</summary>
    /// <remarks>
    /// Tried in turn, and a path the platform will not make a folder of is a path to pass over,
    /// since the next one may work. None of them working leaves the system's own last folder,
    /// which is where this started.
    /// </remarks>
    private async System.Threading.Tasks.Task<IStorageFolder?> Among(IStorageProvider storage)
    {
        foreach (string? home in new[] { Beside(Editor?.Folder), _lastHome, Editor?.Home })
        {
            if (home is not { Length: > 0 } || !Directory.Exists(home)) continue;

            try
            {
                if (await storage.TryGetFolderFromPathAsync(home) is { } folder) return folder;
            }
            catch (Exception) { }
        }

        return null;
    }

    /// <summary>The folder a machine sits in, which is the folder its neighbours sit in.</summary>
    private static string? Beside(string? machine) =>
        machine is { Length: > 0 } ? Path.GetDirectoryName(machine) : null;

    /// <summary>
    /// The folder the last machine was opened out of, which is where the next one starts. Not
    /// stored: it is about this session's afternoon rather than about the installation.
    /// </summary>
    private string? _lastHome;
}
