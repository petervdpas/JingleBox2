using System;

namespace JingleBox2.Audio.Plugins.Interfaces;

/// <summary>
/// One keystroke answered on a window, whatever inside it holds the keyboard.
/// </summary>
/// <remarks>
/// A plugin's interface is another program's window living inside one of ours, and while it has
/// the keyboard the operating system delivers every key to that program. Nothing in this process
/// is on that path, so a shortcut hung on the window by the toolkit is simply never asked:
/// pressing it over Serum's own face does nothing and there is no event to see.
///
/// This is the way round it, and it is unavoidably per platform: what the toolkit cannot see,
/// only the window system can be asked for. So the contract is here and the answer is somebody
/// else's, which is also what keeps the rest of the application free of it.
///
/// It is deliberately one keystroke rather than a keyboard. A window somebody else is drawing in
/// belongs to them, and taking their keys wholesale would break the plugin: what is wanted is the
/// one combination this application has to be able to answer, and everything else left alone.
///
/// Never a global shortcut. Nothing outside the named window is touched, so the same combination
/// goes on meaning whatever it means everywhere else on the machine.
/// </remarks>
public interface IWindowShortcut
{
    /// <summary>
    /// Answers Ctrl+Shift+M on a window until the answer is let go of.
    /// </summary>
    /// <remarks>
    /// Nothing where this platform has no way to do it, which is not a failure: the toolkit's own
    /// handler still answers the keystroke while the window's own chrome has the keyboard, so
    /// what is lost is the case where the plugin has it and what is gained is nothing worse than
    /// the state before.
    /// </remarks>
    /// <param name="kind">
    /// What the toolkit calls this handle, which is what decides whether it can be reached at
    /// all. Asked of the handle rather than of the operating system, because on Linux they are
    /// not the same question: a desktop running Wayland runs X clients through XWayland, so the
    /// toolkit still hands out an X window, while a toolkit drawing natively on Wayland would
    /// hand out something else.
    /// </param>
    /// <param name="window">The window to answer on, as the platform names one.</param>
    /// <param name="pressed">
    /// Told each time the key goes down, on whatever thread the platform answers on. Marshalling
    /// it to the drawing thread is the caller's business, since only the caller knows what it is
    /// going to do.
    /// </param>
    /// <returns>Let go of to stop, or nothing where the platform cannot do it.</returns>
    IDisposable? On(string kind, nint window, Action pressed);
}
