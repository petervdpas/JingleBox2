using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Tracker;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// The editable face of a <see cref="DrumKit"/>: sixteen pads and whichever one is in hand.
/// </summary>
/// <remarks>
/// The kit stays plain data that serialises with the song; this is what the panel's pads and
/// their settings are bound to. Every setter writes through to the pad and says so, because a
/// kit is edited a pad at a time and each edit has to reach the song.
/// </remarks>
public sealed partial class DrumKitViewModel : ObservableObject
{
    /// <summary>The kit itself, written straight through rather than copied.</summary>
    private readonly DrumKit _kit;

    /// <summary>Told after every edit, which is how the song learns it has been changed.</summary>
    private readonly Action _changed;

    /// <summary>Sounds one pad, for the button on the pad's own face.</summary>
    private readonly Action<Note> _tap;

    /// <summary>Builds the sixteen pads over one kit and picks the first of them.</summary>
    public DrumKitViewModel(DrumKit kit, Action changed, Action<Note> tap)
    {
        _kit = kit;
        _changed = changed;
        _tap = tap;

        Rebuild();
    }

    /// <summary>The kit itself, for what needs to ask it rather than the pads.</summary>
    public DrumKit Kit => _kit;

    /// <summary>The pads, in the order they are laid out: four rows of four.</summary>
    public ObservableCollection<DrumPadViewModel> Pads { get; } = new();

    /// <summary>Which pad the settings underneath are about. Never null once there are pads.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPad))]
    private DrumPadViewModel? selected;

    /// <summary>True when there is a pad to show settings for, so the panel can hide them.</summary>
    public bool HasPad => Selected != null;

    /// <summary>Marks the picked pad and unmarks the rest, since the grid draws its own outline.</summary>
    partial void OnSelectedChanged(DrumPadViewModel? value)
    {
        foreach (var pad in Pads) pad.IsSelected = ReferenceEquals(pad, value);
    }

    /// <summary>
    /// Lights the pads that are sounding, from whatever the panel is watching.
    /// </summary>
    /// <remarks>
    /// A kit's pads sound over each other, so this is a set rather than one note: the crash
    /// stays lit under the snare that follows it, which is exactly what it is doing.
    ///
    /// One kit watches at a time. The panel puts a new kit on this same set of notes every time
    /// the machine is changed, and a kit nobody can see any more should not still be listening.
    ///
    /// The pad that was hit also becomes the pad the settings underneath are about. Playing a key
    /// and then hunting for its pad in the grid to change its level is two jobs where the machine
    /// already knows which one you meant.
    /// </remarks>
    public void Follow(SoundingNotes sounding)
    {
        if (sounding == null) return;

        Unfollow();

        _watching = sounding;
        _lighting = (_, _) => Light(sounding);

        sounding.Lit.CollectionChanged += _lighting;

        sounding.Hit += Pick;

        Light(sounding);
    }

    /// <summary>Stops watching, for a kit the panel has moved on from.</summary>
    public void Unfollow()
    {
        if (_watching == null) return;

        if (_lighting != null) _watching.Lit.CollectionChanged -= _lighting;

        _watching.Hit -= Pick;

        _watching = null;
        _lighting = null;
    }

    /// <summary>The notes being watched, or null when nothing is.</summary>
    private SoundingNotes? _watching;

    /// <summary>
    /// The handler that was hung on those notes, kept so it can be taken off again.
    /// </summary>
    /// <remarks>
    /// A closure rather than a method, because it carries which set of notes it was made for.
    /// Held rather than made afresh on the way out, since an anonymous handler cannot be
    /// unsubscribed by building an equal one.
    /// </remarks>
    private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _lighting;

    /// <summary>Puts the settings on the pad that note belongs to, if any pad does.</summary>
    private void Pick(Note note)
    {
        foreach (var pad in Pads)
        {
            if (pad.Semitone != note.Semitone) continue;

            Selected = pad;
            return;
        }
    }

