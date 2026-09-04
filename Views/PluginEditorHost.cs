using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using JingleBox2.Audio.Plugins;
using System;
using JingleBox2.Audio.Plugins.Interfaces;

namespace JingleBox2.Views;

/// <summary>
/// A hole in the window for a plugin to draw its own interface in.
/// </summary>
/// <remarks>
/// The window itself is Avalonia's: <see cref="NativeControlHost"/> already makes a native
/// child window for exactly this, and hands back the handle for it. The plugin is given that
/// handle and everything inside it is the plugin's own from then on.
///
/// Making the window here instead does not work, and fails in a way worth writing down: a
/// window of our own ends up a sibling of the one the toplevel paints into, underneath it, so
/// the plugin draws perfectly into something nobody can see. That looks exactly like a plugin
/// that does not draw at all.
///
/// The plugin is not given the window the moment there is one. Avalonia makes the native child
/// before the first layout, so at that point it is one pixel square and not yet on screen, and
/// a plugin handed that builds its whole interface against it. Some cope. Serum does not: it
/// makes its drawing surface there and then, and dies on the first thing it tries to paint.
/// So the handle is held until the window is really up, at the size it is going to be, and the
/// plugin is let in then.
///
/// What the host still owes the plugin is a run loop, which is what carries a click from the X
/// server to it, and a frame, which is how it asks to be a different size.
/// </remarks>
public sealed class PluginEditorHost : NativeControlHost
{
    /// <summary>The plugin interface to show. Set before this goes on screen.</summary>
    public static readonly StyledProperty<IPluginEditor?> EditorProperty =
        AvaloniaProperty.Register<PluginEditorHost, IPluginEditor?>(nameof(Editor));

    /// <summary>How long to wait for the window to appear before giving the plugin it anyway.</summary>
    private const int SettleRounds = 120;

    /// <summary>
    /// The size the plugin asked for, which is what this control measures at.
    /// </summary>
    /// <remarks>
    /// Nought until a plugin has said, and set again whenever it asks for a different size, which
    /// is what happens when a panel folds out.
    /// </remarks>
    private int _width;

    /// <inheritdoc cref="_width"/>
    private int _height;

    /// <summary>Avalonia's native child window, and nought while there is not one.</summary>
    private nint _handle;

    /// <summary>Whether the plugin has really been let into that window.</summary>
    private bool _attached;

    /// <summary>The wait for the window to be up at its full size. See <see cref="Settle"/>.</summary>
    private DispatcherTimer? _settling;

    /// <summary>
    /// What the window last said about being active, kept for a plugin that was not in it yet.
    /// </summary>
    private bool _active;

    /// <summary>Watches who has the keyboard while the plugin is in its window.</summary>
    private IDisposable? _watching;

    /// <inheritdoc cref="EditorProperty"/>
    public IPluginEditor? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    /// <summary>True when a plugin is actually drawing in here.</summary>
    public bool IsShowing => _attached;

    /// <summary>
    /// Passes the window's own activation through to the plugin drawing inside it.
    /// </summary>
    /// <remarks>
    /// The embedder owes the plugin this for as long as the window is up, not once when it is
    /// handed over. See <see cref="XEmbed.Activated"/> for what a plugin does when it is never
    /// told again.
    /// </remarks>
    public void WindowActivated(bool active)
    {
        _active = active;

        Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Plugins, () =>
            "the window says " + (active ? "active" : "not active") +
            "; the plugin is " + (_attached ? "in it" : "not in it yet"));

