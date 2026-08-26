using System.Collections.Generic;
using System.Linq;

namespace JingleBox2.Tracker.Machines;

/// <summary>One machine a song plays on that this installation has not got.</summary>
/// <param name="Id">The slot the song's instruments name.</param>
/// <param name="Name">What the machine is called, which the song remembers on its own.</param>
/// <param name="Ships">True when the program has a copy to add. False for one that came in from a zip.</param>
public sealed record MissingMachine(string Id, string Name, bool Ships);

/// <summary>
/// Which machines a song needs and this installation has not got.
/// </summary>
/// <remarks>
/// A song carries its instruments whole: every setting, every recording it points at, and the
/// name of the machine each one came off. What it cannot carry is the machine, so a song written
/// where Zampler was installed and opened where it was not still plays exactly as it did, and
/// still saves, and simply has no panel to show for those instruments.
///
/// That is worth saying out loud rather than leaving as an empty box, which is all this is for:
/// look at what the song is asking for, and say which of it is not here.
///
/// What counts as here is what the rack counts: a machine is installed or it is not, and there
/// is one list that answers it.
/// </remarks>
public static class MissingMachines
{
    /// <summary>
    /// The machines this song plays on that are not installed, each named once.
    /// </summary>
    /// <remarks>
    /// Plugins are left out. A missing plugin is already reported when the song's chains are
    /// rebuilt, and there is nothing this program could offer to add: a plugin is somebody
    /// else's, sitting wherever they put it.
    /// </remarks>
    public static IReadOnlyList<MissingMachine> For(Song song)
    {
        var wanted = new List<MissingMachine>();

        if (song?.Instruments is not { } instruments) return wanted;

        // What the program has a copy of, and what those copies call themselves. A machine that
        // is not installed has no name of its own here, so the crate is the only place left that
        // knows it is called Zampler rather than "Sampler", which is all the engine can say.
        var offered = MachineRegistry.Available().ToDictionary(one => one.Id, one => one.Name);

        var said = new HashSet<string>();

        foreach (var sound in instruments)
        {
            if (sound is null || sound.IsPlugin) continue;

            string id = sound.Machine.SlotId;

            if (id.Length == 0 || !said.Add(id)) continue;

            if (Machine.Installed.Any(one => one.SlotId == id)) continue;

            bool ships = offered.TryGetValue(id, out string? called) && called.Length > 0;

            wanted.Add(new MissingMachine(id, ships ? called! : sound.Machine.Name, ships));
        }

        return wanted;
    }
}
