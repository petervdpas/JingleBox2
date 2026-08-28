using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Machines;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JingleBox2.ViewModels;

/// <summary>
/// One thing on the panel, as the designer works on it.
/// </summary>
/// <remarks>
/// The element itself is plain data in the contract and knows nothing about being watched, the
/// same bargain the parameters are on: a machine written by somebody else describes its face
/// without taking a dependency on a view model toolkit. So the designer wraps it, and every edit
/// goes straight through to the element underneath, which is what the project saves.
///
/// It keeps a parent, which the element does not, because a tree that can only be walked
/// downwards cannot have anything taken out of it. Nothing is stored twice: the children here
/// are wrappers around the children there, and both lists are moved together.
/// </remarks>
public sealed partial class MachineElementViewModel : ObservableObject
{
    /// <summary>Wraps an element, and everything inside it, for the given parent.</summary>
    /// <remarks>
    /// The whole subtree is wrapped at once rather than when a branch is opened. A panel is a
    /// few dozen elements at most, and building it lazily would mean the parent of an element
    /// depending on whether anybody had looked at it yet.
    /// </remarks>
    public MachineElementViewModel(MachineElement element, MachineElementViewModel? parent = null)
    {
        Element = element;
        Parent = parent;

        foreach (var child in element.Children) Children.Add(new MachineElementViewModel(child, this));

        foreach (var pair in element.Properties)
        {
            if (Owned(element, pair.Key)) continue;

            Properties.Add(new MachineElementPropertyViewModel(element, pair.Key));
        }
    }

    /// <summary>What a grid is: so many down by so many across.</summary>
    /// <remarks>
    /// Written out rather than built, so the two properties the grid tool owns can be found by
    /// looking for them.
    /// </remarks>
    private const string RowsKey = "rows";

    /// <inheritdoc cref="RowsKey"/>
    private const string ColumnsKey = "columns";

    /// <summary>
    /// True when a property belongs to a tool of its own rather than to the list of rows.
    /// </summary>
    /// <remarks>
    /// A grid's shape is its rows and its columns, and changing either means writing the buttons
    /// again: six by sixteen is ninety six of them, each with a key of its own. A box you could
    /// type four into would say the grid was four wide without making it so, which is why the two
    /// are set where they are acted on.
    /// </remarks>
    private static bool Owned(MachineElement element, string key) =>
        (element.Element == MachineElementKinds.Pads && key is RowsKey or ColumnsKey)
        || (element.Element == MachineElementKinds.Preset && key == Tracker.Machines.MachineProject.SourceProperty);

    /// <summary>True when the picked thing is the picker a machine is started from.</summary>
    public bool IsPicker => Element.Element == MachineElementKinds.Preset;

    /// <summary>
    /// What the two browsers are called, in the order they are offered.
    /// </summary>
    /// <remarks>
    /// Two of them, and they are not one control with a setting: a machine's own presets are a
    /// handful shipped in its folder, and your recordings run to hundreds and are filed under
    /// categories, so one is a picker and the other is a picker with a category list in front of
    /// it. Which of the two this is has to be said somewhere, and the object is where it is true.
    /// </remarks>
    public IReadOnlyList<string> Sources { get; } = new[] { PresetsSaid, TakesSaid };

    /// <summary>What each is called on the page, against the word the file uses.</summary>
    /// <remarks>
    /// Written out both ways round rather than one turned into the other, so the words in the
    /// file can be found by searching for them and the words on the page can be changed without
    /// changing what any machine.json says.
    /// </remarks>
    private const string PresetsSaid = "The machine's own presets";

    /// <inheritdoc cref="PresetsSaid"/>
    private const string TakesSaid = "Your recordings";

