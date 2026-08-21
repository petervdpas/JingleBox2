using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
public sealed partial class InstrumentLibraryViewModel : ObservableObject
{
    /// <summary>How long the knobs have to be still before the file is written.</summary>
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(600);

    private readonly InstrumentLibrary _library;
    private readonly SynthPresetStore _presets = new();
    private readonly IInstrumentAudition _audition;
    private readonly ObservableCollection<Recording> _recordings;
    private readonly DispatcherTimer _saveTimer;

    private TrackerInstrument? _pendingSave;

    public InstrumentLibraryViewModel(
        InstrumentLibrary library,
        IInstrumentAudition audition,
        ObservableCollection<Recording> recordings)
    {
        _library = library;
        _audition = audition;
        _recordings = recordings;

        _saveTimer = new DispatcherTimer { Interval = SaveDelay };
        _saveTimer.Tick += (_, _) => Flush();

        Refresh();
        RefreshPresets();
    }

    /// <summary>Raised after an instrument's sound changes, so open songs can follow it.</summary>
    public event EventHandler<TrackerInstrument>? InstrumentChanged;

    /// <summary>Raised when the set of instruments changes, so pickers elsewhere follow.</summary>
    public event EventHandler? LibraryChanged;

    public ObservableCollection<LibraryInstrument> Instruments { get; } = new();

    public ObservableCollection<SynthPreset> Presets { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInstruments))]
    private LibraryInstrument? selected;

    [ObservableProperty] private InstrumentEditorViewModel? editor;

    [ObservableProperty] private SynthPreset? selectedPreset;

    /// <summary>The name Save as preset writes under. Typed, not taken from the instrument.</summary>
    [ObservableProperty] private string presetName = "";

    [ObservableProperty] private int octave = 4;

    [ObservableProperty] private string status = "Ready";

    /// <summary>True while this page is on screen, so MIDI notes come here instead of the pattern.</summary>
    [ObservableProperty] private bool isEditing;

    public bool HasInstruments => Selected != null;

    /// <summary>Recordings offered as instrument sources, shared with the RECORD tab.</summary>
    public ObservableCollection<Recording> AvailableRecordings => _recordings;

    public IRelayCommand NewFromPresetCommand => new RelayCommand(NewFromPreset);
    public IRelayCommand<Recording> NewFromRecordingCommand => new RelayCommand<Recording>(NewFromRecording);
    public IRelayCommand DuplicateCommand => new RelayCommand(Duplicate);
    public IAsyncRelayCommand DeleteCommand => new AsyncRelayCommand(Delete);
    public IRelayCommand TestCommand => new RelayCommand(Test);
    public IRelayCommand ApplyPresetCommand => new RelayCommand(ApplyPreset);
    public IRelayCommand SaveAsPresetCommand => new RelayCommand(SaveAsPreset);
    public IAsyncRelayCommand DeletePresetCommand => new AsyncRelayCommand(DeletePreset);
    public IRelayCommand ResetPresetsCommand => new RelayCommand(ResetPresets);

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

        Editor = value == null
            ? null
            : new InstrumentEditorViewModel(Instruments.IndexOf(value), value.Instrument, OnInstrumentEdited);

        if (value?.Instrument.IsSynth == true) PresetName = value.Name;
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

    private void NewFromPreset()
    {
        // No preset picked is a fair way to ask for a plain one.
        var patch = SelectedPreset?.Patch ?? new SynthPatch();
        string baseName = SelectedPreset?.Name ?? "synth";

        Add(TrackerInstrument.CreateSynth(UniqueName(baseName), patch));
    }

    private void NewFromRecording(Recording? recording)
    {
        if (recording == null)
        {
            Status = "Pick a recording first.";
            return;
        }

        var instrument = new TrackerInstrument
        {
            Name = UniqueName(recording.Name),
            FilePath = recording.FilePath,
            BaseNote = Note.C4
        };

        instrument.EnsureId();
        Add(instrument);
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

    private void RefreshPresets()
    {
        string? keep = SelectedPreset?.Name;

        Presets.Clear();
        foreach (var preset in _presets.List())
            Presets.Add(preset);

        SelectedPreset = Presets.FirstOrDefault(p => p.Name == keep);
    }

    /// <summary>Drops a preset's sound onto the instrument that is already open.</summary>
    private void ApplyPreset()
    {
        var preset = SelectedPreset;
        var instrument = Selected?.Instrument;

        if (preset == null || instrument == null || !instrument.IsSynth)
        {
            Status = "Pick a synth instrument and a preset first.";
            return;
        }

        instrument.Patch = preset.Patch.Clone();

        // The editor holds a view model over the old patch object, so it is rebuilt.
        OnSelectedChanged(Selected);
        OnInstrumentEdited();

        PresetName = preset.Name;
        Status = $"'{instrument.Name}' now sounds like preset '{preset.Name}'";
    }

    private void SaveAsPreset()
    {
        var instrument = Selected?.Instrument;
        if (instrument == null || !instrument.IsSynth)
        {
            Status = "Only a synth instrument can be saved as a preset.";
            return;
        }

        string typed = PresetName?.Trim() ?? "";
        if (typed.Length == 0)
        {
            Status = "Give the preset a name before saving it.";
            return;
        }

        try
        {
            // Punctuation and spaces are cleaned up, so say which name it actually went under.
            string name = SynthPresetStore.SafeName(typed);
            bool replaced = _presets.Exists(name);

            _presets.Save(name, instrument.Patch);

            RefreshPresets();
            SelectedPreset = Presets.FirstOrDefault(p => p.Name == name);
            PresetName = name;

            Status = replaced ? $"Replaced preset '{name}'" : $"Saved preset '{name}'";
        }
        catch (Exception ex)
        {
            Status = $"Preset save failed: {ex.Message}";
        }
    }

    private async Task DeletePreset()
    {
        var preset = SelectedPreset;
        if (preset == null) return;

        // A starter with no file of its own has nothing to delete, so there is nothing to ask.
        if (!_presets.Exists(preset.Name))
        {
            Status = $"'{preset.Name}' is a starter preset and cannot be deleted.";
            return;
        }

        bool confirmed = await ConfirmDialog.AskAsync(
            "Delete preset",
            $"Delete the preset '{preset.Name}'? Instruments already built from it are left alone. "
                + "This cannot be undone.",
            "Delete");

        if (!confirmed) return;

        try
        {
            bool removed = _presets.Delete(preset.Name);
            RefreshPresets();

            // A starter comes straight back after its file goes, so say what actually happened
            // rather than claiming it is gone.
            bool stillListed = Presets.Any(p => string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase));

            Status = stillListed
                ? removed
                    ? $"'{preset.Name}' is a starter preset, so it went back to the built-in version."
                    : $"'{preset.Name}' is a starter preset and cannot be deleted."
                : $"Deleted preset '{preset.Name}'";
        }
        catch (Exception ex)
        {
            Status = $"Preset delete failed: {ex.Message}";
        }
    }

    private void ResetPresets()
    {
        try
        {
            _presets.ResetStarters();
            RefreshPresets();
            Status = "Starter presets restored";
        }
        catch (Exception ex)
        {
            Status = $"Preset reset failed: {ex.Message}";
        }
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
