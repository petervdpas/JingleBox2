using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using JingleBox2.UI;
using JingleBox2.ViewModels;
using JingleBox2.Views.Enums;

namespace JingleBox2.Views;

/// <summary>
/// The two keys the transport answers, on whichever window you happen to be in.
/// </summary>
/// <remarks>
/// Space starts the transport when it is stopped and stops it when it is running, which is where
/// every tracker and every desk puts it, and Ctrl+R records. They were answered by the main
/// window alone, which is right until something else is in front of you: a machine's panel, an
/// effect off a chain, a plugin's window. There the space bar did nothing, and a transport that
/// stops working because you opened a knob is a transport nobody can trust.
///
/// A door, like <see cref="LinkKey"/> and the log: an application has one transport, and handing
/// it about would be handing the same object about under another name. What it holds is that one
/// deck and nothing else.
///
/// The decision is <see cref="Wants"/> and has no window in it, so what a key means can be put a
/// question to without a keyboard.
///
/// Named for the deck rather than for the transport, since <c>ITransportKeys</c> is already the
/// four words a control surface sends and these are two keys on a computer keyboard: two things
/// with nearly the same name is how two things come to be mistaken for each other.
/// </remarks>
public static class DeckKeys
{
    /// <summary>The transport these keys work, or nothing before the application has one.</summary>
    /// <remarks>
    /// Set once, by the main window, as soon as it has a view model. Every other window asks the
    /// same one: the transport is patched to the page you are on, and which window you happen to
    /// be typing in does not change which page that is.
    /// </remarks>
    public static TransportSwitch? Deck { get; set; }

    /// <summary>
    /// Has every window in this application answer the transport's keys, once and for all.
    /// </summary>
    /// <remarks>
    /// **Once for the type rather than once per window, which is the whole point.** Hung window by
    /// window it was five calls that every new window had to remember, and the sixth forgot: the
    /// mixer taken out into a window of its own answered neither key, and nothing anywhere said
    /// why. That is the shape of fault this codebase has already paid for once, in the paste that
    /// nearly stopped leaving an undo step, and it is the same lesson: the compiler cannot see a
    /// listener that is merely never called.
    ///
    /// A class handler is the toolkit's own answer to it. Registered against
    /// <see cref="Window"/> it applies to every instance of one, including every window written
    /// after this, so there is nothing to remember and nothing to forget.
    ///
    /// On the way down and before the focused control sees it, because otherwise the last button
    /// pressed keeps the key: click Open and space opens the song again instead of playing it. A
    /// space this took is swallowed on the way up as well, since buttons click on the key coming
    /// up and do it whether or not they saw the press.
    ///
    /// The keys held and the space taken are kept per window rather than in one place, because
    /// two windows are two hands as far as this is concerned: a space held on one must not be the
    /// reason a space on another is read as a repeat.
    /// </remarks>
    public static void ListenEverywhere()
    {
        if (_listening) return;

        _listening = true;

        Window.KeyDownEvent.AddClassHandler<Window>(
            (window, e) =>
            {
                var state = StateFor(window);

                bool first = state.Held.Pressed(e.Key);

                if (e.Handled) return;

                var wanted = Wants(e.Key, e.KeyModifiers, Focused(window) || Shortcuts.LearningKeys.On);

                if (wanted == DeckWant.None) return;

                if (wanted == DeckWant.Toggle) state.Took = true;

                if (first) Do(wanted);

                e.Handled = true;
            },
            RoutingStrategies.Tunnel);

        Window.KeyUpEvent.AddClassHandler<Window>(
            (window, e) =>
            {
                var state = StateFor(window);

                state.Held.Released(e.Key);

                if (e.Key != Key.Space || !state.Took) return;

                state.Took = false;
                e.Handled = true;
            },
            RoutingStrategies.Tunnel);
    }

    /// <summary>Whether the handlers are already registered, since once is the point.</summary>
    private static bool _listening;

    /// <summary>What one window is holding down, and whether it took a space.</summary>
    /// <param name="Held">Which keys are down, so a repeat is not read as a fresh press.</param>
    private sealed record WindowKeys(HeldKeys Held)
    {
        /// <summary>Whether a space was taken here and so must be swallowed coming up.</summary>
        public bool Took { get; set; }
    }

    /// <summary>What each window is holding, made on first sight of that window.</summary>
    /// <remarks>
    /// Keyed by the window and holding no strong opinion about when a window goes: a table of a
    /// handful of entries that outlives a closed window costs nothing, where a subscription to
    /// every window's closing would be the per-window bookkeeping this exists to remove.
    /// </remarks>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, WindowKeys> _windows = new();

    /// <inheritdoc cref="_windows"/>
    /// <param name="window">The window the key arrived on.</param>
    private static WindowKeys StateFor(Window window) =>
        _windows.GetValue(window, _ => new WindowKeys(new HeldKeys()));

    /// <summary>
    /// What a keystroke is asking for, which is the whole of the rule and no keystrokes.
    /// </summary>
    /// <remarks>
    /// Out here so it can be put a question to without a window or a keyboard. Three things are
    /// left alone whatever the key: a text box, where a space is a space and Ctrl+R is somebody
    /// typing, a combo box with its list open, where space takes the row that is lit, and a
    /// shortcut being learned, where the whole point is that the key reaches the row waiting for
    /// it rather than the thing that usually answers.
    /// </remarks>
    /// <param name="key">The key that went down.</param>
    /// <param name="modifiers">What was held with it.</param>
    /// <param name="busy">True when the focus is somewhere a key means something else.</param>
    public static DeckWant Wants(Key key, KeyModifiers modifiers, bool busy)
    {
        if (busy) return DeckWant.None;

        if (key == Key.R && modifiers == KeyModifiers.Control) return DeckWant.Record;

        if (key == Key.Space && modifiers == KeyModifiers.None) return DeckWant.Toggle;

        return DeckWant.None;
    }

    /// <summary>True when the keyboard is somewhere a key means something other than the transport.</summary>
    /// <param name="window">The window the key arrived on.</param>
    private static bool Focused(InputElement window) =>
        (window as TopLevel ?? TopLevel.GetTopLevel(window))?.FocusManager?.GetFocusedElement() switch
        {
            TextBox => true,
            ComboBox { IsDropDownOpen: true } => true,
            _ => false
        };

    /// <summary>Works the transport, where there is one to work.</summary>
    /// <remarks>
    /// **Record is asked for and never worked out here.** This used to read the deck's
    /// <c>IsRecording</c> and reach for Stop when it was set, which is right for a page that
    /// records a take and wrong for one where record is an arm: on the tracker it armed the
    /// pattern and then called Stop for ever after, so the arm could not be turned off with the
    /// key that turned it on. What a second press means differs per deck and each deck already
    /// knows, so the key says what was pressed and the deck says what that does.
    /// </remarks>
    /// <param name="wanted">What the key asked for.</param>
    private static void Do(DeckWant wanted)
    {
        if (Deck is not { } deck) return;

        if (wanted == DeckWant.Toggle)
        {
            deck.Toggle();

            return;
        }

        if (deck.CanRecord) deck.RecordCommand.Execute(null);
    }
}
