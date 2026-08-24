using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Machines;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Machines;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.IO;

namespace JingleBox2.ViewModels;

/// <summary>
/// The machine editor: one project open at a time, and what can be done with it.
/// </summary>
/// <remarks>
/// The other half of the rack. The rack holds machines that are registered and ready for a song
/// to take an instrument off; this is where one is made in the first place. New, open, save, and
/// install it so the rack has it.
///
/// Nothing about a song here, and nothing about instruments: a machine is a box, and it becomes
/// an instrument only when a song takes it.
/// </remarks>
public sealed partial class MachineEditorViewModel : ObservableObject
{
    /// <summary>The project being worked on, or null when nothing is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject))]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    [NotifyPropertyChangedFor(nameof(Title))]
    private MachineProject? project;

    public MachineEditorViewModel() => Values = new MachinePreviewValues(Parameters);

    public bool HasProject => Project != null;

    /// <summary>What the editor says it is showing.</summary>
    public string Title => Project == null
        ? "No machine open"
        : Project.Name.Length > 0 ? Project.Name : "Untitled machine";

    [ObservableProperty] private string status = "";

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
            Project.Save(folder);
            Status = "Saved to " + Project.Folder;

            OnPropertyChanged(nameof(Title));
        }
        catch (Exception ex)
        {
            Status = "Could not save: " + ex.Message;
        }
    }

    /// <summary>True when saving needs somewhere to save to.</summary>
    public bool NeedsFolder => Project is { IsSaved: false };

    /// <summary>True once there is something on disc worth installing.</summary>
    public bool CanInstall => Project is { IsSaved: true };

    /// <summary>
    /// Puts the machine where the app looks for machines.
    /// </summary>
    /// <remarks>
    /// A copy, not a move: the project stays where you keep your work, and what is installed is
    /// a copy the app owns, the same way a machine that arrives in a pack would be. Installing
    /// over a machine that is already there replaces it, since two machines with the same id are
    /// the same machine and the later one wins.
    ///
    /// What it cannot do yet is give a machine an engine. One built on an engine we already have
    /// plays as soon as it is installed; one that brings its own is copied in and says so,
    /// because loading an engine is the piece of the contract that is still missing.
    /// </remarks>
    public void Install()
    {
        if (Project is not { IsSaved: true } project) return;

        try
        {
            string into = Path.Combine(MachineRegistry.Installed, Safe(project.Name, project.Id));

            Copy(project.Folder, into);

            bool known = Machine.Register(project.Id, project.Name, project.Summary, project.Theme);

            Status = known
                ? "Installed '" + project.Name + "'. It is on the rack now."
                : "Installed '" + project.Name + "' into " + into +
                  ". Nothing plays it yet: a machine that brings its own engine needs one loading, which is not built.";
        }
        catch (Exception ex)
        {
            Status = "Could not install: " + ex.Message;
        }
    }

    /// <summary>A folder name that will not fight the file system, from the machine's own name.</summary>
    private static string Safe(string name, string fallback)
    {
        string wanted = (name ?? "").Trim();

        foreach (char bad in Path.GetInvalidFileNameChars()) wanted = wanted.Replace(bad, ' ');

        wanted = wanted.Trim();

        return wanted.Length > 0 ? wanted : fallback;
    }

    /// <summary>Copies a project folder whole: the manifest, the sounds, everything in it.</summary>
    private static void Copy(string from, string into)
    {
        Directory.CreateDirectory(into);

        foreach (string file in Directory.GetFiles(from))
            File.Copy(file, Path.Combine(into, Path.GetFileName(file)), overwrite: true);

        foreach (string folder in Directory.GetDirectories(from))
            Copy(folder, Path.Combine(into, Path.GetFileName(folder)));
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
    /// True while the panel is being arranged rather than played with.
    /// </summary>
    /// <remarks>
    /// Designing, a click picks the control up to be worked on; off, the same click turns it.
    /// A designer needs both, because a panel you cannot try is a drawing and a panel you
    /// cannot pick apart is finished.
    /// </remarks>
    [ObservableProperty] private bool designing = true;

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

        SelectedElement = into.Add(new MachineElement { Element = kind! });

        // Adding a part is laying the panel out, so the panel goes back to being laid out.
        // Otherwise what has just been added cannot be picked, and the outline that says which
        // one it is never appears.
        Designing = true;

        Status = "Added a " + kind + ".";
    });

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

        if (!parent.Remove(element)) return;

        SelectedElement = parent;

        Status = "Removed the " + element.Kind + ".";
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
    public MachinePanel? Shown =>
        Project?.Panel is { } panel ? new MachinePanel { Root = panel.Root } : null;

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
    public void Drop(string kind, MachineElement? onto)
    {
        if (Project == null || string.IsNullOrWhiteSpace(kind)) return;

        var target = onto == null ? Elements.FirstOrDefault() : Find(Elements, onto);

        if (target == null) return;

        if (!Holds(target.Kind)) target = target.Parent ?? Elements.FirstOrDefault();

        if (target == null) return;

        SelectedElement = target.Add(new MachineElement { Element = kind });

        Designing = true;

        Status = "Added a " + kind + ".";
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
    /// </remarks>
    private void Edited(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MachineParameterViewModel.Value) or nameof(MachineParameterViewModel.On)) return;

        if (sender is MachineParameterViewModel) ParametersChanged();
        else Redraw();
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
