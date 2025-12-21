using CommunityToolkit.Mvvm.ComponentModel;
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

    public ObservableCollection<OutputDevice> OutputDevices { get; } = new();
    public ObservableCollection<PadViewModel> Pads { get; } = new();

    [ObservableProperty] private OutputDevice? selectedOutputDevice;

    public MainViewModel(IAudioEngine audio, Func<Task<string?>> pickFileAsync, ConfigStore store, AppConfig cfg)
    {
        _audio = audio;
        _store = store;
        _cfg = cfg;

        // Devices
        foreach (var d in _audio.GetOutputDevices())
            OutputDevices.Add(d);

        // restore output device
        SelectedOutputDevice =
            OutputDevices.FirstOrDefault(d => d.Id == _cfg.SelectedOutputDeviceId)
            ?? OutputDevices.FirstOrDefault();

        if (SelectedOutputDevice != null)
            _audio.SetOutputDevice(SelectedOutputDevice.Id);

        // Pads from config
        for (int i = 0; i < _cfg.Pads.Count; i++)
        {
            var padCfg = _cfg.Pads[i];

            var pad = new PadViewModel(i, _audio, pickFileAsync)
            {
                Name = padCfg.Name,
                FilePath = padCfg.Source,
                Volume = (float)padCfg.Volume,
                SourceKind = padCfg.Kind
            };

            pad.PropertyChanged += OnPadChanged;
            Pads.Add(pad);
        }

        PropertyChanged += OnMainChanged;
    }

    private void OnMainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectedOutputDevice))
        {
            if (SelectedOutputDevice != null)
                _audio.SetOutputDevice(SelectedOutputDevice.Id);

            SaveNow();
        }
    }

    private void OnPadChanged(object? sender, PropertyChangedEventArgs e)
    {
        // any pad change -> persist (simple for now)
        SaveNow();
    }

    private void SaveNow()
    {
        _cfg.SelectedOutputDeviceId = SelectedOutputDevice?.Id ?? -1;

        for (int i = 0; i < Pads.Count; i++)
        {
            var vm = Pads[i];
            var pc = _cfg.Pads[i];

            pc.Name = vm.Name ?? $"Pad {i + 1}";
            pc.Source = vm.FilePath ?? "";
            pc.Volume = vm.Volume;          // float -> double implicit
            pc.Kind = vm.SourceKind;
        }

        _store.Save(_cfg);
    }
}
