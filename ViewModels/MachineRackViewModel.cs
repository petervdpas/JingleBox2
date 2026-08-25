using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Diagnostics;
using JingleBox2.Audio;
using JingleBox2.Models;
using JingleBox2.Machines;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Synth;
using JingleBox2.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace JingleBox2.ViewModels;

/// <summary>
/// The instrument rack: the voices you own, kept outside any song. A preset is where a new
/// one starts; after that the instrument is its own thing and every song can use it.
/// </summary>
/// <remarks>
/// Edits save themselves. There is no Save button here on purpose: an instrument is a small
/// document of its own, and a knob you turned is not a change you should have to remember to
/// keep. Writes are held back until the turning stops.
/// </remarks>
public sealed partial class MachineRackViewModel : ObservableObject, IInstrumentDesigner
{
    /// <summary>How long the knobs have to be still before the file is written.</summary>
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(600);

    private readonly MachineRack _rack;
    private readonly IInstrumentAudition _audition;
    private readonly ObservableCollection<Recording> _recordings;

    /// <summary>Reads a sample down to peaks, so a sample instrument can show its shape.</summary>
    private readonly IWaveformService? _waveforms;

    /// <summary>The plugins this machine has, for building an instrument out of one.</summary>
    private readonly PluginLibraryViewModel? _plugins;
    private readonly DispatcherTimer _saveTimer;

    private TrackerInstrument? _pendingSave;

