using System.Collections.Generic;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Records;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// The rules around device bindings: which controller does which job, and what the settings
/// page shows.
/// </summary>
/// <remarks>
/// Nothing here opens a port or touches a window, so every routing decision the application
/// makes about a device can be asked without hardware. That is deliberate: which device drives
/// the pads and which drives the tracker is the one thing here that goes wrong silently, since
/// a device given no job simply does nothing and looks exactly like a device that is unplugged.
/// </remarks>
public interface IMidiDeviceBindings
{
    /// <summary>
    /// Every job a device can be given.
    /// </summary>
    /// <remarks>
    /// It has to name every flag there is. <see cref="Normalize"/> masks the stored role with
    /// it, so a job missing from here is a job silently taken off every device on the way in,
    /// and a device given only that job is a binding that quietly disappears. Adding a role to
    /// <see cref="MidiDeviceRole"/> means adding it here, in the same breath.
    /// </remarks>
    MidiDeviceRole AnyRole { get; }

    /// <summary>The role a message from this device carries, or None when it is not bound.</summary>
    /// <param name="bindings">What every device has been pointed at, or null.</param>
    /// <param name="device">The port a message arrived on.</param>
    MidiDeviceRole RoleFor(IEnumerable<MidiDeviceBinding>? bindings, string? device);

    /// <summary>Points a device at a role, adding or dropping its binding as needed.</summary>
    /// <param name="bindings">The list to change, in place.</param>
    /// <param name="device">The port, which is trimmed before it is written down.</param>
    /// <param name="role">Every job it should do. None takes the binding away entirely.</param>
    void SetRole(List<MidiDeviceBinding> bindings, string device, MidiDeviceRole role);

    /// <summary>The devices carrying any of the given role, in binding order.</summary>
    /// <param name="bindings">What every device has been pointed at, or null.</param>
    /// <param name="role">The job, or several of them.</param>
    IReadOnlyList<string> DevicesWith(IEnumerable<MidiDeviceBinding>? bindings, MidiDeviceRole role);

    /// <summary>
    /// The list the settings page shows: everything connected, then anything bound that is not
    /// plugged in right now.
    /// </summary>
    /// <remarks>
    /// Unplugging a controller must not lose what it was set to drive. Leaving one in the other
    /// room is not a decision to unwire it, and the same rule holds for the links themselves.
    /// </remarks>
    /// <param name="connected">What the system says is plugged in, or null.</param>
    /// <param name="bindings">What every device has been pointed at, or null.</param>
    IReadOnlyList<MidiDeviceEntry> Merge(
        IEnumerable<string>? connected,
        IEnumerable<MidiDeviceBinding>? bindings);

    /// <summary>
    /// Cleans the stored list and brings a config written before there could be several
    /// devices across.
    /// </summary>
    /// <remarks>
    /// The one device such a config could name only ever drove the pads, so that is the job it
    /// is given. Blank names and roles this build does not know are dropped, and two bindings
    /// for one device are folded into one with both jobs on it.
    /// </remarks>
    /// <param name="cfg">The settings to clean, in place.</param>
    void Normalize(MidiConfig cfg);
}
