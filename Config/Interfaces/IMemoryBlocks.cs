using System.Collections.Generic;
using JingleBox2.Audio.Routing.Interfaces;

namespace JingleBox2.Config.Interfaces;

/// <summary>
/// Everything this application knows, in memory, in one place that is handed about.
/// </summary>
/// <remarks>
/// **One of them, handed down, and never reachable.** There is one because there is one
/// application, so it is built where the settings are read and the engine is opened and passed to
/// whoever needs it. What it must not have is a <c>Current</c> or an <c>Instance</c>: an ambient
/// singleton is a static class wearing another hat, whatever a test put in it is still there for
/// the next test, and this executable runs again as a plugin's host where there are no settings
/// at all.
///
/// It is also what stops a page owning a fact about the machine. A page that is built, shown,
/// hidden and thrown away writes a block and reads one, and nothing about the machine depends on
/// which page is in front. The fault that argued for all of this is in
/// <c>docs/memory-blocks.md</c>: a browser lined up on the input came back onto the speakers
/// because somebody changed tab.
/// </remarks>
public interface IMemoryBlocks
{
    /// <summary>What is written down: the audio, the MIDI, the pads, the keys and the rest.</summary>
    /// <remarks>
    /// The document as it is today, one object read from and written to one file. Sections inside
    /// it are the next step rather than this one, and the file on everybody's disc is the
    /// constraint on how far that goes.
    /// </remarks>
    ISettingsBlock Settings { get; }

    /// <summary>What the input is set to this run, which is never written down.</summary>
    IInputSetting Input { get; }

    /// <summary>
    /// What every controller is pointed at, which is what is stored.
    /// </summary>
    /// <remarks>
    /// The links themselves, port and all, in a file of their own. <see cref="Templates"/> is the
    /// same fact in the form a face reads and a person carries between machines; this is the form
    /// this installation keeps.
    /// </remarks>
    Midi.Interfaces.IControlLinkBlock Links { get; }

    /// <summary>
    /// What every controller does to everything, which is the templates.
    /// </summary>
    /// <remarks>
    /// A block of its own rather than a corner of the settings, because it is read by everything
    /// that answers what a control is for: the router per message, a mixer strip, and a sound
    /// device's face and its menu. A list every one of those keeps a reading of is a list they
    /// can come to disagree about.
    /// </remarks>
    Midi.Interfaces.IControlTemplateBlock Templates { get; }

    /// <summary>Every block, for whatever walks them rather than naming one.</summary>
    IReadOnlyList<IMemoryBlock> Blocks { get; }
}
