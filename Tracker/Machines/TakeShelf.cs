using JingleBox2.Machines;
using JingleBox2.Audio.Records;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JingleBox2.Machines.Interfaces;

namespace JingleBox2.Tracker.Machines;

/// <summary>
/// Your recordings, offered to a machine panel as the thing it starts from.
/// </summary>
/// <remarks>
/// The Recording machine has no presets and can have none: what it is, is the take on it, and
/// nobody ships somebody else's recordings. So the picker at the top of that panel offers the
/// shelf instead, which is the same control doing the same job with a different list behind it.
///
/// A shelf of takes is not five presets in a folder. It runs to hundreds, it is filed under
/// categories on the RECORD tab, and two arrows are no way to walk it, which is why this offers
/// the categories as well and narrows the list to one of them. The narrowing is here rather than
/// in the panel because it is a fact about recordings, and the panel knows nothing about
/// recordings beyond having been handed a list of names.
///
/// It does not itself put anything on the machine. What picking a take means is the caller's,
/// since the same shelf serves a panel being designed, where it fills in a preview, and an
/// instrument in a song, where it changes what the instrument plays.
/// </remarks>
public sealed class TakeShelf : IMachinePresets
{
    /// <summary>What the picker shows for a shelf with nothing hidden.</summary>
    /// <remarks>The same wording the takes list on RECORD uses, so the two read as one shelf.</remarks>
    public const string AllTakes = "All takes";

    /// <summary>And for the takes nobody has filed yet.</summary>
    public const string Uncategorized = "Uncategorized";

    /// <summary>The application's own list of recordings, held live rather than copied.</summary>
    private readonly ObservableCollection<Recording> _shelf;

    /// <summary>What picking one means, which is the caller's business and not this one's.</summary>
    private readonly Action<Recording> _picked;

    /// <summary>The takes that pass the filter, in the order <see cref="Names"/> lists them.</summary>
    /// <remarks>
    /// Kept beside the names because <see cref="Picked"/> arrives as a number into the narrowed
    /// list, and a number into a list that is not the whole shelf cannot be resolved without it.
    /// </remarks>
    private List<Recording> _shown = new();

    /// <summary>Which category is in force.</summary>
    private string _filter = AllTakes;

    /// <param name="shelf">
    /// The recordings the app has, live: the same collection RECORD fills, so a take made while
    /// a panel is open is on the picker without anything being rebuilt.
    /// </param>
    /// <param name="picked">What to do with the one that was chosen.</param>
    public TakeShelf(ObservableCollection<Recording> shelf, Action<Recording> picked)
    {
        _shelf = shelf;
        _picked = picked;

        _shelf.CollectionChanged += (_, _) => Restock();

        Restock();
    }

    /// <inheritdoc/>
    /// <remarks>Narrowed to whatever <see cref="Filter"/> says, and rebuilt when the shelf moves.</remarks>
    public IReadOnlyList<string> Names { get; private set; } = Array.Empty<string>();

    /// <inheritdoc/>
    /// <remarks>"Take", because these are recordings and not presets, and saying so is the
    /// whole of how somebody knows this machine has no presets to offer.</remarks>
    public string Caption => "Take";

    /// <inheritdoc/>
    /// <remarks>
    /// Nothing rather than the first: a machine that has just been opened is playing whatever it
    /// was playing, and pointing the picker at the top of the shelf would say it was playing that
    /// instead. Setting it is what puts a take on the machine, and setting it to what it already
    /// says does it again, since asking for the same take twice is a way of saying "that one".
    /// </remarks>
    public int Picked
    {
        get => -1;
        set
        {
            if (value < 0 || value >= _shown.Count) return;

            _picked(_shown[value]);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Everything, the unfiled, and then whatever categories are in use, in order.</remarks>
    public IReadOnlyList<string> Filters { get; private set; } = Array.Empty<string>();

    /// <inheritdoc/>
    /// <remarks>An empty string is read as everything, so a panel can clear it and get the shelf back.</remarks>
    public string Filter
    {
        get => _filter;
        set
        {
            if (value == _filter) return;

            _filter = value.Length > 0 ? value : AllTakes;

            Restock();
        }
    }

    /// <summary>
    /// Reads the shelf again, since a take may have arrived, gone, or been filed.
    /// </summary>
    /// <remarks>
    /// The last take out of a category takes the category with it, and the narrowing with it:
    /// left standing, the picker would be filtered to a category that no longer exists and would
    /// show nothing at all, with no way back except knowing to reset it.
    /// </remarks>
    private void Restock()
    {
        var found = _shelf
            .Select(take => take.Category)
            .Where(category => category.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(category => category, StringComparer.CurrentCultureIgnoreCase);

        Filters = new[] { AllTakes, Uncategorized }.Concat(found).ToList();

        if (!Filters.Contains(_filter)) _filter = AllTakes;

        _shown = _shelf.Where(Passes).ToList();

        Names = _shown.Select(take => take.Name).ToList();
    }

    /// <summary>Whether that take belongs on the picker at all.</summary>
    /// <remarks>
    /// A recording with no file is a row RECORD is still filling in, and offering it would put a
    /// take on a machine that plays nothing.
    /// </remarks>
    private bool Passes(Recording take) => take.FilePath.Length > 0 && InCategory(take);

    /// <summary>Whether that take is in the category in force.</summary>
    private bool InCategory(Recording take) => _filter switch
    {
        AllTakes => true,
        Uncategorized => take.Category.Length == 0,
        var category => string.Equals(take.Category, category, StringComparison.Ordinal),
    };
}
