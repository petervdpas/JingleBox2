using Avalonia.Media;

namespace JingleBox2.Views.Interfaces;

/// <summary>What the patchbay's wires are painted in.</summary>
/// <remarks>
/// Two kinds of cable and they have to be told apart at a glance: what you patched, and how this
/// application is wired inside itself. The first wears the theme's own accent, since it is the
/// colour everything you have chosen wears everywhere else in this program; the second wears the
/// colour opposite it on the wheel, which is the one colour guaranteed to be nothing else's on
/// that theme and to sit as far from the accent as a colour can.
///
/// A rule of its own so it can be put a question to without a window, which matters here because
/// the answer is arithmetic on a colour and getting it wrong is a wire nobody can see.
/// </remarks>
public interface IPatchColours
{
    /// <summary>
    /// The colour opposite the one given, at the same strength.
    /// </summary>
    /// <remarks>
    /// Half a turn round the wheel, keeping the saturation and the brightness, so it belongs to
    /// the same theme rather than being an arbitrary blue laid over somebody's palette. A colour
    /// with no hue in it, which is what a grey accent is, comes back as itself: there is nothing
    /// opposite grey, and inventing a hue would put a colour on a theme that has none.
    /// </remarks>
    /// <param name="colour">The theme's own wire colour.</param>
    Color Counter(Color colour);
}
