// ===============================
// Midi/MidiService.cs
// ===============================
using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;

namespace JingleBox2.Midi;

public sealed class MidiService : IMidiService
{
    private InputDevice? _device;

    public event EventHandler<MidiMessage>? MessageReceived;

    public IReadOnlyList<string> GetInputDevices()
    {
        // DryWetMIDI exposes currently available input devices by name.
        return InputDevice.GetAll()
            .Select(d => d.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void Open(string deviceName)
    {
        Close();

        if (string.IsNullOrWhiteSpace(deviceName))
            return;

        // Find exact match (case-insensitive)
        var dev = InputDevice.GetAll()
            .FirstOrDefault(d => string.Equals(d.Name, deviceName, StringComparison.OrdinalIgnoreCase));

        if (dev is null)
            throw new InvalidOperationException($"MIDI device not found: {deviceName}");

        _device = dev;
        _device.EventReceived += OnEventReceived;
        _device.StartEventsListening();
    }

    public void Close()
    {
        if (_device is null)
            return;

        try
        {
            _device.EventReceived -= OnEventReceived;
            _device.StopEventsListening();
            _device.Dispose();
        }
        finally
        {
            _device = null;
        }
    }

    public void Dispose() => Close();

    private void OnEventReceived(object? sender, MidiEventReceivedEventArgs e)
    {
        var msg = Convert(e.Event);
        if (msg is null) return;

        MessageReceived?.Invoke(this, msg);
    }

    private static MidiMessage? Convert(MidiEvent ev)
    {
        switch (ev)
        {
            case NoteOnEvent noteOn:
                // Treat velocity 0 as NoteOff
                return new MidiMessage
                {
                    Type = MidiMessageType.Note,
                    Channel = noteOn.Channel + 1, // DryWetMIDI channels are 0..15
                    Value = noteOn.NoteNumber,
                    Data = noteOn.Velocity,
                    IsOn = noteOn.Velocity > 0
                };

            case NoteOffEvent noteOff:
                return new MidiMessage
                {
                    Type = MidiMessageType.Note,
                    Channel = noteOff.Channel + 1,
                    Value = noteOff.NoteNumber,
                    Data = noteOff.Velocity,
                    IsOn = false
                };

            case ControlChangeEvent cc:
                return new MidiMessage
                {
                    Type = MidiMessageType.ControlChange,
                    Channel = cc.Channel + 1,
                    Value = cc.ControlNumber,
                    Data = cc.ControlValue,
                    IsOn = cc.ControlValue > 0
                };

            default:
                return null;
        }
    }
}
