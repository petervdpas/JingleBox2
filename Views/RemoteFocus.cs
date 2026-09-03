using Avalonia.Controls;
using JingleBox2.ViewModels.Interfaces;

namespace JingleBox2.Views;

/// <summary>
/// Which window a hardware knob is pointed into: the one with the focus, wherever it is.
/// </summary>
/// <remarks>
/// A door, like <see cref="DeckKeys"/> and the log, and for the same reason: an application has
/// one control surface pointed at one thing at a time, and handing that fact about would be
/// handing the same object about under another name.
///
/// **Once for the type rather than once per window, which is the whole point.** Every window that
/// had something pointable on it wired its own pair of handlers, in its own spelling, and the
/// three that existed already disagreed: one claimed on being activated and let go on being
/// deactivated, one claimed on opening and on being activated and only let go when it closed, and
/// one relayed the whole thing through a callback nobody passed. So a device window left behind
/// the application went on being what a knob wrote into, which is a knob moving a control nobody
/// can see. Hung on <see cref="Window"/> itself it applies to every window there is and every one
/// written after this, so there is nothing to remember and nothing to forget.
///
/// A window says what it offers by what it is showing: its data context, when that is an
/// <see cref="IInFront"/>. A window showing something else offers nothing and is not asked
/// again, which is every dialog and the application's own window.
///
/// Said on opening as well as on being activated, because opening a window is coming to the front
/// and there is no guarantee anything else will say so: whether a window is told it was activated
/// is the window manager's business, and under a bare X server there is nobody to tell it. Saying
/// it twice costs one assignment.
/// </remarks>
public static class RemoteFocus
{
    /// <summary>Whether the handler is already registered, since it is registered for the type.</summary>
    private static bool _listening;

    /// <summary>
    /// Has every window in this application hand the control remote over as it takes the focus.
    /// </summary>
    public static void ListenEverywhere()
    {
        if (_listening) return;

        _listening = true;

        Window.WindowOpenedEvent.AddClassHandler<Window>(
            (window, _) =>
            {
                Face(window)?.InFront();

                window.Activated += (_, _) => Face(window)?.InFront();
                window.Deactivated += (_, _) => Face(window)?.NotInFront();
                window.Closed += (_, _) => Face(window)?.NotInFront();
            });
    }

    /// <summary>What this window offers a knob, or nothing where it offers none.</summary>
    /// <remarks>
    /// Asked again each time rather than kept, since a window can be pointed at another box
    /// while it is open and the answer would then be the one it was showing before.
    /// </remarks>
    private static IInFront? Face(Window window) => window.DataContext as IInFront;
}
