using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Machines;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Machines;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.IO;

namespace JingleBox2.ViewModels;

/// <summary>
/// The machine editor: one project open at a time, and what can be done with it.
/// </summary>
/// <remarks>
/// The other half of the rack. The rack holds machines that are registered and ready for a song
/// to take an instrument off; this is where one is made in the first place. New, open, save, and
/// export it as a zip for somebody else to import.
///
/// Nothing about a song here, and nothing about instruments: a machine is a box, and it becomes
/// an instrument only when a song takes it.
/// </remarks>
public sealed partial class MachineEditorViewModel : ObservableObject
{
    /// <summary>The project being worked on, or null when nothing is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject))]
    [NotifyPropertyChangedFor(nameof(CanExport))]
    [NotifyPropertyChangedFor(nameof(Title))]
    [NotifyPropertyChangedFor(nameof(Folder))]
    private MachineProject? project;

    public MachineEditorViewModel() => Values = new MachinePreviewValues(Parameters);

    public bool HasProject => Project != null;

    /// <summary>What the editor says it is showing.</summary>
    public string Title => Project == null
        ? "No machine open"
        : Project.Name.Length > 0 ? Project.Name : "Untitled machine";

    [ObservableProperty] private string status = "";

    /// <summary>
    /// Where the open machine keeps its own files, or nothing when it has never been saved.
    /// </summary>
    /// <remarks>
    /// The panel needs it, because a picture on a machine's face is named against the machine's
    /// folder and nothing else. Said here rather than read off the project, so that a machine
    /// saved for the first time announces the folder it has just been given: the project is
    /// plain data and says nothing when its folder is written.
    /// </remarks>
    public string Folder => Project?.Folder ?? "";

    /// <summary>
    /// A machine nobody has saved yet, with an id of its own from the start.
    /// </summary>
    /// <remarks>
    /// The id is made once and never changes, because it is what every song that uses this
    /// machine writes down. Made from the name it is given later would mean a machine losing
    /// its songs the first time it is renamed.
    /// </remarks>
    public IRelayCommand NewCommand => new RelayCommand(() =>
    {
        Project = new MachineProject
        {
            Id = "machine." + Guid.NewGuid().ToString("n")[..8],
            Name = "New machine",
            Version = "1.0",
            Theme = new MachineTheme("#7B838C")
        };

        Status = "A new machine. Save it somewhere to start keeping it.";
    });

    /// <summary>Opens the project in that folder.</summary>
    public void Open(string folder)
    {
        var opened = MachineProject.Open(folder);

        if (opened == null)
        {
            Status = "No machine in " + folder;
            return;
        }

        Project = opened;
        Status = "Opened '" + opened.Name + "'";
    }