    /// <summary>Lights every pad whose key is sounding and puts the rest out.</summary>
    /// <remarks>
    /// All sixteen every time rather than the ones that moved, since the set says what is lit and
    /// not what changed, and sixteen comparisons is nothing beside working out the difference.
    /// </remarks>
    private void Light(SoundingNotes sounding)
    {
        foreach (var pad in Pads) pad.IsLit = sounding.Lit.Contains(pad.Semitone);
    }

    /// <summary>
    /// Fills the pads from a folder of recordings, in the order the folder lists them.
    /// </summary>
    /// <remarks>
    /// How a kit is actually made. Sixteen pads loaded one file dialog at a time is sixteen
    /// dialogs; a folder of drum hits is one, and it is how the folder already sits on the
    /// disc. Pads past the end of the list are left alone rather than emptied, so dropping
    /// four hits onto a kit adds four rather than wiping the other twelve.
    /// </remarks>
    public int Fill(IReadOnlyList<string> paths)
    {
        if (paths == null || paths.Count == 0) return 0;

        int put = 0;

        for (int i = 0; i < Pads.Count && i < paths.Count; i++)
        {
            Pads[i].Take(paths[i]);
            put++;
        }

        Selected = Pads.FirstOrDefault();

        return put;
    }

    /// <summary>
    /// Reads the kit again, for a preset that has just landed on it or a take that has been cut up.
    /// </summary>
    /// <remarks>
    /// A kit is always sixteen pads, so there is never a list to rebuild: only what is on each of
    /// them changes.
    /// </remarks>
    public void Resliced()
    {
        foreach (var pad in Pads) pad.Reread();

        OnPropertyChanged(nameof(Pads));
    }

    /// <summary>Picks the pad at that place in the kit, for a piece chosen on the picture.</summary>
    public void SelectAt(int index) => Selected = Pads.ElementAtOrDefault(index) ?? Selected;

    /// <summary>
    /// Builds the pads again, keeping the one that was picked where it can be found.
    /// </summary>
    /// <remarks>
    /// Found by key rather than by place, since a kit rebuilt from a different sound may lay the
    /// same drums out differently, and the pad somebody was working on is the one they want back.
    /// </remarks>
    public void Refresh()
    {
        string? keep = Selected?.Pad.Semitone.ToString();

        Rebuild();

        Selected = Pads.FirstOrDefault(p => p.Pad.Semitone.ToString() == keep) ?? Pads.FirstOrDefault();
    }

    /// <summary>Makes one row per pad in the kit and picks the first.</summary>
    private void Rebuild()
    {
        Pads.Clear();

        foreach (var pad in _kit.Pads) Pads.Add(new DrumPadViewModel(pad, _changed, _tap));

        Selected = Pads.FirstOrDefault();
    }
}

/// <summary>One pad: what is on it, where it sits, and what it takes to change either.</summary>
public sealed partial class DrumPadViewModel : ObservableObject
{
    /// <summary>What a kit and a map do identically with a chopped recording.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly ISlices Pieces = new Slices();

    /// <summary>Told after every edit, which is how the song learns it has been changed.</summary>
    private readonly Action _changed;

    /// <summary>Sounds this pad, for its own button.</summary>
    private readonly Action<Note> _tap;

    /// <summary>One row over one pad, which it edits in place.</summary>
    public DrumPadViewModel(DrumPad pad, Action changed, Action<Note> tap)
    {
        Pad = pad;
        _changed = changed;
        _tap = tap;
    }

    /// <summary>The pad underneath, for anything that wants the data rather than the controls.</summary>
    public DrumPad Pad { get; }

    /// <summary>The key this pad answers to, as a number, which is how the keyboard finds it.</summary>
    public int Semitone => Pad.Semitone;

    /// <summary>The key this pad answers to, written the way the pattern writes it.</summary>
    public string NoteText => Pad.Note.ToString();

    /// <summary>True while a note on this pad is sounding.</summary>
    [ObservableProperty] private bool isLit;

    /// <summary>True while this is the pad the settings underneath are about.</summary>
    [ObservableProperty] private bool isSelected;

