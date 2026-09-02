using System.Collections.Generic;
using System.Linq;
using JingleBox2.SoundDevices.SoundMachines.Interfaces;
using JingleBox2.SoundDevices.SoundMachines.Records;
using JingleBox2.Tracker;
using JingleBox2.SoundDevices.Interfaces;

namespace JingleBox2.SoundDevices.SoundMachines;

/// <inheritdoc/>
/// <param name="registry">
/// Who says which machines ship and which are installed. Left out, the ordinary one, which
/// reads this installation's own folders.
/// </param>
/// <param name="rack">
/// The shelf, which decides which machines a song can be given. Left out, the registry alone
/// answers, which is what a caller with no rack wants: a test, a preview, or the designer.
/// </param>
public sealed class MissingSoundMachines(IRackRegistry<SoundMachineProject>? registry = null, ISoundMachineRack? rack = null) : IMissingSoundMachines
{
    /// <summary>Who says what ships, which is the only place a missing machine's name survives.</summary>
    private readonly IRackRegistry<SoundMachineProject> _registry = registry ?? new SoundMachineRegistry();

    /// <summary>The shelf, or nothing when only the registry is being asked.</summary>
    private readonly ISoundMachineRack? _rack = rack;

    /// <inheritdoc/>
    public IReadOnlyList<MissingSoundMachine> For(Song song)
    {
        var wanted = new List<MissingSoundMachine>();

        if (song?.Instruments is not { } instruments) return wanted;

        var offered = _registry.Available().ToDictionary(one => one.Id, one => one.Name);

        var said = new HashSet<string>();

        foreach (var sound in instruments)
        {
            if (sound is null) continue;

            if (Named(sound, offered) is not { } missing || !said.Add(missing.Id)) continue;

            wanted.Add(missing);
        }

        return wanted;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The shipped list is read for this one instrument, which is a folder walk for a question
    /// asked once when somebody clicks. The alternative is keeping it, and a kept copy is a copy
    /// that is wrong the moment a machine is added in the next tab.
    /// </remarks>
    public MissingSoundMachine? For(TrackerInstrument? sound) =>
        sound is null
            ? null
            : Named(sound, _registry.Available().ToDictionary(one => one.Id, one => one.Name));

    /// <summary>
    /// What that instrument is missing, or nothing when its machine is here.
    /// </summary>
    /// <remarks>
    /// A plugin is never missing a machine: it is not on one. Nor is a kind with no slot, which
    /// is the same thing said in the other direction.
    ///
    /// The name is looked for in the shipped copy first, because that is the only place left
    /// that knows the machine is called Zampler rather than "Sampler", which is all the engine
    /// behind it can say. Where the program ships no copy either, what the song remembered is
    /// the best anything can do.
    /// </remarks>
    /// <param name="sound">The instrument being asked about.</param>
    /// <param name="offered">The shipped machines that are not installed, by id.</param>
    private MissingSoundMachine? Named(TrackerInstrument sound, Dictionary<string, string> offered)
    {
        if (sound.IsPlugin) return null;

        string id = sound.Machine.SlotId;

        if (id.Length == 0) return null;

        bool registered = SoundMachine.Installed.Any(one => one.SlotId == id);

        if (registered && (_rack is null || _rack.Load(id) is not null)) return null;

        bool ships = offered.TryGetValue(id, out string? called) && called.Length > 0;

        return new MissingSoundMachine(
            id,
            registered || !ships ? sound.Machine.Name : called!,
            ships || registered,
            registered);
    }
}
