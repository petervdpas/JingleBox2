using System.Collections.Generic;
using JingleBox2.Config.Interfaces;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// The links block: what every controller is pointed at, in memory.
/// </summary>
/// <remarks>
/// **The stored half of remote control.** A gesture writes here, a writer puts it on disc, and
/// <see cref="IControlTemplateBlock"/> is what the same fact looks like to a face: one controller
/// against one thing, in the travelling form. Two blocks rather than one because they are two
/// shapes of one thing and only one of them can be stored: a template names a controller as its
/// profile calls it and never a port, so keeping templates would mean settling a port again on
/// every start.
///
/// It is written from two threads, which is why the list is handed out rather than wrapped and
/// why <see cref="ControlLink"/> is the one thing that edits it: a hand on a page and a hand on
/// the hardware both reach it, and the one that arrives from the hardware arrives on the MIDI
/// thread.
/// </remarks>
public interface IControlLinkBlock : IMemoryBlock
{
    /// <summary>
    /// Everything pointed at anything, which is the same list everything else is holding.
    /// </summary>
    /// <remarks>
    /// Handed out rather than wrapped, the same as <see cref="ISettingsBlock.Config"/>: it is
    /// what is serialised as it stands, and a second spelling would be a second shape to keep in
    /// step with a file somebody can read.
    /// </remarks>
    List<ControlMapping> Links { get; }

    /// <summary>Says that something among the links has just been changed.</summary>
    void Moved();
}