        Tell(active);
    }

    /// <summary>
    /// Tells the plugin whether the window it is in is the one being used.
    /// </summary>
    /// <remarks>
    /// Said again every time rather than only when our own answer has changed. What we last
    /// said is not what the plugin last believed: a toolkit works its own activation out from
    /// what X tells its window, and quietly decides it has gone inactive without being told
    /// anything by us. Only saying it when we think it has changed leaves that plugin dead
    /// with the host convinced it has nothing to say.
    /// </remarks>
    private void Tell(bool active)
    {
        if (!_attached || _handle == 0) return;

        Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Plugins, () =>
            "telling the plugin the window is " + (active ? "active" : "not active"));

        XEmbed.Activated(_handle, active);
    }

    /// <summary>
    /// Keeps the plugin told, for as long as it is in the window.
    /// </summary>
    /// <remarks>
    /// The window's own activation events are not enough on their own: a click on a plugin's
    /// interface lands on the plugin's window rather than on ours, so the window manager can
    /// hand the keyboard back without the window we own hearing a thing. Without this, a
    /// plugin told it had gone inactive when the transport was clicked can never be told
    /// otherwise however many times it is clicked on, which is a window that draws and answers
    /// nothing with no way back.
    ///
    /// So the keyboard is followed rather than the window's events, and the plugin is told
    /// again every time it comes back. See <see cref="XEmbed.WatchFocus"/>, which does the
    /// asking on a thread of its own.
    ///
    /// The answer is checked against the window it was asked about: the window may have gone, or
    /// been given to a different plugin, between the asking and the answering.
    /// </remarks>
    private void Watch()
    {
        _watching?.Dispose();
        _watching = null;

        if (!OperatingSystem.IsLinux() || _handle == 0) return;

        nint watched = _handle;

        _watching = XEmbed.WatchFocus(watched, inside => Dispatcher.UIThread.Post(() =>
        {
            if (!_attached || _handle != watched) return;

            Tell(inside || (TopLevel.GetTopLevel(this) as Window)?.IsActive == true);
        }));
    }

    /// <summary>
    /// Lets go of the plugin that was in the window and takes up the one arriving.
    /// </summary>
    /// <remarks>
    /// Whatever was in the window is not in it any more: a plugin that has been started again is
    /// a different plugin with the same name, and it has never seen this window.
    ///
    /// The control is measured at the new plugin's own size from the start, so that the window it
    /// is eventually handed is already the size it asked for.
    ///
    /// An editor that arrives after the window was made has to be let in from here. Only the
    /// first one ever came through <see cref="CreateNativeControlCore"/>, so without that last
    /// line a plugin started again after a crash gets a window it is never given.
    /// </remarks>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != EditorProperty) return;

        if (change.OldValue is IPluginEditor leaving) leaving.ResizeRequested -= OnResizeRequested;

        _attached = false;

        _watching?.Dispose();
        _watching = null;

        if (change.NewValue is not IPluginEditor arriving) return;

        arriving.ResizeRequested += OnResizeRequested;

        var wanted = arriving.Size;

        if (wanted.Width > 0 && wanted.Height > 0)
        {
            _width = wanted.Width;
            _height = wanted.Height;

            InvalidateMeasure();
        }

        if (_handle != 0) Settle();
    }

    /// <summary>
    /// The plugin wants a different size, which is what happens when a panel folds out. The
    /// control is made that size and the plugin is told the size it now has.
    /// </summary>
    /// <remarks>
    /// Answered on the spot when the asking thread is the drawing thread, and only posted when it
    /// is not. That is not tidiness, it is the contract: a plugin asks through the frame, and the
    /// host has to resize the window and call the view back with its new size <em>before that
    /// call returns</em>. A plugin that asks and is answered later has no way to know it was
    /// heard, and the ones that ask from inside being given their window simply wait.
    ///
    /// Which is what a black window was. Vital and Arturia's boxes both ask to be resized from
    /// inside <c>attached</c>, the answer was posted for later, and the call never came back: the
    /// application looked alive, because a plugin waiting there pumps the message loop, and the
    /// window stayed empty for ever. Then closing it took the application down, since nothing had
    /// been marked as attached, so nothing was detached, and the window went away underneath a
    /// plugin that was still drawing into it.
    ///
    /// The layout is run rather than merely asked for, because the native window under this
    /// control is resized during a layout pass and the plugin is about to be told a size it does
    /// not yet have.
    ///
    /// And nothing is gated on being attached. The one moment this matters most is the moment
    /// before it: <c>attached</c> has not returned, so nothing is attached yet, and that is
    /// exactly when the question is asked.
    /// </remarks>
    private void OnResizeRequested(int width, int height)
    {
        if (width <= 0 || height <= 0) return;

        if (Dispatcher.UIThread.CheckAccess()) Made(width, height);
        else Dispatcher.UIThread.Post(() => Made(width, height));
    }

    /// <summary>Makes the window that size and tells the plugin it is.</summary>
    /// <param name="width">How wide the plugin asked to be.</param>
    /// <param name="height">How tall.</param>
    private void Made(int width, int height)
    {
        _width = width;
        _height = height;

        InvalidateMeasure();

        Measure(new Size(width, height));
        Arrange(new Rect(0, 0, width, height));

        Said("the plugin asked to be " + width + " by " + height + ", and has been told it is");

        try
        {
            Editor?.Resized(width, height);
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.Enums.LogArea.Plugins,
                "the plugin threw while being told its new size", ex);
        }
    }

    /// <summary>
    /// Whatever size the plugin asked for, and only the base's answer when it has not asked.
    /// </summary>
    /// <remarks>
    /// The window is sized to the plugin rather than the other way round: a plugin's interface is
    /// a picture at a size it chose, not a layout.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        if (_width > 0 && _height > 0) return new Size(_width, _height);

        var wanted = Editor?.Size ?? (0, 0);

        return wanted.Width > 0 && wanted.Height > 0
            ? new Size(wanted.Width, wanted.Height)
            : base.MeasureOverride(availableSize);
    }

    /// <summary>
    /// Takes Avalonia's own child window, made the way this platform makes one, and starts
    /// waiting for it to be worth handing over.
    /// </summary>
    /// <remarks>
    /// Whatever the platform calls it, the plugin is told the same handle, but not yet: at this
    /// point the window is one pixel square and not on screen.
    /// </remarks>
    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var handle = base.CreateNativeControlCore(parent);

        _handle = handle.Handle;

        if (Editor != null && _handle != 0) Settle();

        return handle;
    }

    /// <summary>
    /// How much the screen this control is on is scaled by, or 1 where nothing says.
    /// </summary>
    /// <remarks>
    /// Windows scales by telling each program a number, and a plugin drawing its own interface
    /// believes the host or nothing. Read off the window rather than kept, since a window dragged
    /// to a second screen is on a different scaling from the one it opened on.
    /// </remarks>
    private double Scaling =>
        TopLevel.GetTopLevel(this) is { RenderScaling: > 0 } top ? top.RenderScaling : 1;

    /// <summary>
    /// Waits for the window to be on screen at a real size, then lets the plugin in.
    /// </summary>
    /// <remarks>
    /// A round every frame or so, for up to two seconds. If the window never turns up the
    /// plugin is given it anyway: a plugin drawing into something odd is worth more than a
    /// plugin never shown at all, and on a platform where the question cannot be asked the
    /// answer is yes on the first round.
    /// </remarks>
    /// <summary>Writes a line about the window, where the log is on.</summary>
    /// <remarks>
    /// Every way this could fail used to fail silently, so from a log a plugin that refused its
    /// window and one that was never offered one looked exactly the same, and both looked like a
    /// plugin that simply has no interface.
    /// </remarks>
    /// <param name="what">What happened, in the words the log shows.</param>
    private static void Said(string what) =>
        Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Plugins, () => "editor host: " + what);

    private void Settle()
    {
        _settling?.Stop();

        int rounds = 0;

        _settling = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };

        _settling.Tick += (_, _) =>
        {
            rounds++;

            if (_attached || _handle == 0 || Editor == null)
            {
                if (!_attached)
                    Said("given up waiting for the window: "
                         + (_handle == 0 ? "it went away" : "the editor went away"));

                _settling?.Stop();
                return;
            }

            if (rounds < SettleRounds && !XEmbed.OnScreen(_handle, out _, out _)) return;

            _settling?.Stop();

            Said(rounds < SettleRounds
                ? "the window is on screen after " + rounds + " rounds"
                : "the window never said it was on screen; handing it over anyway");

            Show();
        };

        _settling.Start();
    }

    /// <summary>
    /// Hands the window over, and tells the plugin the two things it cannot find out for itself.
    /// </summary>
    /// <remarks>
    /// A plugin that will not take the window is a plugin without an interface, not an
    /// application without a window, so a throw leaves the host empty rather than going upwards.
    ///
    /// It is told whether the window is active at once. The window is up, and on a desktop that
    /// gives a new window the focus it is already active well before this, since the handover
    /// waits for the window to be really on screen at its full size. So the activation Avalonia
    /// raised when the window opened reached a host with nothing in it and went nowhere, and
    /// XEMBED never says anything twice. A plugin that misses it draws from its own timers and
    /// ignores everything clicked on it, which is a window that looks perfectly alive and answers
    /// nothing. The watch started after it reports where the keyboard is as soon as it starts, so
    /// a window that was not active a moment ago is put right without waiting for anything to
    /// change.
    ///
    /// And it is told its size once it is in place. Some plugins lay themselves out here rather
    /// than when they were attached, and stay blank until they are asked.
    /// </remarks>
    private void Show()
    {
        var editor = Editor;

        if (editor == null || _handle == 0 || _attached)
        {
            Said(editor == null ? "there is no editor to show"
                : _handle == 0 ? "there is no window to show it in"
                : "it is already showing");
            return;
        }

        double scaling = Scaling;

        Said("handing the window over, the screen is scaled by " + scaling);

        try
        {
            editor.Scaled(scaling);
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.Enums.LogArea.Plugins,
                "the plugin threw while being told the screen's scaling", ex);
        }

        try
        {
            Said("calling attach on " + editor.GetType().Name);

            _attached = editor.Attach(_handle);

            Said("attach came back " + _attached);
        }
        catch (Exception ex)
        {
            _attached = false;

            Diagnostics.Log.Fault(Diagnostics.Enums.LogArea.Plugins,
                "the plugin threw while being given its window", ex);
        }

        if (!_attached)
        {
            Said("the plugin would not take the window");
            return;
        }

        bool active = (TopLevel.GetTopLevel(this) as Window)?.IsActive == true || _active;

        Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Plugins, () =>
            "the plugin is in its window, and the window is " +
            (active ? "active" : "not active"));

        if (active) Tell(true);

        Watch();

        var settled = editor.Size;

        if (settled.Width <= 0 || settled.Height <= 0) return;

        _width = settled.Width;
        _height = settled.Height;

        editor.Resized(settled.Width, settled.Height);

        InvalidateMeasure();
    }

    /// <summary>
    /// Takes the plugin out before the window goes.
    /// </summary>
    /// <remarks>
    /// That order is the whole of it: a plugin still drawing into a window that has been
    /// destroyed is a crash inside its own toolkit.
    /// </remarks>
    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _settling?.Stop();
        _settling = null;

        _watching?.Dispose();
        _watching = null;

        _handle = 0;

        if (_attached)
        {
            _attached = false;
            Editor?.Detach();
        }

        base.DestroyNativeControlCore(control);
    }
}
