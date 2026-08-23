using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Tracker;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

namespace JingleBox2.ViewModels;

/// <summary>
/// The lamps on an instrument's panel that say where the track playing it has got to.
/// </summary>
/// <remarks>
/// Eight lamps and a button per page of eight rows, which is the Mother-32's LOCATION row
/// almost exactly. It reports and does not sequence: the pattern is the tracker's, and this
/// only watches it. The one thing the buttons do is choose which eight rows the lamps are
/// showing, and pressing the page already shown hands that choice back to the playhead.
///
/// Rows are numbered from zero here because the pattern grid numbers them from zero, and a
/// panel that disagreed with the grid beside it would be worse than no panel.
/// </remarks>
public sealed partial class TrackLocationViewModel : ObservableObject, IDisposable
{
    /// <summary>How many rows one page of lamps covers.</summary>
    public const int PageLines = 8;

    private readonly ITrackerPanel _tracker;

    /// <summary>True while the shown page chases the playhead rather than being held.</summary>
    private bool _following = true;

    /// <summary>
    /// The lamps for a track, or an idle set of them when there is no track behind the panel.
    /// </summary>
    /// <remarks>
    /// The rack page has no tracker, but it still shows this row, greyed. A control that
    /// has nothing to report is dimmed rather than removed, so the panel is the same panel
    /// wherever it is opened and you learn where things are once.
    /// </remarks>
    public TrackLocationViewModel(ITrackerPanel? tracker)
    {
        _tracker = tracker ?? new Still();

        _tracker.PropertyChanged += OnTrackerChanged;

        IsLive = tracker != null;

        Update();
    }

    /// <summary>True when there is a tracker behind this, so the panel knows to grey it.</summary>
    public bool IsLive { get; private set; }

    /// <summary>A tracker that is not there: a pattern of the usual length, never playing.</summary>
    private sealed class Still : ITrackerPanel
    {
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public int PlayingLine => -1;

        public int PatternLines => Tracker.Pattern.DefaultLines;

        public int Octave { get; set; } = 4;

        public void FollowOctave(int octave) => Octave = octave;

        public event EventHandler<(int Track, Note Note, double Seconds)>? NotePlayed
        {
            add { }
            remove { }
        }
    }

    /// <summary>One button per page of eight rows.</summary>
    public ObservableCollection<LocationPage> Pages { get; } = new();

    /// <summary>Which page of eight rows the lamps are showing.</summary>
    [ObservableProperty] private int page;

    /// <summary>Which lamp is lit, counted within the shown page. Nothing is lit at -1.</summary>
    [ObservableProperty] private int lamp = -1;

    /// <summary>The row number written under the first lamp.</summary>
    [ObservableProperty] private int firstNumber;

    /// <summary>True while the page is chasing the playhead, for the lamp that says so.</summary>
    [ObservableProperty] private bool isFollowing = true;

    public void Dispose() => _tracker.PropertyChanged -= OnTrackerChanged;

    private void OnTrackerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ITrackerPanel.PlayingLine)
            or nameof(ITrackerPanel.PatternLines)
            or null)
        {
            Update();
        }
    }

    /// <summary>Picking a page holds it there; picking the one already shown lets go again.</summary>
    private void Pick(int index)
    {
        if (index == Page && !_following)
        {
            _following = true;
        }
        else
        {
            _following = false;
            Page = index;
        }

        Update();
    }

    private void Update()
    {
        EnsurePages();

        int line = _tracker.PlayingLine;

        if (_following && line >= 0) Page = line / PageLines;

        FirstNumber = Page * PageLines;

        Lamp = line >= 0 && line / PageLines == Page ? line % PageLines : -1;

        IsFollowing = _following;

        foreach (var page in Pages) page.IsShown = page.Index == Page;
    }

    /// <summary>Rebuilds the page buttons, but only when the pattern has changed length.</summary>
    private void EnsurePages()
    {
        int lines = Math.Max(1, _tracker.PatternLines);
        int wanted = (lines + PageLines - 1) / PageLines;

        if (Pages.Count == wanted) return;

        Pages.Clear();

        for (int i = 0; i < wanted; i++)
        {
            int index = i;
            Pages.Add(new LocationPage(index, lines, () => Pick(index)));
        }

        if (Page >= wanted) Page = 0;
    }
}

/// <summary>One page button: eight rows of the pattern, and whether it is the page on show.</summary>
public sealed partial class LocationPage : ObservableObject
{
    private readonly Action _pick;

    public LocationPage(int index, int lines, Action pick)
    {
        Index = index;
        _pick = pick;

        int first = index * TrackLocationViewModel.PageLines;
        int last = Math.Min(lines - 1, first + TrackLocationViewModel.PageLines - 1);

        Text = first.ToString(CultureInfo.InvariantCulture) + "-" + last.ToString(CultureInfo.InvariantCulture);
    }

    public int Index { get; }

    /// <summary>What is written on the cap: the rows this page covers.</summary>
    public string Text { get; }

    [ObservableProperty] private bool isShown;

    public IRelayCommand PickCommand => new RelayCommand(_pick);
}