    /// <summary>Which of the two this picker browses.</summary>
    public string Source
    {
        get => Element.Properties.TryGetValue(Tracker.Machines.MachineProject.SourceProperty, out string? said)
            && string.Equals(said.Trim(), MachineStarts.Takes, StringComparison.OrdinalIgnoreCase)
                ? TakesSaid
                : PresetsSaid;
        set
        {
            string want = value == TakesSaid ? MachineStarts.Takes : MachineStarts.Presets;

            if (Element.Properties.TryGetValue(Tracker.Machines.MachineProject.SourceProperty, out string? was)
                && was == want)
                return;

            Element.Properties[Tracker.Machines.MachineProject.SourceProperty] = want;

            OnPropertyChanged();
        }
    }

    /// <summary>The element this stands for, which is the thing that gets written to the file.</summary>
    public MachineElement Element { get; }

    /// <summary>
    /// Whether this branch of the list is open.
    /// </summary>
    /// <remarks>
    /// Two way, because it is set from both ends: by a hand on the chevron, and by picking
    /// something on the panel, which has to open every branch above it or the thing you just
    /// clicked is selected somewhere nobody can see.
    /// </remarks>
    [ObservableProperty] private bool isOpen;

    /// <summary>Opens every branch above this one, so it can be seen.</summary>
    public void Reveal()
    {
        for (var above = Parent; above != null; above = above.Parent) above.IsOpen = true;
    }

    /// <summary>
    /// What holds this one, or null for the root.
    /// </summary>
    /// <remarks>
    /// Set by whoever puts the element somewhere, and cleared by whoever takes it out. The root
    /// is the one element with nothing above it, which is how the editor knows it cannot be
    /// removed.
    /// </remarks>
    public MachineElementViewModel? Parent
    {
        get => parent;
        set
        {
            parent = value;

            OnPropertyChanged(nameof(Display));
        }
    }

    /// <inheritdoc cref="Parent"/>
    private MachineElementViewModel? parent;

