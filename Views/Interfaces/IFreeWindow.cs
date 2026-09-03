using Avalonia.Controls;

namespace JingleBox2.Views.Interfaces;

/// <summary>
/// Opens a window that stands beside the application rather than over it: a device's face, a
/// plugin's own window, the help.
/// </summary>
/// <remarks>
/// An owned window is always in front of the one that owns it. That is right for a dialog, which
/// is a question that has to be answered before anything else happens, and wrong for everything
/// else here: a machine's panel, an effect off a chain and a plugin's window are all things you
/// work *with* the pattern rather than instead of it, and one that cannot go behind is one you
/// end up dragging out of the way every time you want to see the track it belongs to. Open three
/// of them and the application is underneath a pile of its own windows.
///
/// So they are shown with no owner at all, which is the only way to say it: there is no flag for
/// "owned but not in front", on either platform. Nothing is lost by dropping the owner, because
/// the two things ownership buys are already answered elsewhere. The application shuts down when
/// its main window closes rather than when the last window does, and each of these windows
/// already keeps the one that is open and closes it itself.
///
/// What ownership did buy is where the window lands, since a startup location of CenterOwner
/// needs an owner. That is this, and it is why the two halves are one thing rather than a
/// dropped argument at five call sites: the window is placed over the application by hand,
/// once it is open and has a size to be centred by.
/// </remarks>
public interface IFreeWindow
{
    /// <summary>
    /// Shows the window, unowned, over the middle of another one.
    /// </summary>
    /// <param name="window">The window to open.</param>
    /// <param name="near">
    /// What to open it over, which is the application's own window. Nothing leaves the placing
    /// to whatever the desktop does with a window that asks for nothing.
    /// </param>
    void Show(Window window, Window? near);
}
