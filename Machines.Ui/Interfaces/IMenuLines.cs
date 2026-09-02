using System.Collections.Generic;
using Avalonia.Controls;
using JingleBox2.Machines.Records;

namespace JingleBox2.Machines.Ui.Interfaces;

/// <summary>
/// What a machine offers, drawn as menu items.
/// </summary>
/// <remarks>
/// <see cref="MachineMenuItem"/> is deliberately a shape and not a toolkit type, so that what a
/// host offers can be put a question to without a window. This is the other half: the one place
/// those lines become something on a screen.
///
/// One place because there are two things that show them, a machine's own Menu part and the
/// mixer's button, and the mixer is drawn by the program rather than described by anybody. Two
/// spellings of "a line with children is a submenu, a line with no command is dead" would
/// eventually disagree, and the way that fails is one of them quietly not offering something.
/// </remarks>
public interface IMenuLines
{
    /// <summary>The same lines as menu items, ready to be an items source.</summary>
    /// <remarks>
    /// Handed over as a source rather than built into a menu, since a control put into an items
    /// source is its own container. Nothing here decides anything: a line with nothing to do is a
    /// line with no command.
    /// </remarks>
    /// <param name="offers">The lines to draw.</param>
    IReadOnlyList<MenuItem> Listed(IEnumerable<MachineMenuItem> offers);
}
