using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using JingleBox2.Rack.SoundDevices.Faces.Records;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

/// <summary>
/// The FIRE page: the pads as they are played, with nothing on them that can be edited.
/// </summary>
/// <remarks>
/// The pads are pointed at from here. Every pad offers <see cref="PadViewModel.PadLink"/> through
/// <see cref="Pointable"/>, and this page joins the tally <see cref="LinkKey"/> keeps of views
/// with something pointable on them, which is what makes Ctrl+Shift+M mean anything here.
///
/// Here rather than on PADS, which is where a pad is filled rather than where it is played: the
/// gesture is a hand on the hardware and a pointer on the pad it should fire, and that is the
/// page somebody has open with the controller in front of them.
/// </remarks>
public partial class UseView : UserControl
{
    /// <summary>Builds the page. It holds no state of its own.</summary>
    public UseView()
    {
        InitializeComponent();

        LinkKey.Watch(this);
    }

    /// <summary>What the menu offers, drawn as menu items. The same rule a machine's face uses.</summary>
    private readonly Rack.Controls.Interfaces.IMenuLines _lines = new Rack.Controls.MenuLines();

    /// <summary>
    /// Shows what the hardware on this desk does to the pads.
    /// </summary>
    /// <remarks>
    /// Read when it is pressed rather than held, since what it answers moves under it: a button
    /// pointed at a pad a moment ago should be in the list the next time it opens.
    /// </remarks>
    /// <param name="sender">The button, which the menu opens under.</param>
    /// <param name="e">Unused.</param>
    private void PadsMenu_Pressed(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel main) return;
        if (sender is not Control under) return;

        var offers = new List<PanelMenuItem>(main.PadsMenu.Read());

        new MenuFlyout { ItemsSource = _lines.Listed(offers) }.ShowAt(under);
    }
}
