using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Machines;
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
            Properties.Add(new MachineElementPropertyViewModel(element, pair.Key));
    }

    /// <summary>The element this stands for, which is the thing that gets written to the file.</summary>
    public MachineElement Element { get; }

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
    public void Put(MachineElementViewModel child, int at)
    {
        int place = at < 0 || at > Children.Count ? Children.Count : at;

        Children.Insert(place, child);
        Element.Children.Insert(place, child.Element);

        child.Parent = this;
    }

    /// <summary>Takes one out, and returns whether it was in there to begin with.</summary>
    public bool Remove(MachineElementViewModel child)
    {
        int at = Children.IndexOf(child);

        if (at < 0) return false;

        Children.RemoveAt(at);
        Element.Children.Remove(child.Element);

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
    /// </remarks>
    public bool Move(MachineElementViewModel child, int by)
    {
        int at = Children.IndexOf(child);

        if (at < 0) return false;

        int to = at + by;

        if (to < 0 || to >= Children.Count) return false;

        Children.Move(at, to);

        Element.Children.RemoveAt(at);
        Element.Children.Insert(to, child.Element);

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
    private readonly MachineElement element;
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
