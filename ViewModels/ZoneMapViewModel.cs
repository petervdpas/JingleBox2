using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Tracker;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

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
    private readonly ZoneMap _map;
    private readonly Action _changed;
    private readonly Action<Note> _tap;

    public ZoneMapViewModel(ZoneMap map, Action changed, Action<Note> tap)
    {
        _map = map;
        _changed = changed;
        _tap = tap;

        Rebuild();
    }

    /// <summary>The map itself, for the strip that draws it.</summary>
    public ZoneMap Map => _map;

    public ObservableCollection<SampleZoneViewModel> Zones { get; } = new();

    /// <summary>Which zone the settings underneath are about.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasZone))]
    private SampleZoneViewModel? selected;

    public bool HasZone => Selected != null;

    /// <summary>Bumped whenever the map's shape changes, so the strip knows to redraw.</summary>
    [ObservableProperty] private int revision;

    partial void OnSelectedChanged(SampleZoneViewModel? value)
    {
        foreach (var zone in Zones) zone.IsSelected = ReferenceEquals(zone, value);

        Revision++;
    }

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
                High = 119,
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

    private void Rebuild()
    {
        Zones.Clear();

        foreach (var zone in _map.Zones) Zones.Add(new SampleZoneViewModel(zone, Say, _tap));

        Selected = Zones.FirstOrDefault();

        OnPropertyChanged(nameof(CanRemove));
    }

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
    private readonly Action _changed;
    private readonly Action<Note> _tap;

    public SampleZoneViewModel(SampleZone zone, Action changed, Action<Note> tap)
    {
        Zone = zone;
        _changed = changed;
        _tap = tap;
    }

    public SampleZone Zone { get; }

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

    public string RangeText => Zone.RangeText;

    public string FileText => Zone.HasSound ? Path.GetFileName(Zone.FilePath) : "empty";

    public bool HasSound => Zone.HasSound;

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

    public double Low
    {
        get => Zone.Low;
        set => Move(v => Zone.Low = v, Zone.Low, value, nameof(Low));
    }

    public double High
    {
        get => Zone.High;
        set => Move(v => Zone.High = v, Zone.High, value, nameof(High));
    }

    public double Root
    {
        get => Zone.Root;
        set => Move(v => Zone.Root = v, Zone.Root, value, nameof(Root));
    }

    public string LowText => new Note(Zone.Low).ToString();

    public string HighText => new Note(Zone.High).ToString();

    public string RootText => new Note(Zone.Root).ToString();

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
    public void Take(string? path)
    {
        Zone.FilePath = path ?? "";

        if (Zone.Name.Length == 0 && Zone.HasSound)
            Zone.Name = Path.GetFileNameWithoutExtension(Zone.FilePath);

        Say(nameof(FileText), nameof(HasSound), nameof(Title), nameof(Name));
    }

    /// <summary>Plays this zone at its own root, so what is on it can be heard untransposed.</summary>
    public IRelayCommand TapCommand => new RelayCommand(() => _tap(new Note(Zone.Root)));

    /// <summary>Says every value again, for a map that has been laid out from underneath.</summary>
    public void Reread() => OnPropertyChanged(string.Empty);

    public override string ToString() => Title;

    private void Move(Action<int> write, int current, double value, string name)
    {
        int wanted = (int)Math.Clamp(Math.Round(value), 0, 119);
        if (current == wanted) return;

        write(wanted);
        Zone.Clamp();

        Say(name, nameof(Low), nameof(High), nameof(Root),
            nameof(LowText), nameof(HighText), nameof(RootText),
            nameof(RangeText), nameof(Title));
    }

    private void Say(params string[] names)
    {
        foreach (string name in names) OnPropertyChanged(name);

        _changed();
    }
}
