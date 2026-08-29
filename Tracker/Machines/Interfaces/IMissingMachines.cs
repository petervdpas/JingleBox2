using System.Collections.Generic;
using JingleBox2.Tracker.Machines.Records;

namespace JingleBox2.Tracker.Machines.Interfaces;

/// <summary>
/// Which machines a song needs and this installation has not got.
/// </summary>
/// <remarks>
/// A song carries its instruments whole: every setting, every recording it points at, and the
/// name of the machine each one came off. What it cannot carry is the machine. So a song written
/// where Zampler was installed and opened where it was not saves again unchanged and goes on
/// naming Zampler, and the instruments on it are silent until it is back.
///
/// Which makes saying it out loud the whole of what this is for: look at what the song is asking
/// for, and say which of it is not here, because the song itself has gone quiet about it.
///
/// What counts as here is what the rack counts: a machine is installed or it is not, and there
/// is one list that answers it.
///
/// A seam rather than a static class because the answer depends on what is on the disc: the
/// registry is asked what ships, so nothing could be put a question to about a song without an
/// installation to ask it on.
/// </remarks>
public interface IMissingMachines
{
    /// <summary>
    /// The machines this song plays on that are not installed, each named once.
    /// </summary>
    /// <remarks>
    /// Plugins are left out. A missing plugin is already reported when the song's chains are
    /// rebuilt, and there is nothing this program could offer to add: a plugin is somebody
    /// else's, sitting wherever they put it.
    ///
    /// What the registry offers is read first for the sake of the names. A machine that is not
    /// installed has no name of its own here, so the shipped copy is the only place left that
    /// knows it is called Zampler rather than "Sampler", which is all the engine can say. Where
    /// the program has no copy either, the song's own remembered name is used, which is the best
    /// anything can do for a machine that came in from somebody's zip.
    /// </remarks>
    /// <param name="song">The song being opened, or nothing.</param>
    IReadOnlyList<MissingMachine> For(Song song);

    /// <summary>
    /// The same answer for one instrument: what it is missing, or nothing when it is fine.
    /// </summary>
    /// <remarks>
    /// Asked where somebody has tried to open that one instrument, which is a different moment
    /// from opening a song and wants a different sentence. The naming is the reason it is here
    /// rather than worked out at the point of asking: an instrument whose machine is gone can
    /// only say what its engine is called, so "Ouroboros" has to be fetched from the shipped
    /// copy or from what the song remembered, and a second place doing that would eventually
    /// say "Mono synth" to somebody.
    /// </remarks>
    /// <param name="sound">The instrument being opened, or nothing.</param>
    MissingMachine? For(TrackerInstrument? sound);
}
