// ===============================
// ViewModels/MainViewModel.cs
// ===============================
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio;
using JingleBox2.Config;
using JingleBox2.Models;
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

    public ObservableCollection<OutputDevice> OutputDevices { get; } = new();
    public ObservableCollection<PadViewModel> Pads { get; } = new();

    // Profiles (bind ComboBox to objects, not strings)
    public ObservableCollection<ConfigProfile> Profiles { get; } = new();

    [ObservableProperty] private OutputDevice? selectedOutputDevice;

    [ObservableProperty] private ConfigProfile? selectedProfile;

    [ObservableProperty] private string newProfileName = "";

    public IRelayCommand AddProfileCommand { get; }
    public IRelayCommand DeleteProfileCommand { get; }

    public MainViewModel(IAudioEngine audio, Func<Task<string?>> pickFileAsync, ConfigStore store, AppConfig cfg)
    {
        _audio = audio;
        _pickFileAsync = pickFileAsync;
        _store = store;
        _cfg = cfg;

        AddProfileCommand = new RelayCommand(AddProfile);
        DeleteProfileCommand = new RelayCommand(DeleteProfile);

        // Devices
        foreach (var d in _audio.GetOutputDevices())
            OutputDevices.Add(d);

        SelectedOutputDevice =
            OutputDevices.FirstOrDefault(d => d.Id == _cfg.SelectedOutputDeviceId)
            ?? OutputDevices.FirstOrDefault();

        if (SelectedOutputDevice != null)
            _audio.SetOutputDevice(SelectedOutputDevice.Id);

        // Ensure config baseline + profiles exist
        EnsureProfilesInitialized(padCount: 8);
        RefreshProfilesCollection();

        // Select persisted profile (by name), otherwise default
        var wanted = string.IsNullOrWhiteSpace(_cfg.SelectedProfile) ? "default" : _cfg.SelectedProfile.Trim();

        _suspendSave = true;
        try
        {
            SelectedProfile =
                Profiles.FirstOrDefault(p => string.Equals(p.Name, wanted, StringComparison.OrdinalIgnoreCase))
                ?? Profiles.FirstOrDefault(p => string.Equals(p.Name, "default", StringComparison.OrdinalIgnoreCase))
                ?? Profiles.FirstOrDefault();
        }
        finally
        {
            _suspendSave = false;
        }

        // Build pads
        BuildPadsFromSelectedProfile(padCount: 8);

        PropertyChanged += OnMainChanged;
    }

    partial void OnSelectedProfileChanged(ConfigProfile? value)
    {
        if (_suspendSave) return;
        if (value == null) return;

        // Save current pad edits into the OLD selected profile (based on cfg.SelectedProfile)
        SavePadsIntoSelectedProfile();

        // Persist selection
        _cfg.SelectedProfile = value.Name ?? "default";
        EnsureSelectedProfileExists(padCount: Pads.Count == 0 ? 8 : Pads.Count);

        // Apply new profile data into pads
        ApplySelectedProfileToPads();

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

        var name = raw; // keep user-facing name exactly as typed (no normalization)
        var padCount = Pads.Count == 0 ? 8 : Pads.Count;

        EnsureProfilesInitialized(padCount);

        if (_cfg.Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            return;

        // Save current edits into current profile before switching away
        SavePadsIntoSelectedProfile();

        // CLEAN SLATE
        var created = new ConfigProfile
        {
            Name = name,
            Pads = CreateDefaultPads(padCount)
        };

        _cfg.Profiles.Add(created);
        _cfg.SelectedProfile = name;

        _store.Save(_cfg);

        RefreshProfilesCollection();

        _suspendSave = true;
        try
        {
            SelectedProfile = Profiles.First(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            NewProfileName = "";
        }
        finally
        {
            _suspendSave = false;
        }

        ApplySelectedProfileToPads(); // blank defaults
    }

    private void DeleteProfile()
    {
        if (SelectedProfile == null) return;

        var curName = (SelectedProfile.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(curName)) return;
        if (string.Equals(curName, "default", StringComparison.OrdinalIgnoreCase)) return;

        EnsureProfilesInitialized(padCount: Pads.Count == 0 ? 8 : Pads.Count);

        SavePadsIntoSelectedProfile();

        var idx = _cfg.Profiles.FindIndex(p => string.Equals(p.Name, curName, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return;

        _cfg.Profiles.RemoveAt(idx);
        _cfg.SelectedProfile = "default";

        _store.Save(_cfg);

        RefreshProfilesCollection();

        _suspendSave = true;
        try
        {
            SelectedProfile =
                Profiles.FirstOrDefault(p => string.Equals(p.Name, "default", StringComparison.OrdinalIgnoreCase))
                ?? Profiles.FirstOrDefault();
        }
        finally
        {
            _suspendSave = false;
        }

        ApplySelectedProfileToPads();
    }

    private void SaveNow()
    {
        _cfg.SelectedOutputDeviceId = SelectedOutputDevice?.Id ?? -1;

        SavePadsIntoSelectedProfile();

        if (SelectedProfile != null && !string.IsNullOrWhiteSpace(SelectedProfile.Name))
            _cfg.SelectedProfile = SelectedProfile.Name;

        _store.Save(_cfg);
    }

    private void BuildPadsFromSelectedProfile(int padCount)
    {
        EnsureProfilesInitialized(padCount);

        var profile = GetSelectedProfile();

        Pads.Clear();
        for (int i = 0; i < profile.Pads.Count; i++)
        {
            var padCfg = profile.Pads[i];

            var pad = new PadViewModel(i, _audio, _pickFileAsync)
            {
                Name = padCfg.Name,
                FilePath = padCfg.Source,
                Volume = (float)padCfg.Volume,
                SourceKind = padCfg.Kind
            };

            pad.PropertyChanged += OnPadChanged;
            Pads.Add(pad);
        }
    }

    private void ApplySelectedProfileToPads()
    {
        EnsureProfilesInitialized(padCount: Pads.Count == 0 ? 8 : Pads.Count);

        var profile = GetSelectedProfile();

        _suspendSave = true;
        try
        {
            for (int i = 0; i < Pads.Count && i < profile.Pads.Count; i++)
            {
                var padCfg = profile.Pads[i];
                var vm = Pads[i];

                vm.Name = padCfg.Name;
                vm.FilePath = padCfg.Source;
                vm.Volume = (float)padCfg.Volume;
                vm.SourceKind = padCfg.Kind;
            }
        }
        finally
        {
            _suspendSave = false;
        }
    }

    private void SavePadsIntoSelectedProfile()
    {
        EnsureProfilesInitialized(padCount: Pads.Count == 0 ? 8 : Pads.Count);

        var profile = GetProfileByName(_cfg.SelectedProfile) ?? GetProfileByName("default") ?? _cfg.Profiles[0];

        for (int i = 0; i < Pads.Count && i < profile.Pads.Count; i++)
        {
            var vm = Pads[i];
            var pc = profile.Pads[i];

            pc.Name = vm.Name ?? $"Pad {i + 1}";
            pc.Source = vm.FilePath ?? "";
            pc.Volume = vm.Volume;
            pc.Kind = vm.SourceKind;
        }
    }

    private void RefreshProfilesCollection()
    {
        Profiles.Clear();

        foreach (var p in _cfg.Profiles
                     .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                     .OrderBy(p => string.Equals(p.Name, "default", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                     .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            Profiles.Add(p);
        }

        // keep cfg.SelectedProfile valid
        if (string.IsNullOrWhiteSpace(_cfg.SelectedProfile))
            _cfg.SelectedProfile = "default";

        if (!_cfg.Profiles.Any(p => string.Equals(p.Name, _cfg.SelectedProfile, StringComparison.OrdinalIgnoreCase)))
            _cfg.SelectedProfile = "default";
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
            pr.Pads ??= new System.Collections.Generic.List<PadConfig>();

            while (pr.Pads.Count < padCount)
                pr.Pads.Add(new PadConfig { Name = $"Pad {pr.Pads.Count + 1}", Kind = PadSourceKind.None, Source = "", Volume = 1.0 });

            while (pr.Pads.Count > padCount)
                pr.Pads.RemoveAt(pr.Pads.Count - 1);
        }

        if (string.IsNullOrWhiteSpace(_cfg.SelectedProfile))
            _cfg.SelectedProfile = "default";

        if (!_cfg.Profiles.Any(p => string.Equals(p.Name, _cfg.SelectedProfile, StringComparison.OrdinalIgnoreCase)))
            _cfg.SelectedProfile = "default";
    }

    private void EnsureSelectedProfileExists(int padCount)
    {
        EnsureProfilesInitialized(padCount);

        var name = string.IsNullOrWhiteSpace(_cfg.SelectedProfile) ? "default" : _cfg.SelectedProfile.Trim();

        if (!_cfg.Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            _cfg.Profiles.Add(new ConfigProfile
            {
                Name = name,
                Pads = CreateDefaultPads(padCount)
            });
        }
    }

    private ConfigProfile GetSelectedProfile()
    {
        EnsureProfilesInitialized(padCount: Pads.Count == 0 ? 8 : Pads.Count);

        var selectedName = SelectedProfile?.Name;
        if (!string.IsNullOrWhiteSpace(selectedName))
            _cfg.SelectedProfile = selectedName;

        var name = string.IsNullOrWhiteSpace(_cfg.SelectedProfile) ? "default" : _cfg.SelectedProfile.Trim();

        return GetProfileByName(name)
               ?? GetProfileByName("default")
               ?? _cfg.Profiles[0];
    }

    private ConfigProfile? GetProfileByName(string? name)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) return null;

        return _cfg.Profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static System.Collections.Generic.List<PadConfig> CreateDefaultPads(int padCount)
    {
        var pads = new System.Collections.Generic.List<PadConfig>(padCount);
        for (int i = 0; i < padCount; i++)
            pads.Add(new PadConfig { Name = $"Pad {i + 1}", Kind = PadSourceKind.None, Source = "", Volume = 1.0 });
        return pads;
    }

    private static PadConfig ClonePad(PadConfig p) => new()
    {
        Name = p.Name,
        Kind = p.Kind,
        Source = p.Source,
        Volume = p.Volume
    };
}
