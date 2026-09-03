using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <inheritdoc/>
/// <remarks>
/// The placing is done once the window is open and laid out rather than before it is shown,
/// because a window whose size comes from what is inside it has no size until then: a plugin's
/// window is whatever the plugin's own interface turned out to be, and centring an unmeasured
/// window puts its top left corner in the middle of the screen. Opening is not late enough on
/// its own, since a window that sizes itself to its contents is still growing at that point and
/// the machine panels came out half a title bar high; it is done after the layout that follows.
///
/// Position is in real pixels and a window's size is in the units it is laid out in, so the
/// difference between the two is scaled before it is used. Held inside the screen afterwards,
/// since a window bigger than the one it is being centred over would otherwise have its title
/// bar above the top of the screen, which on most desktops is a window nobody can move.
/// </remarks>
public sealed class FreeWindow : IFreeWindow
{
    /// <inheritdoc/>
    public void Show(Window window, Window? near)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (near != null)
            window.Opened += (_, _) =>
                Dispatcher.UIThread.Post(() => Centre(window, near), DispatcherPriority.Loaded);

        window.Show();
    }

    /// <summary>Puts the window over the middle of the other one, inside the screen.</summary>
    private static void Centre(Window window, Window near)
    {
        var mine = window.FrameSize ?? window.Bounds.Size;
        var theirs = near.FrameSize ?? near.Bounds.Size;

        if (mine.Width <= 1 || theirs.Width <= 1) return;

        double scale = window.RenderScaling <= 0 ? 1 : window.RenderScaling;

        var wanted = new PixelPoint(
            near.Position.X + (int)((theirs.Width - mine.Width) / 2 * scale),
            near.Position.Y + (int)((theirs.Height - mine.Height) / 2 * scale));

        var room = window.Screens.ScreenFromWindow(near)?.WorkingArea;

        window.Position = room == null
            ? wanted
            : new PixelPoint(
                Math.Max(room.Value.X, wanted.X),
                Math.Max(room.Value.Y, wanted.Y));
    }
}
