using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using JingleBox2.Audio.Plugins;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <summary>
/// One plugin on its own, in a window that can be left open while the rest of the app is
/// used. Several can be open at once, one per device.
/// </summary>
/// <remarks>
/// What is inside is the plugin's own interface where it has one, and the host's knobs where
/// it has not. Either way the window itself, its title and its bypass button are the host's.
/// </remarks>
public partial class PluginWindow : Window
{
    /// <summary>
    /// Opens this window beside the application rather than over it, so it can be put behind.
    /// </summary>
    private static readonly IFreeWindow Free = new FreeWindow();

    /// <summary>What is already open, so a thing shows the window it has rather than another.</summary>
    private static readonly Dictionary<object, PluginWindow> Open = new();

    /// <summary>
    /// Builds the window, and takes on the two duties an embedded plugin window puts on
    /// whoever is holding it.
    /// </summary>
    /// <remarks>
    /// XEMBED makes the embedder responsible for telling the plugin when its window is the one
    /// being used, every time, not once when it was handed over. Without these the plugin
    /// believes whatever it was told at attach, which after the first click on anything else is
    /// that it is not active: it carries on drawing from its own timers and ignores everything
    /// clicked on it.
    ///
    /// Ctrl+Shift+M is the other one, and here it is answered by refusing rather than by
    /// <see cref="LinkKey"/>.Listen: a plugin is the one thing in this application a hardware
    /// control cannot be pointed at, and a keystroke that silently did nothing on the one window
    /// where it does nothing would read as the gesture having broken.
    ///
    /// It is answered twice because there are two ways it can arrive. The toolkit's handler gets
    /// it while the window's own chrome has the keyboard; while the plugin's interface has it the
    /// key is delivered to that program and this process never sees it, so
    /// <see cref="IWindowShortcut"/> asks the window system for that one combination instead. The
    /// two cannot both fire for one press, and if they did the clock below would drop the second.
    /// </remarks>
    public PluginWindow()
    {
        InitializeComponent();


        AddHandler(InputElement.KeyDownEvent, Pressed, RoutingStrategies.Tunnel);

        Opened += (_, _) => Catch();
        Closed += (_, _) => Uncatch();

        Activated += (_, _) => TellPlugin(true);
        Deactivated += (_, _) => TellPlugin(false);
    }

    /// <summary>How a keystroke is asked for on a window somebody else is drawing in.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly WindowShortcuts Shortcuts = new();

    /// <summary>The standing request, let go of when the window closes.</summary>
    private IDisposable? _caught;

    /// <summary>
    /// Asks the window system for Ctrl+Shift+M on this window, once there is one to ask about.
    /// </summary>
    /// <remarks>
    /// On Opened rather than in the constructor, because the handle is what the window system
    /// gave the window and there is none until it exists. Nothing where the platform cannot do
    /// it, which leaves the toolkit's handler covering the window's own chrome, exactly as
    /// before.
    /// </remarks>
    private void Catch()
    {
        if (TryGetPlatformHandle() is not { } handle) return;

        _caught = Shortcuts.On(
            handle.HandleDescriptor ?? "",
            handle.Handle,
            () => Dispatcher.UIThread.Post(Refuse));
    }

    /// <summary>Gives the keystroke back, so nothing outlives the window it was asked for.</summary>
    private void Uncatch()
    {
        _caught?.Dispose();
        _caught = null;
    }

    /// <summary>When the refusal was last said, so leaning on the key does not stack dialogs.</summary>
    /// <remarks>
    /// A clock rather than a flag, and the same clock <see cref="LinkKey"/> keeps, for the
    /// reason written there: a flag has to be cleared by the key coming up and the key can come
    /// up in another window.
    /// </remarks>
    private DateTime _refused;

    /// <summary>
    /// Answers Ctrl+Shift+M by saying why it does nothing here.
    /// </summary>
    /// <remarks>
    /// Swallowed rather than passed on, which is the opposite of what <see cref="LinkKey"/> does
    /// with a keystroke it will not answer. There it is left alone because it may mean something
    /// to whatever is in front of you; here it is being answered, with a sentence.
    ///
    /// It cannot be caught while the plugin's own interface has the keyboard, since that is
    /// another program's window and its keys never reach this one. Pressed anywhere on the
    /// window's own chrome, or with the host's knobs showing, it is ours and it is said.
    /// </remarks>
    private void Pressed(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.M) return;
        if (e.KeyModifiers != (KeyModifiers.Control | KeyModifiers.Shift)) return;

