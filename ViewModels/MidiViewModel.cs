using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Config;
using JingleBox2.Midi;
using System.Collections.ObjectModel;
using System.Linq;

namespace JingleBox2.ViewModels;

public sealed partial class MidiViewModel : ObservableObject
{
    private readonly ConfigStore _store;
    private readonly AppConfig _cfg;
    private readonly IMidiService _midi;

    private MidiLearnSession? _learn;

    public ObservableCollection<string> Devices { get; } = new();
    public ObservableCollection<MidiMapping> Pads { get; }

    [ObservableProperty] private string? selectedDevice;
    [ObservableProperty] private bool toggleMode;

    public MidiViewModel(ConfigStore store, AppConfig cfg, IMidiService midi)
    {
        _store = store;
        _cfg = cfg;
        _midi = midi;

        Pads = new ObservableCollection<MidiMapping>(_cfg.Midi.Pads);

        ToggleMode = _cfg.Midi.ToggleMode;
        SelectedDevice = _cfg.Midi.InputDevice;

        RefreshDevices();

        _midi.MessageReceived += OnMidi;
    }

    public IRelayCommand RefreshDevicesCommand => new RelayCommand(RefreshDevices);

    public IRelayCommand<MidiMapping?> LearnCommand => new RelayCommand<MidiMapping?>(StartLearn);


    private void RefreshDevices()
    {
        Devices.Clear();
        foreach (var d in _midi.GetInputDevices())
            Devices.Add(d);
    }

    partial void OnSelectedDeviceChanged(string? value)
    {
        _cfg.Midi.InputDevice = value;
        _store.Save(_cfg);

        _midi.Close();
        if (!string.IsNullOrWhiteSpace(value))
            _midi.Open(value);
    }

    partial void OnToggleModeChanged(bool value)
    {
        _cfg.Midi.ToggleMode = value;
        _store.Save(_cfg);
    }

    private void StartLearn(MidiMapping? mapping)
    {
        if (mapping is null) return;

        _learn = new MidiLearnSession(msg =>
        {
            mapping.Type = msg.Type;
            mapping.Channel = msg.Channel;
            mapping.Value = msg.Value;

            _store.Save(_cfg);
        });

        _learn.Start();
    }

    private void OnMidi(object? sender, MidiMessage msg)
    {
        _learn?.Handle(msg);
    }
}
