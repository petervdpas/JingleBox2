using System.Collections.Generic;
using Avalonia.Controls;
using JingleBox2.Rack.SoundDevices.Faces.Records;

namespace JingleBox2.Rack.Controls.Interfaces;

/// <summary>
/// What a machine offers, drawn as menu items.
/// </summary>
/// <remarks>
/// <see cref="PanelMenuItem"/> is deliberately a shape and not a toolkit type, so that what a
/// host offers can be put a question to without a window. This is the other half: the one place
/// those lines become something on a screen.
///
/// One place because three things show them: a sound device's own Menu part, the mixer's button
/// and FIRE's, and the last two are drawn by the program rather than described by anybody. Three
/// spellings of "a line with no command is dead" would eventually disagree, and the way that
/// fails is one of them quietly not offering something.
/// </remarks>
public interface IMenuLines
{
    /// <summary>The same lines as menu items, with a divider between parts, ready to be an items source.</summary>
    /// <remarks>
    /// Handed over as a source rather than built into a menu, since a control put into an items
    /// source is its own container. Nothing here decides anything: a line with nothing to do is a
    /// line with no command.
    /// </remarks>
    /// <param name="offers">The lines to draw.</param>
    IReadOnlyList<Control> Listed(IEnumerable<PanelMenuItem> offers);

    /// <summary>
    /// Whether a divider goes in front of that line: it is in another part of the Menu from the line above it.
    /// </summary>
    /// <remarks>
    /// Never above the first line, since a divider there divides nothing.
    /// </remarks>
    /// <param name="above">The line above, or nothing for the first.</param>
    /// <param name="line">The line.</param>
    bool Divides(PanelMenuItem? above, PanelMenuItem line);
}
