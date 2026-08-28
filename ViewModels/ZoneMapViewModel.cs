using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Tracker;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// The editable face of a <see cref="ZoneMap"/>: the zones, and whichever one is in hand.
/// </summary>
/// <remarks>
/// The map stays plain data that serialises with the song; this is what the panel's strip and
/// its settings are bound to. Every setter writes through to the zone and says so, because a
/// map is edited a zone at a time and each edit has to reach the song.
/// </remarks>
public sealed partial class ZoneMapViewModel : ObservableObject
{
    /// <summary>The map the song holds, written into in place rather than copied.</summary>
    private readonly ZoneMap _map;

    /// <summary>Told after every edit, which is what marks the song unsaved.</summary>
    private readonly Action _changed;

    /// <summary>
    /// Plays one note, so a zone can be heard from the row it is on. The panel owns how a note
    /// is sounded; a map knows only that it can ask for one.
    /// </summary>
    private readonly Action<Note> _tap;

    /// <summary>
    /// The highest key a zone can answer to, which is ten octaves and as far as the pattern
    /// can name a note.
    /// </summary>
    internal const int TopKey = 119;

    /// <summary>Shows one map. Nothing is copied: the map handed in is the map edited.</summary>
    public ZoneMapViewModel(ZoneMap map, Action changed, Action<Note> tap)
    {
        _map = map;
        _changed = changed;
        _tap = tap;

        Rebuild();
    }

    /// <summary>The map itself, for the strip that draws it.</summary>
    public ZoneMap Map => _map;

    /// <summary>
    /// One row per zone, in the map's own order.
    /// </summary>
    /// <remarks>
    /// Rebuilt rather than patched when the number of zones changes, which throws away
    /// whichever row was in hand; see <see cref="Resliced"/> for why that matters while a
    /// boundary is being dragged.
    /// </remarks>
    public ObservableCollection<SampleZoneViewModel> Zones { get; } = new();

    /// <summary>Which zone the settings underneath are about.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasZone))]
    private SampleZoneViewModel? selected;

    /// <summary>Whether there is a zone to show settings for, which greys the panel below.</summary>
    public bool HasZone => Selected != null;

    /// <summary>Bumped whenever the map's shape changes, so the strip knows to redraw.</summary>
    [ObservableProperty] private int revision;

    /// <summary>
    /// Marks the picked row and unmarks the rest, since the strip draws the selection itself
    /// rather than reading which one this holds.
    /// </summary>
    partial void OnSelectedChanged(SampleZoneViewModel? value)
    {
        foreach (var zone in Zones) zone.IsSelected = ReferenceEquals(zone, value);

        Revision++;
    }

    /// <summary>
    /// Whether the remove cap does anything: a map always keeps at least one zone, since a map
    /// with none is an instrument that cannot make a sound and no way back from it.
    /// </summary>
    public bool CanRemove => Zones.Count > 1;

    /// <summary>Another zone, put where there is room and left for you to fill.</summary>
    public IRelayCommand AddCommand => new RelayCommand(Add);

    /// <summary>Takes the zone in hand off the map. The last one cannot go.</summary>
    public IRelayCommand RemoveCommand => new RelayCommand(Remove);

    /// <summary>
    /// Lays every zone that has a recording out evenly across the playable keyboard.
    /// </summary>
    /// <remarks>
    /// What you want the moment you have dropped eight recordings on a machine and do not care
    /// where each one lands, only that they land somewhere sensible.
    /// </remarks>
    public IRelayCommand SpreadCommand => new RelayCommand(Spread);

    /// <summary>
    /// Adds a zone over the middle octave and picks it, or does nothing at the map's ceiling.
    /// </summary>
    /// <remarks>
    /// One octave from C3 with its root at the bottom, which is somewhere it can be heard
    /// straight away: a zone laid over the whole keyboard would silence every zone under it,
    /// and a zone of one key is a zone you have to hunt for.
    /// </remarks>
    private void Add()
    {
        if (_map.Zones.Count >= ZoneMap.MaxZones) return;

        var zone = new SampleZone { Low = 48, High = 59, Root = 48, Shape = new SampleShape() };

        _map.Zones.Add(zone);
        _map.Clamp();

        Rebuild();

        Selected = Zones.LastOrDefault();
        Say();
    }

    /// <summary>
    /// Takes the zone in hand off the map and leaves the one before it picked.
    /// </summary>
    /// <remarks>
    /// The neighbour rather than nothing, because removing several in a row is one gesture and
    /// a selection that emptied itself each time would mean picking the next one by hand.
    /// </remarks>
    private void Remove()
    {
        var zone = Selected?.Zone;
        if (zone == null || _map.Zones.Count <= 1) return;

        int at = _map.Zones.IndexOf(zone);

        _map.Zones.Remove(zone);
        _map.Clamp();

        Rebuild();

        Selected = Zones.ElementAtOrDefault(Math.Max(0, at - 1)) ?? Zones.FirstOrDefault();
        Say();
    }

