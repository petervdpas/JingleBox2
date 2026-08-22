using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Tracker;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;

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
    private readonly DrumKit _kit;
    private readonly Action _changed;
    private readonly Action<Note> _tap;

    public DrumKitViewModel(DrumKit kit, Action changed, Action<Note> tap)
    {
        _kit = kit;
        _changed = changed;
        _tap = tap;

        Rebuild();
    }

    /// <summary>The pads, in the order they are laid out: four rows of four.</summary>
    public ObservableCollection<DrumPadViewModel> Pads { get; } = new();

    /// <summary>Which pad the settings underneath are about. Never null once there are pads.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPad))]
    private DrumPadViewModel? selected;

    public bool HasPad => Selected != null;

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
    /// </remarks>
    public void Follow(SoundingNotes sounding)
    {
        if (sounding == null) return;

        sounding.Lit.CollectionChanged += (_, _) => Light(sounding);
        Light(sounding);
    }

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

    /// <summary>Reads the kit again, for a preset that has just landed on it.</summary>
    public void Refresh()
    {
        string? keep = Selected?.Pad.Semitone.ToString();

        Rebuild();

        Selected = Pads.FirstOrDefault(p => p.Pad.Semitone.ToString() == keep) ?? Pads.FirstOrDefault();
    }

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
    private readonly Action _changed;
    private readonly Action<Note> _tap;

    public DrumPadViewModel(DrumPad pad, Action changed, Action<Note> tap)
    {
        Pad = pad;
        _changed = changed;
        _tap = tap;
    }

    public DrumPad Pad { get; }

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

    public bool HasSound => Pad.HasSound;

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
    public double Choke
    {
        get => Pad.Choke;
        set
        {
            int wanted = (int)Math.Clamp(Math.Round(value), 0, 8);
            if (Pad.Choke == wanted) return;

            Pad.Choke = wanted;
            Say(nameof(Choke));
        }
    }

    /// <summary>Puts a recording on this pad, or takes one off when given nothing.</summary>
    public void Take(string? path)
    {
        Pad.FilePath = path ?? "";

        // An unnamed pad takes the file's name, which is nearly always what it should be called.
        if (Pad.Name.Length == 0 && Pad.HasSound)
            Pad.Name = Path.GetFileNameWithoutExtension(Pad.FilePath);

        Say(nameof(FileText), nameof(HasSound), nameof(CapText), nameof(Name));
    }

    /// <summary>Hits the pad, so what is on it can be heard.</summary>
    public IRelayCommand TapCommand => new RelayCommand(() => _tap(Pad.Note));

    public override string ToString() => CapText;

    private void Say(params string[] names)
    {
        foreach (string name in names) OnPropertyChanged(name);

        _changed();
    }
}
