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

    // Profiles UI (CONFIG view header)
    public ObservableCollection<string> ProfileNames { get; } = new();

    [ObservableProperty] private OutputDevice? selectedOutputDevice;

    [ObservableProperty] private string selectedProfileName = "default";
    [ObservableProperty] private string newProfileName = "";

    public IRelayCommand AddProfileCommand { get; }
    public IRelayCommand DeleteProfileCommand { get; }

    public MainViewModel(IAudioEngine audio, Func<Task<string?>> pickFileAsync, ConfigStore store, AppConfig cfg)
    {
        _audio = audio;
        _pickFileAsync = pickFileAsync;
        _store = store;
        _cfg = cfg;

        // Devices
        foreach (var d in _audio.GetOutputDevices())
            OutputDevices.Add(d);

        SelectedOutputDevice =
            OutputDevices.FirstOrDefault(d => d.Id == _cfg.SelectedOutputDeviceId)
            ?? OutputDevices.FirstOrDefault();

        if (SelectedOutputDevice != null)
            _audio.SetOutputDevice(SelectedOutputDevice.Id);

        // Profiles
        RefreshProfilesList();
        SelectedProfileName = string.IsNullOrWhiteSpace(_cfg.SelectedProfile) ? "default" : _cfg.SelectedProfile;

        // Pads from selected profile
        BuildPadsFromSelectedProfile(padCount: 8);

        PropertyChanged += OnMainChanged;

        AddProfileCommand = new RelayCommand(AddProfile);
        DeleteProfileCommand = new RelayCommand(DeleteProfile);
    }

    partial void OnSelectedProfileNameChanged(string value)
    {
        if (_suspendSave) return;
        if (string.IsNullOrWhiteSpace(value)) return;

        SavePadsIntoSelectedProfile(); // persist edits from current pads into current profile

        _cfg.SelectedProfile = value.Trim();
        EnsureSelectedProfileExists(padCount: Pads.Count == 0 ? 8 : Pads.Count);

        _store.Save(_cfg);

        _suspendSave = true;
        try
        {
            RefreshProfilesList();
            ApplySelectedProfileToPads();
        }
        finally
        {
            _suspendSave = false;
        }
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

        EnsureProfilesInitialized(padCount: Pads.Count == 0 ? 8 : Pads.Count);

        if (_cfg.Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            return;

        SavePadsIntoSelectedProfile();

        var cloneFrom = GetSelectedProfile();
        var newProfile = new ConfigProfile
        {
            Name = name,
            Pads = cloneFrom.Pads.Select(ClonePad).ToList()
        };

        _cfg.Profiles.Add(newProfile);
        _cfg.SelectedProfile = name;

        _store.Save(_cfg);

        _suspendSave = true;
        try
        {
            RefreshProfilesList();
            SelectedProfileName = name;
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
        var cur = (SelectedProfileName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(cur)) return;
        if (string.Equals(cur, "default", StringComparison.OrdinalIgnoreCase)) return;

        EnsureProfilesInitialized(padCount: Pads.Count == 0 ? 8 : Pads.Count);

        SavePadsIntoSelectedProfile();

        var idx = _cfg.Profiles.FindIndex(p => string.Equals(p.Name, cur, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return;

        _cfg.Profiles.RemoveAt(idx);
        _cfg.SelectedProfile = "default";

        _store.Save(_cfg);

        _suspendSave = true;
        try
        {
            RefreshProfilesList();
            SelectedProfileName = "default";
            ApplySelectedProfileToPads();
        }
        finally
        {
            _suspendSave = false;
        }
    }

    private void SaveNow()
    {
        _cfg.SelectedOutputDeviceId = SelectedOutputDevice?.Id ?? -1;
        SavePadsIntoSelectedProfile();
        _cfg.SelectedProfile = string.IsNullOrWhiteSpace(SelectedProfileName) ? "default" : SelectedProfileName.Trim();
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

        var profile = GetSelectedProfile();

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

    private void RefreshProfilesList()
    {
        EnsureProfilesInitialized(padCount: Pads.Count == 0 ? 8 : Pads.Count);

        ProfileNames.Clear();
        foreach (var p in _cfg.Profiles
                     .Select(x => x.Name)
                     .Where(n => !string.IsNullOrWhiteSpace(n))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            ProfileNames.Add(p);
        }

        if (!ProfileNames.Any(n => string.Equals(n, _cfg.SelectedProfile, StringComparison.OrdinalIgnoreCase)))
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
                pr.Pads.Add(new PadConfig { Name = $"Pad {pr.Pads.Count + 1}" });

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

        var name = string.IsNullOrWhiteSpace(_cfg.SelectedProfile) ? "default" : _cfg.SelectedProfile.Trim();

        return _cfg.Profiles.First(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static System.Collections.Generic.List<PadConfig> CreateDefaultPads(int padCount)
    {
        var pads = new System.Collections.Generic.List<PadConfig>(padCount);
        for (int i = 0; i < padCount; i++)
            pads.Add(new PadConfig { Name = $"Pad {i + 1}" });
        return pads;
    }

    private static PadConfig ClonePad(PadConfig p) => new()
    {
        Name = p.Name,
        Kind = p.Kind,
        Source = p.Source,
        Volume = p.Volume
    };

    private static string NormalizeProfileName(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) return "";

        // keep it simple + stable for JSON keys / filenames later
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
