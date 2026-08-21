using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using JingleBox2.Audio.Plugins;
using System;

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

    private int _width;
    private int _height;

    private nint _handle;
    private bool _attached;
    private DispatcherTimer? _settling;

    public IPluginEditor? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    /// <summary>True when a plugin is actually drawing in here.</summary>
    public bool IsShowing => _attached;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != EditorProperty) return;

        if (change.OldValue is IPluginEditor leaving) leaving.ResizeRequested -= OnResizeRequested;

        if (change.NewValue is IPluginEditor arriving)
        {
            arriving.ResizeRequested += OnResizeRequested;

            // Asked for at the plugin's own size from the start, so that the window it is
            // eventually handed is already the size it asked for.
            var wanted = arriving.Size;

            if (wanted.Width > 0 && wanted.Height > 0)
            {
                _width = wanted.Width;
                _height = wanted.Height;

                InvalidateMeasure();
            }
        }
    }

    /// <summary>
    /// The plugin wants a different size, which is what happens when a panel folds out. The
    /// control is made that size and Avalonia resizes the native window under it.
    /// </summary>
    private void OnResizeRequested(int width, int height)
    {
        if (width <= 0 || height <= 0) return;

        // A plugin may ask from inside a call of its own, on whichever thread that was.
        Dispatcher.UIThread.Post(() =>
        {
            _width = width;
            _height = height;

            InvalidateMeasure();

            if (_attached) Editor?.Resized(width, height);
        });
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The window is sized to the plugin rather than the other way round: a plugin's
        // interface is a picture at a size it chose, not a layout.
        if (_width > 0 && _height > 0) return new Size(_width, _height);

        var wanted = Editor?.Size ?? (0, 0);

        return wanted.Width > 0 && wanted.Height > 0
            ? new Size(wanted.Width, wanted.Height)
            : base.MeasureOverride(availableSize);
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        // Avalonia's own child window, made the way this platform makes one. Whatever the
        // platform calls it, the plugin is told the same handle, but not yet.
        var handle = base.CreateNativeControlCore(parent);

        _handle = handle.Handle;

        if (Editor != null && _handle != 0) Settle();

        return handle;
    }

    /// <summary>
    /// Waits for the window to be on screen at a real size, then lets the plugin in.
    /// </summary>
    /// <remarks>
    /// A round every frame or so, for up to two seconds. If the window never turns up the
    /// plugin is given it anyway: a plugin drawing into something odd is worth more than a
    /// plugin never shown at all, and on a platform where the question cannot be asked the
    /// answer is yes on the first round.
    /// </remarks>
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
                _settling?.Stop();
                return;
            }

            if (rounds < SettleRounds && !XEmbed.OnScreen(_handle, out _, out _)) return;

            _settling?.Stop();

            Show();
        };

        _settling.Start();
    }

    private void Show()
    {
        var editor = Editor;
        if (editor == null || _handle == 0 || _attached) return;

        try
        {
            _attached = editor.Attach(_handle);
        }
        catch (Exception)
        {
            // A plugin that will not take the window is a plugin without an interface, not an
            // application without a window.
            _attached = false;
        }

        if (!_attached) return;

        // Told its size once it is in place. Some plugins lay themselves out here rather than
        // when they were attached, and stay blank until they are asked.
        var settled = editor.Size;

        if (settled.Width <= 0 || settled.Height <= 0) return;

        _width = settled.Width;
        _height = settled.Height;

        editor.Resized(settled.Width, settled.Height);

        InvalidateMeasure();
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _settling?.Stop();
        _settling = null;

        _handle = 0;

        // The plugin comes out before the window goes: a plugin still drawing into a window
        // that has been destroyed is a crash inside its own toolkit.
        if (_attached)
        {
            _attached = false;
            Editor?.Detach();
        }

        base.DestroyNativeControlCore(control);
    }
}
