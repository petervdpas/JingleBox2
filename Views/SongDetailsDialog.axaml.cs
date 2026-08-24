using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Threading.Tasks;

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
    /// <summary>What came back: the name, and what the song says about itself.</summary>
    public sealed record Details(string Name, string Description);

    public SongDetailsDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Asks for both. Gives back null when it is cancelled, when the name is left empty, or
    /// when there is no window to open it over.
    /// </summary>
    public static Task<Details?> AskAsync(string name, string description)
    {
        var dialog = new SongDetailsDialog();

        var nameBox = dialog.FindControl<TextBox>("NameBox");
        var aboutBox = dialog.FindControl<TextBox>("AboutBox");

        if (nameBox != null) nameBox.Text = name;
        if (aboutBox != null) aboutBox.Text = description;

        // Opened on the name with it selected, so typing replaces it and the arrows keep it.
        dialog.Opened += (_, _) =>
        {
            nameBox?.Focus();
            nameBox?.SelectAll();
        };

        return Dialog.ShowAsync<Details?>(dialog, null);
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

    private void Confirm_Click(object? sender, RoutedEventArgs e)
    {
        string name = (this.FindControl<TextBox>("NameBox")?.Text ?? "").Trim();
        string about = (this.FindControl<TextBox>("AboutBox")?.Text ?? "").Trim();

        Close(name.Length == 0 ? null : new Details(name, about));
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
