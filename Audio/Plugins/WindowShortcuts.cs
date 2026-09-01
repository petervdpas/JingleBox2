using System;
using JingleBox2.Audio.Plugins.Interfaces;

namespace JingleBox2.Audio.Plugins;

/// <inheritdoc/>
/// <remarks>
/// Which window system the handle came from, asked of the handle rather than of the operating
/// system. The same binary runs on all of them, and on Linux they are not the same question: a
/// desktop running Wayland runs X clients through XWayland, so the toolkit still hands out an X
/// window and the grab still works, while a toolkit drawing natively on Wayland would hand out
/// something else and it would not. Asking the handle what it is gets both right and needs no
/// list of desktops.
///
/// It is also already true that a plugin drawing its own interface here is an X client whatever
/// is running the desktop, since embedding somebody else's window at all is XEmbed, which is X11.
///
/// Anything that is not an X window answers nothing, and that is a gap rather than a decision.
/// Windows has the same problem and wants the same answer through a hook of its own, and this is
/// where it goes when somebody writes it. Until then the toolkit's own handler covers the
/// window's own chrome there, which is where it always worked.
/// </remarks>
public sealed class WindowShortcuts : IWindowShortcut
{
    /// <summary>What the toolkit calls a handle that is an X window.</summary>
    /// <remarks>
    /// Avalonia's word, written out rather than worked out, so what this compares against is
    /// visible to anybody reading it and to anybody searching for it.
    /// </remarks>
    public const string XWindow = "XID";

    /// <summary>How X11 does it, made once since it holds nothing of its own.</summary>
    private static readonly XWindowShortcut OnX11 = new();

    /// <inheritdoc/>
    public IDisposable? On(string kind, nint window, Action pressed) =>
        string.Equals(kind, XWindow, StringComparison.Ordinal) ? OnX11.On(window, pressed) : null;
}
