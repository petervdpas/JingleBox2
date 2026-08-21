using System;

namespace JingleBox2.Audio.Plugins;

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
    /// Puts the plugin's interface inside a window the host owns. The handle is whatever this
    /// platform calls a window: an X11 window id, an HWND, an NSView.
    /// </summary>
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

/// <summary>A plugin that may have an interface of its own.</summary>
public interface IPluginWindowSource
{
    /// <summary>
    /// Opens the plugin's own interface, or null when it has none. Some plugins are all
    /// parameters and no picture, and those still get the host's knobs.
    /// </summary>
    IPluginEditor? OpenEditor();
}
