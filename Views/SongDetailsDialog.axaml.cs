using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Threading.Tasks;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <summary>
/// What a song is called and what it is, asked together.
/// </summary>
/// <remarks>
/// Together because the moment you name a song is the only moment you reliably know what it
/// is. A description asked for later is a description nobody writes.
/// </remarks>
public partial class SongDetailsDialog : Window
{
    /// <summary>Finding the window a modal sits over. Holds nothing, so one serves them all.</summary>
    private static readonly IDialogs Modal = new Dialogs();

    /// <summary>What came back: the name, and what the song says about itself.</summary>
    public sealed record Details(string Name, string Description);

    /// <summary>Builds the window. Both boxes are filled in by <see cref="AskAsync"/>.</summary>
    public SongDetailsDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Asks for both. Gives back null when it is cancelled, when the name is left empty, or
    /// when there is no window to open it over.
    /// </summary>
    /// <remarks>
    /// It opens on the name with the name selected, so typing replaces it and an arrow key
    /// keeps it. That is what makes renaming an existing song one gesture rather than a
    /// select-all first.
    /// </remarks>
    public static Task<Details?> AskAsync(string name, string description)
    {
        var dialog = new SongDetailsDialog();

        var nameBox = dialog.FindControl<TextBox>("NameBox");
        var aboutBox = dialog.FindControl<TextBox>("AboutBox");

        if (nameBox != null) nameBox.Text = name;
        if (aboutBox != null) aboutBox.Text = description;

        dialog.Opened += (_, _) =>
        {
            nameBox?.Focus();
            nameBox?.SelectAll();
        };

        return Modal.ShowAsync<Details?>(dialog, null);
    }

    /// <summary>
    /// Enter in the name box saves. Not in the description, where it is a new line: that box
    /// takes returns, which is the whole reason it is a box and not a field.
    /// </summary>
    private void Name_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        Confirm_Click(sender, e);
        e.Handled = true;
    }

    /// <summary>
    /// Hands back both, trimmed, or null when the name has been emptied.
    /// </summary>
    /// <remarks>
    /// An empty name reads as cancel rather than as an error, because a song with no name is
    /// not something this can save and there is nothing useful to say about it.
    /// </remarks>
    private void Confirm_Click(object? sender, RoutedEventArgs e)
    {
        string name = (this.FindControl<TextBox>("NameBox")?.Text ?? "").Trim();
        string about = (this.FindControl<TextBox>("AboutBox")?.Text ?? "").Trim();

        Close(name.Length == 0 ? null : new Details(name, about));
    }

    /// <summary>Closes with nothing, leaving the song called what it was called.</summary>
    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
