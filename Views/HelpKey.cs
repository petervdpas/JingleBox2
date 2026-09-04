using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using JingleBox2.Help;

namespace JingleBox2.Views;

/// <summary>
/// Ctrl+H, on whichever window you happen to be in, opening the help on the keyboard page.
/// </summary>
/// <remarks>
/// A door, like <see cref="DeckKeys"/> and <see cref="LinkKey"/>, and for the same reason: an
/// application has one help window, and handing it about would be handing the same object about
/// under another name. What it holds is a class handler and nothing else.
///
/// Registered against <see cref="Window"/> once rather than hung window by window, which is the
/// lesson the transport's two keys cost: hung one at a time it is a call every new window has to
/// remember, and the one that forgets is a window where the key silently does nothing. A class
/// handler reaches every window written after this as well.
///
/// The decision is <see cref="Wants"/> and has no window in it, so what the keystroke means can
/// be put a question to without a keyboard.
///
/// It goes to the keyboard page rather than to whatever the page in front is about. Those two
/// are different questions and each already has its answer: a help badge beside a panel opens
/// that panel's topic, and this is somebody asking what they can press. Both land in the same
/// window, so either is one click from the other.
/// </remarks>
public static class HelpKey
{
    /// <summary>Whether the handler is already registered, since once is the point.</summary>
    private static bool _listening;

    /// <summary>
    /// Has every window in this application answer Ctrl+H, once and for all.
    /// </summary>
    /// <remarks>
    /// On the way down, like the transport's keys and the pointing mode, so a control that
    /// would otherwise spend the keystroke does not get the chance.
    /// </remarks>
    public static void ListenEverywhere()
    {
        if (_listening) return;

        _listening = true;

        Window.KeyDownEvent.AddClassHandler<Window>(
            (window, e) =>
            {
                if (e.Handled || Shortcuts.LearningKeys.On) return;

                if (!Wants(e.Key, e.KeyModifiers)) return;

                HelpWindow.Show(HelpText.AppShortcuts, window);

                e.Handled = true;
            },
            RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Whether a keystroke is asking for the help, which is the whole of the rule.
    /// </summary>
    /// <remarks>
    /// The modifiers have to agree exactly, the same rule the shortcut map keeps: Ctrl+Shift+H
    /// is not Ctrl+H with something else held down, and reading it as one would be reading past
    /// the thing that tells them apart.
    ///
    /// Nothing is asked about where the keyboard is, unlike the transport's keys. A space in a
    /// name is a space and Ctrl+R in one is somebody typing, so both have to stand down while a
    /// caret is blinking; no text box anywhere has ever done anything with Ctrl+H, and somebody
    /// stuck halfway through filling a dialog in is exactly who wants the help.
    /// </remarks>
    /// <param name="key">The key that went down.</param>
    /// <param name="modifiers">What was held with it.</param>
    public static bool Wants(Key key, KeyModifiers modifiers) =>
        key == Key.H && modifiers == KeyModifiers.Control;
}
