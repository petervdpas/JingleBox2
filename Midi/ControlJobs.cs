using JingleBox2.Controllers;
using JingleBox2.Controllers.Interfaces;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Music;
using JingleBox2.Music.Interfaces;

namespace JingleBox2.Midi;

/// <inheritdoc/>
public sealed class ControlJobs : IControlJobs
{
    /// <summary>What is known about the controllers, asked what each one calls its controls.</summary>
    private readonly IControllerProfiles _profiles;

    /// <summary>How the two wheels are read off the wire.</summary>
    private readonly IMidiWheelInput _wire;

    /// <param name="profiles">
    /// What is known about the controllers plugged in. Left out, one of its own; the application
    /// hands the same one to everything, since what a device is doing is remembered in it.
    /// </param>
    /// <param name="wire">How a wheel is read off the wire, defaulted to the real reading.</param>
    public ControlJobs(IControllerProfiles? profiles = null, IMidiWheelInput? wire = null)
    {
        _profiles = profiles ?? new ControllerProfiles();
        _wire = wire ?? new MidiWheelInput();
    }

    /// <inheritdoc/>
    public string Mix => "mix";

    /// <inheritdoc/>
    public string Machine => "machine";

    /// <inheritdoc/>
    public string For(string kind) => kind switch
    {
        "fader" => Mix,
        "knob" or "encoder" => Machine,

        _ => ""
    };

    /// <inheritdoc/>
    public bool Drives(string kind) => For(kind).Length > 0;

    /// <inheritdoc/>
    public bool Turns(MidiMessage? message)
    {
        if (message is null) return false;

        if (message.Type == MidiMessageType.PitchBend) return true;

        if (message.Type != MidiMessageType.ControlChange) return false;
        if (message.Value != _wire.ModulationController) return false;

        if (_profiles.Control(message.Device, message.Channel, message.Value)
            is not { Kind.Length: > 0 } said)
            return true;

        return !Drives(said.Kind);
    }
}
