using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Midi;
using JingleBox2.Tracker;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using JingleBox2.Tracker.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Midi.Records;

namespace JingleBox2.ViewModels;

/// <summary>
/// One track's automation, as the strip under the mixer shows it.
/// </summary>
/// <remarks>
/// The same shape as the chain under the pattern, and deliberately: a block at the head saying
/// which part you are working on, and the room after it given to that part. There it is the
/// instrument and then its effects; here it is the parameter and then its lane. A person who
/// has used one already knows where to look on the other.
///
/// Under the mixer rather than anywhere else because that is where a track's settings are, and
/// automation is a track's settings moving. It is opened per track, from the strip's own AUTO
/// button, so which track it is about is never a question the panel has to ask.
/// </remarks>
public sealed partial class AutomationViewModel : ObservableObject
{
    /// <summary>What can be moved on a strip, and how to reach one of them.</summary>
    /// <remarks>
    /// The same door remote control writes through, which is what makes a lane and a knob one act
    /// rather than two paths that would eventually disagree about what a parameter is.
    /// </remarks>
    private readonly IControlTargets _targets;

    /// <summary>The song, asked rather than held, for the pattern's number in the order.</summary>
    private readonly Func<Song?> _song;

    /// <summary>
    /// The pattern a lane is written against.
    /// </summary>
    /// <remarks>
    /// Asked every time rather than held, because the pattern changes underneath this panel and a
    /// held one would have the strip writing into a pattern nobody is looking at.
    /// </remarks>
    private readonly Func<Pattern?> _pattern;

    /// <summary>Points the panel at a track's targets, and at whichever pattern is current.</summary>
    /// <param name="targets">What can be moved, and how to reach it.</param>
    /// <param name="song">The song, for naming which pattern a lane is in.</param>
    /// <param name="pattern">The pattern the lanes belong to.</param>
    /// <param name="beat">How many lines make a beat, which is what the picture picks out.</param>
    /// <param name="playing">The line being played, or -1 when nothing is.</param>
    public AutomationViewModel(IControlTargets targets, Func<Song?> song, Func<Pattern?> pattern,
                               Func<int> beat, Func<int> playing)
    {
        _targets = targets;
        _song = song;
        _pattern = pattern;
        _beat = beat;
        _playing = playing;
    }

    /// <summary>How many lines make a beat, asked of whoever knows rather than kept.</summary>
    private readonly Func<int> _beat;

    /// <summary>Where the song has got to, asked on every frame the picture draws.</summary>
    private readonly Func<int> _playing;

    /// <summary>The playing line moved, so the picture can show where the song has got to.</summary>
    public void Running() => OnPropertyChanged(nameof(PlayingLine));

    /// <summary>Told before a lane is added or taken away, so undo has somewhere to go.</summary>
    public Action<Pattern, string>? Taking;

    /// <summary>Told after, since the song now has something unsaved in it.</summary>
    public Action? Dirtied;

    /// <summary>Which track, counted from zero. Set by the strip whose button was pressed.</summary>
    public int Track { get; private set; }

    /// <summary>
    /// Narrows what the picker offers, by the parameter's name and by the device's.
    /// </summary>
    /// <remarks>
    /// Here rather than nowhere because a plugin can have two hundred parameters, and a list
    /// that long is not something a picker can be used on. A machine has a dozen and needs it
    /// no more than the chain does.
    /// </remarks>
    [ObservableProperty] private string search = "";

    /// <summary>Reads the list again against the narrowed search.</summary>
    partial void OnSearchChanged(string value) => Restock();

    /// <summary>Every parameter on the track that could be moved, in panel order.</summary>
    public ObservableCollection<AutomationRow> Parameters { get; } = new();

    /// <summary>The one being worked on, which is what the room after the head block is about.</summary>
    [ObservableProperty] private AutomationRow? chosen;

    /// <summary>True when there is anything to automate, so the panel can say <see cref="Nothing"/>.</summary>
    public bool HasAny => Parameters.Count > 0;

    /// <summary>True when a parameter is being worked on, which is what fills the room to the right.</summary>
    public bool HasChosen => Chosen is not null;

