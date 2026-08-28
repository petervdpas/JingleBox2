using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace JingleBox2.Views;

/// <summary>
/// The one window this application asks a yes or no question in, and tells things in.
/// </summary>
/// <remarks>
/// One window for both because a note is a question with one answer, and two windows that look
/// almost the same is how two windows come to drift apart. Deleting a recording no longer needs
/// the "this cannot be undone" wording, since a deleted take moves to <c>deleted/</c> and can be
/// fetched back.
/// </remarks>
public partial class ConfirmDialog : Window
{
    /// <summary>Builds the window. Its text and its buttons are filled in by the two callers.</summary>
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows a modal yes/no prompt. Answers no when it is cancelled, closed, or there is no
    /// window to open it over.
    /// </summary>
    public static Task<bool> AskAsync(string title, string message, string confirmText)
    {
        var dialog = new ConfirmDialog { Title = title };

        Fill(dialog, message, confirmText);

        return Dialog.ShowAsync(dialog, false);
    }

    /// <summary>
    /// The same window with nothing to decide: one button, and no red on it. Used where the app
    /// has already made the decision and is only saying so.
    /// </summary>
    public static Task NoteAsync(string title, string message)
    {
        var dialog = new ConfirmDialog { Title = title };

        Fill(dialog, message, "OK");

        var cancel = dialog.FindControl<Button>("CancelButton");
        if (cancel != null) cancel.IsVisible = false;

        var confirm = dialog.FindControl<Button>("ConfirmButton");
        confirm?.Classes.Remove("danger");

        return Dialog.ShowAsync(dialog, false);
    }

    /// <summary>
    /// Puts the question and the wording of the yes button into the window.
    /// </summary>
    /// <remarks>
    /// Found by name rather than bound, because the window is built twice from two static
    /// methods and giving it a view model for two strings would be more machinery than either
    /// needs.
    /// </remarks>
    private static void Fill(ConfirmDialog dialog, string message, string confirmText)
    {
        var text = dialog.FindControl<TextBlock>("MessageText");
        if (text != null) text.Text = message;

        var confirm = dialog.FindControl<Button>("ConfirmButton");
        if (confirm != null) confirm.Content = confirmText;
    }

    /// <summary>Yes. Also the only button a note has, where the answer is thrown away.</summary>
    private void Confirm_Click(object? sender, RoutedEventArgs e) => Close(true);

    /// <summary>No, which is also what closing the window by any other means answers.</summary>
    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
