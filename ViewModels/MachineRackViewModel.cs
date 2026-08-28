using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Diagnostics;
using JingleBox2.Audio;
using JingleBox2.Audio.Records;
using JingleBox2.Machines;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Synth;
using JingleBox2.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Machines.Interfaces;
using JingleBox2.Midi.Interfaces;
using JingleBox2.ViewModels.Interfaces;
using JingleBox2.Audio.Plugins.Records;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Machines.Interfaces;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Plugins;

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
public sealed partial class MachineRackViewModel : ObservableObject, IInstrumentDesigner, Midi.Interfaces.IPlaysNotes
{
    /// <summary>The one place that knows both plugin standards. Holds nothing, so one is enough.</summary>
    private readonly IPluginHost _host = new PluginHost();

    /// <summary>The machines this run has, the one instance everything shares.</summary>
    private readonly IMachineProjects _machines;

    /// <summary>How long the knobs have to be still before the file is written.</summary>
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(600);

    /// <summary>The shelf itself: one file per instrument, read and written here.</summary>
    private readonly MachineRack _rack;

    /// <summary>How a note is heard, borrowed from the tracker so there is one audio engine.</summary>
    private readonly IInstrumentAudition _audition;

    /// <summary>Your takes, the same list RECORD shows, offered as instrument sources.</summary>
    private readonly ObservableCollection<Recording> _recordings;

    /// <summary>Reads a sample down to peaks, so a sample instrument can show its shape.</summary>
    private readonly IWaveformService? _waveforms;

    /// <summary>The plugins this machine has, for building an instrument out of one.</summary>
    private readonly PluginLibraryViewModel? _plugins;
    /// <summary>What holds a write back until the knobs have been still for a moment.</summary>
    private readonly DispatcherTimer _saveTimer;

    /// <summary>The instrument waiting to be written, or null when nothing is.</summary>
    /// <remarks>
    /// One at a time, since moving to another instrument writes the last one out on the way past.
    /// </remarks>
    private TrackerInstrument? _pendingSave;

    /// <summary>
    /// Reads the rack, brings it to what a rack is, and starts the clocks it needs.
    /// </summary>
    /// <remarks>
    /// The plugin list is watched, because a scan in SETTINGS can happen while this page is open
    /// and a plugin installed since startup should be offerable without restarting.
    ///
    /// The chop editor's cursor runs on the sounding clock, which is running exactly while
    /// something is sounding. Subscribed once here: it used to be done on every change of
    /// machine, so after ten switches the cursor was being moved ten times a tick.
    /// </remarks>
    public MachineRackViewModel(
        MachineRack rack,
        IInstrumentAudition audition,
        IMachineProjects machines,
        ObservableCollection<Recording> recordings,
        IWaveformService? waveforms = null,
        PluginLibraryViewModel? plugins = null)
    {
        _machines = machines;
        _rack = rack;
        _audition = audition;
        _recordings = recordings;
        _waveforms = waveforms;
        _plugins = plugins;

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

        Sounding.Ticked += MovePlayhead;

        Refresh();
    }

    /// <summary>Raised after an instrument's sound changes, so open songs can follow it.</summary>
    public event EventHandler<TrackerInstrument>? InstrumentChanged;

    /// <summary>Raised when the set of instruments changes, so pickers elsewhere follow.</summary>
    public event EventHandler? RackChanged;

    /// <summary>What is on the rack: the machines in the order they are declared, then plugins.</summary>
    public ObservableCollection<RackMachine> Machines { get; } = new();

    /// <summary>Which one is open, and so what the panel below is showing.</summary>
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

    /// <inheritdoc/>
    /// <remarks>Made afresh whenever the selection moves, so it is never the last machine's.</remarks>
    [ObservableProperty] private InstrumentEditorViewModel? editor;

    /// <inheritdoc/>
    /// <remarks>
    /// The rack's own, not the song's. Nothing plays this page but you, so it only ever moves for
    /// a note typed above or below what the keyboard is showing.
    /// </remarks>
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