    /// <summary>
    /// What the picture is drawn against: the pattern's length, its beat, and where it has got
    /// to.
    /// </summary>
    /// <remarks>
    /// Asked of the panel rather than of the tracker, so the strip that draws it needs to know
    /// nothing but which panel it is showing. Told when they move, since a pattern changed
    /// underneath is a different grid.
    /// </remarks>
    public int Lines => _pattern()?.Lines ?? 0;

    /// <inheritdoc cref="Lines"/>
    public int LinesPerBeat => _beat();

    /// <inheritdoc cref="Lines"/>
    public int PlayingLine => _playing();

    /// <summary>Which track and which pattern, since a lane belongs to both.</summary>
    /// <remarks>
    /// The pattern is not on the screen when the mixer is, so it is said. A panel that quietly
    /// wrote into whichever pattern happened to be current is not one anybody could trust.
    /// </remarks>
    public string About
    {
        get
        {
            var pattern = _pattern();
            if (pattern is null) return "";

            string track = Track == Tracker.TrackerPlayer.MasterStrip
                ? "MASTER"
                : "TR-" + (Track + 1).ToString("00", CultureInfo.InvariantCulture);

            if (pattern.Name.Length > 0) return track + "  ·  pattern " + pattern.Name;

            int index = _song()?.Patterns.IndexOf(pattern) ?? -1;

            return index >= 0
                ? track + "  ·  pattern " + (index + 1).ToString("00", CultureInfo.InvariantCulture)
                : track;
        }
    }

    /// <summary>What to say when the list is empty, which is two different situations.</summary>
    /// <remarks>
    /// A search that found nothing and a track with nothing on it are told apart, because only one
    /// of them is something to do about it.
    /// </remarks>
    public string Nothing => Search.Length > 0
        ? "Nothing on this track is called that."
        : "This track has nothing on it that can be moved. Give it an instrument, or put a plugin on its chain.";

    /// <summary>Points the panel at a track and reads it.</summary>
    /// <remarks>
    /// Below nought is the master, which is a strip without being a track and is why a lane names
    /// a strip rather than a track at all. Anything else out of range is a mistake and lands on
    /// the first track, as it always did.
    /// </remarks>
    public void Show(int track)
    {
        Track = track < 0 && track != Tracker.TrackerPlayer.MasterStrip ? 0 : track;

        Restock();
    }

    /// <summary>
    /// Reads the whole list again: the track's parameters, and which of them have lanes.
    /// </summary>
    /// <remarks>
    /// Whole rather than in part, because everything it is built out of moves underneath it: an
    /// instrument is swapped, a plugin is taken off a chain, a pattern is changed to. It is a
    /// few dozen rows, read when somebody opens the strip, which is not a rate worth thinking
    /// about.
    ///
    /// What was being worked on is kept across the read where it still exists, since the list is
    /// read again after every edit to it and being thrown back to nothing after clearing a lane
    /// would be its own small bereavement.
    /// </remarks>
    public void Restock()
    {
        var was = Chosen;

        Parameters.Clear();

        var pattern = _pattern();

        if (pattern is not null && Track < pattern.TrackCount)
        {
            string wanted = Search.Trim();

            foreach (var choice in _targets.On(Track))
            {
                if (wanted.Length > 0
                    && choice.Name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) < 0
                    && choice.Device.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                Parameters.Add(new AutomationRow(this, choice, pattern, Track));
            }
        }

        Chosen = Parameters.FirstOrDefault(one => was is not null && one.Is(was))
                 ?? Parameters.FirstOrDefault();

        OnPropertyChanged(nameof(HasAny));
        OnPropertyChanged(nameof(About));
        OnPropertyChanged(nameof(Nothing));
        OnPropertyChanged(nameof(Lines));
        OnPropertyChanged(nameof(LinesPerBeat));
    }

    /// <summary>Says whether there is anything in the room to the right now.</summary>
    partial void OnChosenChanged(AutomationRow? value) => OnPropertyChanged(nameof(HasChosen));

