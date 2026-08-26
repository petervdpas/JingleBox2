using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace JingleBox2.Views;

/// <summary>
/// Ctrl+Shift+M, wherever it is pressed.
/// </summary>
/// <remarks>
/// The mode is about the pointer, and the pointer goes wherever the windows are: the main one,
/// an instrument opened in a window of its own, a machine on the bench. A shortcut that only
/// works on one of them is a shortcut you have to think about before using, and the thing it
/// switches is most wanted exactly where it did not work, on a panel you have opened big enough
/// to point at comfortably.
///
/// So the gesture lives here and every window asks for it, rather than each one carrying its
/// own copy of a keystroke to keep in step with the others.
/// </remarks>
public static class LinkKey
{
    /// <summary>Whether the key is being held, so leaning on it does not flap the mode.</summary>
    private static bool _down;

    /// <summary>How many panels are on screen to be pointed at.</summary>
    /// <remarks>
    /// The gesture is only a gesture where there is something to point at: a machine's face,
    /// on the rack or in a window of its own, or a plugin's knobs. On the recordings page or
    /// the settings there is nothing the pointer could offer, so the keystroke does nothing and
    /// says nothing, rather than putting the application into a mode with no visible effect and
    /// no way of noticing.
    ///
    /// Counted rather than asked, because the panels are in three different windows and none of
    /// them knows about the others. Each says when it arrives and when it goes.
    /// </remarks>
    private static int _panels;

    /// <summary>A panel that can be pointed at is on screen.</summary>
    public static void Showing() => _panels++;

    /// <summary>
    /// And is gone. The mode goes with the last of them.
    /// </summary>
    /// <remarks>
    /// Left on with nothing on screen, it would be a mode you are in without knowing, waiting
    /// to swallow the first thing you touched on the controller.
    /// </remarks>
    public static void Gone()
    {
        if (_panels > 0) _panels--;

        if (_panels == 0 && Midi.ControlLink.Current is { } link) link.IsLinking = false;
    }

    /// <summary>True when there is a panel to point at.</summary>
    public static bool Pointable => _panels > 0;

    /// <summary>Has this window answer the gesture too.</summary>
    /// <remarks>
    /// Tunnelling, so it is answered before anything inside the window can take the keystroke
    /// for something of its own.
    /// </remarks>
    public static void Listen(TopLevel window)
    {
        if (window is null) return;

        window.AddHandler(InputElement.KeyDownEvent, Pressed, RoutingStrategies.Tunnel);
        window.AddHandler(InputElement.KeyUpEvent, Released, RoutingStrategies.Tunnel);
    }

    private static void Pressed(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.M) return;
        if (e.KeyModifiers != (KeyModifiers.Control | KeyModifiers.Shift)) return;

        // Nothing on screen to point at. Not swallowed either: a keystroke that does nothing
        // here may mean something to whatever is in front of you.
        if (!Pointable) return;

        // Held down rather than pressed again. Swallowed either way, so a leant-on key does
        // nothing at all rather than something else.
        if (!_down && Midi.ControlLink.Current is { } link) link.IsLinking = !link.IsLinking;

        _down = true;
        e.Handled = true;
    }

    private static void Released(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.M) _down = false;
    }
}
