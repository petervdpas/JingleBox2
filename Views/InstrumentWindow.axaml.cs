using Avalonia.Controls;
using JingleBox2.ViewModels;
using System;
using System.Collections.Generic;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <summary>
/// One track's instrument, in a window that can be left open while the rest of the app is used.
/// </summary>
/// <remarks>
/// The same shape as <see cref="PluginWindow"/>, deliberately: a plugin and an instrument of our
/// own are the same kind of thing to a track, and the way you open one should not depend on
/// which it happens to be. One window per instrument, brought forward rather than opened twice.
/// </remarks>
public partial class InstrumentWindow : Window
{
    /// <summary>
    /// Opens this window beside the application rather than over it, so it can be put behind.
    /// </summary>
    private static readonly IFreeWindow Free = new FreeWindow();

    /// <summary>What is already open, so asking twice shows the window there is.</summary>
    private static readonly Dictionary<object, InstrumentWindow> Open = new();

    /// <summary>
    /// Builds the window and lets the other mouse mode be reached from inside it.
    /// </summary>
    /// <remarks>
    /// The pointer goes where the windows are, so the gesture has to be answered on every
    /// window that has something pointable on it, not only on the main one. See
    /// <see cref="LinkKey"/>.
    /// </remarks>
    public InstrumentWindow()
    {
        InitializeComponent();

        LinkKey.Listen(this);

    }

    /// <summary>Opens the designer for a track's instrument, or brings its window forward.</summary>
    /// <remarks>
    /// The window coming to the front is what a knob pointed at "the track you are on" means,
    /// once there are panels open in windows of their own: the pattern cursor is on neither of
    /// them. That is said by <see cref="RemoteFocus"/> rather than here, since it is true of
    /// every window with a face on it and was written out once per window until it was not.
    /// </remarks>
    public static void Show(object key, TrackInstrumentPanel designer, Window owner, Action? closed = null)
    {
        if (key == null || designer == null || owner == null) return;

        if (Open.TryGetValue(key, out var already))
        {
            already.Activate();
            return;
        }

        var window = new InstrumentWindow { DataContext = designer };

        Open[key] = window;

        window.Closed += (_, _) =>
        {
            Open.Remove(key);
            closed?.Invoke();
        };

        Free.Show(window, owner);
    }

    /// <summary>Closes the window a thing has, if it has one.</summary>
    public static void CloseFor(object key)
    {
        if (key == null || !Open.TryGetValue(key, out var window)) return;

        window.Close();
    }

    /// <summary>True when this thing already has a window up.</summary>
    public static bool IsOpenFor(object key) => key != null && Open.ContainsKey(key);
}
