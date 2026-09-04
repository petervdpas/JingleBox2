using Avalonia.Input;
using JingleBox2.Shortcuts.Enums;

namespace JingleBox2.Shortcuts.Interfaces;

/// <summary>
/// What a keystroke means while a shortcut is being put on a key.
/// </summary>
/// <remarks>
/// The whole of it is here rather than in the control that listens, so what a key means in that
/// moment can be put a question to without a window, a keyboard or a hand. That matters more
/// here than usual: every interesting case is a key nobody would think to press on purpose.
///
/// Three of the four answers are not a shortcut, and each one is somebody's hand doing something
/// other than choosing. A modifier on its own is a hand arriving, and a control that took it
/// would learn Ctrl every single time, since Ctrl goes down before the letter does. Escape is
/// changing your mind, which every dialog in the world spells that way. Backspace is taking the
/// shortcut off, which is the only other thing somebody wants from a box that is listening.
///
/// A modifier with something on it is a shortcut, so Ctrl+Escape and Ctrl+Backspace are real
/// answers rather than the two above: what those two mean on their own is a fact about them
/// being alone.
/// </remarks>
public interface IShortcutCatch
{
    /// <summary>What that keystroke means, while listening.</summary>
    /// <param name="key">The key that went down.</param>
    /// <param name="modifiers">What was held with it.</param>
    ShortcutCatch Means(Key key, KeyModifiers modifiers);

    /// <summary>The shortcut that keystroke is, for one this answered <c>Take</c> to.</summary>
    /// <param name="key">The key that went down.</param>
    /// <param name="modifiers">What was held with it.</param>
    KeyGesture Gesture(Key key, KeyModifiers modifiers);
}
