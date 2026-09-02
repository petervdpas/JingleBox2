using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Midi.Records;

namespace JingleBox2.Midi;

/// <inheritdoc/>
public sealed class MidiPortBindings : IMidiPortBindings
{
    /// <inheritdoc cref="IMidiPortBindings.AnyRole"/>
    /// <remarks>
    /// Const as well as answered, so it can be matched as a pattern rather than only tested as
    /// a mask, and so a caller that has never held one of these can still name it.
    /// </remarks>
    public const MidiPortRole EveryRole =
        MidiPortRole.Pads | MidiPortRole.Tracker | MidiPortRole.Controls | MidiPortRole.Transport;

    /// <inheritdoc/>
    MidiPortRole IMidiPortBindings.AnyRole => EveryRole;

    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    /// <inheritdoc/>
    public MidiPortRole RoleFor(IEnumerable<MidiPortBinding>? bindings, string? device)
    {
        if (bindings is null || string.IsNullOrWhiteSpace(device)) return MidiPortRole.None;

        var role = MidiPortRole.None;
        foreach (var binding in bindings)
        {
            if (binding is null) continue;
            if (MidiService.SameName(binding.Device, device)) role |= binding.Role;
        }

        return role;
    }

    /// <inheritdoc/>
    public void SetRole(List<MidiPortBinding> bindings, string device, MidiPortRole role)
    {
        if (bindings is null || string.IsNullOrWhiteSpace(device)) return;

        string name = device.Trim();
        bindings.RemoveAll(b => b is null || MidiService.SameName(b.Device, name));

        if (role != MidiPortRole.None)
            bindings.Add(new MidiPortBinding { Device = name, Role = role });
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> DevicesWith(IEnumerable<MidiPortBinding>? bindings, MidiPortRole role)
    {
        if (bindings is null || role == MidiPortRole.None) return Array.Empty<string>();

        return bindings
            .Where(b => b is not null && !string.IsNullOrWhiteSpace(b.Device) && (b.Role & role) != 0)
            .Select(b => b.Device)
            .Distinct(NameComparer)
            .ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<MidiDeviceEntry> Merge(
        IEnumerable<string>? connected,
        IEnumerable<MidiPortBinding>? bindings)
    {
        var entries = new List<MidiDeviceEntry>();
        var seen = new HashSet<string>(NameComparer);

        foreach (var device in connected ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(device) || !seen.Add(device)) continue;
            entries.Add(new MidiDeviceEntry(device, true, RoleFor(bindings, device)));
        }

        foreach (var binding in bindings ?? Enumerable.Empty<MidiPortBinding>())
        {
            if (binding is null || string.IsNullOrWhiteSpace(binding.Device)) continue;
            if (binding.Role == MidiPortRole.None) continue;
            if (!seen.Add(binding.Device)) continue;

            entries.Add(new MidiDeviceEntry(binding.Device, false, RoleFor(bindings, binding.Device)));
        }

        return entries;
    }

    /// <inheritdoc/>
    public void Normalize(MidiConfig cfg)
    {
        if (cfg is null) return;

        cfg.Devices ??= new List<MidiPortBinding>();

        if (cfg.Devices.Count == 0 && !string.IsNullOrWhiteSpace(cfg.InputDevice))
            cfg.Devices.Add(new MidiPortBinding { Device = cfg.InputDevice.Trim(), Role = MidiPortRole.Pads });

        cfg.InputDevice = null;

        var merged = new List<MidiPortBinding>();
        foreach (var binding in cfg.Devices)
        {
            if (binding is null || string.IsNullOrWhiteSpace(binding.Device)) continue;

            string name = binding.Device.Trim();
            var role = binding.Role & EveryRole;
            if (role == MidiPortRole.None) continue;

            var existing = merged.FirstOrDefault(b => NameComparer.Equals(b.Device, name));
            if (existing is null)
                merged.Add(new MidiPortBinding { Device = name, Role = role });
            else
                existing.Role |= role;
        }

        cfg.Devices = merged;
    }
}
