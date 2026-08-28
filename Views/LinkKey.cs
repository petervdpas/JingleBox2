using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

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
    /// <summary>
    /// When the gesture was last answered, so leaning on the key does not flap the mode.
    /// </summary>
    /// <remarks>
    /// Time rather than a flag saying the key is down, and the difference is the whole of a
    /// fault worth remembering. A flag has to be cleared by the key coming up, and the key can
    /// come up somewhere else: a window loses focus while the key is held, the release goes to
    /// whatever took the focus, and the flag stays set for ever. From then on every press is
    /// swallowed and the mode is stuck in whichever state it was left, which reads as the
    /// keystroke having stopped working.
    ///
    /// A clock cannot be stranded. Held down, the repeats keep pushing the moment forward and
    /// nothing happens; let go and press again and it answers. The same rule the instrument
    /// knobs and the automation recorder use for the same reason: what a person did is one
    /// thing, however many times the machinery says it.
    /// </remarks>
    private static System.DateTime _answered;

    /// <summary>Longer than a keyboard repeats and shorter than anybody presses twice on purpose.</summary>
    public const double AgainMs = 250;

    /// <summary>
    /// Whether the gesture should be answered, which is the whole of the rule and no keystrokes.
    /// </summary>
    /// <remarks>
    /// Out here so it can be put a question to without a window, a keyboard or a controller.
    /// Everything above it is Avalonia and everything below it is a decision.
    /// </remarks>
    public static bool Answers(bool pointable, System.TimeSpan since) =>
        pointable && since.TotalMilliseconds >= AgainMs;

    /// <summary>How many views that allow pointing are on screen.</summary>
    /// <remarks>
    /// The gesture is only a gesture on the views that allow it: a machine's face, on the rack
    /// or in a window of its own; the same machine as a track's instrument; a plugin's knobs;
    /// and the mixer. On the recordings page or the settings there is nothing to lay out, so the
    /// keystroke does nothing and says nothing, rather than putting the application into a mode
    /// with no visible effect and no way of noticing.
    ///
    /// A gate on views and not on controls. Hanging a mapping on a control says what that
    /// control would offer; it does not say the page it stands on is a page anybody meant to lay
    /// out a controller from.
    ///
    /// Counted rather than asked, because the views are in three different windows and none of
    /// them knows about the others. Each says when it arrives and when it goes.
    /// </remarks>
    private static int _panels;

    /// <summary>A view that allows pointing is on screen.</summary>
    /// <remarks>
    /// Called by <see cref="Watch"/> rather than by hand. Public because the count is what the
    /// gesture is gated on and a view that wants to say so directly may.
    /// </remarks>
    public static void Showing() => _panels++;

    /// <summary>
    /// Has this view open the gate while it is on screen, and shut it when it is not.
    /// </summary>
    /// <remarks>
    /// The list of views that allow pointing is exactly the list of views that call this, which
    /// is the point of it being a call: there is one place to look, and a control being
    /// pointable is not on its own an invitation. A knob on a page nobody meant to lay out is
    /// still a knob.
    ///
    /// Visibility as well as attachment, because a page inside the tracker is not taken out of
    /// the tree when another page is shown, it is only hidden. Counted by change rather than by
    /// event, so a view that is told twice is still counted once.
    /// </remarks>
    public static void Watch(Control view)
    {
        if (view is null) return;

        bool counted = false;

        void Look()
        {
            bool showing = view.IsAttachedToVisualTree() && view.IsVisible;

            if (showing == counted) return;

            counted = showing;

            if (showing) Showing();
            else Gone();
        }

        view.AttachedToVisualTree += (_, _) => Look();
        view.DetachedFromVisualTree += (_, _) => Look();

        view.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.IsVisibleProperty) Look();
        };

        Look();
    }

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
    }

    private static void Pressed(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.M) return;
        if (e.KeyModifiers != (KeyModifiers.Control | KeyModifiers.Shift)) return;

        // Nothing on screen to point at. Not swallowed either: a keystroke that does nothing
        // here may mean something to whatever is in front of you.
        if (!Pointable) return;

        var now = System.DateTime.UtcNow;

        if (Answers(Pointable, now - _answered) && Midi.ControlLink.Current is { } link)
            link.IsLinking = !link.IsLinking;

        // Whether it answered or not. A key leant on is one gesture however many times the
        // keyboard says it, and every repeat pushes the moment forward so none of them counts.
        _answered = now;
        e.Handled = true;
    }
}
