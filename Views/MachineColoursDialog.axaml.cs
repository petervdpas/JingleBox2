using Avalonia.Controls;
using Avalonia.Interactivity;
using JingleBox2.Machines;
using JingleBox2.ViewModels;
using System.Threading.Tasks;
using JingleBox2.Machines.Records;

namespace JingleBox2.Views;

/// <summary>
/// Where a machine's colours are settled: the one it is, and the seven distances from it.
/// </summary>
/// <remarks>
/// The colour itself is on the page beside the machine's name, because it is chosen as often as
/// the name is. The seven are here, because they are not: they are what a machine says when it
/// wants a lighter face or a louder mark, and most machines never say anything.
///
/// Nothing is written until Use these. What is on the right is the panel's own code tinted with
/// what is showing, so there is no second recipe to keep in step with the first.
/// </remarks>
public partial class MachineColoursDialog : Window
{
    /// <summary>Builds the window. Its eight swatches and its preview are filled in by <see cref="AskAsync"/>.</summary>
    public MachineColoursDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Asks what colours a machine should wear. Gives back the theme, or null when it is
    /// cancelled or there is no window to open it over.
    /// </summary>
    /// <remarks>
    /// The preview is tinted rather than bound, so it is repainted whenever one of the eight
    /// moves, and once on the way in for what was already there. The subscription is dropped
    /// when the window closes, or a cancelled dialog would go on repainting a window nobody can
    /// see.
    /// </remarks>
    public static Task<MachineTheme?> AskAsync(string name, MachineTheme theme)
    {
        var colours = new MachineColours(name, theme);

        var dialog = new MachineColoursDialog { DataContext = colours };

        if (dialog.FindControl<TextBlock>("Preamble") is { } preamble)
        {
            preamble.Text = "What " + colours.Name + " is painted in, wherever it is shown. " +
                            "A machine keeps its own colours whichever theme the application is wearing.";
        }

        if (dialog.FindControl<TextBlock>("PanelName") is { } shown) shown.Text = colours.Name.ToUpperInvariant();

        void Show() => dialog.Retint(colours);

        colours.Changed += Show;

        dialog.Opened += (_, _) => Show();
        dialog.Closed += (_, _) => colours.Changed -= Show;

        return Dialog.ShowAsync<MachineTheme?>(dialog, null);
    }

    /// <summary>
    /// Paints the preview in what is currently showing, through the panel's own tinting code,
    /// so there is no second recipe to keep in step with the first.
    /// </summary>
    private void Retint(MachineColours colours)
    {
        if (this.FindControl<Border>("Preview") is { } preview) MachineTint.Repaint(preview, colours.Theme);
    }

    /// <summary>
    /// Puts the seven back to what a machine has unless it says otherwise.
    /// </summary>
    /// <remarks>
    /// The colour is left alone. It is the one of the eight that is never a default: a machine
    /// reset to grey is a machine somebody has to name again from memory.
    /// </remarks>
    private void Reset_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MachineColours colours) return;

        var usual = new MachineTheme(colours.AccentHex);

        colours.Face = usual.Face;
        colours.Panel = usual.Panel;
        colours.Edge = usual.Edge;
        colours.Mark = usual.Mark;
        colours.Row = usual.Row;
        colours.RowOver = usual.RowOver;
        colours.RowPicked = usual.RowPicked;
    }

    /// <summary>Hands the eight back as a theme, which is the only moment anything is written.</summary>
    private void Confirm_Click(object? sender, RoutedEventArgs e) =>
        Close(DataContext is MachineColours colours ? colours.Theme : null);

    /// <summary>Closes with nothing, leaving the machine painted as it was.</summary>
    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
