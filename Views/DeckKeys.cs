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
    /// Has a window answer the transport's keys while it is up.
    /// </summary>
    /// <remarks>
    /// On the way down and before the focused control sees it, because otherwise the last button
    /// pressed keeps the key: click Open and space opens the song again instead of playing it.
    /// A space this took is swallowed on the way up as well, since buttons click on the key
    /// coming up and do it whether or not they saw the press.
    /// </remarks>
    /// <param name="window">The window to answer on.</param>
    public static void Listen(InputElement window)
    {
        if (window is null) return;

        var held = new HeldKeys();
        bool took = false;

        window.AddHandler(
            InputElement.KeyDownEvent,
            (_, e) =>
            {
                bool first = held.Pressed(e.Key);

                if (e.Handled) return;

                var wanted = Wants(e.Key, e.KeyModifiers, Focused(window));

                if (wanted == DeckWant.None) return;

                if (wanted == DeckWant.Toggle) took = true;

                if (first) Do(wanted);

                e.Handled = true;
            },
            RoutingStrategies.Tunnel);

        window.AddHandler(
            InputElement.KeyUpEvent,
            (_, e) =>
            {
                held.Released(e.Key);

                if (e.Key != Key.Space || !took) return;

                took = false;
                e.Handled = true;
            },
            RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// What a keystroke is asking for, which is the whole of the rule and no keystrokes.
    /// </summary>
    /// <remarks>
    /// Out here so it can be put a question to without a window or a keyboard. Two things are
    /// left alone whatever the key: a text box, where a space is a space and Ctrl+R is somebody
    /// typing, and a combo box with its list open, where space takes the row that is lit.
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
    /// <param name="wanted">What the key asked for.</param>
    private static void Do(DeckWant wanted)
    {
        if (Deck is not { } deck) return;

        if (wanted == DeckWant.Toggle)
        {
            deck.Toggle();

            return;
        }

        if (deck.IsRecording) deck.StopCommand.Execute(null);
        else if (deck.CanRecord) deck.RecordCommand.Execute(null);
    }
}
