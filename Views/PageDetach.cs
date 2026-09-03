using System;
using Avalonia.Controls;
using Avalonia.VisualTree;
using JingleBox2.ViewModels;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <inheritdoc/>
/// <remarks>
/// Holds the page's home as well as the page, because putting it back is half of what this does
/// and a window that closes has to have somewhere to hand it to.
///
/// The home is a panel with two children in it, the page and something to stand in its place, and
/// only ever one of them is in there at a time. A page that simply vanished from its tab would
/// read as a page that is broken rather than as one that has moved.
/// </remarks>
public sealed class PageDetach : IPageDetach
{
    /// <summary>Where the page lives when it is not in a window.</summary>
    private readonly Panel _home;

    /// <summary>The page itself, which is moved rather than copied.</summary>
    private readonly Control _page;

    /// <summary>What stands in its place while it is away.</summary>
    private readonly Control _gone;

    /// <summary>What the window is called.</summary>
    private readonly string _title;

    /// <summary>
    /// What the page should be bound to, asked for at the moment it is needed.
    /// </summary>
    /// <remarks>
    /// A function rather than a value, and the home's context rather than the page's. A page
    /// usually sets its own context with a binding against whatever is above it, so the window has
    /// to be given the same thing the home was given or that binding resolves against the wrong
    /// object once the page is inside it.
    /// </remarks>
    private readonly Func<object?> _context;

    /// <summary>The application's transport, or nothing for a page that plays none.</summary>
    private readonly Func<TransportSwitch?> _deck;

    /// <summary>The window while there is one.</summary>
    private DetachedWindow? _window;

    /// <summary>Takes charge of one page's coming and going.</summary>
    /// <param name="home">The panel the page lives in, holding it and its stand-in.</param>
    /// <param name="page">The page.</param>
    /// <param name="gone">What to show in its place while it is away.</param>
    /// <param name="title">What to call the window.</param>
    /// <param name="context">What the page should be bound to, asked when it is needed.</param>
    /// <param name="deck">The application's transport, or nothing.</param>
    public PageDetach(
        Panel home,
        Control page,
        Control gone,
        string title,
        Func<object?> context,
        Func<TransportSwitch?> deck)
    {
        _home = home;
        _page = page;
        _gone = gone;
        _title = title;
        _context = context;
        _deck = deck;

        _gone.IsVisible = false;
    }

    /// <inheritdoc/>
    public bool Detached => _window != null;

    /// <inheritdoc/>
    public void Out()
    {
        if (_window is { } already)
        {
            already.Activate();

            return;
        }

        _home.Children.Remove(_page);
        _gone.IsVisible = true;

        _window = DetachedWindow.Show(
            _page,
            _title,
            _context(),
            _home.FindAncestorOfType<Window>(),
            _deck(),
            Home);

        if (_window == null) Home(_page);
    }

    /// <inheritdoc/>
    public void Back() => _window?.Close();

    /// <summary>Puts the page back where it lives and clears the stand-in.</summary>
    /// <remarks>
    /// Called when the window closes, however it was closed, so a window shut from its own frame
    /// gives the page back exactly as one shut from a button does. Guarded against being asked
    /// twice, since a control cannot be in a panel's children twice.
    /// </remarks>
    /// <param name="page">The page coming home.</param>
    private void Home(Control page)
    {
        _window = null;

        if (!_home.Children.Contains(page)) _home.Children.Insert(0, page);

        _gone.IsVisible = false;
    }
}
