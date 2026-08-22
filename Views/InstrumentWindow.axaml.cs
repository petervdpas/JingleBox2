using Avalonia.Controls;
using JingleBox2.ViewModels;
using System;
using System.Collections.Generic;

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
    /// <summary>What is already open, so asking twice shows the window there is.</summary>
    private static readonly Dictionary<object, InstrumentWindow> Open = new();

    public InstrumentWindow()
    {
        InitializeComponent();
    }

    /// <summary>Opens the designer for a track's instrument, or brings its window forward.</summary>
    public static void Show(object key, TrackInstrumentDesigner designer, Window owner, Action? closed = null)
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

        window.Show(owner);
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