    /// <summary>The line along the bottom: what the last thing done here did, or why it did not.</summary>
    [ObservableProperty] private string status = "Ready";

    /// <summary>True while this page is on screen, so MIDI notes come here instead of the pattern.</summary>
    [ObservableProperty] private bool isEditing;

    /// <summary>True when something is open, so the page can show the panel rather than a blank.</summary>
    public bool HasMachines => Selected != null;

    /// <summary>Recordings offered as instrument sources, shared with the RECORD tab.</summary>
    public ObservableCollection<Recording> AvailableRecordings => _recordings;

    /// <summary>Makes a copy of the open instrument under a name nothing else has.</summary>
    /// <remarks>Always enabled; with nothing open it says so in the status line instead.</remarks>
    public IRelayCommand DuplicateCommand => new RelayCommand(Duplicate);

    /// <summary>Throws the open instrument away, after asking.</summary>
    /// <remarks>
    /// Always enabled, and refuses a machine's own slot with a reason rather than being greyed:
    /// <see cref="CanDelete"/> is what the button's own look is bound to.
    /// </remarks>
    public IAsyncRelayCommand DeleteCommand => new AsyncRelayCommand(Delete);

    /// <summary>Plays one note, so what has just been changed can be heard.</summary>
    /// <remarks>Always enabled; with nothing open it says so in the status line.</remarks>
    public IRelayCommand TestCommand => new RelayCommand(Test);

    /// <summary>
    /// Nothing is playing this instrument here, so the lamps are shown but have nothing to say.
    /// </summary>
    public TrackLocationViewModel? Location { get; } = new(null);

    /// <inheritdoc/>
    /// <remarks>Never, here: the rack edits an instrument nothing is playing.</remarks>
    public bool HasLocation => Location?.IsLive == true;

    /// <summary>The same lamps, for a machine that draws them on its own face.</summary>
    public Machines.Interfaces.IMachineLocation? MachineLocation =>
        _place ??= Location is { } place ? new Tracker.Machines.TrackLocation(place) : null;

    /// <inheritdoc cref="MachineLocation"/>
    private Machines.Interfaces.IMachineLocation? _place;

    /// <summary>Reads the rack back off disk, keeping the selection where it can.</summary>
    /// <remarks>
    /// The machines first, in the order they are declared in, and the plugins after them by name.
    /// A machine's place on the rack is a fact about the program and does not move; a plugin's is
    /// alphabetical because there is nothing else to sort them by.
    /// </remarks>
    public void Refresh()
    {
        string? keep = Selected?.Id;

        Rack();

        var held = _rack.List();

        Machines.Clear();

        foreach (var machine in Machine.Installed)
        {
            var slot = held.FirstOrDefault(i => i.Id == machine.SlotId);

            if (slot != null) Machines.Add(new RackMachine(slot));
        }

        foreach (var plugin in held.Where(i => i.IsPlugin).OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase))
            Machines.Add(new RackMachine(plugin));

