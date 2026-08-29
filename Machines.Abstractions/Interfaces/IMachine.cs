using JingleBox2.Machines.Records;

namespace JingleBox2.Machines.Interfaces;

/// <summary>
/// What a machine is, as far as anything outside it is concerned.
/// </summary>
/// <remarks>
/// The host has no business knowing that Zampler has zones or that BongaBong has pads. It has
/// to be able to list what machines there are, tell them apart, put one on a rack and paint it,
/// and that is what this is.
///
/// Everything here is fixed for the life of the machine: what it is called, what it is for, and
/// what colour it is. What an instrument made on it holds, the sound it makes and the panel it
/// is edited on are separate contracts, so that a machine can be described without any of them
/// being loaded.
/// </remarks>
public interface IMachine
{
    /// <summary>
    /// The name this machine is known by in files, forever.
    /// </summary>
    /// <remarks>
    /// Written into every song and every instrument that was made on it, so it can never
    /// change: a machine that renames itself orphans everything anybody made with it. Two
    /// machines with the same id are the same machine, whoever wrote them, which is how a
    /// machine edited in its own project replaces the installed copy of itself.
    ///
    /// It is also what decides whether a machine can be seen at all. A machine is a face over
    /// one of the engines built into the application, and the id is what says which:
    /// <c>Machine.Register</c> looks the id up and refuses one it has no engine for, so a
    /// machine designed elsewhere, under an id of its own, is read off disc and never reaches
    /// the rack. That is deliberate today, since a box that cannot make a sound is worse than
    /// no box, and it is the piece that has to move before a machine written by somebody else
    /// can be installed. There is no mechanism for a machine to bring an engine with it.
    /// </remarks>
    string Id { get; }

    /// <summary>What it is called on the rack.</summary>
    string Name { get; }

    /// <summary>One line saying what it does, for somebody choosing between them.</summary>
    string Summary { get; }

    /// <summary>Its colours, which are its own and not the application's.</summary>
    MachineTheme Theme { get; }
}
