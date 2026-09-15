using Avalonia.Media;
using JingleBox2.UI.Enums;

namespace JingleBox2.UI.Interfaces;

/// <summary>
/// The colour a message is lit in, wherever it is shown.
/// </summary>
/// <remarks>
/// The bar along the bottom and a toast both light a lamp for a message, and one message has to
/// read the same in both: a warning amber in the bar and something else in the corner would be
/// two answers to how that message wants to be read. So the choice is made here once and both
/// ask. A warning is amber, a fault is the red a record button is, and something that worked is
/// green; a plain message is the theme's own colour and the resting state its muted one, since
/// those two belong to whichever theme is on.
/// </remarks>
public interface IStatusLamp
{
    /// <summary>The colour for a kind of message.</summary>
    /// <param name="kind">How the message wants to be read.</param>
    /// <param name="accent">The theme's own colour, which a plain message is lit in.</param>
    /// <param name="muted">The theme's quiet colour, which the resting state is lit in.</param>
    Color For(StatusKind kind, Color accent, Color muted);
}