    public MachineRackViewModel(
        MachineRack rack,
        IInstrumentAudition audition,
        ObservableCollection<Recording> recordings,
        IWaveformService? waveforms = null,
        PluginLibraryViewModel? plugins = null)
    {
        _rack = rack;
        _audition = audition;
        _recordings = recordings;
        _waveforms = waveforms;
        _plugins = plugins;

        // A scan in SETTINGS can happen while this page is open, and a plugin installed since
        // startup should be offerable without restarting.
        if (plugins != null)
        {
            plugins.Plugins.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(AvailablePlugins));
                OnPropertyChanged(nameof(HasAvailablePlugins));
            };
        }

        _saveTimer = new DispatcherTimer { Interval = SaveDelay };
        _saveTimer.Tick += (_, _) => Flush();

        // The chop editor's cursor runs on the same clock, which is running exactly while
        // something is sounding. Subscribed once: this used to be done on every change of
        // machine, so after ten switches the cursor was being moved ten times a tick.
        Sounding.Ticked += MovePlayhead;

        Refresh();
    }

    /// <summary>Raised after an instrument's sound changes, so open songs can follow it.</summary>
    public event EventHandler<TrackerInstrument>? InstrumentChanged;

    /// <summary>Raised when the set of instruments changes, so pickers elsewhere follow.</summary>
    public event EventHandler? RackChanged;

    public ObservableCollection<RackMachine> Machines { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMachines))]
    private RackMachine? selected;

    /// <summary>False while a machine's own slot is picked, which cannot be deleted.</summary>
    public bool CanDelete => Selected is { IsYours: true };

    /// <summary>False for a machine and for a plugin, both of which are named elsewhere.</summary>
    public bool CanRename => Selected is { CanRename: true };

    /// <summary>Where you are, for the bar along the bottom: which machine is open.</summary>
    public string Context
    {
        get
        {
            int held = Machines.Count;
            string rack = held + (held == 1 ? " machine" : " machines");

            var open = Selected?.Instrument;

            return open == null
                ? "rack  ·  " + rack
                : "rack  ·  " + rack + "  ·  " + open.Name;
        }
    }

    [ObservableProperty] private InstrumentEditorViewModel? editor;

    [ObservableProperty] private int octave = 4;

    /// <summary>
    /// Bumped on every note. The envelope view watches it to start its playhead: a counter
    /// rather than an event keeps the view a plain binding.
    /// </summary>
    [ObservableProperty] private int noteTrigger;

    /// <summary>How long an audition holds the note, so the drawn envelope matches what you hear.</summary>
    public double HoldSeconds => TrackerPlayer.PreviewHoldSeconds;

    /// <summary>
    /// How many cycles the wave view shows. A view setting, not part of the sound, so it stays
    /// out of the patch and out of the presets.
    /// </summary>
    [ObservableProperty] private double scopeCycles = 2;

    [ObservableProperty] private string status = "Ready";

    /// <summary>True while this page is on screen, so MIDI notes come here instead of the pattern.</summary>
    [ObservableProperty] private bool isEditing;

    public bool HasMachines => Selected != null;

    /// <summary>Recordings offered as instrument sources, shared with the RECORD tab.</summary>
    public ObservableCollection<Recording> AvailableRecordings => _recordings;

    public IRelayCommand DuplicateCommand => new RelayCommand(Duplicate);
    public IAsyncRelayCommand DeleteCommand => new AsyncRelayCommand(Delete);
    public IRelayCommand TestCommand => new RelayCommand(Test);

    /// <summary>
    /// Nothing is playing this instrument here, so the lamps are shown but have nothing to say.
    /// </summary>
    public TrackLocationViewModel? Location { get; } = new(null);

    public bool HasLocation => Location?.IsLive == true;

    /// <summary>Reads the rack back off disk, keeping the selection where it can.</summary>
    public void Refresh()
    {
        string? keep = Selected?.Id;

        Rack();

        var held = _rack.List();

        Machines.Clear();

        // The machines first, in the order they are declared in, and the plugins after them.
        foreach (var machine in Machine.Ours)
        {
            var slot = held.FirstOrDefault(i => i.Id == machine.SlotId);

            if (slot != null) Machines.Add(new RackMachine(slot));
        }

        foreach (var plugin in held.Where(i => i.IsPlugin).OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase))
            Machines.Add(new RackMachine(plugin));

        Selected = Machines.FirstOrDefault(i => i.Id == keep) ?? Machines.FirstOrDefault();
    }

    /// <summary>
    /// Makes sure every machine of ours has its slot on the shelf.
    /// </summary>
    /// <remarks>
    /// Written once, the first time the rack is opened without them, and then they are
    /// ordinary files that keep whatever you set on them. Not the same thing as stocking a
    /// rack with sounds to start from: those are presets and live beside the program. These
    /// are the machines themselves, and a rack with no boxes in it is not a rack.
    /// </remarks>
    /// <summary>
    /// Brings the shelf to what a rack is: the machines, then the plugins, and nothing else.
    /// </summary>
    /// <remarks>
    /// Anything else comes off. There is no way left to make one, since a machine cannot be
    /// added and there is no duplicating, so what is there came from before the rack and is not
    /// something the program can explain any more.
    ///
    /// Moved rather than deleted. What comes off is the only copy of work somebody did, and it
    /// costs nothing to leave it in a folder beside the rest.
    ///
    /// Runs on every open and does nothing on all but the first, because afterwards the only
    /// things left are the ones it keeps.
    /// </remarks>
    private void Rack()
    {
        int retired = 0;

        foreach (var instrument in _rack.List())
        {
            if (instrument.IsPlugin || Machine.IsSlot(instrument.Id)) continue;

            string name = instrument.Name;

            if (!_rack.Retire(instrument.Id)) continue;

            retired++;

            Log.Write(LogArea.App, () => "retired '" + name + "' from the rack");
        }

        foreach (var machine in Machine.Ours)
        {
            if (_rack.Load(machine.SlotId) != null) continue;

            var made = TrackerInstrument.CreateOn(machine, machine.Name);

            made.Id = machine.SlotId;

            _rack.Save(made);
        }

        if (retired > 0)
        {
            Status = retired + (retired == 1 ? " instrument" : " instruments") +
                     " moved to instruments/retired. The rack holds the machines and your plugins.";
        }
    }

    /// <summary>A note from a MIDI keyboard, which arrives on the MIDI thread.</summary>
    public void PlayMidiNote(Note note, int volume) =>
        Dispatcher.UIThread.Post(() => PlayNote(note, volume));

    /// <summary>
    /// Somewhere to start: the shelf's other instruments on this same machine.
    /// </summary>
    /// <remarks>
    /// Rebuilt whenever the selection moves, since which instruments count as presets depends
    /// on which machine the one being edited is on.
    /// </remarks>
    [ObservableProperty] private InstrumentPresets? presets;

    /// <summary>Where the sound has got to, for the cursor over the picture.</summary>
    /// <remarks>
    /// Both pictures read the same number: the one whole recording a sampler shows, and the
    /// pieces a chopped one shows. A machine has one or the other, never both, so this sets
    /// whichever is there.
    /// </remarks>
    private void MovePlayhead()
    {
        var editor = Editor;

        if (editor == null) return;

        // Nothing lit is nothing sounding, and this is the last beat of the clock before it
        // stops. Asking the engine here would catch a voice still letting go of its release and
        // leave the line standing in the middle of the picture with nothing playing.
        double at = Sounding.Lit.Count == 0 ? -1 : _audition.SamplePosition(0);

        editor.Playhead = at;

        if (editor.Slices != null) editor.Slices.Playhead = at;
    }

    /// <summary>Which notes are sounding, for the panel's keyboard to light.</summary>
    public SoundingNotes Sounding { get; } = new();

    /// <summary>Lets go of one note played by hand, which is what a key coming up means.</summary>
    public void Let(Note note)
    {
        if (Selected?.Instrument is { } instrument) _audition.Let(instrument, note);
    }

    /// <summary>The keyboard a machine draws on its own face, standing on the same two things.</summary>
    public IMachineKeys MachineKeys => _machineKeys ??= new DesignerKeys(this);

    private IMachineKeys? _machineKeys;


    /// <summary>One note from the panel's own keyboard.</summary>
    public void Play(Note note, int volume) => PlayNote(note, volume);

    /// <summary>A note played on the computer keyboard or a MIDI keyboard while editing.</summary>
    public void PlayNote(Note note, int volume = TrackerCell.NoVolume)
    {
        var instrument = Selected?.Instrument;
        if (instrument == null)
        {
            // Silence with no explanation is the worst answer to a key press.
            Status = "Nothing to play: add an instrument to the rack first.";
            return;
        }

        double held = _audition.Audition(instrument, note, volume);

        // Lit for as long as it sounds, which for a recording is the recording's own length.
        Sounding.Struck(note, held > 0 ? held : HoldSeconds);

        // Nothing plays this page but you, so the keyboard only ever moves for a note typed
        // above or below what it is showing.
        Octave = PanelKeyboard.Reveal(note, Octave);

        NoteTrigger++;

        // Say what was heard: a silent key press is otherwise impossible to tell from a key
        // press that never arrived.
        Status = $"{note} on '{instrument.Name}'";
    }

    /// <summary>Writes anything still pending, for leaving the page or closing the app.</summary>
    public void Flush()
    {
        _saveTimer.Stop();

        var instrument = _pendingSave;
        _pendingSave = null;
        if (instrument == null) return;

        // A plugin instrument's sound lives inside the plugin, so it is read back out of the
        // running one before anything is written. Done here rather than per knob move: it
        // means serialising the whole patch, which is not free.
        Editor?.SyncPluginState();

        try
        {
            _rack.Save(instrument);
            Status = $"Saved '{instrument.Name}'";
        }
        catch (Exception ex)
        {
            Status = $"Could not save '{instrument.Name}': {ex.Message}";
        }
    }

    /// <summary>
    /// Draws the machine in front of you again, because the machine itself has changed.
    /// </summary>
    /// <remarks>
    /// A machine imported while the app is running is a new description of something that may be
    /// on screen this moment. The panel is built when an instrument is picked, so the way to
    /// draw the new one is to pick it again, which also puts down whatever the old panel was
    /// holding: a sounding note, an open plugin window, a kit watching the keyboard.
    /// </remarks>
    public void Reopen()
    {
        var was = Selected;

        if (was == null) return;

        Selected = null;
        Selected = was;
    }

    partial void OnSelectedChanged(RackMachine? value)
    {
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanRename));

        // Switching away is a good moment to write: never leave an edit only in memory.
        Flush();

        // What the machine being left was sounding stops with it. A note played on one panel
        // going on under the next one's picture, with that picture's cursor running to it, is
        // one machine wearing another's face.
        if (Editor != null) _audition.Silence(Editor.Instrument);

        // And nothing is lit or running any more, so the new panel starts dark and still
        // rather than inheriting the last one's key and cursor.
        Sounding.Silence();

        // The instrument being left may have had a plugin drawing its own interface. That
        // window goes with it rather than being left behind on a page showing somebody else.
        Editor?.ClosePlugin();

        // And its pads stop watching the keyboard, which is now somebody else's.
        Editor?.Kit?.Unfollow();

        Editor = value == null
            ? null
            : new InstrumentEditorViewModel(
                Machines.IndexOf(value), value.Instrument, OnInstrumentEdited,
                _waveforms, _audition, _recordings, note => PlayNote(note));

        Presets = value == null
            ? null
            : new InstrumentPresets(value.Instrument, Reloaded, Editor?.Takes.Shown, Editor?.Takes);

        // A kit lights its own pads, from the same set the keyboard reads.
        Editor?.Kit?.Follow(Sounding);

        // The keyboard shows the keys of the piece in hand, so playing it is playing that
        // piece rather than whatever happens to live under the octave you were left on.
        Reveal();
        Follow(Editor);
    }

    /// <summary>
    /// Keeps the keyboard on the piece that is picked.
    /// </summary>
    /// <remarks>
    /// A zone answers to its own stretch of keys and a pad to one key. Picking one and then
    /// pressing a key that belongs to another piece is the panel disagreeing with itself, so
    /// the octave moves to where the piece answers.
    /// </remarks>
    private void Follow(InstrumentEditorViewModel? editor)
    {
        if (editor?.Zones != null)
        {
            editor.Zones.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ZoneMapViewModel.Selected)) Reveal();
            };
        }

        if (editor?.Kit != null)
        {
            editor.Kit.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(DrumKitViewModel.Selected)) Reveal();
            };
        }
    }

    /// <summary>Moves the keyboard to where the piece in hand answers, if there is one.</summary>
    private void Reveal()
    {
        int? key = Editor?.Zones?.Selected?.Zone.Root ?? Editor?.Kit?.Selected?.Pad.Semitone;

        if (key is not { } semitone) return;

        Octave = PanelKeyboard.Reveal(new Note(semitone), Octave);
    }

    /// <summary>A preset has landed on the instrument being edited: reread it and write it.</summary>
    private void Reloaded()
    {
        Editor?.Reloaded();
        OnInstrumentEdited();
    }

    partial void OnIsEditingChanged(bool value)
    {
        if (!value) Flush();
    }

    /// <summary>
    /// A field in the editor changed: refresh the row, tell the songs, and queue the write.
    /// </summary>
    private void OnInstrumentEdited()
    {
        var instrument = Selected?.Instrument;
        if (instrument == null) return;

        Selected?.Refresh();
        InstrumentChanged?.Invoke(this, instrument);

        _pendingSave = instrument;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>
    /// A new instrument on the picked machine, ready to shape.
    /// </summary>
    /// <remarks>
    /// Starting from a sound you already have is what Duplicate is for. There is no third kind
    /// of thing to start from: the rack is the shelf of starting points, and every sound on
    /// it is an instrument like any other.
    /// </remarks>


    /// <summary>
    /// The plugins that can be an instrument here: the ones that take notes, in a format this
    /// host knows how to play. An effect is not offered, and neither is an instrument in a
    /// format that would load and then be silent.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<Audio.Plugins.PluginInfo> AvailablePlugins =>
        _plugins == null
            ? System.Array.Empty<Audio.Plugins.PluginInfo>()
            : _plugins.Plugins.Where(Audio.Plugins.PluginHost.CanPlay).ToList();

    public bool HasAvailablePlugins => AvailablePlugins.Count > 0;

    public IRelayCommand<Audio.Plugins.PluginInfo> NewFromPluginCommand =>
        new RelayCommand<Audio.Plugins.PluginInfo>(NewFromPlugin);

    private void NewFromPlugin(Audio.Plugins.PluginInfo? plugin)
    {
        if (plugin == null)
        {
            Status = "Pick a plugin first.";
            return;
        }

        if (!Audio.Plugins.PluginHost.CanPlay(plugin))
        {
            Status = $"'{plugin.Name}' cannot be played as an instrument here.";
            return;
        }

        Add(TrackerInstrument.CreatePlugin(UniqueName(plugin.Name), plugin));
    }


    private void Duplicate()
    {
        var source = Selected?.Instrument;
        if (source == null)
        {
            Status = "Nothing to duplicate.";
            return;
        }

        var copy = source.Clone();
        copy.Id = "";
        copy.EnsureId();
        copy.Name = UniqueName(source.Name);

        Add(copy);
    }

    private void Add(TrackerInstrument instrument)
    {
        try
        {
            _rack.Save(instrument);

            var row = new RackMachine(instrument);
            Machines.Add(row);
            Selected = row;

            RackChanged?.Invoke(this, EventArgs.Empty);
            Status = $"Added '{instrument.Name}'";
        }
        catch (Exception ex)
        {
            Status = $"Could not add the instrument: {ex.Message}";
        }
    }

    private async Task Delete()
    {
        var row = Selected;
        if (row == null) return;

        // A machine is not something you can be without. Duplicating one and deleting the copy
        // is how you throw a sound away.
        if (row.IsSlot)
        {
            Status = row.Name + " is a machine, not an instrument you made. It cannot be deleted.";
            return;
        }

        // The file goes for good, and other songs may be using it.
        bool confirmed = await ConfirmDialog.AskAsync(
            "Delete instrument",
            $"Delete '{row.Name}' from the rack? Songs that already use it keep their own copy, "
                + "but it will no longer be available to new songs. This cannot be undone.",
            "Delete");

        if (!confirmed) return;

        try
        {
            _saveTimer.Stop();
            _pendingSave = null;

            _rack.Delete(row.Id);

            int index = Machines.IndexOf(row);
            Machines.Remove(row);
            Selected = Machines.ElementAtOrDefault(Math.Min(index, Machines.Count - 1));

            // Songs already using it keep their copy: removing it here must not silence a song.
            RackChanged?.Invoke(this, EventArgs.Empty);
            Status = $"Deleted '{row.Name}'. Songs that already use it keep their copy.";
        }
        catch (Exception ex)
        {
            Status = $"Could not delete: {ex.Message}";
        }
    }

    private void Test()
    {
        var instrument = Selected?.Instrument;
        if (instrument == null)
        {
            Status = "No instrument to test.";
            return;
        }

        PlayNote(Note.FromOctave(0, Octave));
        Status = $"Testing '{instrument.Name}'";
    }

    /// <summary>A name nothing else in the rack has, so two instruments never look alike.</summary>
    private string UniqueName(string wanted)
    {
        string baseName = string.IsNullOrWhiteSpace(wanted) ? "instrument" : wanted.Trim();
        if (!Machines.Any(i => string.Equals(i.Name, baseName, StringComparison.OrdinalIgnoreCase)))
            return baseName;

        for (int number = 2; ; number++)
        {
            string candidate = $"{baseName} {number}";
            if (!Machines.Any(i => string.Equals(i.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }
    }
}
