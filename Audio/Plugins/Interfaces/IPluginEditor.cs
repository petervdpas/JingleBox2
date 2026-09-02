using System;

namespace JingleBox2.Audio.Plugins.Interfaces;

/// <summary>
/// A plugin's own interface: the window it draws itself, rather than the knobs the host draws
/// for it.
/// </summary>
/// <remarks>
/// The host lends the plugin an empty window and the plugin fills it. That is the only way a
/// synth with two thousand parameters is usable: nobody programs Serum through an alphabetical
/// list of dials.
///
/// Everything here happens on the UI thread. A plugin drawing into a window expects to be
/// called where the window lives.
/// </remarks>
public interface IPluginEditor : IDisposable
{
    /// <summary>How big the plugin wants to be, in pixels.</summary>
    (int Width, int Height) Size { get; }

    /// <summary>True when the plugin will follow a window being dragged bigger.</summary>
    bool CanResize { get; }

    /// <summary>
    /// Tells the plugin how much the screen it is about to appear on is scaled by.
    /// </summary>
    /// <remarks>
    /// Windows scales by telling each program a number rather than by handing it more pixels, so
    /// a plugin that draws its own interface cannot know that the window it was given is 150 per
    /// cent of the size it thinks. The host has to say so, and has to say so before the window is
    /// handed over, since a view that lays itself out on being attached needs the number by then.
    ///
    /// Nothing happens where the plugin does not offer to be told, which is most of them: a
    /// plugin built on somebody else's toolkit reads the scaling itself. One that draws its own,
    /// as Arturia's range does, believes the host and nothing else, and told nothing lays out at
    /// a size unrelated to its window, which is a window that is up, active, taking the mouse,
    /// and blank.
    ///
    /// Nought or nonsense is ignored rather than passed on, because a scaling of nought is a
    /// window with no pixels in it.
    /// </remarks>
    /// <param name="factor">1 for an unscaled screen, 1.5 for 150 per cent.</param>
    void Scaled(double factor)
    {
    }

    /// <summary>
    /// Puts the plugin's interface inside a window the host owns. The handle is whatever this
    /// platform calls a window: an X11 window id, an HWND, an NSView.
    /// </summary>
    /// <remarks>
    /// The window has to really be on screen at its full size before it is handed over. Avalonia
    /// makes a one-pixel window before the first layout, and giving that to a plugin is what
    /// killed Serum: the plugin lays itself out against the size it is told and never recovers
    /// from having been told one pixel.
    /// </remarks>
    /// <param name="window">The window to draw into.</param>
    bool Attach(nint window);

    /// <summary>Takes it back out, before the window goes away.</summary>
    void Detach();

    /// <summary>Tells the plugin the window it was given is now this size.</summary>
    void Resized(int width, int height);

    /// <summary>
    /// The plugin asking for a different size, which is how one with a fold-out panel opens
    /// it. The host is expected to make its window that size.
    /// </summary>
    event Action<int, int>? ResizeRequested;
}