        e.Handled = true;

        Refuse();
    }

    /// <summary>Says why the keystroke does nothing here, once however it arrived.</summary>
    /// <remarks>
    /// The clock is what makes it once. A key leant on repeats, and the two ways a press can
    /// reach this window could in principle both answer one press, where a dialog apiece would
    /// be a stack of them to dismiss.
    /// </remarks>
    private async void Refuse()
    {
        var now = DateTime.UtcNow;
        var since = now - _refused;

        _refused = now;

        if (since.TotalMilliseconds < LinkKey.AgainMs) return;

        await ConfirmDialog.ErrorAsync(
            "MIDI CC",
            "A plugin does its own MIDI learning",
            "Ctrl+Shift+M points a hardware control at a machine, at one of our own effects or at a mixer strip, and it does not work on a VST3 or a CLAP. Those bring their own MIDI learn and keep the result themselves, so pointing at one here would be a second mapping beside the plugin's own with no way to make the two agree.\n\nUse the plugin's own way of doing it, usually a right click on the control you want.");
    }

    /// <summary>Passes this window's activation to the plugin drawing inside it, if there is one.</summary>
    private void TellPlugin(bool active)
    {
        foreach (var host in this.GetVisualDescendants().OfType<PluginEditorHost>())
        {
            host.WindowActivated(active);
        }
    }

    /// <summary>Opens a device's window, or brings the one it already has to the front.</summary>
    public static void Show(PluginSlotViewModel device, Window owner)
    {
        if (device == null) return;

        device.IsOpen = true;

        Show(device, device.Panel, device.Name, owner, device, () => device.IsOpen = false);
    }

    /// <summary>
    /// Opens a plugin that is not in a chain, an instrument for instance, in the same kind of
    /// window. The key is whatever owns it, so asking twice brings the same window forward.
    /// </summary>
    public static void Show(object key, PluginControlsViewModel panel, string title, Window owner, Action? closed = null)
    {
        Show(key, panel, title, owner, null, closed);
    }

    /// <summary>
    /// The one that actually opens a window, which both public overloads reach.
    /// </summary>
    /// <remarks>
    /// The plugin's interface is opened before the window is built, so the window can size
    /// itself to whatever the plugin turns out to be. A plugin drawing its own interface is a
    /// picture at a size it chose, so it is let out of the caps that keep a wall of host-drawn
    /// knobs from filling the screen.
    ///
    /// The plugin is taken out of its window on the way out rather than after: letting the
    /// window go first leaves the plugin drawing into something that is not there, which is a
    /// crash on closing rather than on opening. Only the picture is put away; the plugin itself
    /// carries on playing.
    /// </remarks>
    private static void Show(
        object key,
        PluginControlsViewModel panel,
        string title,
        Window owner,
        PluginSlotViewModel? device,
        Action? closed)
    {
        if (key == null || panel == null) return;

        if (Open.TryGetValue(key, out var existing))
        {
            existing.Activate();
            return;
        }

        panel.Prepare();

        var window = new PluginWindow
        {
            DataContext = new PluginWindowViewModel(panel, title, device),
            Title = title
        };

        if (panel.HasOwnWindow)
        {
            window.MaxWidth = double.PositiveInfinity;
            window.MaxHeight = double.PositiveInfinity;
        }
        else
        {
            window.MaxWidth = Math.Min(900, owner.Bounds.Width > 0 ? owner.Bounds.Width : 900);
        }

        Open[key] = window;

        window.Closing += (_, _) =>
        {
            panel.Close();
        };

        window.Closed += (_, _) =>
        {
            Open.Remove(key);
            closed?.Invoke();
        };

        Free.Show(window, owner);
    }

    /// <summary>
    /// Closes a window, for whatever owned it going away. Named apart from Window.Close so
    /// that closing a key and closing a window cannot be mistaken for each other.
    /// </summary>
    public static void CloseFor(object key)
    {
        if (key == null || !Open.TryGetValue(key, out var window)) return;

        Open.Remove(key);

        if (key is PluginSlotViewModel device) device.IsOpen = false;

        window.Close();
    }
}
