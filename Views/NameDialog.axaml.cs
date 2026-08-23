using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Threading.Tasks;

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
    public NameDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Asks for a name. Gives back the trimmed name, or null when it is cancelled, left empty,
    /// or there is no window to open it over.
    /// </summary>
    public static Task<string?> AskAsync(string title, string prompt, string current)
    {
        var dialog = new NameDialog { Title = title };

        var promptText = dialog.FindControl<TextBlock>("PromptText");
        if (promptText != null) promptText.Text = prompt;

        var box = dialog.FindControl<TextBox>("NameBox");

        if (box != null)
        {
            box.Text = current;

            // Opened with the old name selected, so typing replaces it and the arrows keep it.
            dialog.Opened += (_, _) =>
            {
                box.Focus();
                box.SelectAll();
            };
        }

        return Dialog.ShowAsync<string?>(dialog, null);
    }

    private void Name_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        Confirm_Click(sender, e);
        e.Handled = true;
    }

    private void Confirm_Click(object? sender, RoutedEventArgs e)
    {
        string wanted = (this.FindControl<TextBox>("NameBox")?.Text ?? "").Trim();

        Close(wanted.Length == 0 ? null : wanted);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
