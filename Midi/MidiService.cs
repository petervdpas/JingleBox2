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

        var dev = InputDevice.GetAll()
            .FirstOrDefault(d =>
                string.Equals(d.Name, deviceName, StringComparison.OrdinalIgnoreCase));

        if (dev is null)
            return; // ← critical fix: never crash on missing device

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
        if (msg != null)
            MessageReceived?.Invoke(this, msg);
    }

    private static MidiMessage? Convert(MidiEvent ev)
    {
        return ev switch
        {
            NoteOnEvent n => new MidiMessage
            {
                Type = MidiMessageType.Note,
                Channel = n.Channel + 1,
                Value = n.NoteNumber,
                Data = n.Velocity,
                IsOn = n.Velocity > 0
            },

            NoteOffEvent n => new MidiMessage
            {
                Type = MidiMessageType.Note,
                Channel = n.Channel + 1,
                Value = n.NoteNumber,
                Data = n.Velocity,
                IsOn = false
            },

            ControlChangeEvent c => new MidiMessage
            {
                Type = MidiMessageType.ControlChange,
                Channel = c.Channel + 1,
                Value = c.ControlNumber,
                Data = c.ControlValue,
                IsOn = c.ControlValue > 0
            },

            _ => null
        };
    }
}
