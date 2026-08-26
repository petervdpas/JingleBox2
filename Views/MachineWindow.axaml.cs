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
public partial class MachineWindow : Window
{
    private static MachineWindow? _open;

    public MachineWindow()
    {
        InitializeComponent();

        // The pointer goes where the windows are, so the mode has to be reachable from all of
        // them. See LinkKey.
        LinkKey.Listen(this);
    }

    /// <summary>
    /// Opens the rack over the app's window, or brings the one that is open forward.
    /// </summary>
    /// <param name="inFront">
    /// Told whenever this window takes or loses the keyboard. A note played while the rack is
    /// in front is auditioning a machine; the same key on the pattern is writing a note, and
    /// which one it is is a question about which window you are looking at.
    /// </param>
    public static void Show(MachineRackViewModel? rack, Window? owner, Action<bool>? inFront = null)
    {
        if (rack == null || owner == null) return;

        if (_open != null)
        {
            _open.Activate();
            return;
        }

        var window = new MachineWindow { DataContext = rack };

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
