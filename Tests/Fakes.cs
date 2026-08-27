using System;
using System.Collections.Generic;
using JingleBox2.Midi;

namespace JingleBox2.Tests;

/// <summary>One parameter a hardware control can drive, with nothing behind it.</summary>
internal sealed class Knob : IControlTarget
{
    public Knob(double at = 0.5, double min = 0, double max = 1)
    {
        Value = at;
        Min = min;
        Max = max;
    }

    public string Name { get; init; } = "a knob";
    public double Min { get; }
    public double Max { get; }
    public double Value { get; private set; }

    /// <summary>How many times it was written, so a test can see what was ignored.</summary>
    public int Writes { get; private set; }

    public void Set(double value)
    {
        Value = Math.Clamp(value, Min, Max);
        Writes++;
    }
}

/// <summary>Everything resolves to the one knob.</summary>
internal sealed class OneTarget : IControlTargets
{
    public OneTarget(Knob knob) => Knob = knob;

    public Knob Knob { get; }

    public IControlTarget? Find(ControlMapping mapping) => Knob;
}

/// <summary>A controller that is not there, for anything that only needs to send.</summary>
internal sealed class NoMidi : IMidiService
{
    public List<(string Device, byte[] Bytes)> Sent { get; } = new();

    public IReadOnlyList<string> GetInputDevices() => Array.Empty<string>();
    public IReadOnlyList<string> OpenDevices => Array.Empty<string>();
    public bool Open(string device) => false;
    public void Close(string device) { }
    public void CloseAll() { }

    /// <summary>Never raised: nothing here has anything to receive. Declared to satisfy the
    /// contract, which everything else that plays a controller does use.</summary>
#pragma warning disable CS0067
    public event EventHandler<MidiMessage>? MessageReceived;
#pragma warning restore CS0067

    public bool Send(string device, byte[] bytes)
    {
        Sent.Add((device, bytes));

        return true;
    }

    public void Dispose() { }
}
