using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Audio.Records;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace JingleBox2.ViewModels;

/// <summary>
/// The shelf of takes, narrowed to one category.
/// </summary>
/// <remarks>
/// One of these sits in front of every picker that offers a recording: the list on RECORD, the
/// take a zone plays, the take a pad plays, and the takes the Recording machine starts from.
/// They all look at the same shelf and each narrows it on its own, so filing on RECORD and
/// hunting on a machine are the same categories seen from two rooms.
///
/// The categories are not kept here, or anywhere: they are read off the takes, so a category
/// exists exactly while a take is filed under it. Each take is watched, since one filed
/// somewhere else has to reach every list that could be showing it.
/// </remarks>
public sealed partial class TakeFilter : ObservableObject
{
    /// <summary>What the picker shows for a shelf with nothing hidden.</summary>
    public const string AllTakes = "All takes";

    /// <summary>And for the takes nobody has put in a category yet.</summary>
    public const string Uncategorized = "Uncategorized";

    /// <summary>
    /// The shelf itself, held rather than copied.
    /// </summary>
    /// <remarks>
    /// Every filter over a shelf watches the same collection, so a take recorded or deleted on
    /// RECORD reaches the pickers on the machines without anybody telling them.
    /// </remarks>
    private readonly ObservableCollection<Recording> _shelf;

    /// <summary>
    /// Puts a filter in front of that shelf, showing everything on it to begin with.
    /// </summary>
    /// <remarks>
    /// The shelf is watched from here on, and so is every take on it, because a category is a
    /// fact about a take rather than a list kept anywhere: filing one somewhere else has to
    /// reach every filter that could be showing it.
    /// </remarks>
    public TakeFilter(ObservableCollection<Recording> shelf)
    {
        _shelf = shelf;

        _shelf.CollectionChanged += OnShelfChanged;

        foreach (var take in _shelf) Watch(take);

        Sort();
    }

    /// <summary>The takes this picker is showing.</summary>
    public ObservableCollection<Recording> Shown { get; } = new();

    /// <summary>The categories in use, in alphabetical order.</summary>
    public ObservableCollection<string> Categories { get; } = new();

    /// <summary>What the list can be narrowed to: everything, the uncategorized, or one category.</summary>
    public ObservableCollection<string> Filters { get; } = new();

    /// <summary>Which of those is picked, and so what the list is narrowed to.</summary>
    /// <remarks>
    /// It falls back to <see cref="AllTakes"/> when what it names is gone, since the last take
    /// out of a category takes the category with it and a filter naming nothing would show an
    /// empty shelf with no way to say why.
    /// </remarks>
    [ObservableProperty] private string filter = AllTakes;

    /// <summary>Part of a name to look for, or empty to look for nothing in particular.</summary>
    /// <remarks>
    /// The category answers "which of the beds"; this answers "the one with saxophone in the
    /// name", which is the other half of finding a take on a shelf of a hundred. They narrow
    /// together: a search inside a category searches that category.
    /// </remarks>
    [ObservableProperty] private string search = "";

    /// <summary>How many takes are being shown out of how many there are, while some are hidden.</summary>
    public string Showing => Shown.Count == _shelf.Count ? "" : $"{Shown.Count} of {_shelf.Count}";

    /// <summary>The categories, the filters and the list, after any of them could have changed.</summary>
    public void Sort()
    {
        RefreshCategories();
        Restock();
    }

    /// <summary>Picking a different category shows a different set of takes and nothing else.</summary>
    partial void OnFilterChanged(string value) => Restock();

    /// <summary>Typing in the search box narrows what is already shown, a letter at a time.</summary>
    partial void OnSearchChanged(string value) => Restock();

    /// <summary>
    /// A take arrived on the shelf or left it.
    /// </summary>
    /// <remarks>
    /// Every take is watched again rather than only the ones the event names, because the event
    /// says nothing on a reset and a take watched twice costs nothing: <see cref="Watch"/>
    /// unsubscribes before it subscribes.
    /// </remarks>
    private void OnShelfChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var take in _shelf) Watch(take);

        Sort();
    }

    /// <summary>Listens to one take, having first stopped listening, so it is heard once.</summary>
    private void Watch(Recording take)
    {
        take.PropertyChanged -= OnTakeChanged;
        take.PropertyChanged += OnTakeChanged;
    }

    /// <summary>
    /// A take was filed somewhere else, which can make a category or take the last one away.
    /// </summary>
    /// <remarks>
    /// Only the category is worth listening for. A take renamed or replayed changes nothing
    /// about which list it belongs in, and sorting on every property a take has would rebuild
    /// the list while somebody was reading it.
    /// </remarks>
    private void OnTakeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Recording.Category)) Sort();
    }

    /// <summary>
    /// Reads the categories off the takes and offers them, since nothing else holds the list.
    /// </summary>
    /// <remarks>
    /// The last take out of a category takes the category with it, and would take the list with
    /// it as well, so a filter naming one that has gone falls back to <see cref="AllTakes"/>.
    /// </remarks>
    private void RefreshCategories()
    {
        var found = _shelf
            .Select(r => r.Category)
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Sync(Categories, found);
        Sync(Filters, new[] { AllTakes, Uncategorized }.Concat(found).ToList());

        if (!Filters.Contains(Filter)) Filter = AllTakes;
    }

    /// <summary>
    /// Puts the takes the filter allows in the list, and only those.
    /// </summary>
    /// <remarks>
    /// A take that is in both lists is left where it is rather than being taken out and put
    /// back, so the one you are looking at stays picked and its picture stays on the page.
    /// </remarks>
    private void Restock()
    {
        var wanted = _shelf.Where(Passes).ToList();

        for (int i = Shown.Count - 1; i >= 0; i--)
            if (!wanted.Contains(Shown[i])) Shown.RemoveAt(i);

        for (int i = 0; i < wanted.Count; i++)
            if (i >= Shown.Count || !ReferenceEquals(Shown[i], wanted[i])) Shown.Insert(i, wanted[i]);

        OnPropertyChanged(nameof(Showing));
    }

    /// <summary>Whether a take survives both halves of the narrowing at once.</summary>
    private bool Passes(Recording take) => InCategory(take) && Named(take);

    /// <summary>Whether a take is in the category the picker names.</summary>
    /// <remarks>
    /// Compared with <see cref="StringComparison.Ordinal"/>, because a category is what somebody
    /// typed and two spellings that differ in case are two categories, however alike they read.
    /// </remarks>
    private bool InCategory(Recording take) => Filter switch
    {
        Uncategorized => take.Category.Length == 0,
        AllTakes => true,
        var category => string.Equals(take.Category, category, StringComparison.Ordinal)
    };

    /// <summary>Whether a take's name carries what is being looked for.</summary>
    /// <remarks>
    /// Case is ignored here and the culture's rules are used, unlike the category test: this is
    /// somebody hunting for a word they half remember rather than naming a thing exactly.
    /// </remarks>
    private bool Named(Recording take) =>
        Search.Length == 0 ||
        take.Name.Contains(Search, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>Brings a list of strings up to date without rebuilding it under a picker.</summary>
    private static void Sync(ObservableCollection<string> list, IReadOnlyList<string> wanted)
    {
        for (int i = list.Count - 1; i >= 0; i--)
            if (!wanted.Contains(list[i])) list.RemoveAt(i);

        for (int i = 0; i < wanted.Count; i++)
            if (i >= list.Count || !string.Equals(list[i], wanted[i], StringComparison.Ordinal))
                list.Insert(i, wanted[i]);
    }
}
