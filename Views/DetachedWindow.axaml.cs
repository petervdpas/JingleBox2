using System;
using Avalonia;
using Avalonia.Controls;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

/// <summary>
/// A page taken out of the window it lived in and put in one of its own.
/// </summary>
/// <remarks>
/// It holds the page itself rather than another copy of it. A control has one parent, so taking
/// it out really takes it out: where it came from has nothing left in it, and closing this window
/// hands the page back.
///
/// That is why this is worth having over a second view. Two views of one page are two pictures
/// that can disagree, and both of them do their own work: two mixers would each poll their meters
/// at the window's own rate while anything is sounding, for one set of levels.
///
/// It knows nothing about which page it is holding. What differs between one page and the next is
/// the title, what the page was bound to and whether there is a transport, and all three are
/// handed in, so a second page wanting a window of its own needs nothing here.
/// </remarks>
public partial class DetachedWindow : Window
{
    /// <summary>
    /// The application's transport, or nothing for a page that plays none.
    /// </summary>
    /// <remarks>
    /// A property on the window rather than something read out of the data context, because the
    /// context here belongs to the page and a page's own context knows nothing about the
    /// transport. Styled rather than plain so the caps follow it: what it says changes while the
    /// song runs.
    /// </remarks>
    public static readonly StyledProperty<TransportSwitch?> DeckProperty =
        AvaloniaProperty.Register<DetachedWindow, TransportSwitch?>(nameof(Deck));

    /// <inheritdoc cref="DeckProperty"/>
    public TransportSwitch? Deck
    {
        get => GetValue(DeckProperty);
        set => SetValue(DeckProperty, value);
    }

    /// <summary>What to do with the page when this window closes.</summary>
    private Action<Control>? _back;

    /// <summary>What this window puts around the page, on each side.</summary>
    /// <remarks>The border's own padding, which is the twelve the page has in its tab.</remarks>
    private const double Inset = 12;

    /// <summary>And the height of the row the transport stands in, where there is one.</summary>
    private const double CapsRow = 46;

    /// <summary>Builds the window.</summary>
    public DetachedWindow() => InitializeComponent();

    /// <summary>
    /// Takes a page out into a window, and says how to put it back.
    /// </summary>
    /// <remarks>
    /// The page is handed over rather than copied, so whoever owned it must have let go of it
    /// first: a control with two parents is an error in the toolkit rather than a picture in two
    /// places.
    ///
    /// What it was bound to has to come with it, and it goes on the window rather than on the
    /// page. A page takes its context from whatever is above it, so a page taken out of a tab has
    /// nothing above it any more and every binding on it reads null. Put on the window it is
    /// inherited while the page is here and the old one is inherited again when it goes back; put
    /// on the page it would be a value of its own and would still be there, stale, afterwards.
    ///
    /// Giving the page back is hung on Closed rather than done by the caller, so a window shut
    /// from its own frame gives it back exactly as one shut from the menu does.
    /// </remarks>
    /// <param name="page">The page itself.</param>
    /// <param name="title">What to call the window.</param>
    /// <param name="context">What the page was bound to where it came from.</param>
    /// <param name="owner">
    /// The window it came out of, used to take its size from and nothing else.
    /// </param>
    /// <param name="deck">The application's transport, or nothing for a page that plays none.</param>
    /// <param name="back">Called with the page when this window closes.</param>
    /// <returns>The window, so a caller can bring it forward again, or nothing where there is no page.</returns>
    public static DetachedWindow? Show(
        Control? page,
        string title,
        object? context,
        Window? owner,
        TransportSwitch? deck,
        Action<Control> back)
    {
        if (page == null) return null;

        var window = new DetachedWindow
        {
            Title = title,
            DataContext = context,
            Deck = deck,
            _back = back,
        };

        window.Caps.IsVisible = deck != null;

        // The window opens the size the page already is, rather than a figure written here. A
        // fixed size is wrong for every page but the one it was measured on, and it was wrong for
        // this one: the mixer came out with its last strips cut off the right hand edge.
        //
        // The page's own rendered size plus what this window puts around it: the border's padding
        // on both sides, and the row the transport stands in where there is one.
        if (page.Bounds.Width > 1 && page.Bounds.Height > 1)
        {
            window.Width = page.Bounds.Width + Inset * 2;
            window.Height = page.Bounds.Height + Inset * 2 + (deck != null ? CapsRow : 0);
        }

        window.Host.Child = page;

        window.Closed += (_, _) =>
        {
            if (window.Host.Child is not Control held) return;

            window.Host.Child = null;
            window._back?.Invoke(held);
        };

        // Shown without an owner, deliberately. An owned window is always in front of the one
        // that owns it, which is right for a dialog and wrong for a page: a mixer you have taken
        // out is a thing you put beside the application, or behind it, and one that cannot go
        // behind is one you end up moving out of the way instead. Nothing is lost by it either,
        // since the application shuts down when its main window closes rather than when the last
        // window does.
        window.Show();

        return window;
    }
}