    /// <summary>Writes it where it lives, or where it is being put for the first time.</summary>
    public void Save(string? folder = null)
    {
        if (Project == null) return;

        try
        {
            // What is written down and what is in the folder have to be the same machine, so the
            // pictures are put in order first: anything nothing names is deleted, the numbers
            // close up, and the panel is pointed at the names the files now have.
            Tidy();

            Project.Save(folder);
            Status = "Saved to " + Project.Folder;

            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Folder));
        }
        catch (Exception ex)
        {
            Status = "Could not save: " + ex.Message;
        }
    }

    /// <summary>True when saving needs somewhere to save to.</summary>
    public bool NeedsFolder => Project is { IsSaved: false };

    /// <summary>True once there is something on disc worth handing to somebody.</summary>
    public bool CanExport => Project is { IsSaved: true };

    /// <summary>What to call the zip, before anybody has said where to put it.</summary>
    public string ExportName => Project == null ? "machine.zip" : Safe(Project.Name, "machine") + ".zip";

    /// <summary>
    /// Writes the machine out as a zip.
    /// </summary>
    /// <remarks>
    /// This is how a machine leaves here. Not installing: what the app runs is read from its own
    /// machines folder, and getting a machine in there is the importer's job on the SETTINGS
    /// tab. Keeping the two apart means a machine you were handed and a machine you made arrive
    /// by exactly the same road, which is the only way to find out that the road works.
    ///
    /// The project stays where you keep your work. The zip is a copy of it, and the folder it
    /// came from is nobody else's business, so nothing about the path goes into the file.
    /// </remarks>
    public void Export(string zipPath)
    {
        if (Project is not { IsSaved: true } project) return;

        try
        {
            MachineArchive.Export(project, zipPath);

            Status = "Exported '" + project.Name + "' to " + zipPath;
        }
        catch (Exception ex)
        {
            Status = "Could not export: " + ex.Message;
        }
    }

    /// <summary>A file name that will not fight the file system, from the machine's own name.</summary>
    private static string Safe(string name, string fallback)
    {
        string wanted = (name ?? "").Trim();

        foreach (char bad in Path.GetInvalidFileNameChars()) wanted = wanted.Replace(bad, ' ');

        wanted = wanted.Trim();

        return wanted.Length > 0 ? wanted : fallback;
    }

    /// <summary>
    /// Adds a parameter, named so it can be told from the others and no more.
    /// </summary>
    /// <remarks>
    /// A machine is its parameters, so this is the ordinary way to build one: add, name, set
    /// the range, and the panel is drawn from what is here.
    /// </remarks>
    public IRelayCommand AddParameterCommand => new RelayCommand(() =>
    {
        if (Project == null) return;

        int at = Project.Parameters.Count + 1;

        var parameter = new MachineParameter
        {
            Key = "p" + at,
            Name = "Parameter " + at,
            Min = 0,
            Max = 1,
            Default = 0,
            Step = 0.01
        };

        var wrapped = new MachineParameterViewModel(parameter);

        wrapped.PropertyChanged += Edited;

        Project.Parameters.Add(parameter);
        Parameters.Add(wrapped);

        ParametersChanged();

        Status = "Added a parameter. Save when you are happy with it.";
    });

    /// <summary>Takes one out. What a song saved under that key is simply not read again.</summary>
    public IRelayCommand<MachineParameterViewModel> RemoveParameterCommand =>
        new RelayCommand<MachineParameterViewModel>(parameter =>
        {
            if (Project == null || parameter == null) return;

            Project.Parameters.Remove(parameter.Parameter);
            Parameters.Remove(parameter);

            ParametersChanged();

            Status = "Removed '" + parameter.Name + "'";
        });

    /// <summary>
    /// The parameters of the machine that is open.
    /// </summary>
    /// <remarks>
    /// A collection of its own rather than the project's list handed out: a list that is added
    /// to is still the same list, so a page bound straight to it is never told anything has
    /// changed. This is filled from the project when one is opened and kept in step with it.
    /// </remarks>
    public ObservableCollection<MachineParameterViewModel> Parameters { get; } = new();

    /// <summary>What the panel in the editor reads and writes: the parameters, and nothing kept.</summary>
    public IMachineValues Values { get; }

    /// <summary>
    /// Where a recording's picture comes from while a panel is being laid out.
    /// </summary>
    /// <remarks>
    /// The real shelf, not a pretend one. A machine with a waveform on its face is laid out
    /// against a real recording or it is laid out against nothing, and nothing is the wrong
    /// size: the picture is 340 wide because somebody looked at one.
    ///
    /// Set by whoever builds the editor, since the shelf belongs to the application and the
    /// editor is only borrowing it.
    /// </remarks>
    public IMachineTakes? Takes { get; set; }

    /// <summary>
    /// The shelf the panel's picker offers, which on a machine holding a recording is yours.
    /// </summary>
    /// <remarks>
    /// The same reason the pictures are real. A picker laid out against five made up names is
    /// laid out against the wrong widths, and the category in front of it cannot be judged at
    /// all without the categories you actually file takes under.
    ///
    /// Set by whoever builds the editor, for the same reason <see cref="Takes"/> is: the shelf
    /// is the application's and the editor is borrowing it.
    /// </remarks>
    public IMachinePresets? Presets { get; set; }

    /// <summary>
    /// Puts a recording on the panel being designed, wherever the panel keeps one.
    /// </summary>
    /// <remarks>
    /// The machine says which setting holds its take, by naming one on its Take element, so
    /// that is what is asked rather than a name being assumed. A machine with two of them takes
    /// the first, which is the one at the top of the panel and the one somebody laying it out
    /// is looking at.
    ///
    /// Nothing is kept: this is a preview, and what it is showing goes when the editor is
    /// closed. What is being laid out is the panel, not the sound.
    /// </remarks>
    public void PutTake(string path)
    {
        if (Project?.Panel.Root is not { } root) return;

        if (Holder(root) is not { Length: > 0 } key) return;

        Values.SetText(key, path);

        Redraw();
    }

    /// <summary>The setting the machine keeps its recording in, or nothing when it keeps none.</summary>
    private static string? Holder(MachineElement element)
    {
        if (element.Element == MachineElementKinds.Take && element.Parameter.Length > 0)
            return element.Parameter;

        foreach (var child in element.Children)
            if (Holder(child) is { Length: > 0 } found) return found;

        return null;
    }

    /// <summary>
    /// True while the panel is being arranged rather than played with.
    /// </summary>
    /// <remarks>
    /// Designing, a click picks the control up to be worked on; off, the same click turns it.
    /// A designer needs both, because a panel you cannot try is a drawing and a panel you
    /// cannot pick apart is finished.
    /// </remarks>
    [ObservableProperty] private bool designing = true;

    /// <summary>
    /// Whether the parts library is unfolded.
    /// </summary>
    /// <remarks>
    /// Five cards stand round one panel, and the panel is the only one of the six anybody draws
    /// on. So each card folds away to its own title, and a column with nothing open left in it
    /// gives its width back, which is what the folding is for. None of this is written down: the
    /// designer opens the way it always has, with everything showing.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PartsHeight))]
    [NotifyPropertyChangedFor(nameof(LeftWidth))]
    [NotifyPropertyChangedFor(nameof(PartsPanelSizable))]
    [NotifyPropertyChangedFor(nameof(LeftColumnSizable))]
    private bool partsOpen = true;

    /// <summary>Whether the panel, as a list, is unfolded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelHeight))]
    [NotifyPropertyChangedFor(nameof(LeftWidth))]
    [NotifyPropertyChangedFor(nameof(PartsPanelSizable))]
    [NotifyPropertyChangedFor(nameof(LeftColumnSizable))]
    private bool panelOpen = true;

    /// <summary>Whether the parameters under the panel are unfolded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ParametersHeight))]
    [NotifyPropertyChangedFor(nameof(PanelParametersSizable))]
    private bool parametersOpen = true;

    /// <summary>Whether the machine's own details are unfolded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RightWidth))]
    [NotifyPropertyChangedFor(nameof(MachinePickedSizable))]
    [NotifyPropertyChangedFor(nameof(RightColumnSizable))]
    private bool machineOpen = true;

    /// <summary>Whether what is picked is unfolded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PickedHeight))]
    [NotifyPropertyChangedFor(nameof(RightWidth))]
    [NotifyPropertyChangedFor(nameof(MachinePickedSizable))]
    [NotifyPropertyChangedFor(nameof(RightColumnSizable))]
    private bool pickedOpen = true;

    /// <summary>
    /// The room the parts library asks for.
    /// </summary>
    /// <remarks>
    /// A row measured in stars keeps its share of the height whatever is in it, so a card folded
    /// inside one would be a title with an empty card hanging under it. Auto is what makes a
    /// fold look like a fold: the row shrinks to the title and whatever is beside it takes the
    /// rest. The sizes these start at are the ones the editor was laid out with in the first
    /// place.
    ///
    /// Written to as well as read, because a handle dragged between two cards sets the size on
    /// the row itself. Taking it back here is what lets a card fold and open again at the size
    /// it was left at rather than at the size it was born with. The Auto that folds it is not
    /// kept, or opening it again would open it onto nothing.
    /// </remarks>
    public GridLength PartsHeight
    {
        get => PartsOpen ? _partsHeight : GridLength.Auto;
        set { if (!value.IsAuto) _partsHeight = value; }
    }

    private GridLength _partsHeight = new(2, GridUnitType.Star);

    /// <summary>The room the panel list asks for.</summary>
    public GridLength PanelHeight
    {
        get => PanelOpen ? _panelHeight : GridLength.Auto;
        set { if (!value.IsAuto) _panelHeight = value; }
    }

    private GridLength _panelHeight = new(1, GridUnitType.Star);

    /// <summary>The room the parameters ask for, which the panel above them takes when they fold.</summary>
    public GridLength ParametersHeight
    {
        get => ParametersOpen ? _parametersHeight : GridLength.Auto;
        set { if (!value.IsAuto) _parametersHeight = value; }
    }

    private GridLength _parametersHeight = new(220);

    /// <summary>The room what is picked asks for.</summary>
    public GridLength PickedHeight
    {
        get => PickedOpen ? _pickedHeight : GridLength.Auto;
        set { if (!value.IsAuto) _pickedHeight = value; }
    }

    private GridLength _pickedHeight = new(1, GridUnitType.Star);

    /// <summary>The width of the parts and panel column, or nothing but its two titles.</summary>
    public GridLength LeftWidth
    {
        get => LeftColumnSizable ? _leftWidth : GridLength.Auto;
        set { if (!value.IsAuto) _leftWidth = value; }
    }

    private GridLength _leftWidth = new(230);

    /// <summary>The width of the details column, or nothing but its two titles.</summary>
    public GridLength RightWidth
    {
        get => RightColumnSizable ? _rightWidth : GridLength.Auto;
        set { if (!value.IsAuto) _rightWidth = value; }
    }

    private GridLength _rightWidth = new(330);

    /// <summary>
    /// True while both cards either side of the parts handle are open.
    /// </summary>
    /// <remarks>
    /// A handle answers by putting a size on the two it lies between, which would undo a fold
    /// the moment it was dragged. It goes away with the card instead. There is nothing to share
    /// out when only one of the two is showing anyway.
    /// </remarks>
    public bool PartsPanelSizable => PartsOpen && PanelOpen;

    /// <summary>True while the parameters are open for the handle above them to share out.</summary>
    public bool PanelParametersSizable => ParametersOpen;

    /// <summary>True while both cards either side of the details handle are open.</summary>
    public bool MachinePickedSizable => MachineOpen && PickedOpen;

    /// <summary>True while the parts and panel column has anything open in it.</summary>
    public bool LeftColumnSizable => PartsOpen || PanelOpen;

    /// <summary>True while the details column has anything open in it.</summary>
    public bool RightColumnSizable => MachineOpen || PickedOpen;

    /// <summary>
    /// The panel, as a tree with the root at the top of it.
    /// </summary>
    /// <remarks>
    /// One item, always: a panel has exactly one root, and a tree control wants a collection to
    /// start from rather than a single item. Holding the root in a collection also means opening
    /// another project is emptying and refilling one list, so nothing bound to it goes stale.
    /// </remarks>
    public ObservableCollection<MachineElementViewModel> Elements { get; } = new();

    /// <summary>What is picked out in the tree, and what the inspector is showing.</summary>
    /// <remarks>
    /// Null is the ordinary state on opening rather than something to be avoided. Adding with
    /// nothing picked puts the element at the root, which is what the first element of a new
    /// panel wants anyway.
    /// </remarks>
    [ObservableProperty]
    private MachineElementViewModel? selectedElement;

    /// <summary>
    /// The kinds of element that can be put on a panel here.
    /// </summary>
    /// <remarks>
    /// Written out rather than found by looking over the constants, so that the order is the one
    /// that suits somebody building a panel, containers first and controls after, and so that a
    /// constant added for a control this designer cannot yet place does not turn up in the list
    /// on its own.
    /// </remarks>
    /// <summary>Every line of the panel list, however deep, for anything that has to touch all of them.</summary>
    public IEnumerable<MachineElementViewModel> Every()
    {
        foreach (var top in Elements)
        {
            foreach (var one in Below(top)) yield return one;
        }
    }

    private static IEnumerable<MachineElementViewModel> Below(MachineElementViewModel element)
    {
        yield return element;

        foreach (var child in element.Children)
        {
            foreach (var one in Below(child)) yield return one;
        }
    }

    public IReadOnlyList<string> Library { get; } = new[]
    {
        MachineElementKinds.Grid,
        MachineElementKinds.Group,
        MachineElementKinds.Row,
        MachineElementKinds.Column,
        MachineElementKinds.Strip,
        MachineElementKinds.Knob,
        MachineElementKinds.Fader,
        MachineElementKinds.Switch,
        MachineElementKinds.Number,
        MachineElementKinds.Button,
        MachineElementKinds.Choice,
        MachineElementKinds.Led,
        MachineElementKinds.Meter,
        MachineElementKinds.Keys,
        MachineElementKinds.Wave,
        MachineElementKinds.Envelope,
        MachineElementKinds.Image,
        MachineElementKinds.Take,
        MachineElementKinds.Preset,
        MachineElementKinds.Label,
        MachineElementKinds.Spacer
    };

    /// <summary>
    /// Puts an element of that kind inside whatever is picked out.
    /// </summary>
    /// <remarks>
    /// Into the root when nothing is picked, because refusing would only mean telling somebody
    /// to click the one element that is certainly there. What has just been added becomes the
    /// selection, since the next thing anybody does to a new element is set it up.
    /// </remarks>
    public IRelayCommand<string> AddElementCommand => new RelayCommand<string>(kind =>
    {
        if (Project == null || string.IsNullOrWhiteSpace(kind)) return;

        var into = SelectedElement ?? Elements.FirstOrDefault();

        if (into == null) return;

        SelectedElement = into.Add(Celled(into, Part(kind!)));

        // Adding a part is laying the panel out, so the panel goes back to being laid out.
        // Otherwise what has just been added cannot be picked, and the outline that says which
        // one it is never appears.
        Designing = true;

        Status = "Added " + kind + ".";
    });

    /// <summary>
    /// Moves something already on the machine inside something else on it.
    /// </summary>
    /// <remarks>
    /// The other half of dropping a part: a panel is built by putting things where they go, and
    /// getting it right first time is not how anybody lays one out. What moves is the element
    /// itself, with its settings and everything inside it, so a group full of knobs travels as
    /// a group full of knobs.
    ///
    /// Three things are refused, all for the same reason: they would leave a tree that is not a
    /// tree. The outermost element cannot be moved, because it is what everything else is inside.
    /// Nothing can be put inside itself. And nothing can be put inside something it already
    /// contains, which is the one that is not obvious: a row dropped on a knob it holds would
    /// take the knob with it and leave both pointing at each other.
    ///
    /// Landing on something that holds nothing, a knob or a picture, means the thing that holds
    /// it, which is the same rule a part off the library follows and the one that makes dropping
    /// on a crowded panel work at all.
    /// </remarks>
    public void MoveInto(MachineElement moved, MachineElement? onto, int at = -1)
    {
        if (Project == null) return;

        if (Find(Elements, moved) is not { Parent: { } from } wrapped) return;

        var target = onto == null ? Elements.FirstOrDefault() : Find(Elements, onto);

        if (target == null) return;

        if (!Holds(target.Kind)) target = target.Parent;

        if (target == null) return;

        if (ReferenceEquals(target, wrapped) || Inside(wrapped, target))
        {
            Status = "A part cannot go inside itself.";
            return;
        }

        // Moving something inside what it is already in is reordering it, which is the ordinary
        // way of saying "after that one". Taking it out first means the place it is going to is
        // counted without it, which is what somebody dragging it there is looking at.
        bool same = ReferenceEquals(target, from);

        int was = from.Children.IndexOf(wrapped);

        if (!from.Remove(wrapped)) return;

        int place = same && at > was ? at - 1 : at;

        Celled(target, moved);

        target.Put(wrapped, place);

        SelectedElement = wrapped;

        Redraw();

        Status = same
            ? "Moved the " + moved.Element + "."
            : "Moved the " + moved.Element + " into the " + target.Kind + ".";
    }

    /// <summary>Every picture an element and everything inside it names.</summary>
    private static List<string> Pictures(MachineElement element)
    {
        var found = new List<string>();

        Gather(element, found);

        return found;
    }

    private static void Gather(MachineElement element, List<string> into)
    {
        if (element.Element == MachineElementKinds.Image &&
            element.Properties.TryGetValue("file", out var named) &&
            named.Length > 0)
        {
            into.Add(named);
        }

        foreach (var child in element.Children) Gather(child, into);
    }

    /// <summary>
    /// Deletes the pictures nothing on the panel shows any more, and says how many went.
    /// </summary>
    /// <remarks>
    /// Asked of what is left rather than of what was removed, because the same picture can be on
    /// a panel twice and removing one of the two is not removing the picture. The file goes only
    /// when the last element naming it has gone.
    /// </remarks>
    private int Forget(IEnumerable<string> pictures)
    {
        if (Project is not { IsSaved: true } project) return 0;

        var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (project.Panel.Root is { } root) foreach (var one in Pictures(root)) kept.Add(one);

        int gone = 0;

        foreach (var picture in pictures.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (kept.Contains(picture)) continue;

            if (project.RemoveImage(picture)) gone++;
        }

        return gone;
    }

    /// <summary>
    /// Makes the pictures on disc and the pictures the panel names one and the same.
    /// </summary>
    /// <remarks>
    /// Two rules, and they are the same rule said twice: nothing in the folder that the machine
    /// does not show, and no gaps in the numbering. A machine is handed over as a folder, and
    /// image1 with image3 beside it is a machine that has plainly lost something even when it
    /// has not.
    /// </remarks>
    private void Tidy()
    {
        if (Project is not { IsSaved: true } project) return;

        var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (project.Panel.Root is { } root) foreach (var one in Pictures(root)) kept.Add(one);

        project.SweepImages(kept);

        Renumbered();
    }

    /// <summary>
    /// Renumbers what is left and points every element at its picture's new name.
    /// </summary>
    /// <remarks>
    /// The files move first and the panel follows, because the folder is the thing that has to
    /// be right: a machine is handed over as a folder, and an element pointing at a name nothing
    /// on disc has is a picture that does not draw on somebody else's rack.
    ///
    /// Only what actually moved is rewritten. A machine whose pictures were already in order
    /// gets nothing done to it at all, which is the ordinary case.
    /// </remarks>
    private void Renumbered()
    {
        if (Project is not { IsSaved: true } project) return;

        var moved = project.RenumberImages();

        if (moved.Count == 0) return;

        if (project.Panel.Root is { } root) Rename(root, moved);

        foreach (var row in Every()) row.Reread();

        Redraw();
    }

    private static void Rename(MachineElement element, IReadOnlyDictionary<string, string> moved)
    {
        if (element.Element == MachineElementKinds.Image &&
            element.Properties.TryGetValue("file", out var named) &&
            moved.TryGetValue(named, out var now))
        {
            element.Properties["file"] = now;
        }

        foreach (var child in element.Children) Rename(child, moved);
    }

    /// <summary>"the picture" or "3 pictures", for a line somebody reads once.</summary>
    private static string Counted(int pictures) =>
        pictures == 1 ? "its picture" : pictures + " pictures";

    /// <summary>Whether that element is somewhere inside this one, however deep.</summary>
    private static bool Inside(MachineElementViewModel holder, MachineElementViewModel wanted)
    {
        for (var at = wanted.Parent; at != null; at = at.Parent)
        {
            if (ReferenceEquals(at, holder)) return true;
        }

        return false;
    }

    /// <summary>
    /// Takes the size somebody dragged an element out to.
    /// </summary>
    /// <remarks>
    /// The panel has already written the width and the height onto the element and drawn it, so
    /// there is nothing here to apply. What is left is everything else that was showing the old
    /// size: the row in the inspector, and the fact that the project now has something to save.
    /// </remarks>
    public void Resized(MachineElement element)
    {
        if (Find(Elements, element) is { } wrapped) wrapped.Reread();

        Status = "Sized the " + element.Element + ".";
    }

    /// <summary>
    /// Puts something arriving in a grid in a cell of its own.
    /// </summary>
    /// <remarks>
    /// A grid puts what it holds where each thing says, and something that says nothing goes in
    /// the first cell. Two of those are drawn on top of each other, which looks like the panel
    /// is broken rather than like a question nobody has answered yet. So a part landing in a
    /// grid is given the next cell nothing is in, reading across the row and then down, which
    /// is the order somebody filling a grid is already thinking in.
    ///
    /// Only where it lands in a grid, and only when it has not been placed already: a part moved
    /// out of one grid and into another keeps nothing, but one being moved about inside the same
    /// grid is being put somewhere on purpose.
    ///
    /// The number of columns comes off the grid itself. A grid that has not said how wide it is
    /// gets one column, so parts stack downwards, which is at least a shape you can see.
    /// </remarks>
    private static MachineElement Celled(MachineElementViewModel into, MachineElement arriving)
    {
        if (into.Kind != MachineElementKinds.Grid) return arriving;

        int columns = Math.Max(1, Split(into.Element, "columns"));

        var taken = new HashSet<(int, int)>();

        foreach (var child in into.Element.Children)
        {
            if (ReferenceEquals(child, arriving)) continue;

            taken.Add((Whole(child, "row"), Whole(child, "column")));
        }

        for (int at = 0; at < 1024; at++)
        {
            var cell = (at / columns, at % columns);

            if (taken.Contains(cell)) continue;

            arriving.Properties["row"] = cell.Item1.ToString(CultureInfo.InvariantCulture);
            arriving.Properties["column"] = cell.Item2.ToString(CultureInfo.InvariantCulture);

            break;
        }

        return arriving;
    }

    /// <summary>How many things a comma separated property lists, or none when it says nothing.</summary>
    private static int Split(MachineElement element, string key) =>
        element.Properties.TryGetValue(key, out var said) && said.Length > 0
            ? said.Split(',').Length
            : 0;

    /// <summary>A whole number a property holds, or nothing at all when it holds something else.</summary>
    private static int Whole(MachineElement element, string key) =>
        element.Properties.TryGetValue(key, out var said) &&
        int.TryParse(said, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : 0;

    /// <summary>
    /// A new part of that kind, with the properties it is no use without already on it.
    /// </summary>
    /// <remarks>
    /// A part arrives with an empty property list, and for most kinds that is right: a knob
    /// knows its own size and a row spaces itself. A picture does not. It has no size of its
    /// own that means anything on a panel, so the two rows somebody will certainly want are put
    /// there to be typed over rather than added by hand from a blank list, which is how you find
    /// out that the property is called "width" only by reading somebody else's machine.
    ///
    /// Values, not placeholders: a picture 120 by 60 is a picture you can see and then adjust,
    /// and an empty width is a picture the panel has to guess about.
    /// </remarks>
    private static MachineElement Part(string kind)
    {
        var made = new MachineElement { Element = kind };

        if (kind == MachineElementKinds.Image)
        {
            made.Properties["width"] = "120";
            made.Properties["height"] = "60";
        }

        // A grid is the one container you have to say the shape of, and a grid that arrives
        // saying nothing is a grid that looks broken: everything dropped in it lands in the same
        // cell. Two by two to start with, which is the smallest shape that is plainly a grid.
        if (kind == MachineElementKinds.Grid)
        {
            made.Properties["rows"] = "Auto,Auto";
            made.Properties["columns"] = "Auto,Auto";
        }

        // A group is a frame with a name on it, and the name is the whole point of choosing one
        // over a plain row. Started with a word to type over rather than an empty frame.
        if (kind == MachineElementKinds.Group) made.Properties["caption"] = "Group";

        return made;
    }

    /// <summary>
    /// Takes the picked element out, and everything inside it with it.
    /// </summary>
    /// <remarks>
    /// The root stays, because a panel with no root is not a panel and there would be nothing
    /// left to add to. The selection moves to whatever held the element, so a run of removals
    /// works upwards instead of stopping dead.
    /// </remarks>
    public IRelayCommand RemoveElementCommand => new RelayCommand(() =>
    {
        var element = SelectedElement;

        if (element?.Parent is not { } parent)
        {
            if (element != null) Status = "The panel's outermost element cannot be removed.";
            return;
        }

        // What the element and everything inside it was showing, before it goes and there is
        // nothing left to ask.
        var pictures = Pictures(element.Element);

        if (!parent.Remove(element)) return;

        SelectedElement = parent;

        int dropped = Forget(pictures);

        // The numbers close up behind what went, so the folder reads image1, image2 with nothing
        // missing, and the panel is told what its pictures are called now.
        if (dropped > 0) Renumbered();

        Status = dropped == 0
            ? "Removed the " + element.Kind + "."
            : "Removed the " + element.Kind + ", and " + Counted(dropped) + " with it.";
    });

    /// <summary>Moves the picked element one place earlier among the things beside it.</summary>
    public IRelayCommand MoveUpCommand => new RelayCommand(() => Shift(-1));

    /// <summary>Moves the picked element one place later among the things beside it.</summary>
    public IRelayCommand MoveDownCommand => new RelayCommand(() => Shift(1));

    /// <summary>
    /// Both reordering commands, which differ only in which way they step.
    /// </summary>
    /// <remarks>
    /// The order of the children is the order they are drawn in, so this is the only positioning
    /// a row or a column has. Moving out of one container into another is a different act and is
    /// not this.
    /// </remarks>
    private void Shift(int by)
    {
        if (SelectedElement?.Parent is not { } parent) return;

        parent.Move(SelectedElement, by);
    }

    /// <summary>
    /// The panel as the canvas is handed it, made new every time the tree has been touched.
    /// </summary>
    /// <remarks>
    /// A fresh wrapper around the same root, on purpose. The canvas draws what it is given and
    /// draws again when it is given something else, and an element added to a tree it is already
    /// holding is not something else. What gets saved is the project's own panel, which this only
    /// points at.
    /// </remarks>
    /// <summary>
    /// The machine as the panel wants it: what it looks like, what it can be set to, and where
    /// it is kept, made fresh so that a change to any of the three is drawn.
    /// </summary>
    /// <remarks>
    /// A new object every time on purpose. The panel redraws when it is handed a different
    /// machine, and the project's own panel is edited in place: without this, moving a knob in
    /// the designer would change everything except the picture of it.
    /// </remarks>
    public MachineFace? Shown =>
        Project is { } project
            ? new MachineFace(new MachinePanel { Root = project.Panel.Root }, project.Parameters, project.Folder)
            : null;

    /// <summary>Just the keys, for choosing which parameter a control turns.</summary>
    public IReadOnlyList<string> ParameterKeys =>
        Project?.Parameters.Select(p => p.Key).Where(k => k.Length > 0).ToArray() ?? Array.Empty<string>();

    /// <summary>The parameters as the canvas reads them: a copy, so a change is a different list.</summary>
    public IReadOnlyList<MachineParameter> Shape =>
        Project?.Parameters.ToArray() ?? Array.Empty<MachineParameter>();

    /// <summary>
    /// What is picked, named the way the canvas names it.
    /// </summary>
    /// <remarks>
    /// The canvas knows elements and the editor knows the wrappers around them, and this is the
    /// one place the two are matched up, so a click on the panel and a click in the tree end on
    /// the same thing without either side knowing the other exists.
    /// </remarks>
    public MachineElement? SelectedShape
    {
        get => SelectedElement?.Element;
        set => SelectedElement = value == null ? null : Find(Elements, value);
    }

    /// <summary>True when there is something to remove, move or set up.</summary>
    public bool HasSelection => SelectedElement != null;

    /// <summary>The wrapper standing for that element, wherever it is in the tree.</summary>
    private static MachineElementViewModel? Find(
        IEnumerable<MachineElementViewModel> among, MachineElement wanted)
    {
        foreach (var one in among)
        {
            if (ReferenceEquals(one.Element, wanted)) return one;

            if (Find(one.Children, wanted) is { } found) return found;
        }

        return null;
    }

    /// <summary>
    /// Puts an element of that kind into whatever it was dropped on.
    /// </summary>
    /// <remarks>
    /// Dropped on something that holds nothing, a knob say, it goes beside it rather than
    /// inside it, since a knob has no inside. That is what somebody dropping onto a full row
    /// means, and refusing would leave the only place to drop a control the gaps between the
    /// ones already there.
    /// </remarks>
    public void Drop(string kind, MachineElement? onto, int at = -1)
    {
        if (Project == null || string.IsNullOrWhiteSpace(kind)) return;

        var target = onto == null ? Elements.FirstOrDefault() : Find(Elements, onto);

        if (target == null) return;

        if (!Holds(target.Kind)) target = target.Parent ?? Elements.FirstOrDefault();

        if (target == null) return;

        SelectedElement = target.Put(Celled(target, Part(kind)), at);

        Designing = true;

        Status = "Added " + kind + ".";
    }

    /// <summary>Whether a kind of element has an inside to put things in.</summary>
    private static bool Holds(string kind) =>
        kind is MachineElementKinds.Grid
             or MachineElementKinds.Group
             or MachineElementKinds.Row
             or MachineElementKinds.Column
             or MachineElementKinds.Strip;

    /// <summary>
    /// Tells whatever is drawing the panel that the description under it has moved.
    /// </summary>
    /// <remarks>
    /// The elements are plain data and say nothing when they are edited, which is the price of
    /// a machine describing itself without a view model toolkit. So the editor watches its own
    /// wrappers instead and says it here, once, however the edit was made.
    /// </remarks>
    public void Redraw()
    {
        OnPropertyChanged(nameof(Shown));
        OnPropertyChanged(nameof(Shape));

        // What is picked can turn into a picture without the selection moving, by being changed
        // from one kind of element into another, and the way of choosing a file has to come and
        // go with it.
        OnPropertyChanged(nameof(PicturePicked));
    }

    /// <summary>
    /// The same, for a change that also alters which parameters there are to choose from.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="Redraw"/> deliberately. A list of keys that is made afresh is
    /// a different list to anything showing it, and a picker rebuilt in the middle of somebody
    /// picking from it answers by picking again: putting a control on a parameter announced the
    /// keys, which rebuilt the picker, which put the control on a parameter, until the stack ran
    /// out. Only a parameter added, renamed or taken out changes the keys, so only those say so.
    /// </remarks>
    private void ParametersChanged()
    {
        Redraw();

        OnPropertyChanged(nameof(ParameterKeys));
    }

    partial void OnSelectedElementChanged(MachineElementViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedShape));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(PicturePicked));
    }

    /// <summary>What a picture element keeps the name of its file under.</summary>
    /// <remarks>
    /// Written out rather than built, so the one key the designer sets by itself can be found by
    /// anybody grepping for it. Every other property on every other element is typed by hand.
    /// </remarks>
    private const string FileKey = "file";

    /// <summary>
    /// True while what is picked is a picture, which is when there is a file to go and get.
    /// </summary>
    /// <remarks>
    /// A picture is an element like any other and is worked on like any other, in the list of
    /// properties beside the panel. The one thing that list cannot do is copy a file into the
    /// machine, so that, and only that, is offered separately, and only while the thing it would
    /// act on is what is picked.
    /// </remarks>
    public bool PicturePicked => SelectedElement?.Kind == MachineElementKinds.Image;

    /// <summary>
    /// Brings a picture into the machine and puts its name on the element that is picked.
    /// </summary>
    /// <remarks>
    /// Two things at once on purpose, because they are one act: a picture that had been copied
    /// in but not named would show nowhere, and a name typed for a file that is not in the
    /// folder is the broken picture this is here to avoid.
    ///
    /// A machine that has never been saved has no folder to keep anything in, and inventing one
    /// would be choosing where somebody's work lives on their behalf. So it says so instead.
    /// </remarks>
    public void PutPicture(string path)
    {
        if (SelectedElement is not { Kind: MachineElementKinds.Image } element) return;

        if (Project is not { IsSaved: true } project)
        {
            Status = "Save the machine somewhere first. Its pictures live in its own folder.";
            return;
        }

        try
        {
            if (project.AddImage(path) is not { Length: > 0 } named)
            {
                Status = "That picture could not be brought in.";
                return;
            }

            Put(element, FileKey, named);

            Status = "The picture is in the machine, as " + named + ".";
        }
        catch (Exception ex)
        {
            Status = "Could not add the picture: " + ex.Message;
        }
    }

    /// <summary>
    /// Writes a property on an element, adding the row for it when there is not one already.
    /// </summary>
    /// <remarks>
    /// Both halves, because the inspector shows the rows and the element holds the dictionary,
    /// and a value written into the dictionary alone is a value nobody can see or correct. The
    /// row that already exists is written through rather than replaced, so somebody who had put
    /// the key there themselves keeps the row they were typing in.
    /// </remarks>
    private static void Put(MachineElementViewModel element, string key, string value)
    {
        if (element.Properties.FirstOrDefault(row => row.Key == key) is { } already)
        {
            already.Value = value;
            return;
        }

        element.Element.Properties[key] = value;

        element.Properties.Add(new MachineElementPropertyViewModel(element.Element, key));
    }

    /// <summary>
    /// Listens to an element and everything under it, so any edit reaches the canvas.
    /// </summary>
    /// <remarks>
    /// The whole subtree at once, and anything added later as it arrives. Nothing is
    /// unsubscribed when a project is closed: the wrappers are thrown away whole, and an event
    /// holds the listener rather than the other way about, so the old tree keeps nothing alive.
    /// </remarks>
    private void Watch(MachineElementViewModel element)
    {
        element.PropertyChanged += Edited;
        element.Children.CollectionChanged += Grew;
        element.Properties.CollectionChanged += Grew;

        foreach (var property in element.Properties) property.PropertyChanged += Edited;

        foreach (var child in element.Children) Watch(child);
    }

    private void Grew(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var added in e.NewItems ?? (System.Collections.IList)Array.Empty<object>())
        {
            if (added is MachineElementViewModel element) Watch(element);
            else if (added is MachineElementPropertyViewModel property) property.PropertyChanged += Edited;
        }

        Redraw();
    }

    /// <summary>
    /// One field of an element or a parameter, changed.
    /// </summary>
    /// <remarks>
    /// A knob being turned in the preview is left out. It reaches here as a parameter's Value,
    /// and drawing the panel again in the middle of a drag would take the knob out from under
    /// the pointer.
    ///
    /// Which is a thing about parameters and not about the word Value. A property on an element
    /// is a key and a value like any other pair, and its value is what the panel is drawn from:
    /// left out by its name alone, a picture given a file, a group given a caption and a grid
    /// given its columns would all sit there unchanged until something else happened to redraw
    /// the panel.
    /// </remarks>
    private void Edited(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is MachineParameterViewModel)
        {
            if (e.PropertyName is nameof(MachineParameterViewModel.Value) or nameof(MachineParameterViewModel.On)) return;

            ParametersChanged();
            return;
        }

        Redraw();
    }

    partial void OnProjectChanged(MachineProject? value)
    {
        Parameters.Clear();
        Elements.Clear();
        SelectedElement = null;

        if (value == null) return;

        foreach (var parameter in value.Parameters)
        {
            var wrapped = new MachineParameterViewModel(parameter);

            wrapped.PropertyChanged += Edited;

            Parameters.Add(wrapped);
        }

        var root = new MachineElementViewModel(Root(value));

        Watch(root);

        Elements.Add(root);

        ParametersChanged();
    }

    /// <summary>
    /// The project's root element, made if the file did not have one.
    /// </summary>
    /// <remarks>
    /// A machine saved by something else, or by hand, can arrive with no panel at all or with a
    /// root that says nothing. There has to be something to hang the first element off, so a
    /// grid is put in and written back into the project, which means saving keeps it.
    /// </remarks>
    private static MachineElement Root(MachineProject project)
    {
        project.Panel ??= new MachinePanel();
        project.Panel.Root ??= new MachineElement { Element = MachineElementKinds.Grid };

        if (project.Panel.Root.Element.Length == 0) project.Panel.Root.Element = MachineElementKinds.Grid;

        return project.Panel.Root;
    }
}