    /// <summary>Which kind of thing this is, by the names in <see cref="MachineElementKinds"/>.</summary>
    /// <remarks>
    /// Settable, because turning a knob into a fader is a smaller edit than deleting one and
    /// adding the other, and it keeps the parameter and the position that were already right.
    /// </remarks>
    public string Kind
    {
        get => Element.Element;
        set
        {
            Element.Element = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(Display));
        }
    }

    /// <summary>The parameter this control turns, by key. Empty for anything that turns nothing.</summary>
    public string Parameter
    {
        get => Element.Parameter;
        set
        {
            Element.Parameter = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(Display));
        }
    }

    /// <summary>What is written on it. Empty leaves it to the parameter's own name.</summary>
    public string Label
    {
        get => Element.Label;
        set
        {
            Element.Label = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(Display));
        }
    }

    /// <summary>
    /// True while a part being dragged would land inside this one.
    /// </summary>
    /// <remarks>
    /// On the row rather than on the list, because the list draws its rows from these and has
    /// nowhere else to put it. Nothing is stored with the machine: it lasts as long as a hand
    /// is over the line.
    /// </remarks>
    [ObservableProperty] private bool isDropTarget;

    /// <summary>What is inside it, wrapped, and in the order the element holds them.</summary>
    public ObservableCollection<MachineElementViewModel> Children { get; } = new();

    /// <summary>
    /// Everything else about it, one row per property.
    /// </summary>
    /// <remarks>
    /// This is what the inspector shows. The properties are free text on both sides on purpose:
    /// which keys mean anything depends on the kind of element, and a designer that only offered
    /// the keys it knew would stop a machine using a control this version has not heard of.
    /// </remarks>
    public ObservableCollection<MachineElementPropertyViewModel> Properties { get; } = new();

    /// <summary>
    /// How the row reads in the tree.
    /// </summary>
    /// <remarks>
    /// The kind first, since that is what the element is, then whichever of the label or the
    /// parameter says something. A group is known by what it is called and a control by what it
    /// turns, so one line covers both without the tree needing a column for each.
    ///
    /// The outermost one is called the machine. It is a Column or a Grid like any other and the
    /// inspector still says so, but on the list it is the thing everything else is inside, and
    /// that is what somebody looking for where a part will land is looking for. Reading it as
    /// "Column" made the one row that is always a valid target the hardest one to find.
    /// </remarks>
    public string Display
    {
        get
        {
            if (Parent == null) return "machine (" + Kind + ")";

            if (Element.Label.Length > 0) return Kind + " \"" + Element.Label + "\"";

            if (Element.Parameter.Length > 0) return Kind + " → " + Element.Parameter;

            return Kind;
        }
    }

    /// <summary>
    /// Reads the properties off the element again, for a change that did not come from a row.
    /// </summary>
    /// <remarks>
    /// Sizing an element by dragging its handle writes width and height straight onto the
    /// element, which is right: the panel owns the drag and the description is what it is
    /// writing. The inspector is then a list of rows for the properties there used to be, so it
    /// is told to look again rather than being rebuilt, which would throw away whatever row
    /// somebody was in the middle of typing into.
    /// </remarks>
    public void Reread()
    {
        for (int at = Properties.Count - 1; at >= 0; at--)
        {
            if (!Element.Properties.ContainsKey(Properties[at].Key)) Properties.RemoveAt(at);
        }

        foreach (var pair in Element.Properties)
        {
            bool known = false;

            foreach (var row in Properties)
            {
                if (row.Key != pair.Key) continue;

                row.Refreshed();

                known = true;

                break;
            }

            if (!known) Properties.Add(new MachineElementPropertyViewModel(Element, pair.Key));
        }
    }

    /// <summary>Puts an element inside this one, at the end, and hands back the wrapper.</summary>
    /// <remarks>
    /// Both lists are added to here rather than at the call site, because the two falling out of
    /// step is the one way a designer can lose an edit: the tree would show it and the file
    /// would not have it.
    /// </remarks>
    public MachineElementViewModel Add(MachineElement child)
    {
        Element.Children.Add(child);

        var wrapped = new MachineElementViewModel(child, this);

        Children.Add(wrapped);

        return wrapped;
    }

    /// <summary>
    /// Puts an element inside this one at a given place, or at the end when there is none.
    /// </summary>
    /// <remarks>
    /// The place is where somebody let go, counted among what is already here. Out of range
    /// means the end rather than a refusal: a drop past the last thing is a drop after the last
    /// thing, and that is what the hand meant.
    /// </remarks>
    public MachineElementViewModel Put(MachineElement child, int at)
    {
        var wrapped = new MachineElementViewModel(child, this);

        Put(wrapped, at);

        return wrapped;
    }

    /// <summary>The same, for a wrapper that already exists, which is what moving one is.</summary>
    /// <remarks>
    /// The description is changed before the wrappers, and that order is the whole of it. The
    /// wrappers are what the editor is listening to, and it redraws the panel the instant they
    /// move. Moving them first means the panel is drawn again from a description that has not
    /// been touched yet, so what you see is what it was.
    /// </remarks>
    public void Put(MachineElementViewModel child, int at)
    {
        int place = at < 0 || at > Children.Count ? Children.Count : at;

        Element.Children.Insert(place, child.Element);
        Children.Insert(place, child);

        child.Parent = this;
    }

    /// <summary>Takes one out, and returns whether it was in there to begin with.</summary>
    /// <remarks>The description first, for the reason given on <see cref="Put(MachineElementViewModel, int)"/>.</remarks>
    public bool Remove(MachineElementViewModel child)
    {
        int at = Children.IndexOf(child);

        if (at < 0) return false;

        Element.Children.Remove(child.Element);
        Children.RemoveAt(at);

        child.Parent = null;

        return true;
    }

    /// <summary>
    /// Shifts a child up or down among its siblings.
    /// </summary>
    /// <remarks>
    /// Order is the only positioning a container has of its own, so this is how a row is put in
    /// the right sequence. A step that would run off either end does nothing rather than
    /// wrapping around, which is what a button held down expects.
    ///
    /// The description first, for the reason given on <see cref="Put(MachineElementViewModel, int)"/>.
    /// A move is where that mattered most, because nothing else happens afterwards to put it
    /// right: adding and removing both change what is picked, and the panel is drawn again for
    /// that instead. Flipping two things over leaves the same one picked, so the stale drawing
    /// stood until the machine was opened again.
    /// </remarks>
    public bool Move(MachineElementViewModel child, int by)
    {
        int at = Children.IndexOf(child);

        if (at < 0) return false;

        int to = at + by;

        if (to < 0 || to >= Children.Count) return false;

        Element.Children.RemoveAt(at);
        Element.Children.Insert(to, child.Element);

        Children.Move(at, to);

        return true;
    }

    /// <summary>Adds a property row with a name nobody is using yet, ready to be typed over.</summary>
    /// <remarks>
    /// It starts with a placeholder key rather than an empty one because a dictionary cannot
    /// hold two blanks, so two empty rows would be one row.
    /// </remarks>
    public IRelayCommand AddPropertyCommand => new RelayCommand(() =>
    {
        string key = "property";

        for (int at = 2; Element.Properties.ContainsKey(key); at++) key = "property" + at;

        Element.Properties[key] = "";

        Properties.Add(new MachineElementPropertyViewModel(Element, key));
    });

    /// <summary>Takes a property row out, and the property with it.</summary>
    public IRelayCommand<MachineElementPropertyViewModel> RemovePropertyCommand =>
        new RelayCommand<MachineElementPropertyViewModel>(property =>
        {
            if (property == null) return;

            Element.Properties.Remove(property.Key);
            Properties.Remove(property);
        });
}