    /// <summary>Puts a lane on the chosen parameter, with one point where it stands.</summary>
    /// <remarks>
    /// One point and not none, which is Renoise's behaviour and the only useful answer. An
    /// empty lane says nothing at all, so a parameter would be listed as automated and would not
    /// move; a lane holding where the knob is now says "this, throughout", which is a thing
    /// somebody can then take hold of.
    /// </remarks>
    internal void Add(AutomationRow row)
    {
        var pattern = _pattern();
        if (pattern is null || row.Lane is not null) return;

        var made = AutomationLane.For(row.Choice.Mapping, row.Track);
        if (made is null) return;

        Taking?.Invoke(pattern, "automating " + row.Name);

        var lane = pattern.Lane(made);

        if (_targets.Find(row.Choice.Mapping) is { } target && target.Max > target.Min)
            lane.Put(0, Math.Clamp((target.Value - target.Min) / (target.Max - target.Min), 0, 1));

        Dirtied?.Invoke();
        Restock();
    }

    /// <summary>Takes a lane off. The parameter stops moving and stays where it was left.</summary>
    internal void Forget(AutomationRow row)
    {
        var pattern = _pattern();
        if (pattern is null || row.Lane is null) return;

        Taking?.Invoke(pattern, "clearing " + row.Name);

        pattern.RemoveLane(row.Lane);

        Dirtied?.Invoke();
        Restock();
    }

    /// <summary>Switches a lane between stepping and sweeping.</summary>
    internal void Next(AutomationRow row)
    {
        var pattern = _pattern();
        if (pattern is null || row.Lane is null) return;

        Taking?.Invoke(pattern, row.Name + "'s shape");

        row.Lane.Play = row.Lane.Play == AutomationPlay.Lines
            ? AutomationPlay.Points
            : AutomationPlay.Lines;

        pattern.LaneChanged();

        Dirtied?.Invoke();
        Restock();
    }

    /// <summary>
    /// A gesture on the picture is starting. Told before it happens, like every other edit.
    /// </summary>
    /// <remarks>
    /// Here rather than in the strip that draws it, so the strip needs to know nothing but the
    /// panel it is showing. It was reaching through to the tracker for the pattern and the
    /// history, which meant it could only ever be the pattern's panel and never the master's.
    /// </remarks>
    public void Editing(string what)
    {
        if (_pattern() is { } pattern) Taking?.Invoke(pattern, what);
    }

    /// <summary>And it has happened: the pattern has changed and the row has to read itself again.</summary>
    public void Edited()
    {
        _pattern()?.LaneChanged();

        Touched();
    }

    /// <summary>
    /// A point on the picture moved. Says so, without reading the whole list again.
    /// </summary>
    /// <remarks>
    /// Not <see cref="Restock"/>, which is what every other edit here ends at, because this one
    /// arrives per mouse move. Rebuilding forty rows and re-resolving every target forty times a
    /// second to change one number would be paying a list's price for a point.
    /// </remarks>
    internal void Touched()
    {
        Chosen?.Reread();

        Dirtied?.Invoke();
    }

    /// <summary>
    /// Where the parameter's own nought sits, nought to one, for the picture to rest on.
    /// </summary>
    /// <remarks>
    /// A level runs from silence upwards and its nothing is the floor. A pan runs from one side
    /// to the other and its nothing is the middle, so a pan curve drawn as a level would read as
    /// hard left all the way along with a bump in it, which is the opposite of what it says.
    ///
    /// Worked out from the target's own range rather than by knowing which parameters are which:
    /// a range that has nought inside it has a middle, and one that starts at nought has a
    /// floor. That is as true of a machine's pitch, which runs either side of nought, as it is
    /// of pan, and nobody has to list them.
    /// </remarks>
    internal double ZeroOf(ControlChoice choice)
    {
        if (_targets.Find(choice.Mapping) is not { } target) return 0;

        double span = target.Max - target.Min;
        if (span <= 0) return 0;

        return Math.Clamp((0 - target.Min) / span, 0, 1);
    }

    /// <summary>
    /// Where the parameter stands, in its own words, for the row to print.
    /// </summary>
    /// <remarks>
    /// The unit is added only where the target has not already said it: a machine's parameter
    /// carries its own unit in the reading and a plugin's usually does not, and appending it
    /// blindly prints "50 % %".
    /// </remarks>
    internal string Reading(ControlChoice choice)
    {
        if (_targets.Find(choice.Mapping) is not { } target) return "";

        string said = target.Reads(target.Value);

        return choice.Unit.Length > 0 && !said.EndsWith(choice.Unit, StringComparison.Ordinal)
            ? said + " " + choice.Unit
            : said;
    }
}

