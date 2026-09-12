using System.Collections.Generic;
using JingleBox2.Config.Interfaces;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// The control templates block: what every controller does to everything, in memory.
/// </summary>
/// <remarks>
/// **A template is one controller's layout for one thing**, which is what your nanoKONTROL2 does
/// to OddSkilla, and the whole of what this application knows about hardware pointed at anything
/// is a list of them. So they are one block, read once, written to by whoever points a control at
/// something, and read by everything that has to answer what a control is for.
///
/// One direction, which is the whole of why it exists. The interface writes here and says so;
/// what reaches the disc, and when, is an observer's business and not a page's. The mixer, a
/// sound device's face and its menu read from here rather than each keeping an answer of their
/// own, so two of them cannot come to disagree about what a knob is doing.
///
/// The list is handed out rather than wrapped, the same as <see cref="ISettingsBlock.Config"/>:
/// a template is a small object of plain words that is already serialised as it stands, and
/// putting it behind a second spelling would be a second shape to keep in step with the file
/// somebody can write by hand.
///
/// <see cref="IMemoryBlock.Changed"/> is a hint here as everywhere: a list added to in place
/// moves nothing anybody could have subscribed to, so a writer is quick because it is told and
/// right because it compares what it would write with what it wrote.
/// </remarks>
public interface IControlTemplateBlock : IMemoryBlock
{
    /// <summary>
    /// Every template this installation has, which is the same list everything else is holding.
    /// </summary>
    /// <remarks>
    /// One per controller per thing pointed at, which is what a card on the MIDI CC page is and
    /// what a <c>.jbtl</c> file holds. Two controllers pointed at one machine are two templates
    /// and never one, since a link answers only its own controller's messages and the two can
    /// never compete.
    /// </remarks>
    List<ControlTemplate> Templates { get; }

    /// <summary>Says that something among the templates has just been changed.</summary>
    /// <remarks>
    /// A hint, for the reason on <see cref="IMemoryBlock.Changed"/>: it makes a writer keep up
    /// rather than making it correct, so forgetting it costs a moment rather than somebody's
    /// layout.
    /// </remarks>
    void Moved();
}