    /// <summary>
    /// Lays the zones out evenly and says every value again, since every zone moved.
    /// </summary>
    /// <remarks>
    /// The rows are not rebuilt: the same zones are still there in the same order, and a
    /// rebuild would take the selection with it.
    /// </remarks>
    private void Spread()
    {
        _map.Spread();

        foreach (var zone in Zones) zone.Reread();

        Say();
    }

    /// <summary>
    /// Builds a map from a folder of recordings: one zone apiece, laid out evenly.
    /// </summary>
    /// <remarks>
    /// A multisample arrives as a folder, so this is the way in. The map is replaced rather
    /// than added to, because half of one instrument's samples under half of another's is not
    /// a thing anybody meant, and the zones are spread straight away so the keyboard is covered
    /// before you have touched anything.
    ///
    /// The order the folder lists them in is taken as the order up the keyboard, which is right
    /// far more often than not: a set of samples is nearly always named so that it sorts.
    /// </remarks>
    public int Fill(IReadOnlyList<string> paths)
    {
        if (paths == null || paths.Count == 0) return 0;

        _map.Zones.Clear();

        foreach (string path in paths.Take(ZoneMap.MaxZones))
        {
            _map.Zones.Add(new SampleZone
            {
                Name = Path.GetFileNameWithoutExtension(path),
                FilePath = path,
                Low = 0,
                High = ZoneMapViewModel.TopKey,
                Root = 48,
                Shape = new SampleShape()
            });
        }

        _map.Clamp();
        _map.Spread();

        Rebuild();
        Say();

        return _map.Zones.Count(z => z.HasSound);
    }

    /// <summary>
    /// Reads the map again after a slicing, rebuilding only when the number of zones changed.
    /// </summary>
    /// <remarks>
    /// A boundary being dragged fires this on every movement of the pointer, and a rebuild
    /// throws away every zone's view model and with it whichever one was selected. So the
    /// common case, the same zones with different windows, only says the values again.
    /// </remarks>
    public void Resliced()
    {
        if (Zones.Count != _map.Zones.Count) Rebuild();
        else foreach (var zone in Zones) zone.Reread();

        Say();
    }

    /// <summary>Picks the zone at that place in the map, for a piece chosen on the picture.</summary>
    public void SelectAt(int index) => Selected = Zones.ElementAtOrDefault(index) ?? Selected;

    /// <summary>Reads the map again, for a preset that has just landed on it.</summary>
    public void Refresh()
    {
        Rebuild();
        Revision++;
    }

    /// <summary>
    /// Builds a row per zone and picks the first.
    /// </summary>
    /// <remarks>
    /// Every row is thrown away, so this is only for a map whose shape has changed. Whatever
    /// was picked is lost, which is why <see cref="Resliced"/> avoids it wherever it can.
    /// </remarks>
    private void Rebuild()
    {
        Zones.Clear();

        foreach (var zone in _map.Zones) Zones.Add(new SampleZoneViewModel(zone, Say, _tap));

        Selected = Zones.FirstOrDefault();

        OnPropertyChanged(nameof(CanRemove));
    }

    /// <summary>
    /// Moves the revision on, re-reads what the caps allow, and tells the owner the song moved.
    /// </summary>
    private void Say()
    {
        Revision++;
        OnPropertyChanged(nameof(CanRemove));

        _changed();
    }
}

/// <summary>One zone: what is on it, which keys it answers to, and what it takes to change either.</summary>
public sealed partial class SampleZoneViewModel : ObservableObject
{
    /// <summary>What a kit and a map do identically with a chopped recording.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly ISlices Pieces = new Slices();

    /// <summary>Told after every edit to this zone, which the map passes on to the song.</summary>
    private readonly Action _changed;

    /// <summary>Plays a note, so the zone can be heard from its own row.</summary>
    private readonly Action<Note> _tap;

    /// <summary>Shows one zone off the map, written into in place.</summary>
    public SampleZoneViewModel(SampleZone zone, Action changed, Action<Note> tap)
    {
        Zone = zone;
        _changed = changed;
        _tap = tap;
    }

    /// <summary>The zone itself, for the strip that draws it and the picture that windows it.</summary>
    public SampleZone Zone { get; }

    /// <summary>
    /// True for the zone the settings below are about. Set by the map rather than by the row,
    /// since only one can be picked at a time.
    /// </summary>
    [ObservableProperty] private bool isSelected;

    /// <summary>What this zone is called: its name, or the file's, or the keys it covers.</summary>
    public string Title
    {
        get
        {
            if (Zone.Name.Length > 0) return Zone.Name;

            return Zone.HasSound
                ? Path.GetFileNameWithoutExtension(Zone.FilePath)
                : Zone.RangeText;
        }
    }

    /// <summary>Which keys it answers to, as two note names.</summary>
    public string RangeText => Zone.RangeText;

    /// <summary>
    /// The recording on it, by name, or the word for having none: a zone with nothing on it is
    /// an ordinary state on a map being built, and a blank row would read as a fault.
    /// </summary>
    public string FileText => Zone.HasSound ? Path.GetFileName(Zone.FilePath) : "empty";