    /// <summary>
    /// What is written on the pad: its name, or the file's, and nothing at all when it is empty.
    /// </summary>
    /// <remarks>
    /// Empty is left blank rather than filled with the key, which is already written under it.
    /// A pad saying the same thing twice reads as a pad with something on it.
    /// </remarks>
    public string CapText
    {
        get
        {
            if (Pad.Name.Length > 0) return Pad.Name;

            return Pad.HasSound ? Path.GetFileNameWithoutExtension(Pad.FilePath) : "";
        }
    }

    /// <summary>True when there is a recording on it, which is what bands its key.</summary>
    public bool HasSound => Pad.HasSound;

    /// <summary>What you have called it, or nothing, in which case the file's name is written.</summary>
    public string Name
    {
        get => Pad.Name;
        set
        {
            if (Pad.Name == (value ?? "")) return;

            Pad.Name = value ?? "";
            Say(nameof(Name), nameof(CapText));
        }
    }

    /// <summary>The recording on this pad, or nothing. Shown as the file's name alone.</summary>
    public string FileText => Pad.HasSound ? Path.GetFileName(Pad.FilePath) : "empty";

    /// <summary>How loud this one drum is, apart from the machine's own level.</summary>
    public double Volume
    {
        get => Pad.Volume;
        set
        {
            if (Math.Abs(Pad.Volume - value) < 1e-9) return;

            Pad.Volume = Math.Clamp(value, 0, 1);
            Say(nameof(Volume));
        }
    }

    /// <summary>Where this one drum sits across the stereo.</summary>
    public double Pan
    {
        get => Pad.Pan;
        set
        {
            if (Math.Abs(Pad.Pan - value) < 1e-9) return;

            Pad.Pan = Math.Clamp(value, -1, 1);
            Say(nameof(Pan));
        }
    }

    /// <summary>Which pads cut this one. Nought is none, which is most of a kit.</summary>
    /// <remarks>
    /// A number rather than a switch because a kit has several of these to hand out: the hats are
    /// one group and a rim and a stick can be another. Held as a double so one control template
    /// serves every setting on the pad, and rounded on the way in.
    /// </remarks>
    public double Choke
    {
        get => Pad.Choke;
        set
        {
            int wanted = (int)Math.Clamp(Math.Round(value), 0, ChokeGroups);
            if (Pad.Choke == wanted) return;

            Pad.Choke = wanted;
            Say(nameof(Choke));
        }
    }

    /// <summary>How many choke groups a kit hands out, beside nought for a pad in none.</summary>
    private const int ChokeGroups = 8;

    /// <summary>
    /// Puts a recording on this pad, or takes one off when given nothing.
    /// </summary>
    /// <remarks>
    /// An unnamed pad takes the file's name, which is nearly always what it should be called, and
    /// so does one still called after the take it used to hold: a cap reading the old recording is
    /// a pad claiming to be something it no longer is. A name typed by hand is kept, since that is
    /// a pad you have named, which is why what it was called after is read before the file moves.
    /// </remarks>
    public void Take(string? path)
    {
        string was = Path.GetFileNameWithoutExtension(Pad.FilePath);

        Pad.FilePath = path ?? "";

        if (Pad.HasSound && Pieces.Auto(Pad.Name, was))
            Pad.Name = Path.GetFileNameWithoutExtension(Pad.FilePath);

        Say(nameof(FileText), nameof(HasSound), nameof(CapText), nameof(Name));
    }

    /// <summary>Hits the pad, so what is on it can be heard.</summary>
    /// <remarks>
    /// Always enabled, empty pad included: hitting one that has nothing on it makes no sound, and
    /// that is an answer rather than an error.
    /// </remarks>
    public IRelayCommand TapCommand => new RelayCommand(() => _tap(Pad.Note));

    /// <summary>Says every value again, for a kit that has been laid out from underneath.</summary>
    public void Reread() => OnPropertyChanged(string.Empty);

    /// <summary>What is written on the cap, which is what a list with no template shows.</summary>
    public override string ToString() => CapText;

    /// <summary>Says the named values moved, and then that the song has been changed.</summary>
    /// <remarks>
    /// One path for every setting on the pad, so no control can be the one that changed the sound
    /// and left the song looking saved.
    /// </remarks>
    private void Say(params string[] names)
    {
        foreach (string name in names) OnPropertyChanged(name);

        _changed();
    }
}
