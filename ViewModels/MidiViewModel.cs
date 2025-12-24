using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Config;
using JingleBox2.Midi;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace JingleBox2.ViewModels;

public sealed partial class MidiViewModel : ObservableObject
{
    private readonly ConfigStore _store;
    private readonly AppConfig _cfg;
    private readonly IMidiService _midi;

    private MidiMapping? _learningTarget;

    public ObservableCollection<string> Devices { get; } = new();

    // IMPORTANT: this wraps the SAME objects in cfg.Midi.Pads
    public ObservableCollection<MidiMapping> Pads { get; }

    [ObservableProperty] private string? selectedDevice;
    [ObservableProperty] private bool toggleMode;

    [ObservableProperty] private string status = ""; // for debugging: last MIDI event / learn state

    public MidiViewModel(ConfigStore store, AppConfig cfg, IMidiService midi)
    {
        _store = store;
        _cfg = cfg;
        _midi = midi;

        // Wrap the actual config list so edits persist
        Pads = new ObservableCollection<MidiMapping>(_cfg.Midi.Pads);

        ToggleMode = _cfg.Midi.ToggleMode;
        SelectedDevice = _cfg.Midi.InputDevice;

        RefreshDevices();

        _midi.MessageReceived += OnMidi;
    }

    public IRelayCommand RefreshDevicesCommand => new RelayCommand(RefreshDevices);
    public IRelayCommand<MidiMapping?> LearnCommand => new RelayCommand<MidiMapping?>(ToggleLearnFor);

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
        _store.Save(_cfg);

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
        _store.Save(_cfg);
    }

    private void ToggleLearnFor(MidiMapping? mapping)
    {
        if (mapping is null) return;

        if (_learningTarget == mapping)
        {
            // cancel
            _learningTarget = null;
            Status = "Learn cancelled.";
            return;
        }

        _learningTarget = mapping;
        Status = $"Listening… Press a key/pad for Pad {mapping.PadIndex}.";
    }

    private void OnMidi(object? sender, MidiMessage msg)
    {
        // Always show last incoming MIDI message (helps debugging)
        Status = $"MIDI: {msg.Type} ch{msg.Channel} val={msg.Value} data={msg.Data} on={msg.IsOn}";

        if (_learningTarget is null)
            return;

        // Only learn "on" events to avoid NoteOff overwriting.
        if (!msg.IsOn)
            return;

        _learningTarget.Type = msg.Type;
        _learningTarget.Channel = msg.Channel;
        _learningTarget.Value = msg.Value;

        // Persist back into config list (same objects)
        _cfg.Midi.Pads = Pads.ToList();
        _store.Save(_cfg);

        Status = $"Learned Pad {_learningTarget.PadIndex}: {msg.Type} ch{msg.Channel} val={msg.Value}";
        _learningTarget = null;
    }
}
