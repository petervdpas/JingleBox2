using System;
using System.Collections.Generic;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Tests;

/// <summary>One parameter a hardware control can drive, with nothing behind it.</summary>
/// <remarks>
/// The routers under test only ever ask a target for its ends and then write to it, so this is
/// the whole of what they need: two bounds, a value, and a count of how often it was written.
/// Nothing here plays a sound, which is why these tests want no audio device.
/// </remarks>
internal sealed class Knob : IControlTarget
{
    /// <summary>
    /// Puts the knob somewhere in a range, both defaulting to the plain nought to one.
    /// </summary>
    public Knob(double at = 0.5, double min = 0, double max = 1)
    {
        Value = at;
        Min = min;
        Max = max;
    }

    /// <summary>What a status line would call it, so a test can tell two targets apart.</summary>
    public string Name { get; init; } = "a knob";

    /// <summary>
    /// The bottom of the range, which is where pickup and parking are worked out from.
    /// </summary>
    public double Min { get; }

    /// <summary>The top of the range.</summary>
    public double Max { get; }

    /// <summary>Where the knob stands, clamped into the range by every write.</summary>
    public double Value { get; private set; }

    /// <summary>How many times it was written, so a test can see what was ignored.</summary>
    public int Writes { get; private set; }

    /// <summary>Takes a write, clamps it and counts it.</summary>
    public void Set(double value)
    {
        Value = Math.Clamp(value, Min, Max);
        Writes++;
    }
}

/// <summary>Everything resolves to the one knob.</summary>
/// <remarks>
/// For the tests that are about how a control is read rather than about where it points: the
/// mapping is beside the point, so answering every one of them with the same knob keeps the test
/// down to the thing it is really asking about.
/// </remarks>
internal sealed class OneTarget : IControlTargets
{
    /// <summary>Takes the knob every lookup will answer with.</summary>
    public OneTarget(Knob knob) => Knob = knob;

    /// <summary>The one knob, so a test can read it back without going through a lookup.</summary>
    public Knob Knob { get; }

    /// <inheritdoc/>
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
    /// <summary>
    /// The knobs that have been asked for, made on first use rather than up front: a desk of
    /// sixteen tracks would otherwise build a hundred knobs nobody in the test ever touches.
    /// </summary>
    private readonly Dictionary<(int Track, MixControl What), Knob> _knobs = new();

    /// <summary>Makes a desk of a stated width, past which a lookup finds nothing.</summary>
    public Desk(int tracks = 16) => Tracks = tracks;

    /// <summary>
    /// How many tracks this desk has, which is what a bank of eight is checked against.
    /// </summary>
    public int Tracks { get; }

    /// <summary>
    /// Every mapping this was asked about, in order, so a test can see what was aimed at.
    /// </summary>
    public List<ControlMapping> Asked { get; } = new();

    /// <summary>
    /// Reads one strip's knob back, whether or not anything has written to it yet.
    /// </summary>
    public Knob At(int track, MixControl what = MixControl.Volume) => Knob(track, what);

    /// <summary>
    /// The knob for one thing on one strip, made on first use. The ranges are the mixer's own:
    /// a pan runs -1 to 1 and rests in the middle, a mute or a solo is a switch, and a level
    /// rests at half, so a test that checks parking or pickup meets the shape it would meet on
    /// the real strip.
    /// </summary>
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

    /// <inheritdoc/>
    /// <remarks>
    /// Records the question before answering it, and answers nothing for a mapping that is not
    /// about the mix or names a track this desk has not got. A surface banking past the end of
    /// the song is exactly that, and it has to reach nothing rather than reach track nought.
    /// </remarks>
    public IControlTarget? Find(ControlMapping mapping)
    {
        Asked.Add(mapping);

        if (mapping.Kind != ControlKind.Mix) return null;
        if (mapping.Track < 0 || mapping.Track >= Tracks) return null;

        return Knob(mapping.Track, mapping.Mix);
    }
}

/// <summary>A controller that is not there, for anything that only needs to send.</summary>
/// <remarks>
/// The writing half of a control surface is what this stands in for: a display line, a fader
/// position or a lamp is bytes going out of a port, and what a test wants is the bytes rather
/// than the port. Opening a device fails, which is honest, since there is no device.
/// </remarks>
internal sealed class NoMidi : IMidiService
{
    /// <summary>Every send, in order, which is the whole record a surface test reads.</summary>
    public List<(string Device, byte[] Bytes)> Sent { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<string> GetInputDevices() => Array.Empty<string>();

    /// <inheritdoc/>
    public IReadOnlyList<string> OpenDevices => Array.Empty<string>();

    /// <inheritdoc/>
    public bool Open(string device) => false;

    /// <inheritdoc/>
    public void Close(string device) { }

    /// <inheritdoc/>
    public void CloseAll() { }

    /// <summary>Never raised: nothing here has anything to receive. Declared to satisfy the
    /// contract, which everything else that plays a controller does use.</summary>
#pragma warning disable CS0067
    public event EventHandler<MidiMessage>? MessageReceived;
#pragma warning restore CS0067

    /// <inheritdoc/>
    /// <remarks>
    /// Always succeeds, and keeps what it was given. A surface drops a message that would say
    /// the same thing again, so a test that wants to prove that has to see every send that was
    /// really made and no more.
    /// </remarks>
    public bool Send(string device, byte[] bytes)
    {
        Sent.Add((device, bytes));

        return true;
    }

    /// <inheritdoc/>
    public void Dispose() { }
}
