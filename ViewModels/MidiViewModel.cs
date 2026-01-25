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

    private PadMidiMappingViewModel? _learningTarget;

    public ObservableCollection<string> Devices { get; } = new();

    // Row VMs so changes from code (learning) refresh the UI immediately
    public ObservableCollection<PadMidiMappingViewModel> Pads { get; }

    [ObservableProperty] private string? selectedDevice;
    [ObservableProperty] private bool toggleMode;
    [ObservableProperty] private string status = "";

    public MidiViewModel(ConfigStore store, AppConfig cfg, IMidiService midi)
    {
        _store = store;
        _cfg = cfg;
        _midi = midi;

        // cfg.Midi should exist (you normalize it), but guard anyway
        _cfg.Midi ??= new MidiConfig();
        _cfg.Midi.Pads ??= new();

        Pads = new ObservableCollection<PadMidiMappingViewModel>(
            _cfg.Midi.Pads.Select(m => new PadMidiMappingViewModel(m)));

        ToggleMode = _cfg.Midi.ToggleMode;
        SelectedDevice = _cfg.Midi.InputDevice;

        RefreshDevices();

        _midi.MessageReceived += OnMidi;
    }

    public IRelayCommand RefreshDevicesCommand => new RelayCommand(RefreshDevices);

    public IRelayCommand<PadMidiMappingViewModel?> LearnCommand =>
        new RelayCommand<PadMidiMappingViewModel?>(ToggleLearnFor);

    private void RefreshDevices()
    {
        Devices.Clear();
        foreach (var d in _midi.GetInputDevices())
            Devices.Add(d);

        Status = Devices.Count == 0 ? "No MIDI devices found." : "";
    }

    partial void OnSelectedDeviceChanged(string? value)
    {
        _cfg.Midi.InputDevice = value;
        SaveMidi();

        _midi.Close();

        if (!string.IsNullOrWhiteSpace(value))
        {
            _midi.Open(value);
            Status = $"Opened: {value}";
        }
        else
        {
            Status = "MIDI device closed.";
        }
    }

    partial void OnToggleModeChanged(bool value)
    {
        _cfg.Midi.ToggleMode = value;
        SaveMidi();
    }

    private void ToggleLearnFor(PadMidiMappingViewModel? row)
    {
        if (row is null) return;

        // Cancel if clicking same row again
        if (_learningTarget == row)
        {
            row.IsLearning = false;
            _learningTarget = null;
            Status = "Learn cancelled.";
            return;
        }

        // Switch learning target
        if (_learningTarget != null)
            _learningTarget.IsLearning = false;

        _learningTarget = row;
        _learningTarget.IsLearning = true;

        Status = $"Listening… Press a key/pad for Pad {row.PadIndex}.";
    }

    private void OnMidi(object? sender, MidiMessage msg)
    {
        Status = $"MIDI: {msg.Type} ch{msg.Channel} val={msg.Value} data={msg.Data} on={msg.IsOn}";

        if (_learningTarget is null)
            return;

        // Only learn "on" events
        if (!msg.IsOn)
            return;

        _learningTarget.Type = msg.Type;
        _learningTarget.Channel = msg.Channel;
        _learningTarget.Value = msg.Value;

        _learningTarget.IsLearning = false;

        Status = $"Learned Pad {_learningTarget.PadIndex}: {msg.Type} ch{msg.Channel} val={msg.Value}";
        _learningTarget = null;

        SaveMidi();
    }

    private void SaveMidi()
    {
        _cfg.Midi.Pads = Pads.Select(p => p.ToModel()).ToList();
        _store.Save(_cfg);
    }

    public void UpdatePadCount(int newPadCount)
    {
        // ConfigStore.Normalize already updated _cfg.Midi.Pads, so just rebuild our VMs
        Pads.Clear();
        foreach (var m in _cfg.Midi.Pads)
            Pads.Add(new PadMidiMappingViewModel(m));
    }
}