/// <summary>
/// One entry in an element's properties, as a row that can be typed into.
/// </summary>
/// <remarks>
/// A dictionary is the right shape for the file and the wrong shape for a list of edit boxes:
/// there is nothing to bind a text box to, and renaming an entry is not an edit a dictionary
/// has. So the row remembers which key it is on and does the removing and adding itself when
/// the key is changed, and the dictionary underneath stays the thing that is saved.
/// </remarks>
public sealed partial class MachineElementPropertyViewModel : ObservableObject
{
    /// <summary>The element whose properties this row is one of, edited in place.</summary>
    private readonly MachineElement element;

    /// <summary>
    /// Which entry the row is on.
    /// </summary>
    /// <remarks>
    /// Kept here rather than read off the dictionary, because a dictionary entry has no identity:
    /// renaming one is removing and adding, and a row that looked its key up would lose track of
    /// itself half way through.
    /// </remarks>
    private string key;

    /// <summary>Takes the row on to an element and the key it is showing.</summary>
    public MachineElementPropertyViewModel(MachineElement element, string key)
    {
        this.element = element;
        this.key = key;
    }

    /// <summary>
    /// The name of the property.
    /// </summary>
    /// <remarks>
    /// Changing it moves the value to the new name and drops the old one, which is what renaming
    /// means to whoever is typing. A name already in use is refused rather than quietly
    /// overwriting the other row, since one of the two rows would then be showing a property
    /// that is not there.
    /// </remarks>
    public string Key
    {
        get => key;
        set
        {
            if (value == key) return;

            if (string.IsNullOrWhiteSpace(value) || element.Properties.ContainsKey(value))
            {
                OnPropertyChanged();
                return;
            }

            string was = element.Properties.TryGetValue(key, out string? held) ? held : "";

            element.Properties.Remove(key);
            element.Properties[value] = was;

            key = value;

            OnPropertyChanged();
        }
    }

    /// <summary>Says the value again, for one that was changed behind the row's back.</summary>
    public void Refreshed() => OnPropertyChanged(nameof(Value));

    /// <summary>What the property is set to, as text, which is how the panel stores all of them.</summary>
    public string Value
    {
        get => element.Properties.TryGetValue(key, out string? held) ? held : "";
        set
        {
            element.Properties[key] = value;

            OnPropertyChanged();
        }
    }
}