        Selected = Machines.FirstOrDefault(i => i.Id == keep) ?? Machines.FirstOrDefault();
    }

    /// <summary>
    /// Brings the shelf to what a rack is: the machines, then the plugins, and nothing else.
    /// </summary>
    /// <remarks>
    /// Every machine installed here gets its slot on the shelf. Written once, the first time the
    /// rack is opened without them, and then they are ordinary files that keep whatever you set on
    /// them. Not the same thing as stocking a rack with sounds to start from: those are presets
    /// and live inside the machine. These are the machines themselves.
    ///
    /// Installed here, and not simply ours. A machine thrown out in SETTINGS has no panel to draw
    /// and no presets to offer, so there is nothing a box on the rack could do; its slot stays on
    /// the shelf untouched and the box comes back when the machine does.
    ///
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

            Log.Write(LogArea.Machines, () => "retired '" + name + "' from the rack");
        }

        foreach (var machine in Machine.Installed)
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

    /// <summary>
    /// A note from a MIDI keyboard, which arrives on the MIDI thread.
    /// </summary>
    /// <remarks>
    /// The key is shown down as well as played. A hand on the hardware is a hand on a key, and
    /// the panel's keyboard is where you look to see which one: it has lit for a mouse and for
    /// the computer keyboard since it stopped lighting from what was sounding, and the hardware
    /// was the one door into it that was never told.
    /// </remarks>
    public void PlayMidiNote(Note note, int volume) =>
        Dispatcher.UIThread.Post(() => PlayNote(note, volume));

    /// <summary>And that key coming up, which lets the note go.</summary>
    public void ReleaseMidiNote(Note note) => Dispatcher.UIThread.Post(() => Let(note));

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
    ///
    /// Nothing lit is nothing sounding, and this is the last beat of the clock before it stops.
    /// Asking the engine there would catch a voice still letting go of its release and leave the
    /// line standing in the middle of the picture with nothing playing.
    /// </remarks>
    private void MovePlayhead()
    {
        var editor = Editor;

        if (editor == null) return;

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

    /// <inheritdoc cref="MachineKeys"/>
    private IMachineKeys? _machineKeys;

    /// <summary>Which keys are down, which is the application's one monitor of the notes.</summary>
    public Midi.Interfaces.IMidiMonitor? MidiKeys { get; set; }

    /// <summary>One note from the panel's own keyboard.</summary>
    public void Play(Note note, int volume) => PlayNote(note, volume);

    /// <summary>
    /// A note played on the computer keyboard, on a drawn key, or on the hardware while editing.
    /// </summary>
    /// <remarks>
    /// The key is lit for as long as the note sounds, which for a recording is the recording's own
    /// length rather than a fixed moment.
    ///
    /// What was heard is said in the status line, and so is a key press that could not be played,
    /// because silence with no explanation is the worst answer to a key press: a silent one is
    /// otherwise impossible to tell from one that never arrived.
    /// </remarks>
    public void PlayNote(Note note, int volume = TrackerCell.NoVolume)
    {
        var instrument = Selected?.Instrument;
        if (instrument == null)
        {
            Status = "Nothing to play: add an instrument to the rack first.";
            return;
        }

        double held = _audition.Audition(instrument, note, volume);

        Sounding.Struck(note, held > 0 ? held : HoldSeconds);

        Octave = PanelKeyboard.Reveal(note, Octave);

        NoteTrigger++;

        Status = $"{note} on '{instrument.Name}'";
    }

    /// <summary>Writes anything still pending, for leaving the page or closing the app.</summary>
    /// <remarks>
    /// A plugin instrument's sound lives inside the plugin, so it is read back out of the running
    /// one before anything is written. Done here rather than per knob move: it means serialising
    /// the whole patch, which is a round trip to another process and a third of a megabyte.
    /// </remarks>
    public void Flush()
    {
        _saveTimer.Stop();

        var instrument = _pendingSave;
        _pendingSave = null;
        if (instrument == null) return;

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
    /// Another machine was picked, so everything the last one had going stops and a fresh panel
    /// is built.
    /// </summary>
    /// <remarks>
    /// Switching away is a good moment to write: never leave an edit only in memory.
    ///
    /// What the machine being left was sounding stops with it. A note played on one panel going on
    /// under the next one's picture, with that picture's cursor running to it, is one machine
    /// wearing another's face. Nothing is left lit or running either, so the new panel starts dark
    /// and still rather than inheriting the last one's key and cursor.
    ///
    /// A plugin drawing its own window goes with the instrument that had it, rather than being
    /// left behind on a page showing somebody else, and the old kit stops watching the keyboard,
    /// which is now somebody else's.
    ///
    /// The keyboard then moves to the piece that is picked, so playing it plays that piece rather
    /// than whatever happens to live under the octave you were left on.
    /// </remarks>
    partial void OnSelectedChanged(RackMachine? value)
    {
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanRename));

        Flush();

        if (Editor != null) _audition.Silence(Editor.Instrument);

        Sounding.Silence();

        Editor?.ClosePlugin();

        Editor?.Kit?.Unfollow();

        Editor = value == null
            ? null
            : new InstrumentEditorViewModel(
                Machines.IndexOf(value), value.Instrument, OnInstrumentEdited, _machines,
                _waveforms, _audition, _recordings, note => PlayNote(note));

        Presets = value == null
            ? null
            : new InstrumentPresets(value.Instrument, Reloaded, _machines, Editor?.Takes.Shown, Editor?.Takes);

        Editor?.Kit?.Follow(Sounding);

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

    /// <summary>Leaving the page writes whatever was still waiting on the clock.</summary>
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
    /// The plugins that can be an instrument here: the ones that take notes, in a format this
    /// host knows how to play. An effect is not offered, and neither is an instrument in a
    /// format that would load and then be silent.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<Audio.Plugins.Records.PluginInfo> AvailablePlugins =>
        _plugins == null
            ? System.Array.Empty<Audio.Plugins.Records.PluginInfo>()
            : _plugins.Plugins.Where(_host.CanPlay).ToList();

    /// <summary>True when there is any plugin worth offering, so the menu can be hidden.</summary>
    public bool HasAvailablePlugins => AvailablePlugins.Count > 0;

    /// <summary>Puts a plugin on the rack as an instrument of its own.</summary>
    /// <remarks>
    /// Always enabled. A plugin that cannot be played here is refused with a reason in the status
    /// line, which is more use than a greyed row nobody can ask about.
    /// </remarks>
    public IRelayCommand<Audio.Plugins.Records.PluginInfo> NewFromPluginCommand =>
        new RelayCommand<Audio.Plugins.Records.PluginInfo>(NewFromPlugin);

    /// <summary>Makes an instrument on that plugin, or says why it cannot be one.</summary>
    private void NewFromPlugin(Audio.Plugins.Records.PluginInfo? plugin)
    {
        if (plugin == null)
        {
            Status = "Pick a plugin first.";
            return;
        }

        if (!_host.CanPlay(plugin))
        {
            Status = $"'{plugin.Name}' cannot be played as an instrument here.";
            return;
        }

        Add(TrackerInstrument.CreatePlugin(UniqueName(plugin.Name), plugin));
    }

    /// <summary>
    /// Copies the open instrument, under a name nothing else on the rack has.
    /// </summary>
    /// <remarks>
    /// The copy is given an id of its own rather than the original's, or the rack would hold two
    /// files claiming to be the same instrument and a song would reach whichever was read last.
    ///
    /// This is how a sound is thrown away as well as kept: a machine cannot be deleted, so
    /// duplicating one and deleting the copy is the whole of what deleting means here.
    /// </remarks>
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

    /// <summary>Writes a new instrument to the shelf, puts it on the list and opens it.</summary>
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

    /// <summary>
    /// Throws an instrument off the rack, after asking.
    /// </summary>
    /// <remarks>
    /// A machine is refused: it is not something you can be without, and duplicating one and
    /// deleting the copy is how a sound is thrown away.
    ///
    /// The file goes for good, so it is asked about first. Songs already using it keep their own
    /// copy, which is why removing it here cannot silence anything that is already written.
    /// </remarks>
    private async Task Delete()
    {
        var row = Selected;
        if (row == null) return;

        if (row.IsSlot)
        {
            Status = row.Name + " is a machine, not an instrument you made. It cannot be deleted.";
            return;
        }

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

            RackChanged?.Invoke(this, EventArgs.Empty);
            Status = $"Deleted '{row.Name}'. Songs that already use it keep their copy.";
        }
        catch (Exception ex)
        {
            Status = $"Could not delete: {ex.Message}";
        }
    }

    /// <summary>Plays one note at the octave the page is on, so the sound can be judged.</summary>
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
