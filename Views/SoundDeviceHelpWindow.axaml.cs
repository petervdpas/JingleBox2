using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using JingleBox2.SoundDevices.Interfaces;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <summary>
/// What a device says about itself, in a window of its own.
/// </summary>
/// <remarks>
/// Every device needs help and none of it belongs in the application's. What this program does
/// is written under <c>help/</c> and changes when this program changes; what a soundmachine's
/// third knob does is written by whoever built the machine and travels in its folder, so it is
/// shown here rather than as a topic in <see cref="HelpWindow"/>: no topic list, no search, and
/// nothing of this program's around it.
///
/// One window per device rather than one window shown different pages, so a page can be left
/// open beside the machine it is about while somebody works the machine. Asking for the same
/// device twice brings the one that is open forward, which is what
/// <see cref="SoundEffectWindow"/> already does and for the same reason: two windows holding the
/// same page is two things to close.
///
/// It is not themed by the application. A device carries its own colours and its face is drawn
/// in them, so its page is too, which is what makes it read as part of the box rather than as a
/// dialog this program put in front of it.
/// </remarks>
public partial class SoundDeviceHelpWindow : Window
{
    /// <summary>Which page is open for which device, so a second ask brings the first forward.</summary>
    private static readonly Dictionary<string, SoundDeviceHelpWindow> Open = new();

    /// <summary>How a device's colours are put on the plate. Holds nothing, so one is enough.</summary>
    private readonly IPanelTint _tint = new PanelTint();

    /// <summary>Where a window is put, which is over the one it was opened from.</summary>
    private static readonly IFreeWindow Free = new FreeWindow();

    /// <summary>Builds the window. What it shows is put in by <see cref="Show"/>.</summary>
    public SoundDeviceHelpWindow() => InitializeComponent();

    /// <summary>
    /// Opens that device's page, or brings the one already open forward.
    /// </summary>
    /// <remarks>
    /// A device with no page still opens one, saying so in the room the page would have been in.
    /// That is deliberate: the line in the Menu is greyed where there is nothing to read, so the
    /// only way to arrive here with an empty page is a device whose page was thrown away while
    /// the window was shut, and an empty window that says why beats a button that does nothing.
    ///
    /// Keyed by the device's id rather than by the project, since what is on the rack is read
    /// again whenever the folders are, and two readings of one machine are two objects and one
    /// box.
    /// </remarks>
    /// <param name="device">The device whose page this is.</param>
    /// <param name="owner">The window it is opened over, or nothing to leave it to the desktop.</param>
    public static void Show(IRackProject? device, Window? owner)
    {
        if (device == null) return;

        if (Open.TryGetValue(device.Id, out var already))
        {
            already.Fill(device);
            already.Activate();

            return;
        }

        var window = new SoundDeviceHelpWindow();

        window.Fill(device);

        Open[device.Id] = window;

        window.Closed += (_, _) => Open.Remove(device.Id);

        Free.Show(window, owner);
    }

    /// <summary>Puts a device's name, its one line and its page into the window.</summary>
    /// <param name="device">The device being shown.</param>
    private void Fill(IRackProject device)
    {
        Title = device.Name + " help";

        NameText.Text = device.Name;
        SummaryText.Text = device.Summary;

        Page.Markdown = device.Help.Length > 0
            ? device.Help
            : "## Nothing written\n\n"
              + device.Name + " carries no help page. One is written in DESIGNER, on the "
              + "Helptext tab, and travels with the device.";

        _tint.Apply(Plate, device.Theme);
    }

    /// <summary>Shuts the page.</summary>
    /// <param name="sender">Unused.</param>
    /// <param name="e">Unused.</param>
    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
