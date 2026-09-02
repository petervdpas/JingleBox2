using Avalonia.Controls;
using JingleBox2.ViewModels;
using System;

namespace JingleBox2.Views;

/// <summary>
/// The rack, in a window of its own.
/// </summary>
/// <remarks>
/// One window, brought forward rather than opened twice, the same as an instrument's own
/// window and a plugin's. It holds the rack itself rather than one machine, because picking
/// which machine to work on is half of what the rack is for.
/// </remarks>
public partial class SoundMachineWindow : Window
{
    /// <summary>
    /// The one that is open, so a second request brings it forward rather than opening another.
    /// </summary>
    /// <remarks>
    /// Static because the rack is one thing and there is nowhere else to keep it: the window is
    /// opened from a command that holds no window of its own. Cleared when it closes, or the
    /// next request would activate a window that is gone.
    /// </remarks>
    private static SoundMachineWindow? _open;

    /// <summary>
    /// Builds the window and lets the other mouse mode be reached from inside it.
    /// </summary>
    /// <remarks>
    /// The pointer goes where the windows are, so the gesture has to be answered on every
    /// window that has something pointable on it, not only on the main one. See
    /// <see cref="LinkKey"/>.
    /// </remarks>
    public SoundMachineWindow()
    {
        InitializeComponent();

        LinkKey.Listen(this);

        DeckKeys.Listen(this);
    }

    /// <summary>
    /// Opens the rack over the app's window, or brings the one that is open forward.
    /// </summary>
    /// <param name="rack">The machines to show, and nothing is opened without one.</param>
    /// <param name="owner">The app's window, which this one sits over, and nothing is opened without one.</param>
    /// <param name="inFront">
    /// Told whenever this window takes or loses the keyboard. A note played while the rack is
    /// in front is auditioning a machine; the same key on the pattern is writing a note, and
    /// which one it is is a question about which window you are looking at.
    /// </param>
    public static void Show(RackViewModel? rack, Window? owner, Action<bool>? inFront = null)
    {
        if (rack == null || owner == null) return;

        if (_open != null)
        {
            _open.Activate();
            return;
        }

        var window = new SoundMachineWindow { DataContext = rack };

        _open = window;

        window.Activated += (_, _) => inFront?.Invoke(true);
        window.Deactivated += (_, _) => inFront?.Invoke(false);

        window.Closed += (_, _) =>
        {
            _open = null;
            inFront?.Invoke(false);
        };

        window.Show(owner);
    }
}
