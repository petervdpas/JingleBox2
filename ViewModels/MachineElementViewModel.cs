using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Rack.Faces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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
    /// <param name="element">The element to wrap.</param>
    /// <param name="parent">What holds it, or nothing for the root.</param>
    /// <param name="edited">
    /// Told whenever anything here writes into the element. Handed down to every child, so one
    /// hook at the root hears the whole panel.
    /// </param>
    public MachineElementViewModel(
        PanelElement element,
        MachineElementViewModel? parent = null,
        Action? edited = null)
    {
        Element = element;
        Parent = parent;
        _edited = edited ?? parent?._edited;

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
    private static bool Owned(PanelElement element, string key) =>
        (element.Element == ElementKinds.Pads && key is RowsKey or ColumnsKey)
        || (element.Element == ElementKinds.Preset && key == Tracker.Machines.MachineProject.SourceProperty)
        || (element.Element == ElementKinds.Menu
            && key is MenuOptionWords.Property or MenuCorners.Property);

    /// <summary>True when the picked thing is the picker a machine is started from.</summary>
    public bool IsPicker => Element.Element == ElementKinds.Preset;

    /// <summary>True when the picked thing is a menu, which carries options rather than a value.</summary>
    public bool IsMenu => Element.Element == ElementKinds.Menu;

    /// <summary>
    /// Which corner a menu sits in, in the words on the page.
    /// </summary>
    /// <remarks>
    /// A choice and not a property to be typed, because there are two of them and both are
    /// spelled in a way nobody would guess. Where the part is dropped in the tree makes no
    /// difference: a menu is drawn over the panel rather than in it, so its corner is the whole
    /// of where it is.
    /// </remarks>
    public IReadOnlyList<string> Corners { get; } = new[] { TopRightSaid, TopLeftSaid };

    /// <summary>What each corner is called on the page, against the word the file uses.</summary>
    /// <remarks>
    /// Written out both ways round rather than one turned into the other, so the words in the
    /// file can be found by searching for them and the words on the page can be changed without
    /// changing what any machine.json says.
    /// </remarks>
    private const string TopRightSaid = "Upper right";

    /// <inheritdoc cref="TopRightSaid"/>
    private const string TopLeftSaid = "Upper left";


    /// <summary>Which corner this menu sits in.</summary>
    public string Corner
    {
        get => Element.Properties.TryGetValue(MenuCorners.Property, out string? said)
            && Words.FirstOrDefault(one =>
                string.Equals(one.Said, said.Trim(), StringComparison.OrdinalIgnoreCase)) is { Page: { } named }
                ? named
                : TopRightSaid;
        set
        {
            string want = Words.FirstOrDefault(one => one.Page == value).Said ?? MenuCorners.TopRight;

            if (want == Words[0].Said && !Element.Properties.ContainsKey(MenuCorners.Property)) return;

            if (Element.Properties.TryGetValue(MenuCorners.Property, out string? was) && was == want)
                return;

            Element.Properties[MenuCorners.Property] = want;

            OnPropertyChanged();

            Wrote();
        }
    }

    /// <summary>
    /// Each corner in the word the file uses against the words on the page.
    /// </summary>
    /// <remarks>
    /// Written out both ways round rather than one turned into the other, so what a machine's
    /// file says can be found by searching for it and what the designer calls it can be reworded
    /// without changing any machine.json.
    /// </remarks>
    private static readonly (string Said, string Page)[] Words =
    {
        (MenuCorners.TopRight, TopRightSaid),
        (MenuCorners.TopLeft, TopLeftSaid)
    };

    /// <summary>
    /// The options a menu can carry, each with a tick saying whether this one does.
    /// </summary>
    /// <remarks>
    /// Built from <see cref="MenuOptionWords.All"/> rather than written out here, so an option
    /// added later turns up in the designer without anybody being told: that is the whole point
    /// of the part being a menu rather than a part named after what is in it today.
    ///
    /// Made once and kept, because the ticks are what the page is bound to and rebuilding the
    /// list would take the bindings with it.
    /// </remarks>
    public IReadOnlyList<MachineMenuOptionViewModel> Options => _options ??= Ticks();

    /// <summary>Says a tick wrote into the element, which is the same edit as any other here.</summary>
    internal void Ticked() => Wrote();

    /// <summary>Behind <see cref="Options"/>.</summary>
    private IReadOnlyList<MachineMenuOptionViewModel>? _options;

    /// <summary>One tick per option there is.</summary>
    private IReadOnlyList<MachineMenuOptionViewModel> Ticks()
    {
        var made = new List<MachineMenuOptionViewModel>();

        foreach (string option in MenuOptionWords.All)
            made.Add(new MachineMenuOptionViewModel(Element, option, Ticked));

        return made;
    }

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
            && string.Equals(said.Trim(), PanelStarts.Takes, StringComparison.OrdinalIgnoreCase)
                ? TakesSaid
                : PresetsSaid;
        set
        {
            string want = value == TakesSaid ? PanelStarts.Takes : PanelStarts.Presets;

            if (Element.Properties.TryGetValue(Tracker.Machines.MachineProject.SourceProperty, out string? was)
                && was == want)
                return;

            Element.Properties[Tracker.Machines.MachineProject.SourceProperty] = want;

            OnPropertyChanged();

            Wrote();
        }
    }

    /// <summary>The element this stands for, which is the thing that gets written to the file.</summary>
    public PanelElement Element { get; }

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

    /// <summary>
    /// Told whenever anything here writes into the element underneath.
    /// </summary>
    /// <remarks>
    /// The elements are plain data and say nothing when they are edited, so an edit made through
    /// one of these reaches the machine and nothing else: the history's idea of what is on screen
    /// stays where it was, and the difference between that and what a save writes is permanent.
    /// From the outside that is a Save button that goes green and never goes back.
    ///
    /// So every setter here that writes into the element ends at <see cref="Wrote"/>, rather than
    /// each one being remembered about at its own call site. The one that was forgotten is the
    /// one that breaks saving, and it will not be the same one twice.
    /// </remarks>
    private readonly Action? _edited;

    /// <summary>Says the element underneath has been written into.</summary>
    private void Wrote() => _edited?.Invoke();

    /// <summary>Which kind of thing this is, by the names in <see cref="ElementKinds"/>.</summary>
    /// <remarks>
    /// Settable, because turning a knob into a fader is a smaller edit than deleting one and
    /// adding the other, and it keeps the parameter and the position that were already right.
    ///
    /// Turning something into a menu is refused where the machine already has one, the same rule
    /// adding one keeps and for the same reason: there is one to a machine. Refused by leaving
    /// the kind where it was and saying so, since the picker would otherwise show a kind the
    /// element is not.
    /// </remarks>
    public string Kind
    {
        get => Element.Element;
        set
        {
            if (value == ElementKinds.Menu && Element.Element != ElementKinds.Menu && Elsewhere())
            {
                OnPropertyChanged();

                return;
            }

            Element.Element = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(Display));

            Wrote();
        }
    }

    /// <summary>Whether a menu already exists somewhere on this machine other than here.</summary>
    /// <remarks>
    /// Asked from the top of the tree, which is what the parent chain is for: an element knows
    /// its parent, so the root can be reached from anywhere in the panel and the whole of it
    /// walked from there.
    /// </remarks>
    private bool Elsewhere()
    {
        var top = this;

        while (top.Parent is { } above) top = above;

        return Under(top).Any(one => !ReferenceEquals(one, this) && one.Kind == ElementKinds.Menu);
    }

    /// <summary>That element and everything under it, depth first.</summary>
    /// <param name="element">Where to start.</param>
    private static IEnumerable<MachineElementViewModel> Under(MachineElementViewModel element)
    {
        yield return element;

        foreach (var child in element.Children)
            foreach (var one in Under(child))
                yield return one;
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

            Wrote();
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

            Wrote();
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
    public MachineElementViewModel Add(PanelElement child)
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
    public MachineElementViewModel Put(PanelElement child, int at)
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
    private readonly PanelElement element;

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
    public MachineElementPropertyViewModel(PanelElement element, string key)
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

/// <summary>
/// One option a menu may carry, as a tick in the designer.
/// </summary>
/// <remarks>
/// The words on the page are here and the words in the file are in
/// <see cref="MenuOptionWords"/>, written out both ways round rather than one turned into the
/// other: what a machine's file says can be found by searching for it, and what the designer
/// calls it can be reworded without changing any machine.json.
///
/// A menu that names no options carries all of them, which is what one dropped on a panel and
/// left alone should do. So every tick starts on, and the property is only written once somebody
/// has taken one off.
/// </remarks>
public sealed partial class MachineMenuOptionViewModel : ObservableObject
{
    /// <summary>The element this is a tick on, edited in place.</summary>
    private readonly PanelElement _element;

    /// <summary>Which option, in the word the file uses.</summary>
    private readonly string _option;

    /// <summary>One option of one menu.</summary>
    /// <param name="element">The menu being worked on.</param>
    /// <param name="option">Which option, in the word the file uses.</param>
    /// <param name="edited">Told when this writes into the element, or nothing.</param>
    public MachineMenuOptionViewModel(PanelElement element, string option, Action? edited = null)
    {
        _element = element;
        _option = option;
        _edited = edited;
    }

    /// <summary>Told when this writes into the element, so the machine counts as changed.</summary>
    private readonly Action? _edited;

    /// <summary>What this option is called on the page.</summary>
    /// <remarks>
    /// A word out of a file that nothing here has a name for still gets a line, spelled as the
    /// file spells it, since a machine naming an option this build has never heard of should be
    /// visible rather than silently dropped from the designer.
    /// </remarks>
    public string Said => Words.TryGetValue(_option, out string? said) ? said : _option;

    /// <summary>What each option is called on the page, against the word the file uses.</summary>
    private static readonly Dictionary<string, string> Words = new(StringComparer.Ordinal)
    {
        [MenuOptionWords.Surfaces] = "The control surfaces pointed at this",
        [MenuOptionWords.Learn] = "Learn a control"
    };

    /// <summary>Whether this menu carries that option.</summary>
    /// <remarks>
    /// Taking the last one off leaves a menu that drops down nothing, which is allowed and is
    /// what somebody laying out a panel around a part they have not decided about wants. It is
    /// not a state the part hides in: the button is still there and still says so by opening
    /// nothing.
    /// </remarks>
    public bool On
    {
        get => Carried().Contains(_option, StringComparer.OrdinalIgnoreCase);
        set
        {
            var carried = Carried();

            if (value == carried.Contains(_option, StringComparer.OrdinalIgnoreCase)) return;

            if (value) carried.Add(_option);
            else carried.RemoveAll(one => string.Equals(one, _option, StringComparison.OrdinalIgnoreCase));

            _element.Properties[MenuOptionWords.Property] =
                string.Join(MenuOptionWords.Between, carried);

            OnPropertyChanged();

            _edited?.Invoke();
        }
    }

    /// <summary>Which options this menu says it carries, which is all of them where it says none.</summary>
    private List<string> Carried() =>
        _element.Properties.TryGetValue(MenuOptionWords.Property, out string? said)
            ? said.Split(MenuOptionWords.Between, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : MenuOptionWords.All.ToList();
}
