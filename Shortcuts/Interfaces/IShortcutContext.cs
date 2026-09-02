using JingleBox2.Shortcuts.Enums;

namespace JingleBox2.Shortcuts.Interfaces;

/// <summary>
/// Something that answers keystrokes, when it is the thing you are looking at.
/// </summary>
/// <remarks>
/// Not every page wants every shortcut, and the ones it does want mean different things: saving
/// on TRACKER is a song and on DESIGNER is a machine, and on the pages that have nothing to save
/// the keystroke should pass through rather than doing something surprising.
///
/// So a page says what it can do rather than being told. The dispatcher starts at whatever has
/// the keyboard and walks outwards until something says yes, which makes the answer follow the
/// pointer without anybody keeping a register of which page is in front. A dialog gets its own
/// answer for the same reason and by the same route.
///
/// The same shape <see cref="ViewModels.TransportSwitch"/> already uses for play and record: the
/// page you are on owns the keys, and a page with nothing to play does nothing with them.
/// </remarks>
public interface IShortcutContext
{
    /// <summary>True when this would do something. False lets the key carry on outwards.</summary>
    bool Can(ShortcutAction action);

    /// <summary>Does it. Only ever called when <see cref="Can"/> has just said yes.</summary>
    void Do(ShortcutAction action);
}
