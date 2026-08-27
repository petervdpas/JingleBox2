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

/// <summary>
/// A mixer of a fixed size: one knob per track per thing on its strip.
/// </summary>
/// <remarks>
/// Enough for a control surface, which asks for a named thing on a numbered track and cares
/// about which one it got. <see cref="OneTarget"/> answers everything with the same knob, which
/// is exactly wrong for testing whether fader three reached track three.
/// </remarks>
internal sealed class Desk : IControlTargets
{
    private readonly Dictionary<(int Track, MixControl What), Knob> _knobs = new();

    public Desk(int tracks = 16) => Tracks = tracks;

    public int Tracks { get; }

    /// <summary>Every mapping this was asked about, in order, so a test can see what was aimed at.</summary>
    public List<ControlMapping> Asked { get; } = new();

    public Knob At(int track, MixControl what = MixControl.Volume) => Knob(track, what);

    private Knob Knob(int track, MixControl what)
    {
        if (!_knobs.TryGetValue((track, what), out var knob))
            _knobs[(track, what)] = knob = what switch
            {
                MixControl.Pan => new Knob(0, -1, 1) { Name = "Pan on TR-" + (track + 1) },
                MixControl.Mute or MixControl.Solo => new Knob(0, 0, 1) { Name = what + " on TR-" + (track + 1) },
                _ => new Knob(0.5, 0, 1) { Name = "Level on TR-" + (track + 1) }
            };

        return knob;
    }

    public IControlTarget? Find(ControlMapping mapping)
    {
        Asked.Add(mapping);

        if (mapping.Kind != ControlKind.Mix) return null;
        if (mapping.Track < 0 || mapping.Track >= Tracks) return null;

        return Knob(mapping.Track, mapping.Mix);
    }
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
