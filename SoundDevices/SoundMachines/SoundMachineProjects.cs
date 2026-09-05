using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Tracker.Enums;
using JingleBox2.SoundDevices.SoundMachines.Interfaces;

namespace JingleBox2.SoundDevices.SoundMachines;

/// <inheritdoc/>
public sealed class SoundMachineProjects : RackSoundDevices<SoundMachineProject>, ISoundMachineProjects
{
    /// <inheritdoc/>
    /// <remarks>
    /// A kind that is not a device of ours has no machine to be missing, so it is never refused:
    /// that is the plugin, whose absence is the plugin itself and has an answer of its own. Asked
    /// of the machine rather than of its id, since a plugin now carries an id like anything else
    /// and an empty string is no longer what marks it out.
    ///
    /// Still asked by engine, which is as far as the question can be taken while a song's
    /// instrument writes down the engine it plays and not the device it came off. So it asks
    /// after **every** device registered on that engine and not the first of them: with one
    /// device to an engine those were the same answer, and they stopped being the same the day a
    /// second kit could exist. An instrument is let through while any device on its engine is on
    /// the rack, which is the truthful answer to a question phrased in engines.
    /// </remarks>
    public bool Has(TrackerInstrumentKind kind)
    {
        var stands = JingleBox2.SoundDevices.SoundMachines.Records.SoundMachine.For(kind);

        if (!stands.IsOurs) return true;

        var ids = JingleBox2.SoundDevices.SoundMachines.Records.SoundMachine.Installed
            .Where(one => one.Kind == kind)
            .Select(one => one.SlotId)
            .ToList();

        if (ids.Count == 0) ids.Add(stands.SlotId);

        return ids.Any(id => id.Length == 0 || (For(id) is not null && (_rack is null || _rack.Contains(id))));
    }

    /// <summary>Which machines are on the rack, or nothing while nobody has said.</summary>
    private HashSet<string>? _rack;

    /// <inheritdoc/>
    public void OnRack(IEnumerable<string> slots) =>
        _rack = slots is null ? null : new HashSet<string>(slots, StringComparer.OrdinalIgnoreCase);
}
