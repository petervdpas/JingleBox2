using Avalonia.Threading;
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

    private PadMidiMappingViewModel? _learningTarget;

    /// <summary>Every controller with a row in SETTINGS, connected or merely remembered.</summary>
    public ObservableCollection<MidiDeviceViewModel> Devices { get; } = new();

    // Row VMs so changes from code (learning) refresh the UI immediately
    public ObservableCollection<PadMidiMappingViewModel> Pads { get; }

    [ObservableProperty] private bool hasDevices;
    [ObservableProperty] private bool toggleMode;
    [ObservableProperty] private string status = "";

    /// <summary>The controller the pad mapping belongs to; blank when none is assigned.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPadDevice))]
    [NotifyPropertyChangedFor(nameof(PadDeviceSummary))]
    private string padDevice = "";

    public MidiViewModel(ConfigStore store, AppConfig cfg, IMidiService midi)
    {
        _store = store;
        _cfg = cfg;
        _midi = midi;

        // cfg.Midi should exist (you normalize it), but guard anyway
        _cfg.Midi ??= new MidiConfig();
        _cfg.Midi.Pads ??= new();
        _cfg.Midi.Devices ??= new();

        Pads = new ObservableCollection<PadMidiMappingViewModel>(
            _cfg.Midi.Pads.Select(m => new PadMidiMappingViewModel(m)));

        ToggleMode = _cfg.Midi.ToggleMode;

        RefreshDevices();

        _midi.MessageReceived += OnMidi;
    }

    public bool HasPadDevice => PadDevice.Length > 0;

    /// <summary>Line above the mapping table: a mapping without a pad controller does nothing.</summary>
    public string PadDeviceSummary => HasPadDevice
        ? $"These pads are triggered by '{PadDevice}'."
        : "No controller drives the pads yet. Tick Pads in SETTINGS.";

    public IRelayCommand RefreshDevicesCommand => new RelayCommand(RefreshDevices);

    public IRelayCommand<PadMidiMappingViewModel?> LearnCommand =>
        new RelayCommand<PadMidiMappingViewModel?>(ToggleLearnFor);

    private void RefreshDevices()
    {
        Devices.Clear();
        foreach (var entry in MidiDeviceBindings.Merge(_midi.GetInputDevices(), _cfg.Midi.Devices))
            Devices.Add(new MidiDeviceViewModel(entry.Device, entry.IsConnected, entry.Role, OnDeviceRoleChanged));

        HasDevices = Devices.Count > 0;

        UpdatePadDevice();
        ApplyBindings();
        Status = DescribeDevices();
    }

    private void OnDeviceRoleChanged(MidiDeviceViewModel device)
    {
        MidiDeviceBindings.SetRole(_cfg.Midi.Devices, device.Name, device.Role);

        UpdatePadDevice();
        ApplyBindings();
        SaveMidi();

        Status = device.Role == MidiDeviceRole.None
            ? $"'{device.Name}' drives nothing."
            : $"'{device.Name}' drives {DescribeRole(device.Role)}.";
    }

    /// <summary>
    /// Opens exactly the devices that were given a job, and closes the rest. Nothing else holds
    /// a port open, so the role list is the single source of truth for what is listening.
    /// </summary>
    private void ApplyBindings()
    {
        var wanted = MidiDeviceBindings.DevicesWith(_cfg.Midi.Devices, MidiDeviceBindings.AnyRole);

        foreach (var open in _midi.OpenDevices)
        {
            if (!wanted.Contains(open, StringComparer.OrdinalIgnoreCase))
                _midi.Close(open);
        }

        foreach (var device in wanted)
            _midi.Open(device);
    }

    private void UpdatePadDevice()
    {
        var pads = MidiDeviceBindings.DevicesWith(_cfg.Midi.Devices, MidiDeviceRole.Pads);
        PadDevice = pads.Count == 0 ? "" : string.Join(", ", pads);
    }

    private string DescribeDevices()
    {
        if (Devices.Count == 0) return "No MIDI devices found.";

        int open = _midi.OpenDevices.Count;
        if (open == 0) return "No controller assigned yet. Tick Pads or Tracker.";

        var missing = Devices.Where(d => !d.IsConnected && d.Role != MidiDeviceRole.None).ToList();
        if (missing.Count > 0)
            return $"{open} controller(s) open. Not connected: {string.Join(", ", missing.Select(d => d.Name))}.";

        return $"{open} controller(s) open.";
    }

    private static string DescribeRole(MidiDeviceRole role) => role switch
    {
        MidiDeviceRole.Pads => "the pads",
        MidiDeviceRole.Tracker => "the tracker",
        MidiDeviceBindings.AnyRole => "the pads and the tracker",
        _ => "nothing"
    };

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

        Status = $"Listening... Press a key/pad for Pad {row.PadIndex}.";
    }

    /// <summary>
    /// MIDI arrives on its own thread, so the status text and the learn result are handed to
    /// the UI thread before anything bound to them is touched.
    /// </summary>
    private void OnMidi(object? sender, MidiMessage msg) => Dispatcher.UIThread.Post(() => HandleMidi(msg));

    private void HandleMidi(MidiMessage msg)
    {
        Status = $"{msg.Device}: {msg.Type} ch{msg.Channel} val={msg.Value} data={msg.Data} on={msg.IsOn}";

        if (_learningTarget is null)
            return;

        // Only learn "on" events
        if (!msg.IsOn)
            return;

        // A keyboard sitting on the tracker must not end up mapped to a pad.
        var role = MidiDeviceBindings.RoleFor(_cfg.Midi.Devices, msg.Device);
        if ((role & MidiDeviceRole.Pads) == 0)
        {
            Status = $"'{msg.Device}' does not drive the pads, so it cannot be learned here.";
            return;
        }

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
