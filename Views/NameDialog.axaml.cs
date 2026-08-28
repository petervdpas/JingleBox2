using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Threading.Tasks;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <summary>
/// Asks for a name and gives it back, or gives back nothing when it is cancelled.
/// </summary>
/// <remarks>
/// The same shape as the confirm dialog, with a box in it. Renaming a thing in a list wants a
/// dialog rather than an editable row: the row has to stay readable while you are picking
/// through the list, and a box that is only sometimes a box is a row you cannot trust.
/// </remarks>
public partial class NameDialog : Window
{
    /// <summary>Finding the window a modal sits over. Holds nothing, so one serves them all.</summary>
    private static readonly IDialogs Modal = new Dialogs();

    /// <summary>Builds the window. Its prompt, its box and its button are filled in by <see cref="AskAsync"/>.</summary>
    public NameDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Asks for a name. Gives back the trimmed name, or null when it is cancelled, left empty,
    /// or there is no window to open it over.
    /// </summary>
    /// <param name="title">What the window's title bar says.</param>
    /// <param name="prompt">The line above the box, saying what is being named.</param>
    /// <param name="current">The name it opens with, selected, so typing replaces it.</param>
    /// <param name="confirm">
    /// What the button that accepts the name says. Renaming is what this box is usually for,
    /// so that is the default, but a box that asks what to call a song before saving it should
    /// not have Rename written on it.
    /// </param>
    /// <remarks>
    /// It opens with the old name selected, so typing replaces it and an arrow key keeps it.
    /// That is what makes correcting a name and replacing one the same single gesture.
    /// </remarks>
    public static Task<string?> AskAsync(string title, string prompt, string current, string confirm = "Rename")
    {
        var dialog = new NameDialog { Title = title };

        var confirmButton = dialog.FindControl<Button>("ConfirmButton");
        if (confirmButton != null) confirmButton.Content = confirm;

        var promptText = dialog.FindControl<TextBlock>("PromptText");
        if (promptText != null) promptText.Text = prompt;

        var box = dialog.FindControl<TextBox>("NameBox");

        if (box != null)
        {
            box.Text = current;

            dialog.Opened += (_, _) =>
            {
                box.Focus();
                box.SelectAll();
            };
        }

        return Modal.ShowAsync<string?>(dialog, null);
    }

    /// <summary>
    /// Enter in the box accepts the name, since a one-box dialog has nothing else Enter could
    /// mean and reaching for the mouse to press a button is a gesture nobody wants here.
    /// </summary>
    private void Name_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        Confirm_Click(sender, e);
        e.Handled = true;
    }

    /// <summary>
    /// Hands the name back, trimmed, or null when it has been emptied.
    /// </summary>
    /// <remarks>
    /// An empty name reads as cancel rather than as an error: there is nothing useful to say
    /// about it and nothing this could do with it.
    /// </remarks>
    private void Confirm_Click(object? sender, RoutedEventArgs e)
    {
        string wanted = (this.FindControl<TextBox>("NameBox")?.Text ?? "").Trim();

        Close(wanted.Length == 0 ? null : wanted);
    }

    /// <summary>Closes with nothing, leaving the thing called what it was called.</summary>
    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
