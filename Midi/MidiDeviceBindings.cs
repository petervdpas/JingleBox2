using System;
using System.Collections.Generic;
using System.Linq;

namespace JingleBox2.Midi;

/// <summary>One device as the settings list shows it: bound or not, plugged in or not.</summary>
public readonly record struct MidiDeviceEntry(string Device, bool IsConnected, MidiDeviceRole Role);

/// <summary>
/// The rules around device bindings, as plain functions on a list. Nothing here opens a port
/// or touches the UI, so the routing decisions can be tested on their own.
/// </summary>
public static class MidiDeviceBindings
{
    /// <summary>
    /// Every job a device can be given.
    /// </summary>
    /// <remarks>
    /// Const so it can be matched as a pattern, not just tested as a mask, and it has to name
    /// every flag there is. <see cref="Normalize"/> masks the stored role with this, so a job
    /// missing from here is a job silently taken off every device on the way in, and a device
    /// given only that job is a binding that quietly disappears. Adding a role to
    /// <see cref="MidiDeviceRole"/> means adding it here, in the same breath.
    /// </remarks>
    public const MidiDeviceRole AnyRole =
        MidiDeviceRole.Pads | MidiDeviceRole.Tracker | MidiDeviceRole.Controls | MidiDeviceRole.Transport;

    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>The role a message from this device carries, or None when it is not bound.</summary>
    public static MidiDeviceRole RoleFor(IEnumerable<MidiDeviceBinding>? bindings, string? device)
    {
        if (bindings is null || string.IsNullOrWhiteSpace(device)) return MidiDeviceRole.None;

        var role = MidiDeviceRole.None;
        foreach (var binding in bindings)
        {
            if (binding is null) continue;
            if (MidiService.SameName(binding.Device, device)) role |= binding.Role;
        }

        return role;
    }

    /// <summary>Points a device at a role, adding or dropping its binding as needed.</summary>
    public static void SetRole(List<MidiDeviceBinding> bindings, string device, MidiDeviceRole role)
    {
        if (bindings is null || string.IsNullOrWhiteSpace(device)) return;

        string name = device.Trim();
        bindings.RemoveAll(b => b is null || MidiService.SameName(b.Device, name));

        if (role != MidiDeviceRole.None)
            bindings.Add(new MidiDeviceBinding { Device = name, Role = role });
    }

    /// <summary>The devices carrying any of the given role, in binding order.</summary>
    public static IReadOnlyList<string> DevicesWith(IEnumerable<MidiDeviceBinding>? bindings, MidiDeviceRole role)
    {
        if (bindings is null || role == MidiDeviceRole.None) return Array.Empty<string>();

        return bindings
            .Where(b => b is not null && !string.IsNullOrWhiteSpace(b.Device) && (b.Role & role) != 0)
            .Select(b => b.Device)
            .Distinct(NameComparer)
            .ToList();
    }

    /// <summary>
    /// The list the settings page shows: everything connected, then anything bound that is not
    /// plugged in right now. Unplugging a controller must not lose what it was set to drive.
    /// </summary>
    public static IReadOnlyList<MidiDeviceEntry> Merge(
        IEnumerable<string>? connected,
        IEnumerable<MidiDeviceBinding>? bindings)
    {
        var entries = new List<MidiDeviceEntry>();
        var seen = new HashSet<string>(NameComparer);

        foreach (var device in connected ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(device) || !seen.Add(device)) continue;
            entries.Add(new MidiDeviceEntry(device, true, RoleFor(bindings, device)));
        }

        foreach (var binding in bindings ?? Enumerable.Empty<MidiDeviceBinding>())
        {
            if (binding is null || string.IsNullOrWhiteSpace(binding.Device)) continue;
            if (binding.Role == MidiDeviceRole.None) continue;
            if (!seen.Add(binding.Device)) continue;

            entries.Add(new MidiDeviceEntry(binding.Device, false, RoleFor(bindings, binding.Device)));
        }

        return entries;
    }

    /// <summary>
    /// Cleans the stored list and brings a pre-multi-device config across: the one device it
    /// could name only ever drove the pads.
    /// </summary>
    public static void Normalize(MidiConfig cfg)
    {
        if (cfg is null) return;

        cfg.Devices ??= new List<MidiDeviceBinding>();

        if (cfg.Devices.Count == 0 && !string.IsNullOrWhiteSpace(cfg.InputDevice))
            cfg.Devices.Add(new MidiDeviceBinding { Device = cfg.InputDevice.Trim(), Role = MidiDeviceRole.Pads });

        cfg.InputDevice = null;

        var merged = new List<MidiDeviceBinding>();
        foreach (var binding in cfg.Devices)
        {
            if (binding is null || string.IsNullOrWhiteSpace(binding.Device)) continue;

            string name = binding.Device.Trim();
            var role = binding.Role & AnyRole;
            if (role == MidiDeviceRole.None) continue;

            var existing = merged.FirstOrDefault(b => NameComparer.Equals(b.Device, name));
            if (existing is null)
                merged.Add(new MidiDeviceBinding { Device = name, Role = role });
            else
                existing.Role |= role;
        }

        cfg.Devices = merged;
    }
}
