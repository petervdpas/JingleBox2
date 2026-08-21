// ===============================
// ViewModels/MainViewModel.cs
// ===============================
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio;
using JingleBox2.Config;
using JingleBox2.Midi;
using JingleBox2.Tracker;
using JingleBox2.Models;
using JingleBox2.UI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace JingleBox2.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAudioEngine _audio;
    private readonly ConfigStore _store;
    private readonly AppConfig _cfg;
    private readonly Func<Task<string?>> _pickFileAsync;

    private bool _suspendSave;

    public MidiViewModel Midi { get; }
    public RecordViewModel Record { get; }
    public TrackerViewModel Tracker { get; }
    public InstrumentLibraryViewModel Instruments { get; }

    public ObservableCollection<OutputDevice> OutputDevices { get; } = new();
    public ObservableCollection<PadViewModel> Pads { get; } = new();

    // PADS header
    public ObservableCollection<string> ProfileNames { get; } = new();

    // THEME picker
    public ObservableCollection<string> ThemeNames { get; } = new()
    {
        "Dark",
        "Neon",
        "Industrial",
        "Light"
    };

    [ObservableProperty] private string selectedTheme = "Dark";

    [ObservableProperty] private OutputDevice? selectedOutputDevice;

    // Selected profile name (bind to ComboBox + also show in USE)
    [ObservableProperty] private string selectedProfileName = "default";

    [ObservableProperty] private string newProfileName = "";

    // Matrix size (rows x columns)
    [ObservableProperty] private int rows = 4;
    [ObservableProperty] private int columns = 2;

    public int PadCount => Rows * Columns;

    // Validation message for matrix size
    [ObservableProperty] private string matrixSizeError = "";

    public bool IsMatrixSizeValid => string.IsNullOrEmpty(MatrixSizeError);

    // Event to notify window to resize for square pads
    public event Action<int, int>? MatrixSizeChanged;

    public IRelayCommand AddProfileCommand { get; }
    public IRelayCommand DeleteProfileCommand { get; }
    public IRelayCommand ApplyMatrixSizeCommand { get; }

    public MainViewModel(
        IAudioEngine audio,
        Func<Task<string?>> pickFileAsync,
        ConfigStore store,
        AppConfig cfg,
        IMidiService midiService,
        IRecordingService recordingService,
        IWaveformService waveformService)
    {
        _audio = audio;
        _pickFileAsync = pickFileAsync;
        _store = store;
        _cfg = cfg;

        Midi = new MidiViewModel(store, cfg, midiService);
        Record = new RecordViewModel(recordingService, new LevelMeterService(), waveformService, store, cfg);

        // Instruments are their own library, shared by every song, and built from recordings
        // on the INSTRUMENTS tab. The tracker borrows the library to fill a song's slots.
        var library = new InstrumentLibrary();

        Tracker = new TrackerViewModel(audio, library, Record.Recordings);
        Instruments = new InstrumentLibraryViewModel(library, Tracker, Record.Recordings);

        Instruments.InstrumentChanged += (_, instrument) => Tracker.ApplyLibraryEdit(instrument);
        Instruments.LibraryChanged += (_, _) => Tracker.RefreshLibrary();

        // And the other way: a song can put one of its own instruments into the library.
        Tracker.LibraryChanged += (_, _) => Instruments.Refresh();

        AddProfileCommand = new RelayCommand(AddProfile);
        DeleteProfileCommand = new RelayCommand(DeleteProfile);
        ApplyMatrixSizeCommand = new RelayCommand(ApplyMatrixSize, CanApplyMatrixSize);

        // Devices
        foreach (var d in _audio.GetOutputDevices())
            OutputDevices.Add(d);

        SelectedOutputDevice =
            OutputDevices.FirstOrDefault(d => d.Id == _cfg.SelectedOutputDeviceId)
            ?? OutputDevices.FirstOrDefault();

        if (SelectedOutputDevice != null)
            _audio.SetOutputDevice(SelectedOutputDevice.Id);

        // Load matrix size from config
        Rows = _cfg.Rows;
        Columns = _cfg.Columns;

        // Ensure profiles exist + list them
        EnsureProfilesInitialized(PadCount);
        RefreshProfilesList();

        // Pick initial selected profile (must match an item in ProfileNames)
        var wanted = string.IsNullOrWhiteSpace(_cfg.SelectedProfile) ? "default" : _cfg.SelectedProfile.Trim();
        var resolved = ProfileNames.FirstOrDefault(n => string.Equals(n, wanted, StringComparison.OrdinalIgnoreCase))
                      ?? ProfileNames.FirstOrDefault()
                      ?? "default";

        _suspendSave = true;
        try
        {
            SelectedProfileName = resolved;
            _cfg.SelectedProfile = resolved;

            // Theme: load from config, validate against known themes
            var t = string.IsNullOrWhiteSpace(_cfg.SelectedTheme) ? "Dark" : _cfg.SelectedTheme.Trim();
            SelectedTheme = ThemeNames.FirstOrDefault(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)) ?? "Dark";
            _cfg.SelectedTheme = SelectedTheme;
        }
        finally
        {
            _suspendSave = false;
        }

        // Pads
        BuildPadsFromSelectedProfile(PadCount);

        // MIDI routing: global, profile-independent mapping. Which controller reaches which
        // half of the app is decided by the roles in SETTINGS, not here.
        var padRouter = new MidiRouter(_cfg.Midi, new PadTriggerAdapter(Pads));
        var noteRouter = new MidiNoteRouter(new TrackerNoteAdapter(Tracker, Instruments));
        var dispatcher = new MidiDispatcher(_cfg.Midi, padRouter.Handle, noteRouter.Handle);

        // NOTE: MidiViewModel already subscribes for learn/status.
        // This subscription is for playing things.
        midiService.MessageReceived += (_, msg) => dispatcher.Handle(msg);

        // Apply initial theme once
        ThemeManager.Apply(SelectedTheme);

        PropertyChanged += OnMainChanged;
    }

    partial void OnRowsChanged(int value)
    {
        ValidateMatrixSize();
        (ApplyMatrixSizeCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    partial void OnColumnsChanged(int value)
    {
        ValidateMatrixSize();
        (ApplyMatrixSizeCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void ValidateMatrixSize()
    {
        int total = Rows * Columns;
        if (Rows < 1 || Columns < 1)
            MatrixSizeError = "Rows and columns must be at least 1";
        else if (total < 4)
            MatrixSizeError = $"Minimum 4 pads required (current: {total})";
        else if (total > 16)
            MatrixSizeError = $"Maximum 16 pads allowed (current: {total})";
        else
            MatrixSizeError = "";

        OnPropertyChanged(nameof(PadCount));
        OnPropertyChanged(nameof(IsMatrixSizeValid));
    }

    private bool CanApplyMatrixSize() => IsMatrixSizeValid && (Rows != _cfg.Rows || Columns != _cfg.Columns);

    private void ApplyMatrixSize()
    {
        if (!IsMatrixSizeValid) return;

        // Save current pads into profile
        SavePadsIntoProfile(_cfg.SelectedProfile);

        // Update config
        _cfg.Rows = Rows;
        _cfg.Columns = Columns;

        // Resize audio engine
        _audio.Resize(PadCount);

        // Rebuild pads
        EnsureProfilesInitialized(PadCount);
        BuildPadsFromSelectedProfile(PadCount);

        // Update MIDI router pad count
        Midi.UpdatePadCount(PadCount);

        // Save
        _store.Save(_cfg);

        // Notify window to resize for square pads
        MatrixSizeChanged?.Invoke(Rows, Columns);

        (ApplyMatrixSizeCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    partial void OnSelectedProfileNameChanged(string value)
    {
        if (_suspendSave) return;

        var name = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        EnsureProfilesInitialized(PadCount);

        // Persist edits of current pads into currently selected profile BEFORE switching
        SavePadsIntoProfile(_cfg.SelectedProfile);

        // Switch selection to the requested name (must exist)
        _cfg.SelectedProfile = EnsureProfileExistsAndReturnResolved(name, padCount: PadCount);

        _store.Save(_cfg);

        _suspendSave = true;
        try
        {
            RefreshProfilesList();

            // Make sure SelectedProfileName matches an existing item (so ComboBox shows it)
            SelectedProfileName =
                ProfileNames.FirstOrDefault(n => string.Equals(n, _cfg.SelectedProfile, StringComparison.OrdinalIgnoreCase))
                ?? "default";

            ApplySelectedProfileToPads();
        }
        finally
        {
            _suspendSave = false;
        }
    }

    partial void OnSelectedThemeChanged(string value)
    {
        if (_suspendSave) return;

        var t = string.IsNullOrWhiteSpace(value) ? "Dark" : value.Trim();
        var resolved = ThemeNames.FirstOrDefault(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)) ?? "Dark";

        _cfg.SelectedTheme = resolved;
        ThemeManager.Apply(resolved);

        _store.Save(_cfg);
    }

    private void OnMainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suspendSave) return;

        if (e.PropertyName == nameof(SelectedOutputDevice))
        {
            if (SelectedOutputDevice != null)
                _audio.SetOutputDevice(SelectedOutputDevice.Id);

            SaveNow();
        }
    }

    private void OnPadChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suspendSave) return;
        SaveNow();
    }

    private void AddProfile()
    {
        var raw = (NewProfileName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(raw)) return;

        var name = NormalizeProfileName(raw);
        if (string.IsNullOrWhiteSpace(name)) return;

        EnsureProfilesInitialized(padCount: PadCount);

        if (_cfg.Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            return;

        // Persist current edits into current selected profile first
        SavePadsIntoProfile(_cfg.SelectedProfile);

        // IMPORTANT: new profile must be a CLEAN SLATE
        var padCount = PadCount;
        var newProfile = new ConfigProfile
        {
            Name = name,
            Pads = CreateDefaultPads(padCount)
        };

        _cfg.Profiles.Add(newProfile);
        _cfg.SelectedProfile = name;

        _store.Save(_cfg);

        _suspendSave = true;
        try
        {
            RefreshProfilesList();
            SelectedProfileName = ProfileNames.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) ?? name;

            ApplySelectedProfileToPads();
            NewProfileName = "";
        }
        finally
        {
            _suspendSave = false;
        }
    }

    private void DeleteProfile()
    {
        var cur = (_cfg.SelectedProfile ?? "default").Trim();
        if (string.IsNullOrWhiteSpace(cur)) return;
        if (string.Equals(cur, "default", StringComparison.OrdinalIgnoreCase)) return;

        EnsureProfilesInitialized(padCount: PadCount);

        // Persist current edits into current selected profile before deleting (optional but safe)
        SavePadsIntoProfile(cur);

        var idx = _cfg.Profiles.FindIndex(p => string.Equals(p.Name, cur, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return;

        _cfg.Profiles.RemoveAt(idx);
        _cfg.SelectedProfile = "default";

        _store.Save(_cfg);

        _suspendSave = true;
        try
        {
            RefreshProfilesList();
            SelectedProfileName =
                ProfileNames.FirstOrDefault(n => string.Equals(n, "default", StringComparison.OrdinalIgnoreCase))
                ?? "default";

            ApplySelectedProfileToPads();
        }
        finally
        {
            _suspendSave = false;
        }
    }

    private void SaveNow()
    {
        EnsureProfilesInitialized(padCount: PadCount);

        _cfg.SelectedOutputDeviceId = SelectedOutputDevice?.Id ?? -1;

        // Persist pads into selected profile
        SavePadsIntoProfile(_cfg.SelectedProfile);

        // Keep SelectedProfileName + cfg in sync
        _cfg.SelectedProfile = string.IsNullOrWhiteSpace(SelectedProfileName) ? "default" : SelectedProfileName.Trim();

        // Theme is already stored via OnSelectedThemeChanged, but keep consistent:
        _cfg.SelectedTheme = string.IsNullOrWhiteSpace(SelectedTheme) ? "Dark" : SelectedTheme.Trim();

        _store.Save(_cfg);
    }

    private void BuildPadsFromSelectedProfile(int padCount)
    {
        EnsureProfilesInitialized(padCount);

        var profile = GetProfileByName(_cfg.SelectedProfile);

        foreach (var old in Pads)
            old.Dispose();
        Pads.Clear();
        for (int i = 0; i < profile.Pads.Count; i++)
        {
            var padCfg = profile.Pads[i];

            var pad = new PadViewModel(i, _audio, _pickFileAsync)
            {
                Name = padCfg.Name,
                FilePath = padCfg.Source,
                Volume = (float)padCfg.Volume,
                SourceKind = padCfg.Kind,
                Loop = padCfg.Loop,
                FadeIn = padCfg.FadeIn,
                FadeOut = padCfg.FadeOut,
                PadColor = padCfg.Color
            };

            pad.PropertyChanged += OnPadChanged;
            Pads.Add(pad);
        }
    }

    private void ApplySelectedProfileToPads()
    {
        EnsureProfilesInitialized(padCount: PadCount);

        var profile = GetProfileByName(_cfg.SelectedProfile);

        _suspendSave = true;
        try
        {
            // If pad count changed elsewhere, keep VM count stable but fill what we have
            var n = Math.Min(Pads.Count, profile.Pads.Count);
            for (int i = 0; i < n; i++)
            {
                var pc = profile.Pads[i];
                var vm = Pads[i];

                vm.Name = pc.Name;
                vm.FilePath = pc.Source;
                vm.Volume = (float)pc.Volume;
                vm.SourceKind = pc.Kind;
                vm.Loop = pc.Loop;
                vm.FadeIn = pc.FadeIn;
                vm.FadeOut = pc.FadeOut;
                vm.PadColor = pc.Color;
            }
        }
        finally
        {
            _suspendSave = false;
        }
    }

    private void SavePadsIntoProfile(string? profileName)
    {
        EnsureProfilesInitialized(padCount: PadCount);

        var name = string.IsNullOrWhiteSpace(profileName) ? "default" : profileName.Trim();
        var profile = GetProfileByName(name);

        for (int i = 0; i < Pads.Count && i < profile.Pads.Count; i++)
        {
            var vm = Pads[i];
            var pc = profile.Pads[i];

            pc.Name = vm.Name ?? $"Pad {i + 1}";
            pc.Source = vm.FilePath ?? "";
            pc.Volume = vm.Volume;
            pc.Kind = vm.SourceKind;
            pc.Loop = vm.Loop;
            pc.FadeIn = vm.FadeIn;
            pc.FadeOut = vm.FadeOut;
            pc.Color = vm.PadColor;
        }
    }

    private void RefreshProfilesList()
    {
        EnsureProfilesInitialized(padCount: PadCount);

        ProfileNames.Clear();

        foreach (var n in _cfg.Profiles
                     .Select(p => (p.Name ?? "").Trim())
                     .Where(s => !string.IsNullOrWhiteSpace(s))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            ProfileNames.Add(n);
        }

        // Guarantee default exists in the list
        if (!ProfileNames.Any(n => string.Equals(n, "default", StringComparison.OrdinalIgnoreCase)))
            ProfileNames.Insert(0, "default");

        // Keep cfg selection valid
        if (string.IsNullOrWhiteSpace(_cfg.SelectedProfile) ||
            !_cfg.Profiles.Any(p => string.Equals(p.Name, _cfg.SelectedProfile, StringComparison.OrdinalIgnoreCase)))
        {
            _cfg.SelectedProfile = "default";
        }
    }

    private void EnsureProfilesInitialized(int padCount)
    {
        _cfg.Profiles ??= new System.Collections.Generic.List<ConfigProfile>();

        if (_cfg.Profiles.Count == 0)
        {
            // migrate legacy Pads if present
            var pads = (_cfg.Pads != null && _cfg.Pads.Count > 0)
                ? _cfg.Pads.Select(ClonePad).ToList()
                : CreateDefaultPads(padCount);

            _cfg.Profiles.Add(new ConfigProfile { Name = "default", Pads = pads });
        }

        if (!_cfg.Profiles.Any(p => string.Equals(p.Name, "default", StringComparison.OrdinalIgnoreCase)))
            _cfg.Profiles.Add(new ConfigProfile { Name = "default", Pads = CreateDefaultPads(padCount) });

        foreach (var pr in _cfg.Profiles)
        {
            pr.Name = (pr.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(pr.Name))
                pr.Name = "default";

            pr.Pads ??= new System.Collections.Generic.List<PadConfig>();

            while (pr.Pads.Count < padCount)
                pr.Pads.Add(new PadConfig { Name = $"Pad {pr.Pads.Count + 1}", Kind = PadSourceKind.File, Source = "", Volume = 1.0 });

            while (pr.Pads.Count > padCount)
                pr.Pads.RemoveAt(pr.Pads.Count - 1);
        }

        if (string.IsNullOrWhiteSpace(_cfg.SelectedProfile))
            _cfg.SelectedProfile = "default";

        if (!_cfg.Profiles.Any(p => string.Equals(p.Name, _cfg.SelectedProfile, StringComparison.OrdinalIgnoreCase)))
            _cfg.SelectedProfile = "default";
    }

    private string EnsureProfileExistsAndReturnResolved(string requested, int padCount)
    {
        EnsureProfilesInitialized(padCount);

        var name = NormalizeProfileName(requested);
        if (string.IsNullOrWhiteSpace(name))
            name = "default";

        if (!_cfg.Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            _cfg.Profiles.Add(new ConfigProfile
            {
                Name = name,
                Pads = CreateDefaultPads(padCount)
            });
        }

        // return exact stored casing
        return _cfg.Profiles.First(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).Name;
    }

    private ConfigProfile GetProfileByName(string? name)
    {
        EnsureProfilesInitialized(padCount: PadCount);

        var n = string.IsNullOrWhiteSpace(name) ? "default" : name.Trim();

        var p = _cfg.Profiles.FirstOrDefault(x => string.Equals(x.Name, n, StringComparison.OrdinalIgnoreCase));
        if (p != null) return p;

        // fallback
        return _cfg.Profiles.First(x => string.Equals(x.Name, "default", StringComparison.OrdinalIgnoreCase));
    }

    private static System.Collections.Generic.List<PadConfig> CreateDefaultPads(int padCount)
    {
        var pads = new System.Collections.Generic.List<PadConfig>(padCount);
        for (int i = 0; i < padCount; i++)
        {
            pads.Add(new PadConfig
            {
                Name = $"Pad {i + 1}",
                Kind = PadSourceKind.File,
                Source = "",
                Volume = 1.0
            });
        }
        return pads;
    }

    private static PadConfig ClonePad(PadConfig p) => new()
    {
        Name = p.Name,
        Kind = p.Kind,
        Source = p.Source,
        Volume = p.Volume,
        Loop = p.Loop,
        FadeIn = p.FadeIn,
        FadeOut = p.FadeOut,
        Color = p.Color
    };

    private static string NormalizeProfileName(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) return "";

        var lower = name.ToLowerInvariant();
        var chars = lower.Select(c =>
            (c >= 'a' && c <= 'z') ||
            (c >= '0' && c <= '9') ||
            c == '-' || c == '_'
                ? c
                : '-').ToArray();

        var cleaned = new string(chars);
        while (cleaned.Contains("--"))
            cleaned = cleaned.Replace("--", "-");

        cleaned = cleaned.Trim('-', '_');
        return cleaned.Length == 0 ? "" : cleaned;
    }
}
