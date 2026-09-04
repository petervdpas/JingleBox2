using Avalonia.Input;
using JingleBox2.Shortcuts.Enums;
using JingleBox2.Shortcuts.Interfaces;

namespace JingleBox2.Shortcuts;

/// <inheritdoc/>
public sealed class ShortcutCatcher : IShortcutCatch
{
    /// <inheritdoc/>
    public ShortcutCatch Means(Key key, KeyModifiers modifiers)
    {
        if (Alone(key)) return ShortcutCatch.Waiting;

        if (modifiers == KeyModifiers.None)
        {
            if (key == Key.Escape) return ShortcutCatch.Cancel;

            if (key is Key.Back or Key.Delete) return ShortcutCatch.Clear;
        }

        return Allowed(key, modifiers) ? ShortcutCatch.Take : ShortcutCatch.Refused;
    }

    /// <summary>
    /// Whether a keystroke is one a page shortcut may be, which is Ctrl+Alt and a letter.
    /// </summary>
    /// <remarks>
    /// Narrow on purpose, and the narrowness is what makes it safe. Every other key this
    /// application answers is written into a door or is one of the system's four, and every one
    /// of those is a letter with Ctrl, or Ctrl and Shift, or nothing at all. Nothing anywhere
    /// uses Ctrl with Alt, so a page key cannot land on top of something that already works, and
    /// there is no need for anybody setting one to know what is taken.
    ///
    /// Letters only, rather than any key: a digit with those two modifiers is a character on
    /// several keyboard layouts, and a function key is where a machine's own window manager
    /// tends to live.
    /// </remarks>
    /// <param name="key">The key that went down.</param>
    /// <param name="modifiers">What was held with it.</param>
    private static bool Allowed(Key key, KeyModifiers modifiers) =>
        modifiers == (KeyModifiers.Control | KeyModifiers.Alt) && key is >= Key.A and <= Key.Z;

    /// <inheritdoc/>
    public KeyGesture Gesture(Key key, KeyModifiers modifiers) => new(key, modifiers);

    /// <summary>
    /// Whether that key is a modifier with nothing on it yet.
    /// </summary>
    /// <remarks>
    /// Ctrl goes down before the letter does, every time, so a listener that took the first key
    /// it was given would learn Ctrl and never see the shortcut anybody meant.
    /// </remarks>
    /// <param name="key">The key that went down.</param>
    private static bool Alone(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin
            or Key.System;
}
