using System.Collections.Generic;
using JingleBox2.Tracker.Enums;

namespace JingleBox2.Devices.SoundMachines.Interfaces;

/// <summary>
/// The machines this run is working with, kept where a panel can ask for one.
/// </summary>
/// <remarks>
/// <see cref="JingleBox2.Devices.Interfaces.IRackRegistry{T}"/> reads the machines off the disc once, at startup, and hands
/// back what it found. Until now that list was counted into the log and dropped, because the
/// only thing anything wanted from a machine was its name and its colour, and those had already
/// been pushed into <see cref="Records.SoundMachine"/>. A panel drawn from a machine's own description wants
/// the machine itself: the parameters and the face. So the list is kept.
///
/// It was a static class holding a static dictionary, and that is the one thing here that was
/// plainly wrong. Which machines are installed is a fact about the run, not about the program,
/// and a static one is a fact about the process: one test calling <see cref="Devices.Interfaces.IRackDevices{T}.Keep"/> changed
/// what every test after it saw, in whatever order the runner happened to pick. The dictionary
/// is an ordinary field on an ordinary object now, so a test makes its own and it is thrown away
/// with the test, and the application keeps the one it made at startup exactly as before.
///
/// Read many times a second while a panel is on screen, written once when the rack is read.
/// </remarks>
public interface ISoundMachineProjects : Devices.Interfaces.IRackDevices<SoundMachineProject>
{
    /// <summary>
    /// Whether the machine an instrument of that kind is on is installed here.
    /// </summary>
    /// <remarks>
    /// Asked before anything sounds. An instrument is on a machine, so one whose machine is not
    /// registered here has nothing to play on and is silent. It names that machine and goes on
    /// naming it until the track is pointed at another instrument.
    ///
    /// By kind rather than by id because that is what an instrument holds. The kind names the
    /// engine, the engine names the machine's own slot, and the slot is the id this was read
    /// from disc under. A kind with no slot, which is a plugin, is never refused here: a plugin
    /// is not a machine project and its absence is a different absence with its own answer.
    /// </remarks>
    /// <param name="kind">The engine an instrument is on, which is how it names its machine.</param>
    bool Has(TrackerInstrumentKind kind);

    /// <summary>
    /// Takes which machines are on the rack, by slot id, replacing whatever was known before.
    /// </summary>
    /// <remarks>
    /// Held here rather than asked for, because <see cref="Has"/> is asked on the audio thread
    /// before every note and the rack is files on a disc.
    ///
    /// The rack decides which machines a song can be given, so a machine taken off it is one
    /// this installation is not offering, and an instrument on it is in exactly the position of
    /// an instrument whose machine was never registered: silent, no panel, and named as missing.
    /// Anything else would be a machine you cannot pick but can still hear, which is a state
    /// nobody chose and nothing on the screen explains.
    ///
    /// Told nothing, everything registered counts, which is what a caller with no rack wants:
    /// a test, a preview, or the machine designer.
    /// </remarks>
    /// <param name="slots">The slot ids of the machines on the rack.</param>
    void OnRack(IEnumerable<string> slots);
}
