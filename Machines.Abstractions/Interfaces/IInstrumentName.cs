namespace JingleBox2.Machines.Interfaces;

/// <summary>
/// The instrument's name in the song, which is the one thing on a panel that is not the machine's.
/// </summary>
/// <remarks>
/// A machine is called what the machine is called. An instrument is a machine in use and is
/// yours to call anything, so two of them off one machine can be Bassline and Lead, and that
/// name is the one thing on the face that is not the machine's.
///
/// So the machine cannot hold it and the host must. The same arrangement
/// <see cref="IMachinePresets"/> and <see cref="IMachineZones"/> already keep, and for the same
/// reason: it is not a setting, it cannot be written into the machine, and it changes while the
/// panel is on screen.
///
/// Where it goes is the machine's, which is the whole point of it being a part. This program
/// used to draw it in a corner over every panel, which is the one thing a machine's face is
/// never supposed to have done to it: a machine that had never asked for a name badge grew one,
/// and a machine that put something of its own in that corner had the two drawn on top of each
/// other.
/// </remarks>
public interface IInstrumentName
{
    /// <summary>What it is called, and what it is renamed to.</summary>
    string Said { get; set; }

    /// <summary>
    /// True when the name may not be changed here.
    /// </summary>
    /// <remarks>
    /// A machine on the rack keeps the machine's own name: renaming it there would be renaming
    /// the machine, which is a different act in a different place. Duplicating it gives you one
    /// that is yours to call anything.
    /// </remarks>
    bool Fixed { get; }
}