/// <summary>One parameter of one track, and the lane it has or has not got.</summary>
public sealed class AutomationRow : ObservableObject
{
    /// <summary>The panel this row belongs to, which does the work the buttons ask for.</summary>
    /// <remarks>
    /// The row asks rather than doing it itself, because adding and clearing a lane is an edit to
    /// the pattern and has to leave one undo step, which is the panel's business and not a row's.
    /// </remarks>
    private readonly AutomationViewModel _owner;

    /// <summary>One row over one parameter, with its lane read as the row is made.</summary>
    public AutomationRow(AutomationViewModel owner, ControlChoice choice, Pattern pattern, int track)
    {
        _owner = owner;
        Choice = choice;
        Track = track;
        Lane = pattern.LaneFor(choice.Mapping, track);
    }

    /// <summary>What this row is about: the mapping, and the words for it.</summary>
    public ControlChoice Choice { get; }

    /// <summary>Which strip, counted from zero, with -1 for the master.</summary>
    public int Track { get; }

    /// <summary>The lane, or nothing. Read once when the row is made and not held live.</summary>
    public AutomationLane? Lane { get; }

    /// <summary>The parameter's own name, without whatever holds it.</summary>
    public string Name => Choice.Name;

    /// <summary>What holds it: the instrument, an insert, or the strip itself.</summary>
    public string Device => Choice.Device;

    /// <summary>What the picker shows: what holds it, then what it is.</summary>
    public string Said => Choice.Device + "  ·  " + Choice.Name;

    /// <summary>Where the parameter stands as the list was read.</summary>
    public string Reads => _owner.Reading(Choice);

    /// <summary>Where its nought sits on the picture: the floor for a level, the middle for a pan.</summary>
    public double Zero => _owner.ZeroOf(Choice);

    /// <summary>True when this parameter is automated, which is what the buttons swap on.</summary>
    public bool HasLane => Lane is not null;

    /// <summary>How much is written down, so a lane with nothing in it is not mistaken for one.</summary>
    public string Says => Lane is null
        ? ""
        : Lane.Points.Count switch
        {
            0 => "empty",
            1 => "1 point",
            var many => many.ToString(CultureInfo.InvariantCulture) + " points"
        };

    /// <summary>Stepped or swept, for the button that changes it.</summary>
    public string How => Lane?.Play == AutomationPlay.Points ? "Steps" : "Sweeps";

    /// <summary>True when that row is about the same parameter as this one.</summary>
    /// <remarks>
    /// By what it names rather than by reference: the rows are made again after every edit, so
    /// the one that was being worked on is never the same object twice.
    /// </remarks>
    public bool Is(AutomationRow other) =>
        other.Track == Track && Lane switch
        {
            _ => string.Equals(other.Said, Said, StringComparison.Ordinal)
        };

    /// <summary>Reads its own lane again, for when the picture changed it underneath.</summary>
    public void Reread()
    {
        OnPropertyChanged(nameof(Says));
        OnPropertyChanged(nameof(How));
        OnPropertyChanged(nameof(Reads));
    }

    /// <summary>Puts a lane on this parameter, holding where it stands.</summary>
    /// <remarks>
    /// Always enabled; the panel refuses a row that already has a lane, and the button is hidden
    /// rather than greyed in that case, since a lane and no lane are two different rows.
    /// </remarks>
    public IRelayCommand AddCommand => new RelayCommand(() => _owner.Add(this));

    /// <summary>Takes the lane off, leaving the parameter where it was last put.</summary>
    /// <remarks>Always enabled; the panel refuses a row with no lane.</remarks>
    public IRelayCommand ForgetCommand => new RelayCommand(() => _owner.Forget(this));

    /// <summary>Swaps the lane between stepping and sweeping.</summary>
    /// <remarks>Always enabled; the panel refuses a row with no lane.</remarks>
    public IRelayCommand NextCommand => new RelayCommand(() => _owner.Next(this));
}
