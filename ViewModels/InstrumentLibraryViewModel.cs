using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio;
using JingleBox2.Models;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Synth;
using JingleBox2.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace JingleBox2.ViewModels;

/// <summary>
/// The instrument library: the voices you own, kept outside any song. A preset is where a new
/// one starts; after that the instrument is its own thing and every song can use it.
/// </summary>
/// <remarks>
/// Edits save themselves. There is no Save button here on purpose: an instrument is a small
/// document of its own, and a knob you turned is not a change you should have to remember to
/// keep. Writes are held back until the turning stops.
/// </remarks>
public sealed partial class InstrumentLibraryViewModel : ObservableObject, IInstrumentDesigner
{
    /// <summary>How long the knobs have to be still before the file is written.</summary>
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(600);

    private readonly InstrumentLibrary _library;
    private readonly IInstrumentAudition _audition;
    private readonly ObservableCollection<Recording> _recordings;

    /// <summary>Reads a sample down to peaks, so a sample instrument can show its shape.</summary>
    private readonly IWaveformService? _waveforms;

    /// <summary>The plugins this machine has, for building an instrument out of one.</summary>
    private readonly PluginLibraryViewModel? _plugins;
    private readonly DispatcherTimer _saveTimer;

    private TrackerInstrument? _pendingSave;

    public InstrumentLibraryViewModel(
        InstrumentLibrary library,
        IInstrumentAudition audition,
        ObservableCollection<Recording> recordings,
        IWaveformService? waveforms = null,
        PluginLibraryViewModel? plugins = null)
    {
        _library = library;
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

        Refresh();
    }

    /// <summary>Raised after an instrument's sound changes, so open songs can follow it.</summary>
    public event EventHandler<TrackerInstrument>? InstrumentChanged;

    /// <summary>Raised when the set of instruments changes, so pickers elsewhere follow.</summary>
    public event EventHandler? LibraryChanged;

    public ObservableCollection<LibraryInstrument> Instruments { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInstruments))]
    private LibraryInstrument? selected;

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

    public bool HasInstruments => Selected != null;

    /// <summary>Recordings offered as instrument sources, shared with the RECORD tab.</summary>
    public ObservableCollection<Recording> AvailableRecordings => _recordings;

    public IRelayCommand NewSynthCommand => new RelayCommand(NewSynth);
    public IRelayCommand<Recording> NewFromRecordingCommand => new RelayCommand<Recording>(NewFromRecording);
    public IRelayCommand DuplicateCommand => new RelayCommand(Duplicate);
    public IAsyncRelayCommand DeleteCommand => new AsyncRelayCommand(Delete);
    public IRelayCommand TestCommand => new RelayCommand(Test);

    public IRelayCommand OctaveDownCommand => new RelayCommand(() => Octave = Math.Max(0, Octave - 1));

    public IRelayCommand OctaveUpCommand => new RelayCommand(() => Octave = Math.Min(9, Octave + 1));

    /// <summary>
    /// Nothing is playing this instrument here, so the lamps are shown but have nothing to say.
    /// </summary>
    public TrackLocationViewModel? Location { get; } = new(null);

    public bool HasLocation => Location?.IsLive == true;

    /// <summary>Reads the library back off disk, keeping the selection where it can.</summary>
    public void Refresh()
    {
        string? keep = Selected?.Id;

        Instruments.Clear();
        foreach (var instrument in _library.List())
            Instruments.Add(new LibraryInstrument(instrument));

        Selected = Instruments.FirstOrDefault(i => i.Id == keep) ?? Instruments.FirstOrDefault();
    }

    /// <summary>A note from a MIDI keyboard, which arrives on the MIDI thread.</summary>
    public void PlayMidiNote(Note note, int volume) =>
        Dispatcher.UIThread.Post(() => PlayNote(note, volume));

    /// <summary>A note played on the computer keyboard or a MIDI keyboard while editing.</summary>
    public void PlayNote(Note note, int volume = TrackerCell.NoVolume)
    {
        var instrument = Selected?.Instrument;
        if (instrument == null)
        {
            // Silence with no explanation is the worst answer to a key press.
            Status = "Nothing to play: add an instrument to the library first.";
            return;
        }

        _audition.Audition(instrument, note, volume);

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
            _library.Save(instrument);
            Status = $"Saved '{instrument.Name}'";
        }
        catch (Exception ex)
        {
            Status = $"Could not save '{instrument.Name}': {ex.Message}";
        }
    }

    partial void OnSelectedChanged(LibraryInstrument? value)
    {
        // Switching away is a good moment to write: never leave an edit only in memory.
        Flush();

        // The instrument being left may have had a plugin drawing its own interface. That
        // window goes with it rather than being left behind on a page showing somebody else.
        Editor?.ClosePlugin();

        Editor = value == null
            ? null
            : new InstrumentEditorViewModel(Instruments.IndexOf(value), value.Instrument, OnInstrumentEdited, _waveforms, _audition);
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
    /// of thing to start from: the library is the shelf of starting points, and every sound on
    /// it is an instrument like any other.
    /// </remarks>
    private void NewSynth()
    {
        var machine = SelectedMachine ?? Machine.Ouroboros;

        Add(TrackerInstrument.CreateOn(machine, UniqueName(machine.Name)));
    }

    /// <summary>The machines a new instrument can be built on. A plugin is picked separately.</summary>
    public System.Collections.Generic.IReadOnlyList<Machine> Machines { get; } = Machine.Ours;

    /// <summary>Which machine + New builds on.</summary>
    [ObservableProperty] private Machine? selectedMachine = Machine.Ouroboros;

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

    private void NewFromRecording(Recording? recording)
    {
        if (recording == null)
        {
            Status = "Pick a recording first.";
            return;
        }

        Add(TrackerInstrument.CreateSample(UniqueName(recording.Name), recording.FilePath, Note.C4));
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
            _library.Save(instrument);

            var row = new LibraryInstrument(instrument);
            Instruments.Add(row);
            Selected = row;

            LibraryChanged?.Invoke(this, EventArgs.Empty);
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

        // The file goes for good, and other songs may be using it.
        bool confirmed = await ConfirmDialog.AskAsync(
            "Delete instrument",
            $"Delete '{row.Name}' from the library? Songs that already use it keep their own copy, "
                + "but it will no longer be available to new songs. This cannot be undone.",
            "Delete");

        if (!confirmed) return;

        try
        {
            _saveTimer.Stop();
            _pendingSave = null;

            _library.Delete(row.Id);

            int index = Instruments.IndexOf(row);
            Instruments.Remove(row);
            Selected = Instruments.ElementAtOrDefault(Math.Min(index, Instruments.Count - 1));

            // Songs already using it keep their copy: removing it here must not silence a song.
            LibraryChanged?.Invoke(this, EventArgs.Empty);
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

    /// <summary>A name nothing else in the library has, so two instruments never look alike.</summary>
    private string UniqueName(string wanted)
    {
        string baseName = string.IsNullOrWhiteSpace(wanted) ? "instrument" : wanted.Trim();
        if (!Instruments.Any(i => string.Equals(i.Name, baseName, StringComparison.OrdinalIgnoreCase)))
            return baseName;

        for (int number = 2; ; number++)
        {
            string candidate = $"{baseName} {number}";
            if (!Instruments.Any(i => string.Equals(i.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }
    }
}