    /// <summary>Whether there is anything on it to play.</summary>
    public bool HasSound => Zone.HasSound;

    /// <summary>
    /// What you have called it, which beats the recording's own name; see <see cref="Take"/>.
    /// </summary>
    public string Name
    {
        get => Zone.Name;
        set
        {
            if (Zone.Name == (value ?? "")) return;

            Zone.Name = value ?? "";
            Say(nameof(Name), nameof(Title));
        }
    }

    /// <summary>The bottom key of the zone, as a number so a slider can drive it.</summary>
    public double Low
    {
        get => Zone.Low;
        set => Move(v => Zone.Low = v, Zone.Low, value, nameof(Low));
    }

    /// <summary>And the top key.</summary>
    public double High
    {
        get => Zone.High;
        set => Move(v => Zone.High = v, Zone.High, value, nameof(High));
    }

    /// <summary>
    /// The key at which the recording plays untransposed, which is what the rest are pitched
    /// against.
    /// </summary>
    public double Root
    {
        get => Zone.Root;
        set => Move(v => Zone.Root = v, Zone.Root, value, nameof(Root));
    }

    /// <summary>The bottom key as a note name, which is how a keyboard is read.</summary>
    public string LowText => new Note(Zone.Low).ToString();

    /// <summary>The top key, the same way.</summary>
    public string HighText => new Note(Zone.High).ToString();

    /// <summary>And the root.</summary>
    public string RootText => new Note(Zone.Root).ToString();

    /// <summary>The zone's own level, nought to one, before anything the mixer does.</summary>
    public double Volume
    {
        get => Zone.Volume;
        set
        {
            if (Math.Abs(Zone.Volume - value) < 1e-9) return;

            Zone.Volume = Math.Clamp(value, 0, 1);
            Say(nameof(Volume));
        }
    }

    /// <summary>Where it sits, -1 hard left to 1 hard right.</summary>
    public double Pan
    {
        get => Zone.Pan;
        set
        {
            if (Math.Abs(Zone.Pan - value) < 1e-9) return;

            Zone.Pan = Math.Clamp(value, -1, 1);
            Say(nameof(Pan));
        }
    }

    /// <summary>
    /// Its detune, a semitone either way in cents, for a recording that was not quite in tune
    /// when it was made.
    /// </summary>
    public double FineCents
    {
        get => Zone.FineCents;
        set
        {
            if (Math.Abs(Zone.FineCents - value) < 1e-9) return;

            Zone.FineCents = Math.Clamp(value, -100, 100);
            Say(nameof(FineCents));
        }
    }

    /// <summary>Puts a recording on this zone, or takes one off when given nothing.</summary>
    /// <remarks>
    /// The name follows the take unless the zone has a name of its own. What it was called
    /// after is worked out first, so a name nobody chose can be told from one somebody did: a
    /// zone still called after the recording it used to hold is a zone nobody has named, and
    /// leaving that name on it after another take lands makes the map say the old recording is
    /// still there.
    /// </remarks>
    public void Take(string? path)
    {
        string was = Path.GetFileNameWithoutExtension(Zone.FilePath);

        Zone.FilePath = path ?? "";

        if (Zone.HasSound && Pieces.Auto(Zone.Name, was))
            Zone.Name = Path.GetFileNameWithoutExtension(Zone.FilePath);

        Say(nameof(FileText), nameof(HasSound), nameof(Title), nameof(Name));
    }

    /// <summary>Plays this zone at its own root, so what is on it can be heard untransposed.</summary>
    public IRelayCommand TapCommand => new RelayCommand(() => _tap(new Note(Zone.Root)));

    /// <summary>Says every value again, for a map that has been laid out from underneath.</summary>
    public void Reread() => OnPropertyChanged(string.Empty);

    /// <summary>Its title, so a list handed rows rather than text still reads.</summary>
    public override string ToString() => Title;

    /// <summary>
    /// Moves one of the three keys, rounded to a whole one and held on the keyboard.
    /// </summary>
    /// <remarks>
    /// The zone clamps itself afterwards, which can move the other two: a low pushed above a
    /// high is not a zone. So all three are announced whatever moved, along with everything
    /// written from them, rather than only the one that was set.
    /// </remarks>
    private void Move(Action<int> write, int current, double value, string name)
    {
        int wanted = (int)Math.Clamp(Math.Round(value), 0, ZoneMapViewModel.TopKey);
        if (current == wanted) return;

        write(wanted);
        Zone.Clamp();

        Say(name, nameof(Low), nameof(High), nameof(Root),
            nameof(LowText), nameof(HighText), nameof(RootText),
            nameof(RangeText), nameof(Title));
    }

    /// <summary>
    /// Says every name that now reads differently and tells the map, which tells the song.
    /// </summary>
    private void Say(params string[] names)
    {
        foreach (string name in names) OnPropertyChanged(name);

        _changed();
    }
}
